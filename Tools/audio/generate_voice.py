"""Speak every line of dialogue in Project G1, using eSpeak NG.

The first pass at NPC voices synthesized six vowels and blipped them in time
with the typewriter. It had cadence and it had character, but it said nothing —
and a survivor telling you the iteration count should be able to say the
number out loud.

So this walks the C# that *defines* the dialogue, hands each line to a formant
speech synthesizer with that character's voice settings, and writes one clip
per line into Assets/Resources/Audio/Voice/. Two things make that safe to do:

  * The clip name is a hash of the line's text. Reword a line and it gets a new
    clip; the old one is orphaned rather than silently played over the new
    words. Nothing has to be renumbered by hand.
  * The runtime falls back to the syllable blips when a clip is missing, so a
    line added in the editor and not yet spoken still makes a noise instead of
    nothing.

The C# stays the single source of truth for what everyone says — there is no
second copy of the script to drift out of sync.

eSpeak rather than the OS voice on purpose: it regenerates identically on any
platform, its output is unambiguously ours to ship, and its formant synthesis
sounds like 1998, which is the whole brief.

Requires:  a Piper voice model per role in external/piper-voices/, and the
           piper virtualenv at .piper-venv/ (see PIPER below).

Piper replaced eSpeak here. eSpeak is a formant synthesiser: it builds speech
out of filtered buzz, which is why every character sounded like the same robot
at a different pitch, and why "the Auditor is slow and unbothered" had to be
faked with a words-per-minute number. Piper is neural, with one trained speaker
per model, so two characters differ because they are different people rather
than the same oscillator retuned. MIT-licensed, entirely offline; the models
are 60-128 MB each and live outside Assets/ because a game does not need to
ship its own recording studio.
Run:       python3 Tools/audio/generate_voice.py
"""
import hashlib
import json
import math
import os
import re
import shutil
import struct
import subprocess
import sys
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio", "Voice")

# voice, words-per-minute, pitch 0-99, word gap (10ms units).
#
# Tuned for intelligibility first, character second — the first pass reached
# for the extremes of every dial and the result was hard to listen to:
#
#   * Pitch near 0 or 99 drags the formants out of the range a listener's ear
#     expects a voice to occupy, so it stops parsing as speech. Held to 22-72.
#   * 192 wpm is faster than an auctioneer. Held to 118-172.
#   * The high-numbered +mN/+fN variants are the buzziest. Only the gentle ones.
#   * The word gap is the single biggest win for formant synthesis and the
#     first pass had none: without it every sentence is one long word.
#
# Character comes from the *relative* placement, which is preserved: the chief
# is still the lowest and flattest, the signals tech still the fastest.
PIPER = os.path.join(os.path.dirname(ROOT), ".piper-venv", "bin", "piper")
MODELS = os.path.join(os.path.dirname(ROOT), "external", "piper-voices")

# One trained speaker per role, plus a length scale (higher is slower) and a
# noise scale (higher is less even). The casting is the performance now: there
# is no pitch knob to hide behind, so a role that has to sound tired has to be
# given a voice that sounds tired.
VOICES = {
    # role             model                      length  noise
    "SecurityChief": ("en_US-joe-medium",         1.00,  0.55),
    "Quartermaster": ("en_GB-alan-medium",        1.04,  0.60),
    "Engineer":      ("en_US-lessac-medium",      0.96,  0.60),
    "Researcher":    ("en_US-amy-medium",         1.00,  0.62),
    "Medic":         ("en_GB-jenny_dioco-medium", 0.98,  0.62),
    "SignalTech":    ("en_US-lessac-medium",      0.90,  0.70),
    # The Echo is what you used to be, forty loops ago. Same model as the
    # player's own voice, slowed and made unsteady, which says "this was me"
    # better than a broken-throat effect ever did.
    "Echo":          ("en_US-lessac-medium",      1.45,  0.90),
    # narration
    "Vi":            ("en_US-amy-medium",         0.94,  0.30),  # a machine: too even
    "Auditor":       ("en_US-ryan-high",          1.18,  0.45),  # slow, low, unbothered
    "Self":          ("en_US-lessac-medium",      1.00,  0.60),
}

SR = 22050
TARGET_PEAK = 0.85      # leaves headroom; -a 170 was railing the loudest lines

SPEAKER_ALIAS = {"VI": "Vi", "AU": "Auditor", "ME": "Self"}

# a run of "..." literals joined by +, as the C# wraps long lines
STR_RUN = r'((?:"(?:[^"\\]|\\.)*"\s*(?:\+\s*)?)+)'


def unquote(run):
    """Turn a C# string-concatenation run into the string it evaluates to."""
    parts = re.findall(r'"((?:[^"\\]|\\.)*)"', run)
    s = "".join(parts)
    return (s.replace('\\"', '"').replace("\\\\", "\\")
             .replace("\\n", "\n").replace("\\t", "\t"))


def key(text):
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:16]


def split_calls(src, opener):
    """Yield the body of each `opener(...)` call, brackets balanced."""
    for m in re.finditer(re.escape(opener), src):
        i = src.index("(", m.start())
        depth, j = 0, i
        while j < len(src):
            if src[j] == "(":
                depth += 1
            elif src[j] == ")":
                depth -= 1
                if depth == 0:
                    break
            j += 1
        yield src[i + 1:j]


def collect():
    """(speaker, text) for every spoken line in the game."""
    lines = []

    # --- quest contacts: role comes from the call, lines from named arguments
    for rel in ("Assets/G1/Editor/G1QuestNpcBuilder.cs",
                "Assets/G1/Editor/G1DoorKitBuilder.cs"):
        path = os.path.join(ROOT, rel)
        if not os.path.exists(path):
            continue
        src = open(path, encoding="utf-8").read()
        for body in split_calls(src, "Contact("):
            role = re.search(r"G1NpcRole\.(\w+)", body)
            if not role:
                continue
            role = role.group(1)
            for arg in ("offer", "accept", "nag", "turnIn", "done"):
                m = re.search(arg + r"\s*:\s*" + STR_RUN, body)
                if m:
                    lines.append((role, unquote(m.group(1))))

    # --- narration beats: B(SPEAKER, "...")
    #
    # The opening sequence uses the same three speakers and the same marker, so
    # it is scanned the same way. Listing the files rather than globbing keeps
    # an unrelated `B(` somewhere in the codebase from silently becoming
    # dialogue.
    for rel in ("Assets/G1/Editor/G1StoryBuilder.cs",
                "Assets/G1/Editor/G1OpeningBuilder.cs"):
        path = os.path.join(ROOT, rel)
        if not os.path.exists(path):
            continue
        src = open(path, encoding="utf-8").read()
        for m in re.finditer(r"\bB\(\s*(VI|AU|ME)\s*,\s*" + STR_RUN, src):
            lines.append((SPEAKER_ALIAS[m.group(1)], unquote(m.group(2))))

    return lines


def polish(path):
    """Clean up what the synthesiser hands back.

    Three things, in order, each fixing something measurable:
      * trim the silence it pads either end with — the game paces its
        typewriter to the clip's length, so padding makes the text lag the
        voice;
      * normalise to a fixed peak instead of letting amplitude ride, which
        both stops the loud lines railing and makes every character sit at the
        same level.
    """
    with wave.open(path, "rb") as w:
        n, sr = w.getnframes(), w.getframerate()
        s = list(struct.unpack("<%dh" % n, w.readframes(n)))
    if not s:
        return 0.0

    peak = max(abs(x) for x in s) or 1
    gate = peak * 0.02
    first = next((i for i, x in enumerate(s) if abs(x) > gate), 0)
    last = next((i for i in range(len(s) - 1, -1, -1) if abs(s[i]) > gate), len(s) - 1)
    pad = int(sr * 0.03)
    s = s[max(0, first - pad):min(len(s), last + pad)]
    if not s:
        return 0.0

    # The 7 kHz roll-off that used to live here existed to tame formant buzz —
    # eSpeak put a lot of energy above where consonants are, and cutting it made
    # the difference between grating and listenable. Neural speech has no such
    # buzz; the same filter on it only removes sibilance and leaves everyone
    # sounding like they are speaking through a door. Trimming and levelling
    # below are still worth doing.

    peak = max(abs(x) for x in s) or 1.0
    g = TARGET_PEAK * 32767.0 / peak
    fade = int(sr * 0.005)                    # 5ms, kills the edge click
    out = []
    for i, x in enumerate(s):
        v = x * g
        if i < fade:
            v *= i / fade
        elif i > len(s) - fade:
            v *= max(0.0, (len(s) - i) / fade)
        out.append(int(max(-32767, min(32767, v))))

    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(b"".join(struct.pack("<h", v) for v in out))
    return len(out) / sr


def speak(role, text, path):
    model, length, noise = VOICES.get(role, VOICES["Self"])
    # eSpeak reads "—" and "..." as literal punctuation names in some voices;
    # give it prose it can pronounce without narrating the typography
    spoken = (text.replace("—", ", ").replace("…", "...")
                  .replace("...", ", ").replace("’", "'"))
    subprocess.run(
        [PIPER, "-m", os.path.join(MODELS, model + ".onnx"),
         "--length-scale", str(length), "--noise-scale", str(noise),
         "-f", path, "--", spoken],
        check=True, capture_output=True)
    return polish(path)


def main():
    if not os.path.exists(PIPER):
        sys.exit(f"piper not found at {PIPER} — create it with:\n"
                 "  python3 -m venv .piper-venv && "
                 ".piper-venv/bin/pip install piper-tts")
    missing = sorted({m for m, _, _ in VOICES.values()
                      if not os.path.exists(os.path.join(MODELS, m + ".onnx"))})
    if missing:
        sys.exit("missing Piper voice models in external/piper-voices/:\n  " +
                 "\n  ".join(missing))

    lines = collect()
    if not lines:
        sys.exit("no dialogue found — did the builders move?")

    os.makedirs(OUT, exist_ok=True)
    index, seen = {}, set()
    for role, text in lines:
        k = key(text)
        if k in seen:
            continue                     # two characters, identical line
        seen.add(k)
        speak(role, text, os.path.join(OUT, k + ".wav"))
        index[k] = {"role": role, "text": text}

    # An index is not needed at runtime — the game hashes the line it is about
    # to say — but it is what lets the build check that every line the player
    # can hear actually got spoken.
    with open(os.path.join(OUT, "voice_index.json"), "w", encoding="utf-8") as fh:
        json.dump(index, fh, indent=1, ensure_ascii=False)

    by_role = {}
    for v in index.values():
        by_role[v["role"]] = by_role.get(v["role"], 0) + 1
    print(f"VOICE DONE — {len(index)} lines spoken into {OUT}")
    for r in sorted(by_role):
        v = VOICES.get(r, VOICES["Self"])
        print(f"  {r:<15} {by_role[r]:>2} lines  {v[0]:<26} length {v[1]}  noise {v[2]}")


if __name__ == "__main__":
    main()
