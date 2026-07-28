"""Synthesise the trailer's music bed.

Everything the research says about trailer audio comes down to one thing: the
cuts land on the beat, so the beat has to exist first and the edit is built to
it. Rather than licence a track and cut to it, this generates one at a known
tempo — so the shot boundaries in render_trailer.py are literally computed from
the same numbers.

It is four elements and no more, because a bed that competes with dialogue is
a bed that has to be ducked, and ducking is a mixing problem this does not need:

    drone     a low pedal that never resolves; the whole 30 seconds sit on it
    pulse     a filtered kick on every beat, the thing the cuts land on
    riser     one long sweep into the last section
    impact    a single hit at the title, which is the only loud moment
"""
import math, os, struct, wave

SR = 44100
BPM = 84.0                      # slow enough to feel heavy, fast enough to cut on
BEAT = 60.0 / BPM               # 0.714 s
BARS = 22                       # 22 bars of 4 = 62.9 s
# Was 11. The shot list in render_trailer.py is written in *bars* and runs
# to bar 22, so a bed sized for 11 left the entire second half of the
# trailer — every vehicle shot, the Cradle reveal and the title — in
# silence. Both files now derive their length from the same number.
DUR = BEAT * 4 * BARS
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "..", "renders", "trailer_bed.wav")

n = int(SR * DUR)
buf = [0.0] * n


def env(i, t0, attack, hold, release):
    t = i / SR - t0
    if t < 0 or t > attack + hold + release:
        return 0.0
    if t < attack:
        return t / attack
    if t < attack + hold:
        return 1.0
    return 1.0 - (t - attack - hold) / release


# --- drone: two detuned saws an octave apart, deliberately never in tune
for i in range(n):
    t = i / SR
    a = 0.0
    for f, g in ((41.2, 0.34), (41.9, 0.26), (82.4, 0.12), (123.5, 0.05)):
        ph = (t * f) % 1.0
        a += (2.0 * ph - 1.0) * g          # saw
    swell = 0.55 + 0.45 * math.sin(t * 0.19 * math.tau)
    buf[i] += a * 0.16 * swell

# --- pulse: a kick on every beat, harder on the downbeat
beats = int(DUR / BEAT)
for b in range(beats):
    t0 = b * BEAT
    strong = (b % 4 == 0)
    peak = 0.62 if strong else 0.34
    for k in range(int(0.30 * SR)):
        i = int(t0 * SR) + k
        if i >= n:
            break
        u = k / SR
        f = 132.0 * math.exp(-u * 26.0) + 38.0      # pitch drop: that is the thump
        buf[i] += math.sin(u * f * math.tau) * peak * math.exp(-u * 11.0)

# --- riser into the last five bars
r0 = BEAT * 4 * (BARS - 5)
for i in range(int(r0 * SR), n):
    t = i / SR - r0
    k = min(1.0, t / (BEAT * 4 * 4))
    f = 180.0 + 900.0 * k * k
    buf[i] += math.sin((i / SR) * f * math.tau) * 0.11 * k
    # noise sweep on top of it
    buf[i] += ((i * 1103515245 + 12345) % 2048 / 1024.0 - 1.0) * 0.05 * k * k

# --- one impact, on the title
hit = BEAT * 4 * (BARS - 2)
for k in range(int(1.8 * SR)):
    i = int(hit * SR) + k
    if i >= n:
        break
    u = k / SR
    buf[i] += math.sin(u * (58.0 * math.exp(-u * 3.0) + 30.0) * math.tau) \
        * 0.85 * math.exp(-u * 2.2)

peak = max(abs(x) for x in buf) or 1.0
g = 0.80 / peak
os.makedirs(os.path.dirname(OUT), exist_ok=True)
w = wave.open(os.path.abspath(OUT), "w")
w.setnchannels(1)
w.setsampwidth(2)
w.setframerate(SR)
w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x * g)) * 32767))
                       for x in buf))
w.close()
print(f"BED DONE — {DUR:.1f}s at {BPM:.0f} BPM, beat = {BEAT:.3f}s -> {os.path.abspath(OUT)}")
