#!/usr/bin/env python3
# -----------------------------------------------------------------------------
#  gen_architecture.py
#  DECRYPTED — the museum shell  (Blender pipeline)
#
#  Builds the enclosing architecture as six self-contained room shells, one per
#  museum state, positioned at the same X spacing the Unity SceneBuilder uses
#  (0, 30, 60 … 150 m). Each shell is a complete enclosed space — floor slab,
#  ceiling, four walls with a framed doorway connecting to the next room, plus a
#  baseboard and cornice trim. Because rooms are activated independently at
#  runtime (room-based culling; the player is repositioned per room), the shells
#  do not need to physically bridge the gap between anchors — each simply lines
#  up with its room root.
#
#  Output collection: "Decrypted_Architecture".
# -----------------------------------------------------------------------------

import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import gen_common as gc

ROOM_X = [0.0, 30.0, 60.0, 90.0, 120.0, 150.0]
ROOM_NAMES = ["Splash", "Atrium", "Ancient", "WWII", "Vault", "Reveal"]

WIDTH = 11.0      # X
DEPTH = 9.0       # Y
HEIGHT = 4.0      # Z
WALL_T = 0.3
DOOR_W = 2.4
DOOR_H = 2.9


def build():
    gc.reset_scene()
    col = gc.ensure_collection("Decrypted_Architecture")

    floor_mat = gc.make_material("FloorConcrete", (0.18, 0.18, 0.20, 1), metallic=0.0, roughness=0.4)
    wall_mat = gc.make_material("WallPlaster", (0.62, 0.60, 0.56, 1), metallic=0.0, roughness=0.85)
    trim_mat = gc.make_material("TrimDark", (0.10, 0.10, 0.11, 1), metallic=0.2, roughness=0.6)
    ceil_mat = gc.make_material("Ceiling", (0.30, 0.30, 0.32, 1), metallic=0.0, roughness=0.9)

    for x, name in zip(ROOM_X, ROOM_NAMES):
        first = (name == "Splash")
        last = (name == "Reveal")
        build_room(col, x, name, floor_mat, wall_mat, trim_mat, ceil_mat,
                   entry=not first, exit=not last)

    print(f"[gen_architecture] built {len(ROOM_X)} room shells with doorways and trim.")
    return col


def build_room(col, x, name, floor_mat, wall_mat, trim_mat, ceil_mat, entry, exit):
    cx = x
    # Floor + ceiling.
    gc.box(f"{name}_Floor", size=(WIDTH, DEPTH, 0.2), location=(cx, 0, -0.1),
           col=col, mat=floor_mat)
    gc.box(f"{name}_Ceiling", size=(WIDTH, DEPTH, 0.2), location=(cx, 0, HEIGHT + 0.1),
           col=col, mat=ceil_mat)

    half_w, half_d = WIDTH * 0.5, DEPTH * 0.5

    # Front (+Y) and back (-Y) walls: solid.
    gc.box(f"{name}_WallFront", size=(WIDTH, WALL_T, HEIGHT),
           location=(cx, half_d, HEIGHT * 0.5), col=col, mat=wall_mat)
    gc.box(f"{name}_WallBack", size=(WIDTH, WALL_T, HEIGHT),
           location=(cx, -half_d, HEIGHT * 0.5), col=col, mat=wall_mat)

    # Left (-X) wall: doorway if this room has an entry.
    wall_with_optional_door(col, f"{name}_WallLeft", center=(cx - half_w, 0, 0),
                            along='Y', length=DEPTH, mat=wall_mat, trim_mat=trim_mat,
                            door=entry)
    # Right (+X) wall: doorway if this room has an exit.
    wall_with_optional_door(col, f"{name}_WallRight", center=(cx + half_w, 0, 0),
                            along='Y', length=DEPTH, mat=wall_mat, trim_mat=trim_mat,
                            door=exit)

    # Trim: baseboard + cornice around the perimeter (simple ribbons).
    gc.box(f"{name}_Base", size=(WIDTH, DEPTH, 0.12), location=(cx, 0, 0.06),
           col=col, mat=trim_mat)
    gc.box(f"{name}_Cornice", size=(WIDTH, DEPTH, 0.12), location=(cx, 0, HEIGHT - 0.06),
           col=col, mat=trim_mat)


def wall_with_optional_door(col, name, center, along, length, mat, trim_mat, door):
    """Build a side wall. If `door`, leave a centered doorway gap and add a frame."""
    cx, cy, cz = center
    if not door:
        gc.box(name, size=(WALL_T, length, HEIGHT), location=(cx, cy, HEIGHT * 0.5),
               col=col, mat=mat)
        return

    side = (length - DOOR_W) * 0.5           # width of each wall segment beside the door
    # Two segments either side of the doorway (along Y).
    gc.box(f"{name}_A", size=(WALL_T, side, HEIGHT),
           location=(cx, cy - (DOOR_W * 0.5 + side * 0.5), HEIGHT * 0.5), col=col, mat=mat)
    gc.box(f"{name}_B", size=(WALL_T, side, HEIGHT),
           location=(cx, cy + (DOOR_W * 0.5 + side * 0.5), HEIGHT * 0.5), col=col, mat=mat)
    # Lintel above the door.
    gc.box(f"{name}_Lintel", size=(WALL_T, DOOR_W, HEIGHT - DOOR_H),
           location=(cx, cy, DOOR_H + (HEIGHT - DOOR_H) * 0.5), col=col, mat=mat)
    # Doorway frame trim.
    gc.box(f"{name}_FrameTop", size=(WALL_T + 0.06, DOOR_W + 0.12, 0.12),
           location=(cx, cy, DOOR_H), col=col, mat=trim_mat)
    gc.box(f"{name}_FrameL", size=(WALL_T + 0.06, 0.12, DOOR_H),
           location=(cx, cy - DOOR_W * 0.5, DOOR_H * 0.5), col=col, mat=trim_mat)
    gc.box(f"{name}_FrameR", size=(WALL_T + 0.06, 0.12, DOOR_H),
           location=(cx, cy + DOOR_W * 0.5, DOOR_H * 0.5), col=col, mat=trim_mat)


if __name__ == "__main__":
    build()
