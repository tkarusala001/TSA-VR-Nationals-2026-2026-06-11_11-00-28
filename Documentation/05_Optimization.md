# 05 · Optimization

Target: a locked **72 FPS** on Meta Quest standalone (mobile Snapdragon-class tile
GPU). That is **~13.9 ms/frame for both eyes**. Everything below exists to defend
that budget. The guiding principle: **the cheapest work is the work you don't do**
— so the design leans hard on baking, culling and room-based activation.

## Frame budget mindset

| Constraint | Implication |
|------------|-------------|
| Tile (TBDR) GPU | Avoid overdraw and full-screen post; MSAA is cheap, transparency is not |
| Mobile CPU | Minimise draw calls, per-frame allocations, and `Update` work |
| Two eyes | Use **single-pass instanced** (Multiview) so the scene is submitted once |
| Stereo | Bandwidth matters; keep textures ASTC + mipped, HDR off |

## Rendering

- **Single-pass instanced stereo (Multiview).** Set in Oculus XR settings; roughly
  halves CPU submission and draw-call cost versus multi-pass.
- **Universal RP, mobile-tuned.** MSAA 4x (cheap on tile GPUs), **HDR off**, shadow
  distance ~18 m with a single cascade, minimal renderer features.
- **Opaque-first materials.** The environment is opaque so the GPU can early-out via
  depth and benefit from static batching. Transparency/additive is restricted to a
  few small FX surfaces (hologram sculpture, vault data panels, lamp/status glow),
  where overdraw area is tiny.
- **Custom shaders are single-pass, texture-free, loop-free.** `Hologram_URP` and
  `EnergyScanline_URP` use procedural hashes/grids and cheap fresnel/scanline math
  — no texture fetches, no dynamic loops — so they stay friendly on the tile GPU.

## Geometry & draw calls

- **Static batching** on all non-moving geometry (walls, fixtures, pedestals).
- **Room-based activation = the biggest win.** Only one room root is enabled at a
  time (`SceneController` toggles them on transition), so the renderer, lights and
  colliders for five of six rooms cost nothing. This is occlusion culling for free,
  at room granularity.
- **Occlusion culling** baked on top for within-room occluders.
- **Procedural meshes are modest.** The Blender generators favour low-poly forms
  (cylinders at 20–96 segments sized to need); join sub-parts into single objects to
  cut object/draw-call count where it doesn't hurt wiring.

## Lighting

- **Fully baked GI + baked lights.** No realtime shadow-casting lights in the play
  path. Per-room lightmaps stay small because rooms are isolated.
- **Reflection probes are baked**, one per room, enabled on entry via
  `RoomDescriptor` — no realtime probe rendering.
- **Dynamic-looking accents are emissive**, not lights: lamps, status LEDs, the
  archive glow and the hologram all change brightness via **`MaterialPropertyBlock`**
  (see below) with zero added light cost and no GI rebuild.

## CPU & memory hygiene

- **No per-frame allocations in hot paths.** One-shot SFX use a **pre-built source
  pool**; the Caesar mapping lines reuse a fixed set of `LineRenderer`s; controllers
  cache a single `MaterialPropertyBlock` and reuse it.
- **`MaterialPropertyBlock` everywhere a colour/emission changes.** This avoids
  instantiating materials (which would break batching and balloon memory) — used in
  the vault, lampboard, reveal stages and glyph highlights.
- **Event bus over polling.** Systems react to typed events rather than checking
  state every `Update`, keeping per-frame CPU low and the call graph shallow.
- **Baked, not realtime, reverb.** Audio reverb is rendered into the clips offline;
  the runtime mixer runs no reverb DSP. The voice pool is capped.
- **Execution order pinned.** Managers use `[DefaultExecutionOrder]` so init is
  deterministic and cheap (no frame-one scramble).

## The `PerformanceManager`

Holds the headset-level budget, behind an `OCULUS_XR_PRESENT` guard so it compiles
with or without the Oculus package:

- Locks the **display refresh rate to 72 Hz**.
- Enables **fixed foveated rendering** (peripheral pixels shaded at lower rate) and
  can raise the level under load.
- Pins sensible **CPU/GPU performance levels** for sustained (not bursty) clocks.
- Centralises `Application.targetFrameRate` / `QualitySettings` so there's one place
  to tune.

When the Oculus package is absent the manager still runs; the Oculus-specific calls
are simply compiled out, so the project builds and runs in-editor regardless.

## Texture & asset budget

- **ASTC** compression (6x6 baseline), mips on, max size 1024 for most surfaces.
- Trim-sheet/atlas-friendly UVs from the art pass to keep material count low.
- Audio: short mono clips, Decompress On Load (SFX) / ADPCM (ambient).

## A practical profiling loop

1. Build to device; open the **OVR Metrics Tool** (or `adb logcat` perf overlays).
2. Watch **GPU vs CPU frame time** to know which side is the bottleneck.
3. If GPU-bound: check overdraw (transparency), shadow distance, MSAA, foveation.
4. If CPU-bound: check draw calls (batching), `Update` count, GC spikes.
5. Confirm only one room is active during transitions and that lighting is baked.

Holding 72 FPS here is mostly a matter of **not regressing** the defaults above —
keep the environment opaque and batched, keep lighting baked, keep one room active,
and keep the FX shaders off the critical mass of pixels.
