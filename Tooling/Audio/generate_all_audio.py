#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  generate_all_audio.py
#  DECRYPTED — A Walk Through the History of Secret Writing
#
#  Bakes EVERY sound the museum uses, entirely from synthesis (no samples). Each
#  recipe below is a short, readable composition of the synth_engine primitives,
#  and the output filenames match the keys the Unity AudioManager looks up, so a
#  designer just drops the generated WAVs into the AudioManager's clip library.
#
#  Output:
#    Assets/_Project/Audio/SFX/      sfx_*.wav   (one-shots, mono, 48 kHz)
#    Assets/_Project/Audio/Ambient/  amb_*.wav   (seamless loops)
#
#  Run:  python generate_all_audio.py
#  (NumPy is the only dependency; WAV writing uses the standard library.)
# -----------------------------------------------------------------------------

import os
import numpy as np
import synth_engine as se

# Resolve output dirs relative to this file so it works from any CWD.
HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SFX_DIR = os.path.join(PROJECT, "Assets", "_Project", "Audio", "SFX")
AMB_DIR = os.path.join(PROJECT, "Assets", "_Project", "Audio", "Ambient")


# ============================================================== ONE-SHOT SFX

def sfx_brass_click():
    """Short, bright metallic click for dial detents / generic UI."""
    dur = 0.12
    body = se.bandpass(se.noise(dur, seed=1), 2600, q=3.0) * se.perc_env(dur, 0.0005)
    p1 = se.sine(1850, dur) * se.perc_env(dur, 0.0008) * 0.5
    p2 = se.sine(3300, dur) * se.perc_env(dur, 0.0008) * 0.3
    out = se.soft_clip(se.mix(body, p1, p2), 1.3)
    return se.fade(se.normalize(out, 0.9), 0.0005, 0.02)


def sfx_gear_step():
    """A single ratchet/gear advance: a low thunk under a crisp tick."""
    dur = 0.16
    thunk = se.sine(128, dur) * se.perc_env(dur, 0.001) * 0.8
    tick = se.bandpass(se.noise(dur, seed=2), 1300, q=2.5) * se.perc_env(dur, 0.0004)
    hi = se.highpass(se.noise(0.03, seed=3), 4000) * se.perc_env(0.03, 0.0003) * 0.5
    out = se.mix(thunk, tick * 0.7, hi)
    return se.fade(se.normalize(out, 0.85), 0.0005, 0.03)


def sfx_key_clack():
    """Mechanical keyboard key: a key-down strike and a softer key-up."""
    down = se.bandpass(se.noise(0.05, seed=4), 950, q=2.0) * se.perc_env(0.05, 0.0004)
    wood = se.sine(330, 0.06) * se.perc_env(0.06, 0.001) * 0.6
    up = se.bandpass(se.noise(0.04, seed=5), 1400, q=2.5) * se.perc_env(0.04, 0.0004) * 0.5
    # Place the key-up ~70 ms after the strike.
    out = se.mix(se.mix(down, wood), se.pad_to(np.concatenate([se.silence(0.07), up]), 0.13))
    return se.fade(se.normalize(out, 0.85), 0.0005, 0.02)


def sfx_lamp_on():
    """Soft glassy 'ping' as an Enigma lamp lights — a tiny upward chirp + tone."""
    attack_chirp = se.chirp(900, 1150, 0.05)
    tone = se.sine(1150, 0.5)
    body = np.concatenate([attack_chirp, tone[attack_chirp.size:]])
    body *= se.adsr(0.5, 0.005, 0.12, 0.32, 0.34)
    partial = se.sine(2300, 0.5) * se.adsr(0.5, 0.005, 0.1, 0.15, 0.34) * 0.25
    out = se.reverb(se.mix(body, partial), mix_wet=0.18, room=0.6)
    return se.fade(se.normalize(out, 0.8), 0.002, 0.05)


def sfx_lever_pull():
    """A weighted lever thrown down, ending in a solid clunk."""
    dur = 0.55
    env = se.adsr(dur, 0.01, 0.2, 0.5, 0.2)
    sweep = se.lowpass(se.noise(dur, seed=6), 1200) * env * 0.7      # friction/throw
    rattle = se.chirp(220, 120, dur) * se.square(8, dur) * 0.15 * env  # mechanical rattle
    # Clunk at the end of the travel.
    clunk = se.mix(
        se.sine(90, 0.12) * se.perc_env(0.12, 0.001),
        se.bandpass(se.noise(0.12, seed=7), 700, q=1.5) * se.perc_env(0.12, 0.0006) * 0.6,
    )
    tail = np.concatenate([se.silence(dur - 0.12), clunk])
    out = se.soft_clip(se.mix(sweep, rattle, se.pad_to(tail, dur)), 1.2)
    return se.fade(se.normalize(out, 0.92), 0.003, 0.03)


def sfx_success_chime():
    """A bright ascending major arpeggio (C5–E5–G5–C6) with light reverb."""
    notes = [72, 76, 79, 84]
    stagger = 0.12
    total = stagger * len(notes) + 0.7
    out = se.silence(total)
    for i, m in enumerate(notes):
        f = se.note_freq(m)
        n = se.mix(se.triangle(f, 0.7), se.sine(f * 2, 0.7) * 0.2)
        n *= se.adsr(0.7, 0.005, 0.15, 0.4, 0.5)
        placed = np.concatenate([se.silence(i * stagger), n])
        out = se.mix(out, placed)
    out = se.reverb(out, mix_wet=0.32, room=1.0)
    return se.fade(se.normalize(out, 0.9), 0.002, 0.1)


def sfx_vault_rumble():
    """A deep mechanical rumble that swells as the vault unlocks."""
    dur = 2.5
    swell = se.adsr(dur, 0.8, 0.4, 0.9, 0.5)
    lows = se.mix(
        se.sine(40, dur), se.sine(55, dur) * 0.7, se.sine(80, dur) * 0.5,
    ) * swell
    grind = se.lowpass(se.noise(dur, seed=8), 120) * swell * 0.6
    wobble = 1.0 + 0.2 * se.sine(6, dur)
    out = se.soft_clip((lows + grind) * wobble, 1.1)
    return se.fade(se.normalize(out, 0.95), 0.05, 0.2)


def sfx_final_chord():
    """A lush, slow-blooming major chord that resolves the experience."""
    dur = 3.5
    midis = [48, 55, 60, 64, 67, 72]   # C major spread across octaves
    out = se.silence(dur)
    for k, m in enumerate(midis):
        f = se.note_freq(m) * (1.0 + 0.0015 * (k - 2))  # gentle per-voice detune
        voice = se.mix(se.triangle(f, dur) * 0.7, se.saw(f, dur) * 0.3)
        voice *= se.adsr(dur, 1.2, 0.3, 0.85, 1.0)
        out = se.mix(out, voice)
    shimmer = se.sine(se.note_freq(84), dur) * se.adsr(dur, 1.8, 0.4, 0.4, 1.0) * 0.2
    out = se.reverb(se.mix(out, shimmer), mix_wet=0.4, room=1.3)
    return se.fade(se.normalize(out, 0.95), 0.01, 0.3)


# ============================================================ AMBIENT LOOPS

def amb_ancient():
    """Airy stone-hall bed: low drone, soft wind, sparse high shimmer."""
    dur = 7.0
    drone = se.mix(
        se.sine(110, dur), se.sine(111.1, dur) * 0.8,   # slow beating
        se.sine(55, dur) * 0.6,
    ) * 0.25
    wind = se.lowpass(se.noise(dur, seed=11), 400)
    wind *= (0.15 + 0.1 * se.one_pole_lp(se.noise(dur, seed=12), 0.0008))  # slow gusts
    shimmer = (se.sine(1200, dur) * (0.03 + 0.02 * se.sine(0.2, dur)) +
               se.sine(1500, dur) * (0.02 + 0.015 * se.sine(0.13, dur)))
    out = se.mix(drone, wind, shimmer)
    out = se.make_seamless(out, 0.8)
    return se.normalize(out, 0.62)


def amb_wwii():
    """Operations-room bed: mains hum, faint teleprinter ticks, room noise."""
    dur = 6.0
    hum = se.mix(se.sine(60, dur), se.sine(120, dur) * 0.4) * 0.2
    roomn = se.bandpass(se.noise(dur, seed=13), 500, q=0.8) * 0.08
    # A sparse train of mechanical ticks (~ every 0.5 s, slightly irregular).
    ticks = se.silence(dur)
    rng = np.random.default_rng(21)
    pos = 0.2
    while pos < dur - 0.1:
        click = (se.bandpass(se.noise(0.03, seed=int(pos * 1000) % 9999), 1500, q=2.0)
                 * se.perc_env(0.03, 0.0004) * 0.25)
        ticks = se.mix(ticks, np.concatenate([se.silence(pos), click]))
        pos += 0.45 + rng.uniform(-0.05, 0.1)
    out = se.mix(hum, roomn, se.pad_to(ticks, dur))
    out = se.make_seamless(out, 0.6)
    return se.normalize(out, 0.6)


def amb_vault():
    """Modern server-room bed: clean low drone, digital shimmer, soft beeps."""
    dur = 6.0
    drone = se.mix(se.sine(80, dur), se.sine(160.4, dur) * 0.5) * 0.22
    shimmer = se.sine(2000, dur) * (0.02 + 0.015 * se.sine(0.3, dur))
    beeps = se.silence(dur)
    for at in (1.3, 3.9):
        b = se.sine(880, 0.08) * se.perc_env(0.08, 0.002) * 0.18
        beeps = se.mix(beeps, np.concatenate([se.silence(at), b]))
    out = se.mix(drone, shimmer, se.pad_to(beeps, dur))
    out = se.make_seamless(out, 0.6)
    return se.normalize(out, 0.6)


def amb_atrium():
    """Welcoming neutral pad with a little air — sets the tone at entry."""
    dur = 7.0
    pad = se.chord([57, 61, 64], dur, osc=se.triangle)   # A major
    pad *= (0.5 + 0.1 * se.sine(0.15, dur))              # slow tremolo
    air = se.lowpass(se.noise(dur, seed=14), 800) * (0.08 + 0.05 * se.sine(0.1, dur))
    out = se.mix(pad * 0.5, air)
    out = se.make_seamless(out, 0.9)
    return se.normalize(out, 0.6)


# ===================================================================== driver

SFX = {
    "sfx_brass_click":   sfx_brass_click,
    "sfx_gear_step":     sfx_gear_step,
    "sfx_key_clack":     sfx_key_clack,
    "sfx_lamp_on":       sfx_lamp_on,
    "sfx_lever_pull":    sfx_lever_pull,
    "sfx_success_chime": sfx_success_chime,
    "sfx_vault_rumble":  sfx_vault_rumble,
    "sfx_final_chord":   sfx_final_chord,
}

AMBIENT = {
    "amb_ancient": amb_ancient,
    "amb_wwii":    amb_wwii,
    "amb_vault":   amb_vault,
    "amb_atrium":  amb_atrium,
}


def main():
    os.makedirs(SFX_DIR, exist_ok=True)
    os.makedirs(AMB_DIR, exist_ok=True)

    print("Baking one-shot SFX…")
    for key, fn in SFX.items():
        path = os.path.join(SFX_DIR, key + ".wav")
        se.write_wav(path, fn())
        print(f"  ✓ {key}.wav")

    print("Baking ambient loops…")
    for key, fn in AMBIENT.items():
        path = os.path.join(AMB_DIR, key + ".wav")
        se.write_wav(path, fn())
        print(f"  ✓ {key}.wav")

    print("\nDone. Import these into Unity and assign them by key in the "
          "AudioManager clip library (see Documentation/04_Audio_Design.md).")


if __name__ == "__main__":
    main()
