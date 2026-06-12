#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_cipher_disk.py
#  DECRYPTED — Exhibit 1: the Caesar cipher disk  (Blender pipeline)
#
#  Builds the Ancient Cryptography centrepiece entirely in code: a Roman-styled
#  pedestal carrying two concentric lettered rings. The OUTER ring (ciphertext)
#  is fixed; the INNER disk (plaintext) is a separate object so it can become the
#  grabbable XRGrabTwistDisk in Unity. For both rings we lay 26 extruded letters
#  around the face and drop an anchor EMPTY at each letter — those map directly to
#  the CaesarCipherController's _outerAnchors[26] / _innerAnchors[26] so the
#  runtime mapping-lines connect the right glyphs.
#
#  Output collection: "Decrypted_CipherDisk".
# -----------------------------------------------------------------------------

import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gen_common as gc


def build():
    gc.reset_scene()
    col = gc.ensure_collection("Decrypted_CipherDisk")

    # --- materials ------------------------------------------------------------
    bronze = gc.make_material("Bronze", (0.45, 0.30, 0.12, 1), metallic=0.9, roughness=0.35)
    dark_bronze = gc.make_material("DarkBronze", (0.20, 0.13, 0.06, 1), metallic=0.9, roughness=0.5)
    stone = gc.make_material("Stone", (0.42, 0.40, 0.36, 1), metallic=0.0, roughness=0.9)
    glyph = gc.make_material("GlyphEmissive", (0.95, 0.78, 0.35, 1), metallic=0.6,
                             roughness=0.3, emission=(0.95, 0.78, 0.35, 1), emission_strength=0.0)

    OUTER_R = 0.56
    INNER_R = 0.40
    TOP_Z = 1.02       # play height for the disk face

    # --- pedestal -------------------------------------------------------------
    gc.cylinder("Disk_Pedestal", radius=0.34, depth=0.95, location=(0, 0, 0.475),
                col=col, mat=stone, segments=48)
    gc.cylinder("Disk_PedestalCap", radius=0.62, depth=0.06, location=(0, 0, 0.95),
                col=col, mat=dark_bronze, segments=64)

    # --- outer (ciphertext) ring ---------------------------------------------
    gc.ring("Disk_OuterRing", outer=OUTER_R + 0.06, inner=INNER_R + 0.02, depth=0.05,
            location=(0, 0, TOP_Z), col=col, mat=bronze, segments=96)
    place_letters(col, glyph, radius=(OUTER_R - 0.02), z=TOP_Z + 0.03,
                  prefix="OuterGlyph", anchor_prefix="OuterAnchor", parent=None)

    # --- inner (plaintext) rotating disk --------------------------------------
    inner = gc.cylinder("InnerDisk", radius=INNER_R + 0.01, depth=0.05,
                        location=(0, 0, TOP_Z + 0.02), col=col, mat=bronze, segments=96)
    place_letters(col, glyph, radius=(INNER_R - 0.07), z=TOP_Z + 0.06,
                  prefix="InnerGlyph", anchor_prefix="InnerAnchor", parent=inner)

    # A small grip notch hint on the inner disk so players know to twist it.
    grip = gc.box("InnerDisk_Grip", size=(0.04, 0.10, 0.04),
                  location=(0, INNER_R - 0.04, TOP_Z + 0.07), col=col, mat=dark_bronze)
    grip.parent = inner

    print("[gen_cipher_disk] built Caesar disk with 26 outer + 26 inner glyphs/anchors.")
    return col


def place_letters(col, glyph_mat, radius, z, prefix, anchor_prefix, parent):
    """Lay A..Z around a circle as extruded text + an anchor empty at each slot."""
    for i, ch in enumerate(gc.letters()):
        ang = math.pi / 2 - gc.TWO_PI * i / 26.0   # 'A' at top, going clockwise
        x, y = radius * math.cos(ang), radius * math.sin(ang)
        t = gc.text(f"{prefix}_{ch}", ch, size=0.05, location=(x, y, z),
                    rotation=(0, 0, ang - math.pi / 2), col=col, mat=glyph_mat,
                    extrude=0.006, align_x='CENTER', align_y='CENTER')
        a = gc.add_empty(f"{anchor_prefix}_{ch}", location=(x, y, z), col=col, size=0.02)
        if parent is not None:
            t.parent = parent
            a.parent = parent


if __name__ == "__main__":
    build()
