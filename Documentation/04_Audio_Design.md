# 04 · Audio Design

All audio is **synthesised from scratch** — no samples, no downloads. The toolkit
is pure NumPy (`Tooling/Audio/synth_engine.py`); the compositions are in
`generate_all_audio.py`. The output is already baked into the project as **12 mono
48 kHz WAVs** under `Assets/_Project/Audio/`, so you can listen immediately and
regenerate any time.

## Design goals

- **Diegetic and tactile.** Every interaction has a physical-feeling sound so cause
  and effect register in the body, not just the eyes. Mechanisms sound mechanical.
- **Period-appropriate timbre.** Ancient = warm/airy/stone; WWII = electrical hum +
  teleprinter clatter; modern = clean digital hum. The reveal resolves to music.
- **Quest-cheap.** Mono clips, baked reverb (no realtime DSP reverb), a small
  spatialised voice pool, 2D ambient. CPU spent on synthesis happens offline.

## The synthesis toolkit (`synth_engine.py`)

A compact but real DSP kit, written to be read:

- **Oscillators:** `sine`, `saw`, `square`, `triangle`, band-limited-ish; `noise`;
  `chirp` (phase-correct linear sweep).
- **Envelopes:** `adsr`, `perc_env` (fast attack + exponential decay for
  transients), `fade` (click-free edges).
- **Filters:** RBJ-design **biquad** `lowpass`/`highpass`/`bandpass`, plus a cheap
  `one_pole_lp` for slow control-rate shaping.
- **Saturation:** `soft_clip` (tanh) for warmth and glue.
- **Reverb:** a **Schroeder** network (4 parallel combs + 2 series allpasses) with a
  `room` size control — used sparingly on the chime, lamp and final chord.
- **Loop helper:** `make_seamless` crossfades a buffer's tail into its head so
  ambient beds tile without a click.
- **I/O:** 16-bit PCM WAV via the standard-library `wave` module (so only NumPy is
  needed); plus musical helpers (`note_freq`, `chord`).

## The twelve sounds

### One-shots (`Assets/_Project/Audio/SFX/`)

| Key | Character | How it's made |
|-----|-----------|---------------|
| `sfx_brass_click` | crisp dial detent | band-passed noise burst + two high partials, fast perc env, light soft-clip |
| `sfx_gear_step` | ratchet advance | low sine thunk + band-passed tick + tiny HF noise |
| `sfx_key_clack` | typewriter key | key-down strike (BP noise + woody sine) and a softer key-up ~70 ms later |
| `sfx_lamp_on` | glassy ping | upward chirp into a sustained tone + a partial, small-room reverb |
| `sfx_lever_pull` | weighted throw + clunk | falling LP-noise friction + mechanical rattle, ending in a low clunk |
| `sfx_success_chime` | bright reward | ascending C–E–G–C arpeggio (triangle+sine), medium reverb |
| `sfx_vault_rumble` | deep unlock swell | layered 40/55/80 Hz sines + LP noise, slow swell, amplitude wobble |
| `sfx_final_chord` | resolving cadence | six-voice C-major spread (triangle+saw, per-voice detune), slow bloom, big reverb + shimmer |

### Ambient loops (`Assets/_Project/Audio/Ambient/`)

| Key | Room | How it's made |
|-----|------|---------------|
| `amb_atrium` | Splash/Atrium/Reveal | soft A-major triangle pad + airy LP noise, slow tremolo, seamless |
| `amb_ancient` | Ancient | low beating drone + slow LP-noise "wind" + sparse high shimmer |
| `amb_wwii` | WWII | 60/120 Hz mains hum + band-passed room noise + an irregular teleprinter tick train |
| `amb_vault` | Vault | clean 80/160 Hz drone + faint 2 kHz digital shimmer + occasional soft beeps |

All are baked at 48 kHz mono. SFX durations range ~0.12 s (clicks) to 3.5 s (final
chord); ambient loops are ~5.4–6.2 s and tile seamlessly.

## Runtime audio (`AudioManager`)

- **Pooled spatial one-shots.** A fixed pool of 3D `AudioSource`s (no per-call
  allocation), linear rolloff, doppler off (mechanical/UI sounds shouldn't pitch-
  shift). Played via `AudioManager.Instance.Play(key, position, …)`.
- **Crossfading ambient bed.** Two 2D ambient sources crossfade on room entry,
  driven by the room's `RoomDescriptor.ambientKey`.
- **Mixer snapshots.** Optional per-room snapshot transitions for tonal shifts.
- **Synthesis fallback.** If a key isn't found in the clip library and *Synthesise
  Missing* is on, the runtime `AudioSynth` (`Scripts/Util/`) generates a stand-in so
  the project is never silent — useful before you import the WAVs.

## Assigning the WAVs

1. Select the **AudioManager** in the scene.
2. For each WAV in `Assets/_Project/Audio/`, add an entry to the **Clip Library**
   with the **key = filename without extension** (e.g. `sfx_lever_pull`).
3. Route the SFX/Ambient/UI mixer groups if you use a mixer (optional).
4. Keep **Synthesise Missing** enabled as a safety net.

> Import tip: for these short mono clips, **Decompress On Load** (or ADPCM for the
> longer ambient loops) keeps latency low at negligible memory cost on Quest.

## Re-baking / tweaking

Edit a recipe in `generate_all_audio.py` (or a primitive in `synth_engine.py`) and
run `python Tooling/Audio/generate_all_audio.py`. Files are overwritten in place;
re-import in Unity. `synth_engine.py` also has a `__main__` self-test that writes a
single 440 Hz ADSR tone, handy for verifying your environment.
