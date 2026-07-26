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

Requires:  brew install espeak-ng   (or apt install espeak-ng)
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
VOICES = {
    # role            voice        wpm  pitch  gap
    "SecurityChief": ("en-us+m3", 152, 30, 4),
    "Quartermaster": ("en-us+m4", 142, 24, 5),
    "Engineer":      ("en-us+m1", 158, 46, 4),
    "Researcher":    ("en-us+f2", 158, 60, 4),
    "Medic":         ("en-us+f3", 166, 66, 3),
    "SignalTech":    ("en-us+m2", 172, 70, 3),
    # was +croak, which is heavy vocal fry — atmospheric and close to
    # unintelligible. The Echo drags because of the pauses now, not because
    # its throat is broken, which is both clearer and more unsettling.
    "Echo":          ("en-us+m5", 118, 26, 12),
    # narration
    "Vi":            ("en-us+f4", 168, 72, 5),   # synthetic, too even
    "Auditor":       ("en-us+m4", 138, 22, 6),   # slow, low, unbothered
    "Self":          ("en-us+m1", 152, 44, 4),
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
    path = os.path.join(ROOT, "Assets/G1/Editor/G1StoryBuilder.cs")
    if os.path.exists(path):
        src = open(path, encoding="utf-8").read()
        for m in re.finditer(r"\bB\(\s*(VI|AU|ME)\s*,\s*" + STR_RUN, src):
            lines.append((SPEAKER_ALIAS[m.group(1)], unquote(m.group(2))))

    return lines


def polish(path):
    """Clean up what eSpeak hands back.

    Three things, in order, each fixing something measurable:
      * trim the silence it pads either end with — the game paces its
        typewriter to the clip's length, so padding makes the text lag the
        voice;
      * a gentle one-pole roll-off at 7kHz, which is above everything that
        carries consonants and squarely on the buzz that makes formant
        synthesis grating;
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

    # one-pole low-pass at ~7kHz
    a = 1.0 / (1.0 + sr / (2.0 * math.pi * 7000.0))
    y = 0.0
    for i, x in enumerate(s):
        y += a * (x - y)
        s[i] = y

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
    voice, speed, pitch, gap = VOICES.get(role, VOICES["Self"])
    # eSpeak reads "—" and "..." as literal punctuation names in some voices;
    # give it prose it can pronounce without narrating the typography
    spoken = (text.replace("—", ", ").replace("…", "...")
                  .replace("...", ", ").replace("’", "'"))
    subprocess.run(
        ["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch),
         "-g", str(gap), "-a", "100", "-w", path, spoken],
        check=True, capture_output=True)
    return polish(path)


def main():
    if shutil.which("espeak-ng") is None:
        sys.exit("espeak-ng not found — brew install espeak-ng "
                 "(or apt install espeak-ng), then re-run.")

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
        print(f"  {r:<15} {by_role[r]:>2} lines  {v[0]:<12} {v[1]}wpm pitch {v[2]} gap {v[3]}")


if __name__ == "__main__":
    main()
