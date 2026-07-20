"""Procedural retro synth music for Project G1 — stdlib only.

Two loopable tracks in A minor:
  music_tension — 120 BPM, brooding saw bass + sparse square lead (~16 s)
  music_action  — 140 BPM, arpeggiated chords + noise snare hits (~13.7 s)

Output: Assets/Resources/Audio/Music/. Run: python3 Tools/audio/generate_music.py
"""
import math
import os
import random
import struct
import wave

SR = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "..",
                   "Assets", "Resources", "Audio", "Music")
random.seed(1972)

NOTE = {n: 440.0 * 2 ** ((i - 9) / 12.0) for i, n in enumerate(
    ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"])}


def freq(name, octave):
    return NOTE[name] * 2 ** (octave - 4)


def write_wav(name, samples):
    os.makedirs(OUT, exist_ok=True)
    peak = max(1e-9, max(abs(s) for s in samples))
    scale = 0.85 / peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(
            struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767))
            for s in samples))
    print(f"{name}.wav  {len(samples) / SR:.1f}s")


def render(total_n, events):
    """events: list of (start_sample, dur_samples, freq, amp, kind)."""
    out = [0.0] * total_n
    for start, dur, f, amp, kind in events:
        phase = 0.0
        for i in range(dur):
            idx = start + i
            if idx >= total_n:
                break
            t = i / dur
            env = min(1.0, t * 12) * math.exp(-2.2 * t)
            if kind == "saw":
                phase = (phase + f / SR) % 1.0
                s = 2 * phase - 1
            elif kind == "square":
                phase = (phase + f / SR) % 1.0
                s = 1.0 if phase < 0.5 else -1.0
            else:                                   # noise (snare)
                s = random.uniform(-1, 1)
                env = math.exp(-14 * t)
            out[idx] += amp * env * s
    return out


def beats(bpm, count):
    return int(SR * 60.0 / bpm * count)


# ---- music_tension: 120 BPM, 8 bars of 4 beats = 16 s
BPM = 120
bar = beats(BPM, 4)
total = bar * 8
ev = []
bassline = ["A", "A", "G", "G", "F", "F", "E", "E"]
for b, root in enumerate(bassline):
    for beat in range(4):
        ev.append((b * bar + beats(BPM, beat), beats(BPM, 1),
                   freq(root, 2), 0.55, "saw"))
lead = [("A", 4, 0, 2), ("C", 5, 4, 2), ("B", 4, 8, 1), ("E", 4, 12, 3),
        ("A", 4, 16, 2), ("G", 4, 22, 2), ("F", 4, 26, 1), ("E", 4, 28, 4)]
for note, octv, at_beat, dur_beats in lead:
    ev.append((beats(BPM, at_beat), beats(BPM, dur_beats),
               freq(note, octv), 0.22, "square"))
write_wav("music_tension", render(total, ev))

# ---- music_action: 140 BPM, 8 bars = ~13.7 s
BPM = 140
bar = beats(BPM, 4)
total = bar * 8
ev = []
chords = [["A", "C", "E"], ["A", "C", "E"], ["F", "A", "C"], ["F", "A", "C"],
          ["G", "B", "D"], ["G", "B", "D"], ["E", "G#", "B"], ["E", "G#", "B"]]
for b, chord in enumerate(chords):
    for six in range(16):                            # 16th-note arpeggio
        note = chord[six % 3]
        ev.append((b * bar + beats(BPM, six * 0.25), beats(BPM, 0.25),
                   freq(note, 3 + (six % 6) // 3), 0.4, "saw"))
    for beat in (1, 3):                              # snare on the off-beats
        ev.append((b * bar + beats(BPM, beat), int(SR * 0.09), 0, 0.5, "noise"))
    ev.append((b * bar, beats(BPM, 4), freq(chord[0], 2), 0.35, "square"))
write_wav("music_action", render(total, ev))

print("MUSIC DONE")
