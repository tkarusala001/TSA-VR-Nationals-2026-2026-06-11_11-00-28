#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_enigma.py
#  DECRYPTED — Exhibit 2: the Enigma-inspired machine  (Blender pipeline)
#
#  Builds the WWII centrepiece in code: a bakelite case carrying three lettered
#  rotor wheels, a historically-laid-out keyboard (QWERTZ rows of 9/8/9 = 26), a
#  matching lampboard above it, a plugboard accent on the front, and a hinged
#  commit lever on the right side. Naming is chosen so the Unity wiring is direct:
#
#    Rotor_0 / Rotor_1 / Rotor_2  → EnigmaRotor objects (rotate about Y)
#    KeyCap_<L>                    → EnigmaKeyboard keys (poke targets)
#    Lamp_<L>                      → EnigmaLampboard lamps (emissive)
#    Lever                         → EnigmaLeverPull grab target
#    SignalWaypoint_0..N           → SignalTraceRenderer path (key→plug→rotors→
#                                     reflector→back), so the live current is
#                                     literally drawn through the machine.
#
#  Output collection: "Decrypted_Enigma".
# -----------------------------------------------------------------------------

import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gen_common as gc

# Historical Enigma key layout, top → bottom.
ROWS = ["QWERTZUIO", "ASDFGHJK", "PYXCVBNML"]


def build():
    gc.reset_scene()
    col = gc.ensure_collection("Decrypted_Enigma")

    # --- materials ------------------------------------------------------------
    bakelite = gc.make_material("Bakelite", (0.05, 0.05, 0.06, 1), metallic=0.1, roughness=0.5)
    steel = gc.make_material("Steel", (0.55, 0.56, 0.58, 1), metallic=0.95, roughness=0.3)
    brass = gc.make_material("Brass", (0.62, 0.45, 0.16, 1), metallic=0.9, roughness=0.35)
    ivory = gc.make_material("KeyIvory", (0.88, 0.86, 0.80, 1), metallic=0.0, roughness=0.45)
    rotorglyph = gc.make_material("RotorGlyph", (0.92, 0.90, 0.85, 1), metallic=0.0,
                                  roughness=0.4, emission=(0.9, 0.85, 0.7, 1), emission_strength=0.0)
    lampmat = gc.make_material("LampGlass", (0.95, 0.85, 0.45, 1), metallic=0.0,
                               roughness=0.2, emission=(0.95, 0.8, 0.4, 1), emission_strength=0.0)

    # --- case -----------------------------------------------------------------
    gc.box("Enigma_Case", size=(1.10, 0.85, 0.30), location=(0, 0, 1.05),
           col=col, mat=bakelite, bevel=0.02)
    gc.box("Enigma_Lid", size=(1.10, 0.40, 0.04), location=(0, -0.22, 1.21),
           col=col, mat=bakelite)  # raised back lid behind the rotors

    # --- rotors (3 wheels at the back) ---------------------------------------
    rotor_y = 0.26
    rotor_z = 1.27
    spacing = 0.20
    for i in range(3):
        x = (i - 1) * spacing
        rotor = gc.cylinder(f"Rotor_{i}", radius=0.085, depth=0.10,
                            location=(x, rotor_y, rotor_z), col=col, mat=steel,
                            segments=48, axis='Y')
        # Knurled brass cap on each end for grip.
        gc.cylinder(f"Rotor_{i}_CapL", radius=0.095, depth=0.012,
                    location=(x - 0.055, rotor_y, rotor_z), col=col, mat=brass,
                    segments=48, axis='Y').parent = rotor
        # Lettered ring: place a few visible glyphs around the wheel (top arc).
        place_rotor_glyphs(col, rotorglyph, rotor, center=(x, rotor_y, rotor_z),
                           radius=0.092)
        # Window frame in front of each rotor so one letter shows.
        gc.box(f"Rotor_{i}_Window", size=(0.05, 0.01, 0.05),
               location=(x, rotor_y - 0.10, rotor_z + 0.085), col=col, mat=brass)

    # --- keyboard + lampboard -------------------------------------------------
    place_keys_and_lamps(col, ivory, lampmat, bakelite)

    # --- plugboard accent (front face) ---------------------------------------
    gc.box("Plugboard_Panel", size=(0.9, 0.02, 0.18), location=(0, 0.42, 0.92),
           col=col, mat=bakelite)
    for r in range(2):
        for c in range(10):
            px = -0.40 + c * 0.089
            pz = 0.88 + r * 0.06
            gc.cylinder(f"Plug_{r}_{c}", radius=0.012, depth=0.02,
                        location=(px, 0.43, pz), col=col, mat=brass, segments=12, axis='Y')

    # --- commit lever (right side) -------------------------------------------
    lever_pivot = gc.add_empty("Lever_Pivot", location=(0.60, 0.0, 1.10), col=col, size=0.04)
    lever = gc.box("Lever", size=(0.04, 0.04, 0.26), location=(0.60, 0.0, 1.24),
                   col=col, mat=steel)
    lever.parent = lever_pivot
    gc.cylinder("Lever_Knob", radius=0.035, depth=0.04, location=(0.60, 0.0, 1.37),
                col=col, mat=brass, segments=24, axis='Z').parent = lever

    # --- signal-trace waypoints (key → plug → R1 → R2 → R3 → reflector → back)
    waypoints = [
        (0.0, 0.30, 0.78),    # keyboard row
        (0.0, 0.42, 0.90),    # plugboard
        (-spacing, rotor_y, rotor_z),  # rotor 0
        (0.0, rotor_y, rotor_z),       # rotor 1
        (spacing, rotor_y, rotor_z),   # rotor 2
        (spacing + 0.12, rotor_y + 0.02, rotor_z),  # reflector
        (0.0, 0.36, 0.84),    # return toward lampboard
    ]
    for idx, p in enumerate(waypoints):
        gc.add_empty(f"SignalWaypoint_{idx}", location=p, col=col, size=0.02)

    print("[gen_enigma] built Enigma: 3 rotors, 26 keys, 26 lamps, plugboard, lever, "
          f"{len(waypoints)} signal waypoints.")
    return col


def place_rotor_glyphs(col, mat, rotor, center, radius):
    """Place 26 letters around a rotor wheel in the Y-axis plane (X/Z circle)."""
    cx, cy, cz = center
    for i, ch in enumerate(gc.letters()):
        ang = gc.TWO_PI * i / 26.0
        x = cx + radius * math.sin(ang)
        z = cz + radius * math.cos(ang)
        t = gc.text(f"{rotor.name}_G{ch}", ch, size=0.018, location=(x, cy - 0.052, z),
                    rotation=(math.pi / 2, 0, -ang), col=col, mat=mat,
                    extrude=0.002, align_x='CENTER', align_y='CENTER')
        t.parent = rotor


def place_keys_and_lamps(col, key_mat, lamp_mat, base_mat):
    """Lay the keyboard (lower) and lampboard (upper) using the historical rows."""
    key_z = 0.74
    lamp_z = 0.86
    row_y0 = 0.30      # front (closest to player)
    row_dy = -0.10     # rows recede toward the rotors
    key_dx = 0.105

    for r, row in enumerate(ROWS):
        y = row_y0 + r * row_dy
        offset = -(len(row) - 1) * key_dx * 0.5
        for c, ch in enumerate(row):
            x = offset + c * key_dx
            # Keycap (poke target).
            cap = gc.cylinder(f"KeyCap_{ch}", radius=0.035, depth=0.03,
                              location=(x, y, key_z), col=col, mat=key_mat,
                              segments=20, axis='Z')
            gc.text(f"KeyLabel_{ch}", ch, size=0.022, location=(x, y, key_z + 0.018),
                    rotation=(0, 0, 0), col=col, mat=base_mat, extrude=0.002).parent = cap
            # Lamp (emissive disc) on the lampboard, slightly behind/above.
            gc.cylinder(f"Lamp_{ch}", radius=0.026, depth=0.012,
                        location=(x, y, lamp_z), col=col, mat=lamp_mat,
                        segments=20, axis='Z')


if __name__ == "__main__":
    build()
