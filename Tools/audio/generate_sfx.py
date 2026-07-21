"""Procedural retro SFX generator for Project G1 — stdlib only (no numpy).

Synthesizes 16-bit mono 22.05kHz WAVs with a 90s-shooter flavor: filtered
noise bursts for gunfire, swept tones for feedback sounds. Output goes to
Assets/Resources/Audio/ so G1Audio can Resources.Load them at runtime.

Run:  python3 Tools/audio/generate_sfx.py
"""
import math
import os
import random
import struct
import wave

SR = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "..",
                   "Assets", "Resources", "Audio")
random.seed(1998)


def write_wav(name, samples):
    os.makedirs(OUT, exist_ok=True)
    peak = max(1e-9, max(abs(s) for s in samples))
    scale = 0.9 / peak
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(
            struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767))
            for s in samples))
    print(f"{name}.wav  {len(samples) / SR:.2f}s")


def lowpass(samples, alpha):
    out, y = [], 0.0
    for s in samples:
        y += alpha * (s - y)
        out.append(y)
    return out


def env_exp(n, rate):
    return [math.exp(-rate * i / SR) for i in range(n)]


def noise(n):
    return [random.uniform(-1, 1) for _ in range(n)]


def sine(n, f0, f1=None):
    f1 = f1 if f1 is not None else f0
    out, phase = [], 0.0
    for i in range(n):
        f = f0 + (f1 - f0) * i / n
        phase += 2 * math.pi * f / SR
        out.append(math.sin(phase))
    return out


def gunshot(name, dur, decay, body_hz, body_amt, lp):
    n = int(SR * dur)
    e = env_exp(n, decay)
    crack = lowpass(noise(n), lp)
    body = sine(n, body_hz, body_hz * 0.6)
    write_wav(name, [(crack[i] + body_amt * body[i]) * e[i] for i in range(n)])


# --- weapons
gunshot("fire_pistol", 0.16, 34, 210, 0.5, 0.45)
gunshot("fire_smg", 0.10, 48, 240, 0.4, 0.50)
gunshot("fire_shotgun", 0.38, 14, 85, 0.9, 0.30)
gunshot("fire_magnum", 0.30, 18, 62, 1.1, 0.35)

# crowbar swing: noise whoosh with a mid-swing amplitude bump
n = int(SR * 0.22)
wh = lowpass(noise(n), 0.18)
write_wav("swing", [wh[i] * math.sin(math.pi * i / n) ** 2 for i in range(n)])

# crowbar/bullet thunk on solids
n = int(SR * 0.12)
e = env_exp(n, 40)
th = sine(n, 105, 70)
cl = lowpass(noise(n), 0.6)
write_wav("hit_thunk", [(th[i] * 0.8 + cl[i] * 0.5) * e[i] for i in range(n)])

# crate shatter: crackle spikes over decaying noise
n = int(SR * 0.28)
e = env_exp(n, 16)
base = lowpass(noise(n), 0.5)
crackle = [0.0] * n
for _ in range(26):
    at = random.randrange(0, n - 80)
    amp = random.uniform(0.5, 1.0)
    for j in range(80):
        crackle[at + j] += amp * math.exp(-j / 14.0) * random.uniform(-1, 1)
write_wav("crate_break", [(base[i] * 0.5 + crackle[i]) * e[i] for i in range(n)])

# player hurt: falling rough square-ish grunt
n = int(SR * 0.24)
e = env_exp(n, 12)
sw = sine(n, 130, 65)
write_wav("player_hurt",
          [(1 if sw[i] > 0 else -1) * 0.6 * e[i] + sw[i] * 0.4 * e[i]
           for i in range(n)])

# enemy death: descending saw with noisy tail
n = int(SR * 0.5)
e = env_exp(n, 6)
out, phase = [], 0.0
for i in range(n):
    f = 180 - 130 * i / n
    phase = (phase + f / SR) % 1.0
    saw = 2 * phase - 1
    out.append((saw * 0.7 + random.uniform(-1, 1) * 0.25) * e[i])
write_wav("enemy_death", lowpass(out, 0.35))

# door servo: wobbling hum, ramps in and out
n = int(SR * 0.6)
hum = sine(n, 88)
wob = sine(n, 9)
write_wav("door_servo",
          [hum[i] * (0.7 + 0.3 * wob[i]) * math.sin(math.pi * i / n)
           for i in range(n)])

# pickup: two rising blips
n1, n2 = int(SR * 0.09), int(SR * 0.13)
b1 = [s * e for s, e in zip(sine(n1, 660), env_exp(n1, 26))]
b2 = [s * e for s, e in zip(sine(n2, 990), env_exp(n2, 20))]
write_wav("pickup", b1 + [0.0] * int(SR * 0.02) + b2)

# horde roar: low growl, amplitude-modulated, long decay
n = int(SR * 1.1)
e = env_exp(n, 3)
low = sine(n, 55, 40)
gr = lowpass(noise(n), 0.12)
am = sine(n, 13)
write_wav("horde_roar",
          [(low[i] * 0.8 + gr[i] * 0.7) * (0.65 + 0.35 * am[i]) * e[i]
           for i in range(n)])

# grenade explosion: 40 Hz boom + heavy noise burst, long decay
n = int(SR * 1.2)
e = env_exp(n, 5)
boom = sine(n, 40, 28)
burst = lowpass(noise(n), 0.25)
write_wav("explosion", [(boom[i] * 1.1 + burst[i] * 0.9) * e[i] for i in range(n)])

# footsteps: 4 concrete variants — low thud + crunch, small recipe variations
for v in range(4):
    n = int(SR * 0.09)
    e = env_exp(n, 55 + v * 6)
    thud = sine(n, 120 + v * 8, 80)
    crunch = lowpass(noise(n), 0.5 + v * 0.05)
    write_wav(f"step_concrete_{v}",
              [(thud[i] * 0.8 + crunch[i] * 0.45) * e[i] for i in range(n)])

# alarm siren: two-tone sweep, 1.5 s
n = int(SR * 1.5)
out, phase = [], 0.0
for i in range(n):
    t = i / SR
    f = 600 + 300 * (1 if int(t * 2) % 2 == 0 else 0)
    phase += 2 * math.pi * f / SR
    tri = 2 / math.pi * math.asin(math.sin(phase))
    out.append(tri * (0.9 if t < 1.35 else (1.5 - t) / 0.15))
write_wav("alarm_siren", out)


# --- seamless ambient loops (8 s; all periodic components are integer-cycle
# --- at the loop length so tail meets head without a pop)
def ambient(name, layers):
    n = SR * 8
    out = [0.0] * n
    for freq, amp, am_freq, am_depth in layers:
        phase = 0.0
        amp_phase = 0.0
        for i in range(n):
            phase += 2 * math.pi * freq / SR
            amp_phase += 2 * math.pi * am_freq / SR
            mod = 1.0 - am_depth + am_depth * math.sin(amp_phase)
            out[i] += amp * mod * math.sin(phase)
    write_wav(name, out)


# HECU radio barks: squelch (noise burst) + a short two-tone warble that
# reads as a garbled radio callout. Two variants for variety.
def radio_bark(name, tone_a, tone_b):
    pre = [s * e for s, e in zip(lowpass(noise(int(SR * 0.05)), 0.7),
                                 env_exp(int(SR * 0.05), 60))]
    n = int(SR * 0.35)
    body = []
    for i in range(n):
        f = tone_a if (i // int(SR * 0.05)) % 2 == 0 else tone_b
        body.append(math.sin(2 * math.pi * f * i / SR))
    body = [b * (0.5 + 0.5 * (i % 400 < 200)) for i, b in enumerate(body)]  # chop
    env = env_exp(n, 5)
    tail = [s * e for s, e in zip(lowpass(noise(int(SR * 0.04)), 0.7),
                                  env_exp(int(SR * 0.04), 70))]
    write_wav(name, pre + [body[i] * 0.5 * env[i] for i in range(n)] + tail)


radio_bark("radio_bark_a", 620, 480)
radio_bark("radio_bark_b", 540, 700)

ambient("ambient_hum", [(60, 0.7, 0.25, 0.15), (120, 0.15, 0.125, 0.3)])
ambient("ambient_lab", [(60, 0.6, 0.25, 0.1), (120, 0.2, 0.5, 0.4),
                        (240, 0.06, 0.125, 0.5)])
ambient("ambient_industrial", [(50, 0.8, 2.0, 0.35), (100, 0.25, 0.5, 0.3),
                               (25, 0.3, 0.125, 0.2)])
ambient("ambient_alien", [(33, 0.8, 0.5, 0.5), (66, 0.2, 0.125, 0.4),
                          (190, 0.05, 1.0, 0.7)])

print("ALL SFX DONE")
