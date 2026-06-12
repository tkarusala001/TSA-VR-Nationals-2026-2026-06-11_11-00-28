# 06 · Storyboard & Recording

This is the shot list for the showcase video and the workflow to capture it. The
project ships a **Demo Mode** that plays the entire museum hands-free, so the
capture is repeatable and the camera operator can focus on framing.

Target runtime: **~2:30–2:50**.

## Minute-by-minute

| Time | Beat | On screen | Audio |
|------|------|-----------|-------|
| 0:00–0:12 | **Cold open / Splash** | Dark antechamber, glowing **PLAY**, tutorial cards miming grab/poke/point. Title fades in. | `amb_atrium`; soft UI tone on PLAY |
| 0:12–0:30 | **Atrium** | Lofty hub, museum title treatment, the one-line intent plaque; camera drifts toward the first doorway. | `amb_atrium` |
| 0:30–1:05 | **Caesar disk** | Hands grab the inner ring and twist; mapping lines snap between letter pairs; readout ticks through shifts; at **+3** `CROSS THE RUBICON` resolves and chimes. Door opens. | `amb_ancient`; `sfx_brass_click` per detent; `sfx_success_chime` |
| 1:05–1:45 | **Enigma** | Rotors set to **MAC**; keys pressed for `ZLDFDQO`; the signal trace lights through the rotors to a lamp on each press; decoded word builds to **`VICTORY`**; the lever is pulled — machine powers up. Door opens. | `amb_wwii`; `sfx_key_clack`, `sfx_lamp_on`, `sfx_gear_step`, `sfx_lever_pull`; `sfx_success_chime` |
| 1:45–2:15 | **Vault** | Laser pointer taps `V-I-C-T-O-R-Y` on the keypad; ENTER; status flips green; the locking ring spins; the door swings open; the room warms and the cyan archive is revealed. | `amb_vault`; `sfx_key_clack`; `sfx_vault_rumble`; `sfx_success_chime` |
| 2:15–2:45 | **Reveal** | The sculpture morphs Roman stele → clockwork gears → circuit lattice as the conclusion plaque fades in; final chord resolves; gentle hold on the finished tableau. | `amb_atrium`; `sfx_final_chord` |
| 2:45–2:50 | **Tag** | Title/credit card; fade to black. | tail of chord |

The conclusion plaque reads, verbatim: *"From Caesar's alphabet shifts to modern
digital security, cryptography protects information by transforming meaning into
secrets only the intended recipient can reveal."*

## Camera approach

Two good options:

1. **Headset POV (simplest, most honest).** Record the actual device view. With
   Demo Mode driving the puzzles, the operator just looks where the action is. Use
   a slightly **reduced comfort vignette** and a steady gaze; avoid fast head turns.
2. **Mixed-reality / spectator camera (most cinematic).** Add a second camera rig as
   a "spectator" that dollies along a pre-authored path (a simple animated transform
   or a spline). Frame each exhibit at a 3/4 angle, push in on the solve moment, and
   carry the cut to the next doorway as it opens. Keep moves slow and motivated.

Either way: **one focal point per shot** (the design already guarantees it), let
the lighting lead the eye, and time your pushes to land on the reward (chime / lamp
/ door / morph).

## Demo Mode (hands-free auto-play)

`DemoDirector` (in `Scripts/Core/`) performs every exhibit's solution on a human-
paced timeline so you can record without controllers in your hands.

**Enable it:** on the `GameManager`, turn on **Demo Mode**. On Start it calls
`DemoDirector.BeginAutoPlay(...)`, which:

- presses **PLAY** after a short beat (Splash → Atrium),
- dwells in the Atrium, then advances to the Ancient room,
- rotates the **Caesar** disk to +3 (solve → auto-advance),
- keys **MAC**, types `ZLDFDQO` → `VICTORY`, pulls the lever (solve → auto-advance),
- types **`VICTORY`** on the vault and presses Enter (unlock → auto-advance),
- lets the **Reveal** play out to `Complete`.

Pacing is tunable on the component: splash delay, atrium dwell, per-exhibit read
dwell, and per-keypress cadence. The director only scripts **input** — room loads,
fades and player placement remain the `SceneController`'s job — so the same scripted
solves double as an integration smoke-test.

> Tip: the per-exhibit `AutoSolve`/`AutoEnter` coroutines are public, so you can
> trigger an individual exhibit's solution from a custom recording timeline if you
> want finer control than the full auto-run.

## Capture & export

**On-device (recommended for true frame timing):**
- Use the headset's built-in capture, or `adb`:
  - Start: `adb shell setprop debug.oculus.capture.width 1920` (optional sizing),
    then record via the system capture UI.
  - Pull: `adb pull /sdcard/Oculus/VideoShots/ ./capture/`.
- Verify the clip holds frame-rate with the **OVR Metrics Tool** overlay during a
  dry run (then disable the overlay for the clean take).

**In-editor (fastest iteration, not true device perf):**
- Use Unity **Recorder** (Window ▸ General ▸ Recorder) to capture the Game view (or
  the spectator camera) to an MP4 at 1080p/60. Great for blocking the edit before
  the on-device take.

**Post:**
- Trim to the beat list above; keep cuts on the reward moments.
- Export **H.264/MP4, 1080p, 30–60 fps**. Add the title/credit card on the tag.
- If you used the headset POV, a light stabilise pass on yaw helps comfort for
  viewers watching on a flat screen.

## Pre-flight checklist

- [ ] One room active at a time during transitions (no double-rendered rooms).
- [ ] Lighting baked; reflection probes assigned per room.
- [ ] Audio WAVs assigned by key on the `AudioManager`.
- [ ] Demo Mode on; pacing tuned for your shot length.
- [ ] 72 Hz confirmed (OVR Metrics) on a dry run.
- [ ] Comfort vignette set; clean take recorded without debug overlays.
