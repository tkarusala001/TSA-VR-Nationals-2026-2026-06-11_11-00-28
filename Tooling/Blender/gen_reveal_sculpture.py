#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_reveal_sculpture.py
#  DECRYPTED — final chamber sculpture  (Blender pipeline)
#
#  Builds the closing sculpture that morphs through the museum's three eras. It
#  provides BOTH representations the FinalRevealController supports so the artist
#  can choose either at integration time:
#
#   1. THREE STAGE OBJECTS sharing one pivot (the controller's default path,
#      cross-dissolved via the Hologram shader's _Dissolve):
#        Reveal_Stage_Roman   — a tapered inscription stele
#        Reveal_Stage_Gears   — a clockwork gear cluster
#        Reveal_Stage_Circuit — a modern circuit-board lattice
#
#   2. A SINGLE MORPH MESH with two shape keys in the order the controller
#      expects (index 0 = toGears, index 1 = toCircuit):
#        Reveal_MorphMesh
#
#  Output collection: "Decrypted_Reveal".
# -----------------------------------------------------------------------------

import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gen_common as gc

try:
    import bpy
    import bmesh
    from mathutils import Vector
except ImportError:
    bpy = None


def build():
    gc.reset_scene()
    col = gc.ensure_collection("Decrypted_Reveal")

    stone = gc.make_material("RevealStone", (0.50, 0.47, 0.42, 1), metallic=0.0, roughness=0.85)
    brass = gc.make_material("RevealBrass", (0.62, 0.45, 0.16, 1), metallic=0.9, roughness=0.35)
    glow = gc.make_material("RevealGlow", (0.2, 0.9, 1.0, 1), metallic=0.2, roughness=0.3,
                            emission=(0.2, 0.9, 1.0, 1), emission_strength=1.0)

    pivot = gc.add_empty("Reveal_Pivot", location=(0, 0, 1.4), col=col, size=0.1)
    gc.cylinder("Reveal_Pedestal", radius=0.6, depth=0.9, location=(0, 0, 0.45),
                col=col, mat=stone, segments=48)

    build_stele(col, stone, pivot)
    build_gears(col, brass, pivot)
    build_circuit(col, glow, pivot)
    if bpy is not None:
        build_morph_mesh(col, glow, pivot)

    print("[gen_reveal_sculpture] built 3 stages (stele/gears/circuit) + morph mesh.")
    return col


# ----------------------------------------------------------------- stage 1

def build_stele(col, mat, pivot):
    parts = []
    parts.append(gc.box("Reveal_Stage_Roman_Base", size=(0.7, 0.5, 0.2),
                        location=(0, 0, 1.0), col=col, mat=mat))
    slab = gc.box("Reveal_Stage_Roman_Slab", size=(0.5, 0.18, 1.4),
                  location=(0, 0, 1.8), col=col, mat=mat)
    parts.append(slab)
    # Engraved inscription lines (thin recessed ribs).
    for i in range(5):
        z = 1.45 + i * 0.16
        parts.append(gc.box(f"Reveal_Stele_Line{i}", size=(0.34, 0.02, 0.03),
                            location=(0, -0.09, z), col=col, mat=mat))
    stage = gc.join(parts, "Reveal_Stage_Roman")
    if stage:
        stage.parent = pivot


# ----------------------------------------------------------------- stage 2

def make_gear(col, name, radius, teeth, thickness, location, mat):
    parts = [gc.cylinder(f"{name}_Body", radius=radius * 0.85, depth=thickness,
                         location=location, col=col, mat=mat, segments=32, axis='Y')]
    for i in range(teeth):
        a = gc.TWO_PI * i / teeth
        x = location[0] + radius * math.cos(a)
        z = location[2] + radius * math.sin(a)
        tooth = gc.box(f"{name}_T{i}", size=(0.03, thickness, 0.03),
                       location=(x, location[1], z), col=col, mat=mat)
        tooth.rotation_euler = (0, a, 0)
        parts.append(tooth)
    # Hub hole accent.
    parts.append(gc.cylinder(f"{name}_Hub", radius=radius * 0.18, depth=thickness * 1.1,
                             location=location, col=col, mat=mat, segments=16, axis='Y'))
    return gc.join(parts, name)


def build_gears(col, mat, pivot):
    gears = [
        make_gear(col, "Gear_A", 0.45, 18, 0.10, (0.0, 0.0, 1.8), mat),
        make_gear(col, "Gear_B", 0.30, 14, 0.09, (0.55, 0.0, 1.55), mat),
        make_gear(col, "Gear_C", 0.22, 12, 0.08, (-0.45, 0.0, 2.15), mat),
    ]
    stage = gc.join(gears, "Reveal_Stage_Gears")
    if stage:
        stage.parent = pivot


# ----------------------------------------------------------------- stage 3

def build_circuit(col, mat, pivot):
    parts = []
    board = gc.box("Reveal_Circuit_Board", size=(1.0, 0.05, 1.4),
                   location=(0, 0, 1.8), col=col, mat=mat)
    parts.append(board)
    # Trace grid (thin ribs) + a few chip blocks.
    for i in range(6):
        x = -0.4 + i * 0.16
        parts.append(gc.box(f"Reveal_Trace_V{i}", size=(0.015, 0.01, 1.2),
                            location=(x, 0.03, 1.8), col=col, mat=mat))
    for j in range(7):
        z = 1.25 + j * 0.18
        parts.append(gc.box(f"Reveal_Trace_H{j}", size=(0.9, 0.01, 0.015),
                            location=(0, 0.03, z), col=col, mat=mat))
    for k, (cx, cz) in enumerate([(-0.25, 1.6), (0.3, 2.0), (0.0, 1.4)]):
        parts.append(gc.box(f"Reveal_Chip{k}", size=(0.16, 0.05, 0.16),
                            location=(cx, 0.05, cz), col=col, mat=mat))
    stage = gc.join(parts, "Reveal_Stage_Circuit")
    if stage:
        stage.parent = pivot


# --------------------------------------------------- optional shape-key mesh

def build_morph_mesh(col, mat, pivot):
    """A single mesh with two shape keys (toGears, toCircuit) in controller order.
    Basis is a rounded blob; toGears rounds it toward a disc; toCircuit flattens
    it toward a board. Designed as the alternate to the three-stage cross-fade."""
    mesh = bpy.data.meshes.new("Reveal_MorphMesh")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.2)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=6, use_grid_fill=True)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("Reveal_MorphMesh", mesh)
    gc.link(obj, col)
    gc.assign(obj, mat)
    obj.location = (0, 0, 1.8)
    obj.parent = pivot

    # Basis first.
    obj.shape_key_add(name="Basis", from_mix=False)

    # Shape key 0: toGears — push vertices toward a disc profile in X/Z.
    kg = obj.shape_key_add(name="toGears", from_mix=False)
    for i, v in enumerate(mesh.vertices):
        co = v.co
        r = math.hypot(co.x, co.z)
        target_r = 0.6
        scale = (target_r / r) if r > 1e-5 else 1.0
        kg.data[i].co = Vector((co.x * scale, co.y * 0.8, co.z * scale))

    # Shape key 1: toCircuit — flatten in Y to a board and widen X/Z.
    kc = obj.shape_key_add(name="toCircuit", from_mix=False)
    for i, v in enumerate(mesh.vertices):
        co = v.co
        kc.data[i].co = Vector((co.x * 1.25, co.y * 0.08, co.z * 1.25))

    # Start fully on Basis; the controller drives the weights at runtime.
    obj.data.shape_keys.key_blocks["toGears"].value = 0.0
    obj.data.shape_keys.key_blocks["toCircuit"].value = 0.0
    # Hidden by default so it doesn't overlap the three stages unless chosen.
    obj.hide_render = True
    obj.hide_viewport = True


if __name__ == "__main__":
    build()
