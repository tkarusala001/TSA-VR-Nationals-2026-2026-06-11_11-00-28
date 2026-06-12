#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  export_all.py
#  DECRYPTED — batch asset export  (Blender pipeline)
#
#  Runs every procedural generator in turn and exports each one's collection to a
#  Quest-friendly FBX (or GLB), into Assets/_Project/Art/Generated/. Because each
#  generator resets the scene on entry, we build and export one asset at a time,
#  in order, so a single Blender session produces the whole art set.
#
#  Run:
#    blender --background --python export_all.py            # FBX (default)
#    blender --background --python export_all.py -- --glb   # glTF/GLB instead
#
#  The "--" separates Blender's args from this script's args.
# -----------------------------------------------------------------------------

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.append(HERE)

import gen_common as gc
import gen_architecture
import gen_cipher_disk
import gen_enigma
import gen_vault
import gen_reveal_sculpture

PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "_Project", "Art", "Generated")

# (module, output base name). Order is arbitrary since each rebuilds the scene.
PIPELINE = [
    (gen_architecture,     "Museum_Architecture"),
    (gen_cipher_disk,      "CipherDisk"),
    (gen_enigma,           "Enigma"),
    (gen_vault,            "Vault"),
    (gen_reveal_sculpture, "RevealSculpture"),
]


def parse_fmt():
    fmt = "FBX"
    if "--" in sys.argv:
        extra = sys.argv[sys.argv.index("--") + 1:]
        if "--glb" in extra or "--gltf" in extra:
            fmt = "GLB"
    return fmt


def main():
    fmt = parse_fmt()
    ext = ".fbx" if fmt == "FBX" else ".glb"
    os.makedirs(OUT_DIR, exist_ok=True)
    print(f"[export_all] exporting {len(PIPELINE)} assets as {fmt} → {OUT_DIR}")

    for module, base in PIPELINE:
        print(f"[export_all] building {base} via {module.__name__}.build() …")
        col = module.build()                      # resets scene, returns its collection
        out_path = os.path.join(OUT_DIR, base + ext)
        gc.export_collection(col, out_path, fmt=fmt)

    print("[export_all] all assets exported. Import the folder into Unity, set the "
          "model scale/axis on import, and assign materials/URP shaders as needed.")


if __name__ == "__main__":
    main()
