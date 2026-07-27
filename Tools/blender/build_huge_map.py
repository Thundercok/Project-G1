"""Generate the 'Corvus Sprawl' — an 800x800 m military-industrial base for
Project G1. Low-poly, flat-shaded, exported as one Unity-ready FBX plus a JSON
manifest of interiors so the Unity builder can light and stock them without
anyone typing the same coordinate twice.

The v1 map was 600x600 m of *solid* blocks: every building was a boulder you
walked around. Half-Life's spaces are the opposite — you are almost always
inside something, and the outside exists to get you to the next inside. So the
buildings here are hollow, with doorways, floors, upper storeys and roof
access, and the extra 200 m of footprint is spent on things a real base has
that a block of concrete does not: a runway with revetments, earth-covered
ammo igloos, a trench line, a tank park, and blast walls that break the
sightlines across the open ground.

Layout, Blender XY (exports to Unity XZ):

  INNER BASE (unchanged centres from v1, so existing spawns still land right)
    Central Command Tower   (0, 0)      enterable lobby, roof-access landmark
    Allied Base             (-160, 0)   enterable barracks row, helipad
    Lab Complex             (0, 165)    two-storey blocks + dome
    Hangar / Motor Pool     (165, 0)    open shed + vehicles
    Alien Breach Ruins      (0, -165)   broken walls, pods, breach ring
    Living Quarters         (-150, 150) enterable apartment ground floors
    Warehouse Yard          (150, 150)  container stacks + enterable shed
    Comms Array             (155, -150) dish, masts, control hut
    Fuel Depot              (-155,-150) tank farm inside containment berms

  OUTER RING (the new ground)
    Airstrip                (east)      runway, revetments, control tower
    Ammo Bunker Field       (north)     six earth-bermed igloos
    Trench Line             (south)     zigzag parapets + pillboxes
    Tank Park               (west)      revetments + enterable workshop
    Perimeter               T-walls, guard posts, floodlights, watchtowers

AXIS WARNING — the FBX export flips north/south. Blender +Y arrives in Unity as
-Z, so a district authored at Blender y=+320 appears at Unity z=-320. Blender X
passes through unchanged. `NS` below is that sign: multiply any north/south
coordinate by it and the district lands where its name says it does in Unity.
(The inner districts predate this being understood and are left as they are, so
the pre-existing lab/ruins swap in the Unity spawn lists is untouched.)

Run:  blender --background --python build_huge_map.py -- <project_dir> <unity_models_dir>
"""
import bpy
import json
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BASE = args[0] if args else "."
UNITY = args[1] if len(args) > 1 else "."

objs = []
ROOMS = []       # interiors the Unity side lights and stocks
LIGHTS = []      # explicit light placements (floodlights, lamps)
COVER = []       # firing positions the AI can claim


def cover(x, y, z=0.0):
    """Mark a spot as usable cover, in UNITY space.

    G1CoverPoint's test is specific: crouching there must be blocked from the
    threat and standing must not, i.e. cover you pop up over. That rules out
    most of this map on its own — trench walls, T-walls and revetments are all
    chest-to-head high, which is cover you shoot *around*. So rather than
    scatter points and hope, every call site here is somewhere the geometry was
    deliberately built to that height: behind a sandbag line, on a trench fire
    step, at a pillbox slit, behind a parapet.
    """
    COVER.append({"x": x, "z": y * NS, "y": z})


def M(name, color, rough=0.85, metal=0.0, emit=None, estr=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = estr
    m.diffuse_color = (*color, 1)
    return m


MATS = {}


def mat(key):
    return MATS[key]


def box(name, loc, dims, key, rot=(0, 0, 0)):
    # size=1 cube is 1 unit across, so scale by dims to get the true size in
    # metres (an earlier /2 here silently halved the whole map).
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector((dims[0], dims[1], dims[2]))
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_flat()
    objs.append((ob, key))
    return ob


def cyl(name, loc, radius, height, key, verts=12, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius,
                                        depth=height, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    bpy.ops.object.shade_flat()
    objs.append((ob, key))
    return ob


# --------------------------------------------------------------- architecture
def room(name, cx, cy, w, d, h, key, doors="S", floor=None, roof=True,
         wall_t=0.6, door_w=3.4, door_h=3.0, z0=0.0, light=True):
    """A building you can walk into: four walls with doorway gaps cut in the
    sides named by `doors` (any of NSEW), an optional floor, and a roof.

    Everything is joined into one mesh downstream, so a doorway has to be an
    actual gap between two wall segments — there is no boolean pass and no
    second material to fake one with.
    """
    if floor:
        box(f"{name}_floor", (cx, cy, z0 + 0.06), (w, d, 0.12), floor)

    for side in "NSEW":
        along_x = side in "NS"
        span = w if along_x else d
        off = (d if along_x else w) / 2 - wall_t / 2
        sx = cx if along_x else cx + (off if side == "E" else -off)
        sy = (cy + (off if side == "N" else -off)) if along_x else cy

        if side in doors:
            seg = (span - door_w) / 2
            for s in (-1, 1):
                c = (span + door_w) / 4 * s
                if along_x:
                    box(f"{name}_w{side}{s}", (sx + c, sy, z0 + h / 2), (seg, wall_t, h), key)
                else:
                    box(f"{name}_w{side}{s}", (sx, sy + c, z0 + h / 2), (wall_t, seg, h), key)
            if h > door_h:                      # lintel across the opening
                lh = h - door_h
                dims = (door_w, wall_t, lh) if along_x else (wall_t, door_w, lh)
                box(f"{name}_l{side}", (sx, sy, z0 + door_h + lh / 2), dims, key)
        else:
            dims = (span, wall_t, h) if along_x else (wall_t, span, h)
            box(f"{name}_w{side}", (sx, sy, z0 + h / 2), dims, key)

    if roof:
        box(f"{name}_roof", (cx, cy, z0 + h + 0.15), (w + 0.4, d + 0.4, 0.3), key)

    # record it in UNITY space, not Blender space — the consumer places lights
    # and crates from these numbers, and an unconverted Y puts every one of
    # them on the opposite side of the map from the room it belongs to
    ROOMS.append({"name": name, "x": cx, "z": cy * NS, "y": z0,
                  "w": w, "d": d, "h": h, "doors": doors, "light": light})
    return name


def window_band(name, cx, cy, w, d, h, key, sides="NS", z=1.6, tall=1.2, inset=0.05):
    """Glass strip let into a wall — pure decoration, but it is what stops a
    hollow box from reading as a shipping container from across the map."""
    for side in sides:
        along_x = side in "NS"
        off = (d if along_x else w) / 2 + inset
        sx = cx if along_x else cx + (off if side == "E" else -off)
        sy = (cy + (off if side == "N" else -off)) if along_x else cy
        dims = (w * 0.7, 0.16, tall) if along_x else (0.16, d * 0.7, tall)
        box(f"{name}_gl{side}", (sx, sy, z + tall / 2), dims, key)


def ramp(name, cx, cy, length, width, rise, key, axis="y", flip=False, z0=0.0):
    """A walkable slope. Ramps rather than stairs wherever the NavMesh has to
    follow — a stepped stair bakes into a broken strip of polygons at this
    scale, a 20-degree ramp bakes into one clean surface.

    `flip` swaps which end is the high one: by default the high end is toward
    -X (axis="x") or -Y (axis="y"). Getting this backwards builds a ramp that
    climbs away from the thing it is supposed to reach, which is invisible in
    a top-down render and very obvious in game.
    """
    ang = math.atan2(rise, length)
    if axis == "y":
        rot = (-ang if not flip else ang, 0, 0)
        dims = (width, math.hypot(length, rise), 0.4)
    else:
        rot = (0, ang if not flip else -ang, 0)
        dims = (math.hypot(length, rise), width, 0.4)
    box(name, (cx, cy, z0 + rise / 2), dims, key, rot=rot)


def ramp_to(name, tx, ty, length, width, rise, key, axis="x", side=1, z0=0.0):
    """Ramp whose *top* lands at (tx, ty), climbing from `side` (+1 = the ramp
    lies on the +X/+Y side of the target, -1 = the other side). Saves working
    out the flip by hand at every call site."""
    if axis == "x":
        ramp(name, tx + side * length / 2, ty, length, width, rise, key,
             axis="x", flip=(side < 0), z0=z0)
    else:
        ramp(name, tx, ty + side * length / 2, length, width, rise, key,
             axis="y", flip=(side < 0), z0=z0)


def stairs(name, cx, cy, width, run, rise, steps, key, axis="y", sign=1):
    """Player-only steps. Rise is kept under the CharacterController's 0.4 m
    step offset so they can be climbed without jumping."""
    for i in range(steps):
        d = (i + 0.5) * run * sign
        h = (i + 1) * rise
        if axis == "y":
            box(f"{name}_{i}", (cx, cy + d, h / 2), (width, run, h), key)
        else:
            box(f"{name}_{i}", (cx + d, cy, h / 2), (run, width, h), key)


def catwalk(name, x0, y0, x1, y1, z, key, width=3.0, rail="hazard"):
    """Rooftop-to-rooftop link. Verticality is the cheapest way to make an
    open battlefield read as architecture instead of a field with props."""
    along_x = abs(x1 - x0) > abs(y1 - y0)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    L = abs(x1 - x0) if along_x else abs(y1 - y0)
    dims = (L, width, 0.4) if along_x else (width, L, 0.4)
    box(name, (cx, cy, z), dims, key)
    for s in (-1, 1):
        rd = (L, 0.2, 1.1) if along_x else (0.2, L, 1.1)
        rx = cx if along_x else cx + s * width / 2
        ry = cy + s * width / 2 if along_x else cy
        box(f"{name}_r{s}", (rx, ry, z + 0.75), rd, rail)


ROAD_CLEAR = 13.0


def on_road(x, y):
    """The two main roads have to stay driveable end to end. Barrier runs are
    laid out by length, so without this check a wall line simply grows across
    the highway — and the south gate in particular needs clean ground to find
    a slot on."""
    return abs(x) < ROAD_CLEAR or abs(y) < ROAD_CLEAR


def twall(name, cx, cy, h=3.6, w=4.0, yaw=0.0, key="concrete"):
    """A single free-standing T-wall blast barrier — the panel plus the foot
    that keeps it upright. Scattered along open ground these are what turn a
    400 m sightline into a series of 40 m ones."""
    box(f"{name}_p", (cx, cy, h / 2), (w, 0.5, h), key, rot=(0, 0, yaw))
    box(f"{name}_f", (cx, cy, 0.25), (w * 0.55, 2.2, 0.5), key, rot=(0, 0, yaw))


def twall_run(name, x0, y0, x1, y1, key="concrete", h=3.6, gap=0.4):
    """A line of T-walls with the odd gap left in it, because a solid wall is
    a boundary but a broken one is cover you can fight through."""
    L = math.hypot(x1 - x0, y1 - y0)
    yaw = math.atan2(y1 - y0, x1 - x0)
    n = max(1, int(L / 4.4))
    for i in range(n):
        if i % 7 == 5:                     # a firing gap every seven panels
            continue
        t = (i + 0.5) / n
        px, py = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        if on_road(px, py):
            continue
        twall(f"{name}_{i}", px, py, h=h, yaw=yaw, key=key)


def hesco_run(name, cx, cy, length, key="sand", axis="x", h=2.2, t=2.0):
    """Gabion barrier: fat earth-filled blocks, chest-to-head high."""
    n = max(1, int(length / 3.2))
    for i in range(n):
        d = -length / 2 + (i + 0.5) * (length / n)
        x, y = (cx + d, cy) if axis == "x" else (cx, cy + d)
        if on_road(x, y):
            continue
        dims = (length / n * 0.96, t, h) if axis == "x" else (t, length / n * 0.96, h)
        box(f"{name}_{i}", (x, y, h / 2), dims, key)


def sandbags(name, cx, cy, length, key="sand", axis="x", h=1.3, cover_side=1):
    n = max(1, int(length / 2.6))
    for i in range(n):
        d = -length / 2 + (i + 0.5) * (length / n)
        x, y = (cx + d, cy) if axis == "x" else (cx, cy + d)
        if on_road(x, y):
            continue
        dims = (length / n * 0.95, 1.5, h) if axis == "x" else (1.5, length / n * 0.95, h)
        box(f"{name}_{i}", (x, y, h / 2), dims, key, rot=(0, 0, 0.06 * (i % 3)))
        # a firing position behind every other bag section — 1.3m is exactly
        # the height you kneel behind and stand up over
        if i % 2 == 0:
            cover(x - (0 if axis == "x" else 1.9 * cover_side),
                  y - (1.9 * cover_side if axis == "x" else 0))


def revetment(name, cx, cy, w, d, h=4.0, t=2.4, key="sand", open_side="S"):
    """U-shaped blast berm — the thing aircraft and armour park inside. Open on
    one side, so it is cover with exactly one way in and out."""
    if open_side != "N":
        box(f"{name}_N", (cx, cy + d / 2, h / 2), (w + 2 * t, t, h), key)
    if open_side != "S":
        box(f"{name}_S", (cx, cy - d / 2, h / 2), (w + 2 * t, t, h), key)
    if open_side != "E":
        box(f"{name}_E", (cx + w / 2, cy, h / 2), (t, d, h), key)
    if open_side != "W":
        box(f"{name}_W", (cx - w / 2, cy, h / 2), (t, d, h), key)


def guard_post(name, cx, cy):
    """Sentry box: small, enterable, and a readable silhouette at a gate."""
    room(f"{name}", cx, cy, 4.0, 4.0, 3.0, "concrete", doors="S",
         floor="concrete", door_w=1.6, door_h=2.4, light=True)
    window_band(name, cx, cy, 4.0, 4.0, 3.0, "glass", sides="NEW", z=1.5, tall=1.0)
    box(f"{name}_cap", (cx, cy, 3.55), (5.2, 5.2, 0.35), "metal")


def floodlight(name, cx, cy, h=14.0, color=(0.9, 0.93, 1.0)):
    box(f"{name}_m", (cx, cy, h / 2), (0.7, 0.7, h), "metal")
    box(f"{name}_h", (cx, cy, h + 0.5), (3.0, 1.4, 1.0), "hazard")
    LIGHTS.append({"x": cx, "z": cy * NS, "y": h + 0.2, "range": 46.0,
                   "intensity": 2.4, "spot": True, "color": list(color)})


def trench_seg(name, x0, y0, x1, y1, key="sand", width=3.4, h=1.9, t=1.3):
    """One axis-aligned leg of the trench line, walled both sides."""
    # the line has a deliberate break where the highway runs through it —
    # that gap is the way in, and the south gate is built to sit in it
    if min(abs(x0), abs(x1)) < ROAD_CLEAR + 6 and abs(x1 - x0) < 2 * (ROAD_CLEAR + 6):
        return
    # A 1.9m wall on both sides is a corridor you cannot shoot out of: standing
    # eye height is 1.5, so a soldier in here is blind and so is the cover test.
    # Real trenches solve this with a fire step — a ledge along the parapet you
    # stand on to shoot and drop off to reload. Adding it makes the trench work
    # for the player, the AI and G1CoverPoint at the same time.
    step_h, step_w = 0.55, 1.1
    if abs(x1 - x0) >= abs(y1 - y0):
        if min(x0, x1) < ROAD_CLEAR + 6 and max(x0, x1) > -(ROAD_CLEAR + 6):
            return
        cx, cy, L = (x0 + x1) / 2, y0, abs(x1 - x0)
        box(f"{name}_a", (cx, cy - width / 2 - t / 2, h / 2), (L, t, h), key)
        box(f"{name}_b", (cx, cy + width / 2 + t / 2, h / 2), (L, t, h), key)
        sy_ = cy - width / 2 + step_w / 2
        box(f"{name}_step", (cx, sy_, step_h / 2), (L, step_w, step_h), key)
        for k in range(max(1, int(L / 9))):
            cover(cx - L / 2 + (k + 0.5) * (L / max(1, int(L / 9))), sy_, step_h)
    else:
        cx, cy, L = x0, (y0 + y1) / 2, abs(y1 - y0)
        box(f"{name}_a", (cx - width / 2 - t / 2, cy, h / 2), (t, L, h), key)
        box(f"{name}_b", (cx + width / 2 + t / 2, cy, h / 2), (t, L, h), key)
        sx_ = cx - width / 2 + step_w / 2
        box(f"{name}_step", (sx_, cy, step_h / 2), (step_w, L, step_h), key)
        for k in range(max(1, int(L / 9))):
            cover(sx_, cy - L / 2 + (k + 0.5) * (L / max(1, int(L / 9))), step_h)


def decal(name, cx, cy, w, d, key, yaw=0.0, z=0.05):
    """A flat patch laid just above the ground: oil, scorch, tyre tracks, paint.

    No projector and no decal shader here — the whole map is flat-shaded boxes
    merged into one mesh, so the cheapest honest way to break up 800m of
    identical grey is a very thin box a few centimetres proud of the floor.
    Costs 12 triangles and does more for how the ground reads than any amount
    of extra architecture.
    """
    box(name, (cx, cy, z), (w, d, 0.02), key, rot=(0, 0, yaw))


def scatter_decals(name, cx, cy, spread, n, key, rng, wmin=2.0, wmax=7.0):
    for i in range(n):
        dx = rng.uniform(-spread, spread)
        dy = rng.uniform(-spread, spread)
        w = rng.uniform(wmin, wmax)
        decal(f"{name}_{i}", cx + dx, cy + dy, w, w * rng.uniform(0.4, 1.0), key,
              yaw=rng.uniform(0, math.pi), z=0.05 + 0.005 * (i % 3))


def barrel(name, cx, cy, key="rust", tipped=False):
    if tipped:
        cyl(name, (cx, cy, 0.3), 0.3, 0.9, key, verts=8,
            rot=(math.radians(90), 0, 0))
    else:
        cyl(name, (cx, cy, 0.45), 0.3, 0.9, key, verts=8)
        box(f"{name}_rim", (cx, cy, 0.9), (0.66, 0.66, 0.06), key)


def pallet_stack(name, cx, cy, n=3, key="wood"):
    for i in range(n):
        box(f"{name}_{i}", (cx, cy, 0.09 + i * 0.18), (1.2, 1.0, 0.16), key,
            rot=(0, 0, 0.05 * i))


def spool(name, cx, cy, key="wood"):
    for s in (-1, 1):
        cyl(f"{name}_c{s}", (cx, cy + s * 0.45, 0.9), 0.9, 0.14, key, verts=12,
            rot=(math.radians(90), 0, 0))
    cyl(f"{name}_core", (cx, cy, 0.9), 0.45, 0.9, "metal", verts=10,
        rot=(math.radians(90), 0, 0))


def nest(name, cx, cy, yaw=0.0, key="sand"):
    """An L of sandbags with firing positions behind it — a fighting position.

    The trench and the pillboxes gave the map plenty of cover, but all of it
    is on the southern approach, and the platoons fight over the plaza, the
    hangar apron and the warehouse yard. Cover the AI can't reach is cover that
    doesn't exist, so these go where the fighting actually happens.
    """
    h, t = 1.3, 1.4
    c, s = math.cos(yaw), math.sin(yaw)

    def place(ox, oy, w, d):
        box(f"{name}_{ox:.0f}_{oy:.0f}", (cx + ox * c - oy * s, cy + ox * s + oy * c, h / 2),
            (w, d, h), key, rot=(0, 0, yaw))

    place(0, 0, 7.0, t)
    place(3.2, -2.6, t, 6.0)
    for ox, oy in ((-2.0, -1.6), (1.0, -1.6), (1.6, -4.6)):
        cover(cx + ox * c - oy * s, cy + ox * s + oy * c)


def hedgehog(name, cx, cy, s=1.6):
    for i, rot in enumerate(((0.7, 0.7, 0), (-0.7, 0.7, 0), (0, 0.7, 0.7))):
        box(f"{name}_{i}", (cx, cy, s * 0.7), (0.3, 0.3, s * 2.4), "metal", rot=rot)


# =============================================================== build it
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

MATS.update({
    "ground": M("map_ground", (0.16, 0.17, 0.18), rough=1.0),
    "road": M("map_road", (0.10, 0.10, 0.11), rough=1.0),
    "asphalt": M("map_asphalt", (0.13, 0.13, 0.14), rough=1.0),
    "paint": M("map_paint", (0.82, 0.82, 0.78), rough=0.9),
    "concrete": M("map_concrete", (0.42, 0.43, 0.45)),
    "concrete_d": M("map_concrete_d", (0.30, 0.31, 0.33)),
    "metal": M("map_metal", (0.32, 0.34, 0.37), rough=0.5, metal=0.7),
    "rust": M("map_rust", (0.42, 0.24, 0.14), rough=0.9, metal=0.3),
    "hazard": M("map_hazard", (0.80, 0.42, 0.06)),
    "allied": M("map_allied", (0.16, 0.36, 0.62)),
    # A colour per district, so you can tell where you are from across the map
    # rather than only by the shape of the nearest building. Muted enough to
    # stay inside a 1998 palette, separated enough to read through fog.
    "lab_teal": M("map_lab_teal", (0.14, 0.42, 0.44)),
    "med_white": M("map_med_white", (0.78, 0.80, 0.80), rough=0.7),
    "med_red": M("map_med_red", (0.62, 0.13, 0.12)),
    "depot_yellow": M("map_depot_yellow", (0.72, 0.55, 0.09)),
    "air_grey": M("map_air_grey", (0.40, 0.44, 0.50)),
    "quarter_brick": M("map_quarter_brick", (0.44, 0.26, 0.20)),
    "warn_stripe": M("map_warn_stripe", (0.86, 0.72, 0.06)),
    "signal_green": M("map_signal_green", (0.10, 0.55, 0.26),
                      emit=(0.06, 0.5, 0.24), estr=1.4),
    "signal_red": M("map_signal_red", (0.60, 0.08, 0.08),
                    emit=(0.7, 0.06, 0.06), estr=1.6),
    "lab": M("map_lab", (0.72, 0.75, 0.78)),
    "alien": M("map_alien", (0.10, 0.55, 0.55), emit=(0.06, 0.5, 0.5), estr=1.2),
    "wood": M("map_wood", (0.38, 0.26, 0.14)),
    "glass": M("map_glass", (0.15, 0.4, 0.5), rough=0.2),
    "sand": M("map_sand", (0.46, 0.41, 0.30), rough=1.0),
    "earth": M("map_earth", (0.28, 0.25, 0.18), rough=1.0),
    "olive": M("map_olive", (0.24, 0.26, 0.17), rough=0.9),
    "container_a": M("map_cont_a", (0.55, 0.28, 0.16)),
    "container_b": M("map_cont_b", (0.20, 0.45, 0.40)),
    "lamp": M("map_lamp", (1.0, 0.86, 0.55), emit=(1.0, 0.8, 0.45), estr=3.0),
    # ground grime — the difference between "a floor" and "a floor things have
    # been happening on for years"
    "oil": M("map_oil", (0.055, 0.05, 0.045), rough=0.55),
    "scorch": M("map_scorch", (0.09, 0.08, 0.075), rough=1.0),
    "tracks": M("map_tracks", (0.115, 0.11, 0.10), rough=1.0),
    "spill": M("map_spill", (0.22, 0.19, 0.10), rough=0.7),
})

HALF = 400.0
# Blender +Y exports to Unity -Z. Author north/south with this factor so a
# district described as "north" actually builds in Unity's north.
NS = -1.0

# --- ground + roads (cross, inner ring, outer ring, diagonal spurs)
box("Ground", (0, 0, -0.25), (2 * HALF, 2 * HALF, 0.5), "ground")
box("Road_NS", (0, 0, 0.02), (16, 2 * HALF, 0.04), "road")
box("Road_EW", (0, 0, 0.02), (2 * HALF, 16, 0.04), "road")
for R, wdt in ((200, 12), (330, 10)):
    box(f"Ring{R}_N", (0, R, 0.02), (2 * R + 16, wdt, 0.04), "road")
    box(f"Ring{R}_S", (0, -R, 0.02), (2 * R + 16, wdt, 0.04), "road")
    box(f"Ring{R}_E", (R, 0, 0.02), (wdt, 2 * R + 16, 0.04), "road")
    box(f"Ring{R}_W", (-R, 0, 0.02), (wdt, 2 * R + 16, 0.04), "road")
for sx in (-1, 1):
    for sy in (-1, 1):
        box(f"Spur_{sx}_{sy}", (sx * 265, sy * 265, 0.02), (190, 9, 0.04),
            "road", rot=(0, 0, math.radians(45 * sx * sy)))
box("Plaza", (0, 0, 0.03), (60, 60, 0.05), "road")

# --- perimeter wall + watchtowers (corner + mid-wall)
wall_h, wall_t = 8.0, 1.6
edge = HALF - 2
box("Wall_E", (edge, 0, wall_h / 2), (wall_t, 2 * HALF, wall_h), "concrete")
box("Wall_W", (-edge, 0, wall_h / 2), (wall_t, 2 * HALF, wall_h), "concrete")
# the gap for the south gate has to end up on Unity's south side, which is
# Blender +Y — the solid run is the one at -Y
box("Wall_Solid", (0, -edge, wall_h / 2), (2 * HALF, wall_t, wall_h), "concrete")
box("Wall_S_L", (-(HALF / 2 + 8), edge, wall_h / 2), (HALF - 16, wall_t, wall_h), "concrete")
box("Wall_S_R", (HALF / 2 + 8, edge, wall_h / 2), (HALF - 16, wall_t, wall_h), "concrete")

for sx, sy in ((-1, -1), (-1, 1), (1, -1), (1, 1), (0, 1), (0, -1), (1, 0), (-1, 0)):
    # mid-wall towers are offset along the wall instead of sitting on the axis:
    # centred, they straddle the two main highways, and their access ramps then
    # land squarely on the south road where the player spawns
    cx = sx * (HALF - 6) if sx else 62
    cy = sy * (HALF - 6) if sy else 62
    nm = f"Tower_{sx}_{sy}"
    # the tower is a room with a ramp to its firing deck, not a solid pillar
    room(nm, cx, cy, 9, 9, 5.0, "concrete",
         doors="N" if sy < 0 else "S", floor="concrete", light=True)
    box(f"{nm}_deck", (cx, cy, 10.4), (11, 11, 1.6), "metal")
    # a parapet, not a lid — the v1 map put a solid 11x11 block on top of every
    # tower, so the "walkway" was sealed off by its own railing
    # parapet tops out at 12.3, below a standing eyeline of 12.7 on the 11.2m
    # deck — high enough to kneel behind, low enough to shoot over
    for s in (-1, 1):
        box(f"{nm}_railN{s}", (cx, cy + s * 5.4, 11.75), (11, 0.3, 1.1), "hazard")
        box(f"{nm}_railE{s}", (cx + s * 5.4, cy, 11.75), (0.3, 11, 1.1), "hazard")
    for ox, oy in ((0, 3.6), (0, -3.6), (3.6, 0), (-3.6, 0)):
        cover(cx + ox, cy + oy, 11.2)
    # ramp climbs from the map-centre side up to the deck surface (z = 11.2)
    if sx:
        ramp_to(f"{nm}_ramp", cx - sx * 5.0, cy, 16, 3.0, 11.2, "metal",
                axis="x", side=-sx)
    else:
        ramp_to(f"{nm}_ramp", cx, cy - sy * 5.0, 16, 3.0, 11.2, "metal",
                axis="y", side=-sy)
box("GatePost_L", (-9, edge, 5), (3, 3, 10), "hazard")
box("GatePost_R", (9, edge, 5), (3, 3, 10), "hazard")
box("GateBeam", (0, edge, 11), (21, 2, 1.6), "hazard")
guard_post("GuardPost_S", -18, edge - 8)
for i in range(12):
    a = i * (2 * math.pi / 12)
    floodlight(f"Flood_{i}", math.cos(a) * (HALF - 26), math.sin(a) * (HALF - 26))

# =============================== CENTRAL COMMAND TOWER (the landmark)
# The base is now a lobby you can walk into and shelter in; the tower above it
# is still the thing you navigate by from anywhere on the map.
# lobby height is chosen so its roof meets the underside of CmdT2 at z=8 —
# a floating tower reads as a bug from every angle on the plaza
room("CmdLobby", 0, 0, 30, 30, 7.6, "concrete", doors="NSEW",
     floor="concrete", door_w=5.0, door_h=4.0, roof=True)
window_band("CmdLobby", 0, 0, 30, 30, 7.6, "glass", sides="NSEW", z=5.4, tall=1.6)
box("CmdT2", (0, 0, 12), (24, 24, 8), "metal")
box("CmdT3", (0, 0, 20), (18, 18, 8), "concrete")
box("CmdT4", (0, 0, 28), (12, 12, 8), "metal")
box("CmdTop", (0, 0, 35), (8, 8, 6), "concrete")
box("CmdAntenna", (0, 0, 44), (1.4, 1.4, 12), "metal")
box("CmdBeacon", (0, 0, 50.4), (2.2, 2.2, 1.6), "signal_red")
for a in range(4):
    ang = a * math.pi / 2
    box(f"CmdWin_{a}", (math.cos(ang) * 12.1, math.sin(ang) * 12.1, 12),
        (0.3 if a % 2 == 0 else 12, 12 if a % 2 == 0 else 0.3, 4), "glass")
# blast walls ringing the plaza: the centre of the map was a killing field
for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
    twall_run(f"PlazaWall_{sx}_{sy}", sx * 22, sy * 8, sx * 22, sy * 26)
    twall_run(f"PlazaWallB_{sx}_{sy}", sx * 8, sy * 22, sx * 26, sy * 22)

# =============================== ALLIED BASE (west)
bx, by = -160, 0
for i, oy in enumerate((-30, -10, 10, 30)):
    room(f"Barracks_{i}", bx, by + oy, 30, 14, 4.2, "allied", doors="E",
         floor="concrete", door_w=3.2)
    window_band(f"Barracks_{i}", bx, by + oy, 30, 14, 4.2, "glass", sides="NS", z=2.0)
    box(f"BarracksRoof_{i}", (bx, by + oy, 4.7), (31, 15, 0.6), "metal")
catwalk("BarracksWalk", bx, by - 30, bx, by + 30, 5.2, "metal")
# lands on the barracks roof edge (x = bx + 15.5), climbing from the yard
ramp_to("BarracksRamp", bx + 15, by, 13, 3.0, 5.2, "metal", axis="x", side=1)
box("Helipad", (bx - 26, by, 0.08), (22, 22, 0.16), "concrete")
cyl("HelipadRing", (bx - 26, by, 0.1), 9, 0.05, "hazard", verts=24)
sandbags("AlliedBags", bx + 22, by, 54, axis="y")
hesco_run("AlliedHesco", bx + 30, by + 26, 30, axis="x")
box("AlliedFlag", (bx - 12, by, 9), (0.5, 0.5, 18), "metal")
box("AlliedBanner", (bx - 10, by, 15), (4, 0.3, 5), "allied")
room("AlliedOps", bx - 4, by + 52, 22, 16, 5.0, "med_white", doors="S",
     floor="concrete", door_w=3.4)
window_band("AlliedOps", bx - 4, by + 52, 22, 16, 5.0, "glass", sides="SEW", z=2.4)
for _mi in range(2):
    decal_side = 1 if _mi else -1
    box(f"MedCross_v{_mi}", (bx - 4 + decal_side * 11.2, by + 52, 3.4),
        (0.2, 1.0, 3.0), "med_red")
    box(f"MedCross_h{_mi}", (bx - 4 + decal_side * 11.2, by + 52, 3.4),
        (0.2, 3.0, 1.0), "med_red")

# =============================== LAB COMPLEX (north) — two storeys
lx, ly = 0, 165
for i, ox in enumerate((-26, 26)):
    room(f"LabBlock{i}", ox, ly, 28, 26, 5.0, "lab_teal", doors="S",
         floor="concrete", door_w=4.0)
    room(f"LabUpper{i}", ox, ly, 28, 26, 4.6, "lab_teal", doors="S",
         floor="concrete", door_w=4.0, z0=5.3)
    window_band(f"LabBlock{i}", ox, ly, 28, 26, 5.0, "glass", sides="NSEW", z=2.2)
    window_band(f"LabUpper{i}", ox, ly, 28, 26, 4.6, "glass", sides="NSEW", z=7.4)
    ramp_to(f"LabRamp{i}", ox, ly - 13, 14, 3.4, 5.3, "concrete", axis="y", side=-1)
catwalk("LabBridge", -14, ly, 14, ly, 5.6, "lab")
catwalk("LabBridgeHi", -14, ly, 14, ly, 10.4, "metal")
cyl("LabDome", (0, ly + 20, 7), 14, 14, "glass", verts=22)
box("LabHazard", (0, ly - 16, 2), (64, 1.4, 4), "hazard")
for _c in range(12):
    box(f"LabChevron_{_c}", (-30 + _c * 5.4, ly - 16.8, 2.0),
        (2.6, 0.2, 3.4), "warn_stripe", rot=(0, 0, 0.5))
for i in range(5):
    cyl(f"LabTank_{i}", (-36 + i * 18, ly + 20, 5), 2.6, 10, "metal", verts=10)
twall_run("LabScreen", -46, ly - 26, 46, ly - 26)

# =============================== HANGAR / MOTOR POOL (east)
hx, hy = 165, 0
box("HangarFloor", (hx, hy, 0.06), (64, 60, 0.12), "concrete")
box("HangarWallN", (hx, hy + 29, 8), (64, 1.4, 16), "metal")
box("HangarWallS", (hx, hy - 29, 8), (64, 1.4, 16), "metal")
box("HangarWallE", (hx + 31, hy, 8), (1.4, 60, 16), "metal")
box("HangarRoof", (hx, hy, 16.3), (66, 62, 0.8), "metal")
box("HangarStripe", (hx - 31, hy, 15), (1.4, 60, 2), "hazard")   # header, not a wall
for i in range(4):
    vx = hx - 18 + i * 12
    box(f"Truck_{i}_body", (vx, hy - 12, 2), (6, 12, 4), "olive")
    box(f"Truck_{i}_cab", (vx, hy - 19, 1.8), (6, 4, 3.4), "metal")
catwalk("HangarCat", hx - 28, hy + 20, hx + 28, hy + 20, 9.0, "metal")
ramp_to("HangarCatRamp", hx - 26, hy + 19, 16, 3.0, 9.0, "metal", axis="y", side=-1)
room("HangarOffice", hx + 18, hy + 18, 16, 14, 4.4, "concrete", doors="W",
     floor="concrete")
cyl("FuelTankH", (hx + 16, hy - 22, 4), 4.5, 8, "hazard", verts=14)
LIGHTS.append({"x": hx, "z": hy * NS, "y": 14.0, "range": 60.0,
               "intensity": 2.0, "spot": False, "color": [1.0, 0.9, 0.7]})

# =============================== ALIEN BREACH RUINS (south)
sx_, sy_ = 0, -165
box("RuinFloor", (sx_, sy_, -0.1), (90, 66, 0.3), "alien")
for i in range(10):
    x = -42 + i * 9.4
    h = 3 + (i % 3) * 3
    box(f"Ruin_{i}", (x, sy_ + 12 - (i % 2) * 24, h / 2), (6, 4, h),
        "concrete_d", rot=(0.08 * (i % 3), 0, 0.15 * i))
for i in range(9):
    ang = i * (2 * math.pi / 9)
    px, py = math.cos(ang) * 26, sy_ + math.sin(ang) * 18
    cyl(f"Pod_{i}", (px, py, 3.2), 3.0, 6.4, "alien", verts=10)
for i in range(14):
    a = i * (2 * math.pi / 14)
    box(f"BreachRing_{i}", (math.cos(a) * 8, sy_ + math.sin(a) * 8, 7),
        (1.0, 1.0, 1.0), "alien", rot=(0, 0, a))
box("Crater", (sx_, sy_, 0.05), (28, 28, 0.1), "road")
# a half-collapsed shelter to fight out of, on the edge of the ruins
room("RuinShelter", -34, sy_ + 26, 16, 12, 3.8, "concrete_d", doors="SE",
     floor="concrete_d", door_w=3.6)

# =============================== LIVING QUARTERS (NW)
qx, qy = -150, 150
for i in range(3):
    for j in range(2):
        cx, cy = qx + i * 22, qy + j * 22
        room(f"Quarters_{i}_{j}", cx, cy, 16, 16, 4.4, "quarter_brick",
             doors="S" if j == 0 else "N", floor="concrete")
        window_band(f"Quarters_{i}_{j}", cx, cy, 16, 16, 4.4, "glass",
                    sides="EW", z=2.0)
        box(f"QUpper_{i}_{j}", (cx, cy, 9.6), (16, 16, 9.6), "quarter_brick")
        box(f"QRoof_{i}_{j}", (cx, cy, 14.6), (17, 17, 0.6), "metal")
sandbags("QuartersBags", qx + 11, qy - 12, 46, axis="x")

# =============================== WAREHOUSE YARD (NE)
wxc, wyc = 150, 150
for i in range(4):
    for j in range(3):
        key = "container_a" if (i + j) % 2 == 0 else "container_b"
        h = 3.2
        for s in range(1 + ((i + j) % 3)):
            box(f"Cont_{i}_{j}_{s}", (wxc + i * 14, wyc + j * 8, 1.7 + s * (h + 0.1)),
                (12, 6, h), key)
room("WarehouseShed", wxc + 20, wyc + 26, 40, 20, 7.0, "metal", doors="SW",
     floor="concrete", door_w=6.0, door_h=5.0)
catwalk("WarehouseCat", wxc + 2, wyc + 26, wxc + 38, wyc + 26, 7.6, "metal")

# =============================== COMMS ARRAY (SE)
ax, ay = 155, -150
cyl("DishBase", (ax, ay, 6), 3, 12, "metal", verts=12)
cyl("Dish", (ax, ay + 5, 14), 10, 2, "lab", verts=20, rot=(math.radians(60), 0, 0))
for i in range(4):
    box(f"Mast_{i}", (ax - 24 + i * 16, ay - 20, 12), (1.2, 1.2, 24), "metal")
    box(f"MastLight_{i}", (ax - 24 + i * 16, ay - 20, 24.4), (2, 2, 0.8), "signal_red")
room("CommsHut", ax - 22, ay + 12, 14, 12, 4.0, "signal_green", doors="E",
     floor="concrete")

# =============================== FUEL DEPOT (SW)
fx, fy = -155, -150
for i in range(3):
    for j in range(2):
        cyl(f"FuelTank_{i}_{j}", (fx + i * 20, fy + j * 20, 7), 8, 14, "depot_yellow", verts=16)
# containment berm around the tank farm — a real depot has one, and it doubles
# as a waist-high firing line
for side, (dx, dy, w_, d_) in {
        "N": (20, 34, 76, 2.4), "S": (20, -14, 76, 2.4),
        "E": (58, 10, 2.4, 50), "W": (-18, 10, 2.4, 50)}.items():
    box(f"FuelBerm_{side}", (fx + dx, fy + dy, 1.1), (w_, d_, 2.2), "earth")
room("PumpHouse", fx - 34, fy, 14, 14, 4.2, "depot_yellow", doors="E",
     floor="concrete")
box("PipeRun", (fx, fy - 24, 1.5), (60, 1.4, 1.4), "metal")
box("PipeRun2", (fx + 30, fy - 24, 1.5), (1.4, 52, 1.4), "metal")

# ======================================================= OUTER RING: AIRSTRIP
rx, ry = 310, 0
box("Runway", (rx, ry, 0.04), (44, 280, 0.08), "asphalt")
for i in range(13):
    box(f"RwyDash_{i}", (rx, -132 + i * 22, 0.09), (1.6, 11, 0.04), "paint")
for end in (-1, 1):
    for k in range(6):
        box(f"RwyThresh_{end}_{k}", (rx - 15 + k * 6, end * 130, 0.09),
            (3.0, 16, 0.04), "paint")
box("Taxiway", (rx - 60, ry, 0.04), (76, 22, 0.08), "asphalt")
for i, ty in enumerate((-90, -30, 30, 90)):
    revetment(f"Revet_{i}", rx - 52, ty, 26, 24, h=4.2, open_side="E")
    box(f"Jet_{i}_body", (rx - 52, ty, 2.2), (4, 16, 3), "olive")
    box(f"Jet_{i}_wing", (rx - 52, ty - 1, 2.2), (18, 4, 0.8), "olive")
# control tower — enterable, three levels, and visible from the plaza
room("ATCBase", rx - 78, 108, 16, 16, 5.0, "air_grey", doors="W",
     floor="concrete", door_w=3.4)
room("ATCMid", rx - 78, 108, 14, 14, 4.6, "air_grey", doors="S", floor="concrete",
     z0=5.3, light=True)
box("ATCCab", (rx - 78, 108, 12.6), (18, 18, 4.0), "glass")
box("ATCRoof", (rx - 78, 108, 14.8), (20, 20, 0.5), "metal")
ramp_to("ATCRamp", rx - 78, 100, 14, 3.2, 5.3, "concrete", axis="y", side=-1)
for i, hy_ in enumerate((-60, -114)):
    room(f"AirHangar_{i}", rx - 86, hy_, 34, 30, 9.0, "air_grey", doors="E",
         floor="concrete", door_w=10.0, door_h=7.5)
box("Windsock", (rx + 30, 60, 5), (0.4, 0.4, 10), "metal")
box("WindsockFlag", (rx + 32, 60, 9.5), (4, 1.2, 1.2), "hazard")
for i in range(6):
    floodlight(f"RwyFlood_{i}", rx + 26, -120 + i * 48, h=10.0)

# ==================================================== OUTER RING: AMMO FIELD
# Six earth-covered igloos in a dispersed row: hollow inside, bermed outside,
# widely spaced because that is exactly what an ammunition storage area is.
# `front` is the direction the igloo's mouth faces — toward the service road,
# which is on the base side. Everything in front of the door (headwalls, apron)
# takes +front and everything behind it (the back berm, the earth cap's offset)
# takes -front. Getting one of these backwards buries the doorway under six
# metres of earth and the igloo reads as a solid mound.
front = -NS
for i in range(6):
    cx = -160 + i * 64
    cy = 320 * NS
    nm = f"Igloo_{i}"
    room(nm, cx, cy, 12, 16, 4.2, "concrete_d", doors="N" if front > 0 else "S",
         floor="concrete_d", door_w=3.4, door_h=3.2)
    for k, shrink in enumerate((0, 2.5, 5.0)):
        box(f"{nm}_cap{k}", (cx, cy - 1.2 * front, 4.6 + k * 1.1),
            (20 - shrink, 22 - shrink, 1.1), "earth")
    box(f"{nm}_bL", (cx - 9, cy - 1 * front, 2.2), (6, 20, 4.4), "earth")
    box(f"{nm}_bR", (cx + 9, cy - 1 * front, 2.2), (6, 20, 4.4), "earth")
    box(f"{nm}_bB", (cx, cy - 11 * front, 2.2), (24, 6, 4.4), "earth")
    for s in (-1, 1):
        box(f"{nm}_head{s}", (cx + s * 6.0, cy + 8.6 * front, 2.4), (5, 1.0, 4.8), "concrete")
    box(f"{nm}_apron", (cx, cy + 14 * front, 0.05), (14, 12, 0.1), "asphalt")
box("AmmoRoad", (0, 300 * NS, 0.03), (400, 10, 0.06), "road")
guard_post("AmmoGuard", -206, 306 * NS)
hesco_run("AmmoHesco", 0, 288 * NS, 300, axis="x")

# =================================================== OUTER RING: TRENCH LINE
# The southern approach, dug in. A zigzag so no single burst rakes the whole
# line, pillboxes anchoring the corners, wire out front.
ty0 = -300 * NS               # Unity south, whichever way the export flips
out = -1 * NS                 # sign that points away from the base (outward)
legs = []
for i in range(8):
    x0 = -240 + i * 60
    legs.append((x0, ty0, x0 + 30, ty0))
    legs.append((x0 + 30, ty0, x0 + 30, ty0 + 22 * out))
    legs.append((x0 + 30, ty0 + 22 * out, x0 + 60, ty0 + 22 * out))
    legs.append((x0 + 60, ty0 + 22 * out, x0 + 60, ty0))
for i, (x0, y0, x1, y1) in enumerate(legs):
    trench_seg(f"Trench_{i}", x0, y0, x1, y1)
for i, px in enumerate((-210, -90, 30, 150)):
    nm = f"Pillbox_{i}"
    room(nm, px, ty0 + 30 * out, 10, 9, 3.2, "concrete_d",
         doors="S" if out > 0 else "N", floor="concrete_d", door_w=2.4, door_h=2.4)
    # Firing slit: concrete over and under a gap facing the open ground. The
    # gap sits at 1.15–1.85m so a standing soldier's eyeline (1.5) is through
    # it and a crouching one is behind concrete — chest height, which is both
    # what a real embrasure is and what makes the cover test resolve.
    box(f"{nm}_slitL", (px, ty0 + 34.4 * out, 0.575), (10, 0.7, 1.15), "concrete_d")
    box(f"{nm}_slitU", (px, ty0 + 34.4 * out, 2.525), (10, 0.7, 1.35), "concrete_d")
    box(f"{nm}_cap", (px, ty0 + 30 * out, 3.6), (12.5, 11.5, 0.7), "concrete_d")
    for s in (-2.5, 0, 2.5):
        cover(px + s, ty0 + 32.5 * out)
for i in range(40):
    hx_ = -260 + i * 13.5
    if abs(hx_) < ROAD_CLEAR + 8:          # leave the highway lane open
        continue
    hedgehog(f"Hedge_{i}", hx_, ty0 + (46 - (i % 3) * 5) * out)
sandbags("TrenchBagsA", -60, ty0 - 9 * out, 220, axis="x")
room("TrenchCP", -270, ty0 + 8 * out, 18, 14, 4.0, "concrete_d",
     doors="S" if out > 0 else "N", floor="concrete_d")

# ==================================================== OUTER RING: TANK PARK
tpx = -320
for i, tpy in enumerate((-96, -32, 32, 96)):
    revetment(f"TankRevet_{i}", tpx, tpy, 30, 26, h=3.4, open_side="E")
    for k in range(2):
        bxp = tpx - 6 + k * 12
        box(f"Tank_{i}_{k}_hull", (bxp, tpy, 1.5), (5, 9, 3), "olive")
        box(f"Tank_{i}_{k}_turret", (bxp, tpy + 1, 3.6), (3.6, 4.2, 1.8), "olive")
        box(f"Tank_{i}_{k}_gun", (bxp, tpy + 6, 3.7), (0.5, 8, 0.5), "metal")
# set back far enough that the revetment mouths stay clear of its west wall
room("TankWorkshop", tpx - 42, 0, 40, 46, 9.0, "metal", doors="EN",
     floor="concrete", door_w=9.0, door_h=7.0)
# mezzanine along the back wall, reached from inside so the vehicle door
# stays clear of it
catwalk("WorkshopCat", tpx - 56, 16, tpx - 28, 16, 5.4, "metal")
ramp_to("WorkshopRamp", tpx - 32, 14, 13, 3.0, 5.4, "metal", axis="y", side=-1)
box("TankApron", (tpx, 0, 0.04), (56, 240, 0.08), "asphalt")
hesco_run("TankHesco", tpx + 24, 0, 220, axis="y")
for i in range(4):
    floodlight(f"TankFlood_{i}", tpx + 30, -96 + i * 64, h=11.0)

# =============================== corner landmarks: silhouettes to navigate by
# A water tower and a radar make the NE and SE corners tellable apart from a
# kilometre away, which is the only thing that stops a big map feeling samey.
wtx, wty = 300, 300
for i in range(4):
    a = i * math.pi / 2 + math.pi / 4
    box(f"WaterLeg_{i}", (wtx + math.cos(a) * 7, wty + math.sin(a) * 7, 11),
        (1.2, 1.2, 22), "metal", rot=(0, 0, a))
cyl("WaterTank", (wtx, wty, 25), 10, 9, "metal", verts=14)
box("WaterCap", (wtx, wty, 30.2), (12, 12, 1.4), "rust")
room("PumpShack", wtx - 20, wty - 12, 12, 10, 3.6, "metal", doors="S", floor="concrete")

rdx, rdy = 300, -300
box("RadarMast", (rdx, rdy, 9), (3.0, 3.0, 18), "metal")
box("RadarArm", (rdx, rdy, 19), (2.0, 22, 1.2), "metal")
box("RadarPanel", (rdx, rdy + 9, 22), (1.0, 8, 7), "lab", rot=(0.35, 0, 0))
room("RadarControl", rdx - 18, rdy, 16, 14, 4.4, "concrete", doors="E",
     floor="concrete")
window_band("RadarControl", rdx - 18, rdy, 16, 14, 4.4, "glass", sides="NS", z=2.2)

# training ground (SW corner): low walls and pipes, a natural firefight space
tgx, tgy = -300, -300
for i in range(6):
    box(f"TrainWall_{i}", (tgx - 40 + i * 16, tgy + (i % 2) * 14, 1.3),
        (12, 1.0, 2.6), "concrete_d")
for i in range(4):
    cyl(f"TrainPipe_{i}", (tgx - 20 + i * 14, tgy - 16, 1.6), 1.6, 12,
        "rust", verts=10, rot=(0, math.radians(90), 0))
room("TrainHouse", tgx + 26, tgy + 6, 18, 18, 4.2, "concrete_d", doors="NSEW",
     floor="concrete_d")

# =============================== scattered cover + sightline breakers
import random as _r
_r.seed(7)
for i in range(110):
    ang = _r.uniform(0, 2 * math.pi)
    rad = _r.uniform(34, 370)
    x, y = math.cos(ang) * rad, math.sin(ang) * rad
    if on_road(x, y):
        continue
    roll = _r.random()
    if roll < 0.30:
        # tall panel: a sightline breaker you shoot around, not over
        twall(f"CoverTW_{i}", x, y, yaw=_r.uniform(0, math.pi))
    elif roll < 0.62:
        # jersey barrier — 1.1m, the one height that is genuinely cover: you
        # kneel behind it and stand to shoot. The old scatter was 2m crates,
        # which are just walls you cannot see past.
        yaw = _r.uniform(0, math.pi)
        box(f"Jersey_{i}", (x, y, 0.55), (4.0, 0.9, 1.1), "concrete_d", rot=(0, 0, yaw))
        box(f"JerseyFoot_{i}", (x, y, 0.14), (4.0, 1.5, 0.28), "concrete_d", rot=(0, 0, yaw))
        # both sides: which one is cover depends on where the threat is, and
        # G1CoverPoint works that out per-fight
        for s in (-1, 1):
            cover(x + math.sin(yaw) * 1.5 * s, y - math.cos(yaw) * 1.5 * s)
    else:
        box(f"Crate_{i}", (x, y, 0.6), (2.0, 2.0, 1.2), "wood",
            rot=(0, 0, _r.uniform(0, 1.5)))
        for s in (-1, 1):
            cover(x + 1.9 * s, y)
# long blast-wall screens across the widest empty runs
for x0, y0, x1, y1 in ((-250, 120, -110, 120), (110, -120, 250, -120),
                       (-90, 250, 90, 250), (250, 90, 250, 230),
                       (-250, -90, -250, -230)):
    twall_run(f"Screen_{x0}_{y0}", x0, y0, x1, y1, h=4.0)

# --- lamp posts along the ring roads
for R in (200, 330):
    n = 16 if R == 200 else 24
    for i in range(n):
        a = i * (2 * math.pi / n)
        lx2, ly2 = math.cos(a) * R, math.sin(a) * R
        if on_road(lx2, ly2):          # a lamp post planted in the highway
            continue
        box(f"Lamp_{R}_{i}", (lx2, ly2, 6), (0.6, 0.6, 12), "metal")
        box(f"LampHead_{R}_{i}", (lx2, ly2, 12.4), (2.2, 2.2, 0.8), "lamp")

# --- catwalk network near the centre
catwalk("CatwalkNS", 0, 20, 0, 70, 6.0, "metal")
ramp("CatwalkRamp", 0, 76, 14, 3.4, 5.8, "metal", axis="y")

# =============================== FIGHTING POSITIONS
# Placed at the contested ground the spawn lists actually put troops on: the
# plaza approaches where the two lines meet, the hangar apron, the warehouse
# yard, the lab screen and the airstrip revetments.
for i, (nx, ny, nyaw) in enumerate((
        (-46, 18, 0.0), (-46, -18, 0.4), (-34, 0, -0.3),
        (34, 16, math.pi), (34, -16, math.pi * 0.8), (26, 0, math.pi * 1.1),
        (0, 38, -math.pi / 2), (0, -38, math.pi / 2),
        (hx - 44, hy + 14, math.pi * 0.5), (hx - 44, hy - 14, math.pi * 1.5),
        (hx - 20, hy + 26, math.pi), (hx - 20, hy - 26, 0.0),
        (wxc - 16, wyc + 4, math.pi * 1.5), (wxc + 6, wyc - 14, 0.0),
        (bx + 44, by + 12, math.pi), (bx + 44, by - 12, math.pi * 0.9),
        (lx - 20, ly - 34, 0.0), (lx + 20, ly - 34, 0.2),
        (rx - 66, 20, math.pi * 0.5), (rx - 66, -20, math.pi * 1.5),
        (tpx + 34, 40, math.pi * 1.5), (tpx + 34, -40, math.pi * 0.5))):
    if on_road(nx, ny):
        continue
    nest(f"Nest_{i}", nx, ny, nyaw)

# =============================== GRIME AND CLUTTER
# Everything above is architecture, and architecture on its own reads as a
# model kit. This pass is what makes it look occupied: oil under the vehicles,
# scorch where the fighting was, tyre tracks on the aprons, and the small junk
# that accumulates wherever people work — barrels, pallets, cable spools.
_d = _r.Random(21)

# oil and tracks where machines live
scatter_decals("OilHangar", hx, hy, 26, 16, "oil", _d, 1.4, 4.0)
scatter_decals("TrackHangar", hx - 34, hy, 14, 8, "tracks", _d, 5.0, 14.0)
scatter_decals("OilMotorpool", -30, -58, 20, 10, "oil", _d, 1.2, 3.4)
scatter_decals("TrackTankPark", tpx, 0, 90, 22, "tracks", _d, 5.0, 16.0)
scatter_decals("OilTankPark", tpx - 6, 0, 80, 16, "oil", _d, 1.2, 3.6)
scatter_decals("RwyRubber", rx, 0, 110, 18, "tracks", _d, 2.5, 9.0)
scatter_decals("FuelSpill", fx, fy, 28, 14, "spill", _d, 1.6, 5.0)

# scorch where the map's own story says fighting happened
scatter_decals("BreachScorch", sx_, sy_, 34, 22, "scorch", _d, 2.0, 8.0)
scatter_decals("TrenchScorch", -40, ty0 + 30 * out, 190, 30, "scorch", _d, 1.5, 6.0)
scatter_decals("PlazaScorch", 0, 0, 26, 10, "scorch", _d, 1.5, 5.0)
scatter_decals("GateScorch", 0, edge - 40, 60, 12, "scorch", _d, 1.5, 5.5)

# junk clusters at the places people work
for nm, jx, jy, spread, cnt in (
        ("JunkHangar", hx - 8, hy + 16, 16, 12),
        ("JunkMotor", -34, -52, 14, 10),
        ("JunkWarehouse", wxc + 18, wyc + 20, 16, 12),
        ("JunkFuel", fx + 6, fy - 18, 14, 9),
        ("JunkTank", tpx - 20, 24, 14, 10),
        ("JunkAir", rx - 74, -60, 16, 10),
        ("JunkAllied", bx + 26, by - 18, 14, 9),
        ("JunkTrench", -150, ty0 - 12 * out, 26, 10)):
    for i in range(cnt):
        px, py = jx + _d.uniform(-spread, spread), jy + _d.uniform(-spread, spread)
        if on_road(px, py):
            continue
        pick = _d.random()
        if pick < 0.45:
            barrel(f"{nm}_b{i}", px, py, "rust" if _d.random() < 0.6 else "hazard",
                   tipped=_d.random() < 0.25)
        elif pick < 0.75:
            pallet_stack(f"{nm}_p{i}", px, py, n=_d.randint(2, 5))
        elif pick < 0.9:
            box(f"{nm}_c{i}", (px, py, 0.4), (1.4, 1.1, 0.8), "wood",
                rot=(0, 0, _d.uniform(0, 1.5)))
        else:
            spool(f"{nm}_s{i}", px, py)

# painted ground markings — cheap, and they tell you what a yard is for
for i in range(10):
    decal(f"BayLine_{i}", hx - 24 + i * 5.6, hy - 14, 0.35, 22, "paint")
for i in range(8):
    decal(f"TankBay_{i}", tpx + 12, -84 + i * 24, 22, 0.35, "paint")
decal("HelipadCross_a", bx - 26, by, 14, 1.6, "paint", z=0.11)
decal("HelipadCross_b", bx - 26, by, 1.6, 14, "paint", z=0.11)

# ------------------------------------------------------------- assign mats
for ob, key in objs:
    ob.data.materials.append(mat(key))

# ------------------------------------------------------------- chunking
# v1 joined the entire map into one object. At 600 m that was merely wasteful;
# at 800 m with interior lighting it breaks two things outright. Unity picks
# the brightest few pixel lights *per renderer*, so one giant mesh means one
# light budget for the whole map and every interior lamp gets dropped — and a
# single bounding box that always spans the camera is never frustum-culled.
# Joining into a grid of district-sized chunks fixes both.
GRID = 4
CHUNK = 2 * HALF / GRID

ground = next(ob for ob, k in objs if ob.name == "Ground")
chunks = {}
shell = []
for ob, key in objs:
    if ob is ground:
        continue
    # Bucket by centre and a 400 m road lands in whichever cell its midpoint
    # falls in, dragging that chunk's bounds across the entire map — which
    # hands the chunk back the single giant collider we were trying to avoid.
    # Anything longer than a chunk goes to a shell object instead: roads,
    # perimeter walls, aprons and runways, none of which need local lighting.
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    span_x = max(v.x for v in bb) - min(v.x for v in bb)
    span_y = max(v.y for v in bb) - min(v.y for v in bb)
    if max(span_x, span_y) > CHUNK * 0.6:
        shell.append(ob)
        continue
    ix = min(GRID - 1, max(0, int((ob.location.x + HALF) / CHUNK)))
    iy = min(GRID - 1, max(0, int((ob.location.y + HALF) / CHUNK)))
    chunks.setdefault((ix, iy), []).append(ob)

pieces = []


def _join(members, name):
    bpy.ops.object.select_all(action="DESELECT")
    for ob in members:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = members[0]
    if len(members) > 1:
        bpy.ops.object.join()
    piece = bpy.context.active_object
    piece.name = name
    pieces.append(piece)


for (ix, iy), members in sorted(chunks.items()):
    _join(members, f"Sprawl_{ix}_{iy}")
if shell:
    _join(shell, "Sprawl_Shell")

# ------------------------------------------------------------- studio/render
bpy.ops.object.light_add(type="SUN", location=(120, -120, 240))
sun = bpy.context.active_object
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(55), 0, math.radians(35))
bpy.ops.object.camera_add(location=(0, -12, 980))
cam = bpy.context.active_object
cam.data.type = "ORTHO"
cam.data.ortho_scale = 880
cam.rotation_euler = (0, 0, 0)
sc = bpy.context.scene
sc.camera = cam
sc.render.engine = "CYCLES"
sc.cycles.samples = 20
sc.cycles.use_denoising = True
sc.render.resolution_x = 1200
sc.render.resolution_y = 1200
sc.view_settings.view_transform = "Standard"
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "METAL"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = True
    sc.cycles.device = "GPU"
except Exception:
    sc.cycles.device = "CPU"

os.makedirs(f"{BASE}/renders", exist_ok=True)
os.makedirs(f"{BASE}/blender", exist_ok=True)
sc.render.filepath = f"{BASE}/renders/huge_map_top.png"
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/huge_map.blend")

os.makedirs(UNITY, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
ground.select_set(True)
for piece in pieces:
    piece.select_set(True)
bpy.context.view_layer.objects.active = pieces[0]
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/HugeMap.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", use_space_transform=True,
    bake_space_transform=True, object_types={"MESH"}, mesh_smooth_type="FACE")

# The interiors only exist as geometry; Unity has no way to know a hollow box
# is a room. Ship a manifest alongside the FBX so lights and loot get placed
# from the same numbers that built the walls.
manifest = {"half": HALF, "rooms": ROOMS, "lights": LIGHTS, "cover": COVER}
with open(f"{UNITY}/HugeMap.manifest.json", "w") as fh:
    json.dump(manifest, fh, indent=1)

tris = sum((len(p.vertices) - 2) for ob in [ground] + pieces for p in ob.data.polygons)
print(f"HUGE MAP DONE — {int(2 * HALF)}x{int(2 * HALF)}m, ~{tris} tris, "
      f"{len(pieces)} chunks, {len(ROOMS)} interiors, {len(LIGHTS)} lights, "
      f"{len(COVER)} cover points")
