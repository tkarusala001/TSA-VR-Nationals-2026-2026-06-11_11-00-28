# 00 · Project Overview

## Executive vision

**DECRYPTED** turns an abstract subject — how humans have kept secrets in writing —
into something you do with your hands. A visitor walks a single, unbranching path
through four spaces. In each, a real mechanism teaches one idea by letting the
visitor operate it: rotate a cipher disk and watch letters re-map; type on an
Enigma and watch current trace through rotors to a lamp; speak a recovered secret
to a vault that only opens for whoever holds it. The museum never lectures. It
hands you the machine and lets the "aha" arrive through use.

The emotional arc is deliberate: **curiosity → mastery → consequence**. You begin
playing with a toy-like brass disk, graduate to a genuinely intricate machine, and
end by realising that the same principle — *transform meaning so only the intended
recipient can recover it* — runs from Caesar straight through to the encryption
protecting a modern archive. The final chamber states that thesis in one sentence
and shows it as a sculpture dissolving from Roman stone to clockwork to circuitry.

## Learning design (why it teaches without narration)

Spoken narration is fragile in VR (localisation, pacing, accessibility, and it
competes with presence). Instead the museum teaches through four reinforcing
channels:

1. **Direct manipulation.** The core mechanic of each exhibit *is* the concept.
   Caesar = a constant alphabet offset; the disk literally is that offset. Enigma
   = a stepped poly-alphabetic substitution; the rotors literally are that.
2. **Live visualisation.** The Caesar disk draws mapping lines between ciphertext
   and plaintext letters; the Enigma renders the electrical path of each keypress.
   The transformation is made visible the instant you cause it.
3. **Diegetic text.** TextMeshPro plaques give the historical frame and the "why",
   short enough to read standing up. No walls of text.
4. **Audio feedback.** Every action has a tactile sound — a detent click, a key
   clack, a lamp's glassy ping, the vault's rumble — so cause and effect register
   physically.

Progression is **gated**: a room's exit only opens once its exhibit is solved, so
nobody reaches the vault without having decrypted the word it asks for. The reward
for solving is always immediate and sensory (a chime, a power-up, a door).

## System architecture

The project is **event-driven** around a tiny static `EventBus`. Systems never
call each other directly across domains; they publish and subscribe to typed
events (readonly structs in `GameEvents.cs`). This keeps exhibits, audio, UI and
flow fully decoupled and individually testable.

```
                         ┌───────────────┐
                         │   EventBus     │  (static publish/subscribe)
                         └──────┬─────────┘
        publishes / subscribes  │
   ┌───────────────┬────────────┼─────────────┬─────────────────┐
   │               │            │             │                 │
┌──▼───────┐ ┌─────▼──────┐ ┌───▼────────┐ ┌──▼──────────┐ ┌────▼────────┐
│GameManager│ │SceneCtrl  │ │AudioManager│ │ UIManager   │ │ Exhibits    │
│(flow,     │ │(room load,│ │(pooled SFX,│ │(plaques,    │ │ Caesar/     │
│ gating)   │ │ fades)    │ │ ambient,   │ │ hints,      │ │ Enigma/     │
│           │ │           │ │ snapshots) │ │ tutorial)   │ │ Vault/Reveal│
└──┬────────┘ └───────────┘ └────────────┘ └─────────────┘ └─────────────┘
   │ owns
   └── MuseumState machine: Boot→Splash→Atrium→Ancient→WWII→Vault→Reveal→Complete
```

Key components:

- **`GameManager`** owns the linear `MuseumState` machine, enforces gating, and on a
  solve publishes `ExhibitSolvedEvent` and auto-advances after a short delay.
- **`SceneController`** performs the actual room transition: fade out, toggle the
  next room root active (room-based culling), reposition the player rig, swap
  ambient audio + reflection probe via the room's `RoomDescriptor`, fade in.
- **`AudioManager`** is a pooled, spatialised one-shot player + crossfading ambient
  bed + mixer-snapshot controller. Clips come from baked WAVs, with a runtime
  `AudioSynth` fallback so the project is never silent even before import.
- **`PerformanceManager`** holds the frame budget (fixed foveation, fixed refresh,
  CPU/GPU levels) behind an `OCULUS_XR_PRESENT` guard so it compiles with or
  without the Oculus package present.
- **Exhibit controllers** are self-contained and talk to the world only through the
  bus and the `GameManager` solve API.
- **`DemoDirector`** drives a hands-free run for recording (see `06`).

## The puzzle chain

| Exhibit | Input | Output | Wired so… |
|---------|-------|--------|-----------|
| Caesar disk | rotate to shift **+3** | `FURVV WKH UXELFRQ` → `CROSS THE RUBICON` | target shift fixed in controller |
| Enigma | key **MAC**, type `ZLDFDQO` | **`VICTORY`** | machine auto-calibrated to the word pair |
| Vault | type the recovered word | opens | passphrase auto-sourced from the Enigma |

Because the vault reads its passphrase from the Enigma component, and the Enigma
calibrates itself to the fixed plaintext/ciphertext pair, the three exhibits share
a single source of truth and cannot fall out of sync if a designer edits a string.

## A note on historical accuracy (Enigma)

The Enigma exhibit is a **historically-inspired, pedagogically-calibrated**
machine, **not** a bit-exact simulation of a wartime Enigma. It implements the
property that makes Enigma *feel* like Enigma and matters for the lesson — a
**reciprocal** poly-alphabetic substitution (the same setting that encrypts a
letter also decrypts it) with **per-position stepping** — and it is auto-calibrated
so that, at key **MAC**, the ciphertext `ZLDFDQO` decodes to `VICTORY`. We verified
the cipher's core properties (reciprocity; no letter maps to itself) with a
standalone Python check during development. We document this openly rather than
implying a fidelity we did not build: a true Enigma's specific rotor wirings,
ring settings, double-stepping anomaly and plugboard are a deep rabbit hole that
would not improve — and could muddy — the thing a visitor is meant to learn.

## Scope honesty

- **Included and complete:** all runtime C# and shaders; the full audio pipeline
  *and* 12 baked WAVs; the full Blender asset pipeline; editor tooling that
  configures the build and stamps the wired scene skeleton; this documentation.
- **Produced by running the included tools:** the binary 3D models (run the Blender
  pipeline) and, if you want to re-bake or tweak them, the audio WAVs.
- **Expected of the integrator:** installing packages, baking lighting, the art
  polish pass (PBR texturing, set dressing), and recording the capture — all
  documented in `01`–`06`.
