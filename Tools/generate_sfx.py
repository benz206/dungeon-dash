#!/usr/bin/env python3
"""
Generates DungeonDash's retro SFX + ambience loop as 44.1kHz 16-bit mono WAV files.

A small jsfxr-style synth (square/saw/triangle/sine/noise oscillators, ADSR envelopes,
pitch slides, vibrato, a one-pole lowpass for lo-fi/whoosh filtering) built entirely on
the Python stdlib (wave/math/struct/random) -- no third-party dependencies, so this runs
anywhere Python 3 does.

Run:
    python3 Tools/generate_sfx.py

Writes to Assets/Resources/Audio/*.wav (Unity imports these as AudioClip resources on
the next editor pass; this script never writes .meta files). Deterministic: every
oscillator/noise call is seeded from SEED below, so re-running reproduces identical
bytes. Re-tune a sound by editing its gen_* function and re-running.
"""
import math
import os
import random
import struct
import wave

SEED = 20260717
SR = 44100
OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                        "Assets", "Resources", "Audio")

# ---------- oscillators (phase in cycles, wrap-safe) ----------

def osc_square(phase):
    return 1.0 if (phase % 1.0) < 0.5 else -1.0

def osc_saw(phase):
    return 2.0 * (phase % 1.0) - 1.0

def osc_triangle(phase):
    p = phase % 1.0
    return 4.0 * abs(p - 0.5) - 1.0

def osc_sine(phase):
    return math.sin(2.0 * math.pi * phase)

# ---------- building blocks ----------

def freq_slide(t, duration, start, end, curve=1.0):
    if start <= 0 or end <= 0 or duration <= 0:
        return start + (end - start) * (t / duration if duration > 0 else 0.0)
    x = (t / duration) ** curve
    return start * (end / start) ** x

def note(duration, wave_fn, start_freq, end_freq=None, curve=1.0, vibrato_hz=0.0, vibrato_depth=0.0):
    n = int(duration * SR)
    end_freq = start_freq if end_freq is None else end_freq
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = freq_slide(t, duration, start_freq, end_freq, curve)
        if vibrato_hz > 0:
            f *= 1.0 + vibrato_depth * math.sin(2.0 * math.pi * vibrato_hz * t)
        phase += f / SR
        out[i] = wave_fn(phase)
    return out

def noise(duration, local_rng):
    n = int(duration * SR)
    return [local_rng.uniform(-1.0, 1.0) for _ in range(n)]

def lowpass(samples, cutoff):
    # one-pole lowpass; cutoff is a fixed float or a per-sample list (Hz)
    out = [0.0] * len(samples)
    y = 0.0
    dt = 1.0 / SR
    for i, x in enumerate(samples):
        c = cutoff[i] if isinstance(cutoff, list) else cutoff
        rc = 1.0 / (2.0 * math.pi * max(c, 1.0))
        alpha = dt / (rc + dt)
        y += alpha * (x - y)
        out[i] = y
    return out

def adsr(n, attack, decay, sustain_level, release):
    a = min(int(attack * SR), n)
    r = min(int(release * SR), n - a)
    d = min(int(decay * SR), max(0, n - a - r))
    s = max(0, n - a - d - r)
    env = [i / a for i in range(a)] if a > 0 else []
    env += [1.0 - (1.0 - sustain_level) * (i / d) for i in range(d)] if d > 0 else []
    env += [sustain_level] * s
    env += [sustain_level * (1.0 - i / r) for i in range(r)] if r > 0 else []
    while len(env) < n:
        env.append(0.0)
    return env[:n]

def apply_env(samples, env):
    return [s * e for s, e in zip(samples, env)]

def pad(samples, n):
    return samples[:n] + [0.0] * max(0, n - len(samples))

def gain(samples, g):
    return [s * g for s in samples]

def mix(*layers):
    n = max(len(l) for l in layers)
    out = [0.0] * n
    for l in layers:
        for i, v in enumerate(l):
            out[i] += v
    return out

# ---------- one-shot clips ----------

def gen_swing_whoosh():
    d = 0.14
    n = int(d * SR)
    filtered = lowpass(noise(d, random.Random(SEED + 1)),
                        [freq_slide(i / SR, d, 3500, 500, 1.6) for i in range(n)])
    return apply_env(filtered, adsr(n, 0.01, 0.05, 0.3, 0.08))

def gen_bow_shot():
    d = 0.15
    n = int(d * SR)
    tone = apply_env(note(d, osc_saw, 950, 260, curve=1.8), adsr(n, 0.002, 0.1, 0.15, 0.05))
    click = noise(0.02, random.Random(SEED + 2))
    click = pad(apply_env(click, adsr(len(click), 0.001, 0.01, 0.0, 0.02)), n)
    return mix(gain(tone, 0.8), gain(click, 0.5))

def gen_hit_impact():
    d = 0.12
    n = int(d * SR)
    thud = apply_env(note(d, osc_sine, 180, 55, curve=2.2), adsr(n, 0.001, 0.09, 0.0, 0.03))
    click = noise(0.01, random.Random(SEED + 3))
    click = pad(apply_env(click, adsr(len(click), 0.0005, 0.008, 0.0, 0.005)), n)
    return mix(gain(thud, 0.9), gain(click, 0.35))

def gen_crit_impact():
    d = 0.16
    n = int(d * SR)
    thud = apply_env(note(d, osc_sine, 260, 60, curve=2.0), adsr(n, 0.001, 0.11, 0.0, 0.045))
    blip = note(0.05, osc_square, 1400, 900, curve=1.5)
    blip = pad(apply_env(blip, adsr(len(blip), 0.001, 0.03, 0.0, 0.015)), n)
    click = noise(0.015, random.Random(SEED + 4))
    click = pad(apply_env(click, adsr(len(click), 0.0005, 0.01, 0.0, 0.008)), n)
    return mix(gain(thud, 0.85), gain(blip, 0.5), gain(click, 0.45))

def gen_enemy_die():
    d = 0.32
    n = int(d * SR)
    sweep = note(d, osc_square, 420, 70, curve=1.6, vibrato_hz=28, vibrato_depth=0.03)
    sweep = apply_env(sweep, adsr(n, 0.005, 0.2, 0.1, 0.12))
    tail = noise(d, random.Random(SEED + 5))
    tail = lowpass(tail, [freq_slide(i / SR, d, 2200, 300, 1.4) for i in range(n)])
    tail = apply_env(tail, adsr(n, 0.01, 0.18, 0.05, 0.15))
    return mix(gain(sweep, 0.75), gain(tail, 0.35))

def gen_player_hurt():
    d = 0.2
    n = int(d * SR)
    tone = note(d, osc_saw, 500, 140, curve=1.7, vibrato_hz=35, vibrato_depth=0.05)
    tone = apply_env(tone, adsr(n, 0.002, 0.14, 0.05, 0.06))
    noiz = lowpass(noise(d, random.Random(SEED + 6)),
                    [freq_slide(i / SR, d, 2500, 400, 1.5) for i in range(n)])
    noiz = apply_env(noiz, adsr(n, 0.001, 0.1, 0.0, 0.09))
    return mix(gain(tone, 0.8), gain(noiz, 0.45))

def gen_coin():
    d1, d2 = 0.06, 0.09
    n1, n2 = int(d1 * SR), int(d2 * SR)
    a = apply_env(note(d1, osc_square, 1200), adsr(n1, 0.002, 0.02, 0.4, 0.03))
    b = apply_env(note(d2, osc_square, 1800), adsr(n2, 0.002, 0.03, 0.3, 0.05))
    return a + b

def gen_potion():
    d = 0.25
    n = int(d * SR)
    tone = note(d, osc_triangle, 420, 880, curve=0.7)
    return apply_env(tone, adsr(n, 0.02, 0.05, 0.6, 0.15))

def gen_chest_open():
    out = []
    for f in (520, 660, 880):
        n = int(0.13 * SR)
        t = apply_env(note(0.13, osc_square, f), adsr(n, 0.003, 0.03, 0.5, 0.06))
        out += t
    return out

def gen_bomb_explode():
    d = 0.8
    n = int(d * SR)
    thump = apply_env(note(d, osc_sine, 95, 35, curve=2.5), adsr(n, 0.002, 0.35, 0.0, 0.2))
    tail = lowpass(noise(d, random.Random(SEED + 7)),
                    [freq_slide(i / SR, d, 3500, 250, 1.3) for i in range(n)])
    tail = apply_env(tail, adsr(n, 0.001, 0.5, 0.15, 0.35))
    crack = noise(0.03, random.Random(SEED + 8))
    crack = pad(apply_env(crack, adsr(len(crack), 0.0005, 0.02, 0.0, 0.01)), n)
    return mix(gain(thump, 0.85), gain(tail, 0.6), gain(crack, 0.4))

def gen_dash_whoosh():
    d = 0.1
    n = int(d * SR)
    filtered = lowpass(noise(d, random.Random(SEED + 9)),
                        [freq_slide(i / SR, d, 4200, 900, 1.7) for i in range(n)])
    return apply_env(filtered, adsr(n, 0.005, 0.04, 0.2, 0.05))

def gen_ui_click():
    d = 0.045
    n = int(d * SR)
    tone = note(d, osc_square, 1500, 1100, curve=1.2)
    return apply_env(tone, adsr(n, 0.001, 0.02, 0.0, 0.02))

def gen_ui_hover_soft():
    d = 0.03
    n = int(d * SR)
    tone = note(d, osc_sine, 900, 950)
    return apply_env(tone, adsr(n, 0.002, 0.01, 0.0, 0.015))

def gen_wave_start():
    out = []
    for f in (440, 554, 660, 880):
        n = int(0.11 * SR)
        t = apply_env(note(0.11, osc_square, f, f * 1.01), adsr(n, 0.004, 0.03, 0.55, 0.05))
        out += t
    return out

def gen_game_over():
    notes = (523, 466, 415, 349)
    out = []
    for i, f in enumerate(notes):
        n = int(0.14 * SR)
        release = 0.09 if i == len(notes) - 1 else 0.04
        t = apply_env(note(0.14, osc_triangle, f, f * 0.98), adsr(n, 0.005, 0.03, 0.5, release))
        out += t
    return out

def gen_artifact_drop():
    out = []
    for f in (660, 880, 1100, 1320):
        n = int(0.09 * SR)
        a = note(0.09, osc_triangle, f, f * 1.02)
        b = note(0.09, osc_triangle, f * 1.008, f * 1.02 * 1.008)
        t = mix(gain(a, 0.7), gain(b, 0.5))
        t = apply_env(t, adsr(n, 0.003, 0.03, 0.5, 0.05))
        out += t
    return out

# ---------- ambient loop ----------

def gen_dungeon_ambience():
    loop_duration = 10.0
    crossfade = 1.0
    loop_n = int(loop_duration * SR)
    cf_n = int(crossfade * SR)
    total_n = loop_n + cf_n

    # Drone: sine partials at exact k / loop_duration Hz are perfectly periodic over the
    # loop, so no crossfade is needed for the tonal layer -- it just wraps cleanly.
    f1, f2 = 550 / loop_duration, 554 / loop_duration  # 55.0Hz + 55.4Hz -> slow 0.4Hz beat
    swell_f = 1 / loop_duration
    drone = [0.0] * loop_n
    for i in range(loop_n):
        t = i / SR
        d = 0.6 * math.sin(2.0 * math.pi * f1 * t) + 0.4 * math.sin(2.0 * math.pi * f2 * t)
        drone[i] = d * (0.75 + 0.25 * math.sin(2.0 * math.pi * swell_f * t))

    # Airy noise isn't naturally periodic, so generate one continuous filtered-noise take
    # spanning loop+crossfade and splice the overrun back into the head (equal-power-ish
    # linear crossfade) so the loop point has no discontinuity.
    local = random.Random(SEED + 100)
    raw = [local.uniform(-1.0, 1.0) for _ in range(total_n)]
    airy = lowpass(lowpass(raw, 600.0), 600.0)
    spliced = [0.0] * loop_n
    for i in range(cf_n):
        w = i / cf_n
        spliced[i] = airy[loop_n + i] * (1.0 - w) + airy[i] * w
    for i in range(cf_n, loop_n):
        spliced[i] = airy[i]

    return mix(gain(drone, 0.5), gain(spliced, 0.35))

# ---------- finalize / write / verify ----------

def finalize(samples, target_peak_db=-3.0, fade_ms=6.0, loop=False):
    if any(math.isnan(s) or math.isinf(s) for s in samples):
        raise ValueError("NaN/Inf in generated samples")
    peak = max((abs(s) for s in samples), default=0.0)
    if peak > 0:
        scale = (10 ** (target_peak_db / 20.0)) / peak
        samples = [s * scale for s in samples]
    if not loop and fade_ms > 0:
        fade_n = min(int(fade_ms / 1000.0 * SR), len(samples))
        for i in range(fade_n):
            samples[-(i + 1)] *= i / fade_n
    peak_after = max((abs(s) for s in samples), default=0.0)
    if peak_after > 1.0 + 1e-6:
        raise ValueError("clipping after normalize")
    return samples, peak_after

def write_wav(path, samples):
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SR)
        frames = struct.pack("<%dh" % len(samples),
                              *(int(max(-1.0, min(1.0, s)) * 32767) for s in samples))
        f.writeframes(frames)

CLIPS = [
    ("swing_whoosh", gen_swing_whoosh, False),
    ("bow_shot", gen_bow_shot, False),
    ("hit_impact", gen_hit_impact, False),
    ("crit_impact", gen_crit_impact, False),
    ("enemy_die", gen_enemy_die, False),
    ("player_hurt", gen_player_hurt, False),
    ("coin", gen_coin, False),
    ("potion", gen_potion, False),
    ("chest_open", gen_chest_open, False),
    ("bomb_explode", gen_bomb_explode, False),
    ("dash_whoosh", gen_dash_whoosh, False),
    ("ui_click", gen_ui_click, False),
    ("ui_hover_soft", gen_ui_hover_soft, False),
    ("wave_start", gen_wave_start, False),
    ("game_over", gen_game_over, False),
    ("artifact_drop", gen_artifact_drop, False),
    ("dungeon_ambience", gen_dungeon_ambience, True),
]

def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    print(f"{'clip':<20}{'duration':>10}{'peak dBFS':>12}")
    for name, gen_fn, is_loop in CLIPS:
        samples, peak = finalize(gen_fn(), loop=is_loop)
        write_wav(os.path.join(OUT_DIR, name + ".wav"), samples)
        duration = len(samples) / SR
        peak_db = 20 * math.log10(peak) if peak > 0 else float("-inf")
        print(f"{name:<20}{duration:>9.3f}s{peak_db:>10.2f}dB")

if __name__ == "__main__":
    main()
