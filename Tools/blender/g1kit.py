"""Shared geometry kit for the Project G1 map generators.

Lifted verbatim out of build_huge_map.py, which grew these helpers over the
course of building the Corvus Sprawl and had no reason to share them until
there was a second map. It is a copy rather than an extraction on purpose:
build_huge_map.py still ships the map the game is currently played on, and a
refactor that silently moves 400 lines out from under a working 800x800 m
level is not a trade worth making mid-project. The huge map can migrate onto
this module later, when it is not the only map.

A generator using this kit must, before calling anything:

    import g1kit
    g1kit.NS = -1.0            # the Blender+Y -> Unity-Z export flip
    g1kit.MATS.update({...})   # its own palette, keyed by the names below

and afterwards read back `objs`, `ROOMS`, `LIGHTS`, `COVER` to export.
"""
import bpy
import json
import math
import os
import sys
from mathutils import Vector

objs = []
ROOMS = []       # interiors the Unity side lights and stocks
LIGHTS = []      # explicit light placements (floodlights, lamps)
COVER = []       # firing positions the AI can claim
DEVICES = []     # interactive equipment the Unity builder instantiates

# Set by the generator before it builds anything: the Blender +Y -> Unity -Z
# export flip. Left as None so a generator that forgets fails loudly on the
# first `cover()` call rather than quietly mirroring half the map.
NS = None


def device(kind, x, y, z=0.0, yaw=0.0, tag=""):
    """Record a piece of interactive equipment, in UNITY space.

    The geometry for a door panel or a card reader is just boxes, and boxes
    do nothing. Rather than have the Unity builder re-type where every console
    stands — the mistake the room manifest already exists to prevent — the
    generator that placed the box also declares what it is, and the builder
    attaches the component.
    """
    DEVICES.append({"kind": kind, "x": x, "z": y * NS, "y": z,
                    "yaw": yaw, "tag": tag})


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


# Everything on this map has been standing in the same dust for two hundred
# and six iterations. Coating the whole palette by one factor at the point of
# creation keeps the districts telling each other apart — the hues stay in
# their relative places — while pulling the saturation down to something that
# looks like it has weathered rather than like it was just unwrapped.
DUST = (0.26, 0.23, 0.18)
DUSTINESS = 0.20


def M(name, color, rough=0.85, metal=0.0, emit=None, estr=0.0):
    # lights, screens and beacons are the things that are *supposed* to cut
    # through the murk, so they keep their colour
    if emit is None:
        color = tuple(c * (1.0 - DUSTINESS) + d * DUSTINESS
                      for c, d in zip(color, DUST))
        rough = min(1.0, rough + 0.10)          # dust is never glossy
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
def bay_centres(span, n, w):
    """Where the openings are in a wall `span` long carrying `n` doors `w` wide.

    Exists so the shutter and the hole it covers cannot disagree. The first
    motor pool had one 9 m doorway and three 8 m shutters spread 18 m apart, so
    two of the three were bolted to solid concrete and opened onto nothing —
    which is invisible in the editor and obvious the moment you walk up to one.
    """
    pier = (span - n * w) / (n + 1)
    return [-span / 2 + pier * (k + 1) + w * k + w / 2 for k in range(n)]


def room(name, cx, cy, w, d, h, key, doors="S", floor=None, roof=True,
         wall_t=0.6, door_w=3.4, door_h=3.0, z0=0.0, light=True, door_n=1):
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
            # n openings and n+1 piers between them
            centres = bay_centres(span, door_n, door_w)
            pier = (span - door_n * door_w) / (door_n + 1)
            for j in range(door_n + 1):
                c = -span / 2 + j * (pier + door_w) + pier / 2
                if along_x:
                    box(f"{name}_w{side}{j}", (sx + c, sy, z0 + h / 2), (pier, wall_t, h), key)
                else:
                    box(f"{name}_w{side}{j}", (sx, sy + c, z0 + h / 2), (wall_t, pier, h), key)
            if h > door_h:                      # lintel across each opening
                lh = h - door_h
                dims = (door_w, wall_t, lh) if along_x else (wall_t, door_w, lh)
                for k, c in enumerate(centres):
                    lx = sx + c if along_x else sx
                    ly = sy if along_x else sy + c
                    box(f"{name}_l{side}{k}", (lx, ly, z0 + door_h + lh / 2), dims, key)
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


