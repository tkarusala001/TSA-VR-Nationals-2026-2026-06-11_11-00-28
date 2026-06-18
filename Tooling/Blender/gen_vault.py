#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_vault.py
#  DECRYPTED — Exhibit 3: the modern security vault  (Blender pipeline)
#
#  Builds the digital-security vault in code. Naming maps straight onto the Unity
#  components:
#
#    Vault_Door         → VaultController _door (the moving slab)
#    Vault_LockingRing  → VaultController _lockingRing (spins on unlock)
#    Vault_StatusLight* → VaultController _statusLights (emissive, red→green)
#    Vault_Archive*     → emissive interior panels revealed on open
#    VaultKey_<L> / VaultKey_ENTER / VaultKey_CLEAR → VaultKeypad keys
#    Vault_Display      → the passphrase read-out surface (TMP target)
#
#  Output collection: "Decrypted_Vault".
# -----------------------------------------------------------------------------

import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gen_common as gc

DOOR_W = 2.0   # Y
DOOR_H = 2.8   # Z
DOOR_T = 0.35  # X


def build():
    gc.reset_scene()
    col = gc.ensure_collection("Decrypted_Vault")

    steel = gc.make_material("VaultSteel", (0.55, 0.56, 0.58, 1), metallic=0.95, roughness=0.3)
    dark = gc.make_material("VaultDark", (0.12, 0.13, 0.15, 1), metallic=0.6, roughness=0.5)
    keymat = gc.make_material("VaultKey", (0.20, 0.22, 0.26, 1), metallic=0.4, roughness=0.5)
    red = gc.make_material("StatusRed", (1.0, 0.18, 0.15, 1), metallic=0.0, roughness=0.4,
                           emission=(1.0, 0.18, 0.15, 1), emission_strength=2.0)
    cyan = gc.make_material("ArchiveGlow", (0.2, 0.9, 1.0, 1), metallic=0.2, roughness=0.3,
                            emission=(0.2, 0.9, 1.0, 1), emission_strength=1.0)
    label = gc.make_material("VaultLabel", (0.85, 0.95, 1.0, 1), metallic=0.0, roughness=0.4,
                             emission=(0.6, 0.85, 1.0, 1), emission_strength=0.5)

    # --- frame (recessed wall opening) ---------------------------------------
    gc.box("Vault_Frame_Top", size=(0.5, DOOR_W + 0.8, 0.4),
           location=(0, 0, DOOR_H + 0.2), col=col, mat=dark)
    gc.box("Vault_Frame_L", size=(0.5, 0.4, DOOR_H + 0.4),
           location=(0, -(DOOR_W * 0.5 + 0.2), (DOOR_H + 0.4) * 0.5), col=col, mat=dark)
    gc.box("Vault_Frame_R", size=(0.5, 0.4, DOOR_H + 0.4),
           location=(0, (DOOR_W * 0.5 + 0.2), (DOOR_H + 0.4) * 0.5), col=col, mat=dark)

    # --- the door (hinged in Unity; modelled closed here) --------------------
    # Hinge pivot sits at the left edge of the door opening.  The door mesh is
    # offset so its left edge is at the pivot; Unity's VaultController rotates
    # this pivot about local-up and the door sweeps open correctly.
    HINGE_Y = -(DOOR_W * 0.5)
    door_pivot = gc.add_empty("Vault_DoorPivot", location=(0, HINGE_Y, 0), col=col, size=0.06)
    door = gc.box("Vault_Door", size=(DOOR_T, DOOR_W, DOOR_H),
                  location=(0, HINGE_Y + DOOR_W * 0.5, DOOR_H * 0.5), col=col, mat=steel, bevel=0.03)
    gc.parent_keep_world(door, door_pivot)

    # Circular locking ring — built then transform-baked before parenting to door.
    ring = gc.ring("Vault_LockingRing", outer=0.7, inner=0.5, depth=0.12,
                   location=(DOOR_T * 0.5 + 0.02, HINGE_Y + DOOR_W * 0.5, DOOR_H * 0.5),
                   col=col, mat=dark, segments=64)
    ring.rotation_euler = (0, 1.5708, 0)
    gc.parent_keep_world(ring, door)

    # Central wheel hub + spokes.
    hub = gc.cylinder("Vault_Wheel", radius=0.16, depth=0.10,
                      location=(DOOR_T * 0.5 + 0.05, HINGE_Y + DOOR_W * 0.5, DOOR_H * 0.5),
                      col=col, mat=steel, segments=32, axis='X')
    gc.parent_keep_world(hub, door)
    for k in range(4):
        ang = k * 1.5708
        spoke = gc.box(f"Vault_Spoke{k}", size=(0.05, 0.5, 0.05),
                       location=(DOOR_T * 0.5 + 0.04, HINGE_Y + DOOR_W * 0.5, DOOR_H * 0.5),
                       col=col, mat=steel)
        spoke.rotation_euler = (ang, 0, 0)
        gc.parent_keep_world(spoke, door)  # parent to door not ring so spokes don't spin with lock animation

    # --- status lights (beside the door) -------------------------------------
    for i in range(3):
        gc.cylinder(f"Vault_StatusLight_{i}", radius=0.05, depth=0.04,
                    location=(DOOR_T * 0.5 + 0.02, DOOR_W * 0.5 + 0.18, 2.4 - i * 0.22),
                    col=col, mat=red, segments=20, axis='X')

    # --- keypad panel (to the right of the door) -----------------------------
    panel_x = DOOR_T * 0.5 + 0.02
    panel_y = DOOR_W * 0.5 + 0.55
    gc.box("Vault_KeypadPanel", size=(0.06, 0.55, 0.8),
           location=(panel_x, panel_y, 1.3), col=col, mat=dark)
    # Read-out surface at the top of the panel.
    gc.box("Vault_Display", size=(0.02, 0.46, 0.16),
           location=(panel_x + 0.035, panel_y, 1.62), col=col, mat=label)

    keys = gc.letters() + ["ENTER", "CLEAR"]   # 28 entries
    cols, rows = 7, 4
    kx_step, kz_step = 0.066, 0.12
    y0 = panel_y - (cols - 1) * kx_step * 0.5
    z0 = 1.42
    for idx, key in enumerate(keys):
        r = idx // cols
        c = idx % cols
        ky = y0 + c * kx_step
        kz = z0 - r * kz_step
        cap = gc.box(f"VaultKey_{key}", size=(0.03, 0.05, 0.05),
                     location=(panel_x + 0.04, ky, kz), col=col, mat=keymat)
        glyph = "↵" if key == "ENTER" else ("⌫" if key == "CLEAR" else key)
        key_label = gc.text(f"VaultKeyLabel_{key}", glyph, size=0.03,
                            location=(panel_x + 0.06, ky, kz), rotation=(1.5708, 0, 1.5708),
                            col=col, mat=label, extrude=0.002)
        gc.parent_keep_world(key_label, cap)

    # --- interior archive shelving (revealed on open) ------------------------
    for s in range(3):
        gc.box(f"Vault_Archive_Shelf{s}", size=(0.8, DOOR_W - 0.3, 0.05),
               location=(-0.7, 0, 0.6 + s * 0.8), col=col, mat=dark)
    gc.box("Vault_Archive_Glow", size=(0.02, DOOR_W - 0.4, DOOR_H - 0.6),
           location=(-1.05, 0, DOOR_H * 0.5), col=col, mat=cyan)

    print("[gen_vault] built vault: door, locking ring, 28-key pad, status lights, archive.")
    gc.parent_all_to_root(col, "Vault_Root")
    return col


if __name__ == "__main__":
    build()
