"""Generate 'Cradle Station' — the Corvus Robotics & Research Facility.

A second, separate 480x480 m level. The Sprawl is a battlefield with a base
scattered over it; this is the opposite — a working installation you are
walking through while it is still running, which is what makes the parasitised
robots frightening rather than merely present. Everything here was built by
people who expected to come back tomorrow.

Where the Sprawl is dust and revetments, this is concrete floors, painted
lane markings, server hum and clean-room white. The palette is deliberately
colder and less dusty for exactly that reason: arriving here should feel like
arriving somewhere else.

Layout (Blender XY, exported to Unity XZ — see the AXIS note below):

    Gatehouse & checkpoint    ( 0, -200)  boom barrier, inspection bay
    Barracks quarter          (-150,-110) three sleeping blocks, mess hall,
                                          armoury, ablutions, parade ground
    Warehouse & logistics     ( 150,-110) span shed, racking, loading dock,
                                          container yard
    Motor pool                ( 150,  60) garage bays, service pits, fuel point
    Headquarters              (   0,   0) five storeys, lift shaft, roof pad
    Power hall                (-150,  30) turbine hall, switchgear
    Robotics yard             (-150, 130) assembly line, chassis racks — this
                                          is where the outbreak started
    Research complex          (   0, 170) clean labs, server hall, containment

AXIS WARNING — the FBX export flips north/south: Blender +Y arrives in Unity as
-Z, and X passes through unchanged. Everything here is authored in *Blender*
coordinates and the manifest applies the flip on the way out, so geometry and
manifest always agree. When reasoning about where a player stands in Unity,
negate the Y written here.

Run:  blender --background --python build_research_base.py -- <project_dir> <unity_env_models_dir>
"""
import bpy
import json
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import g1kit
from g1kit import (M, box, cyl, room, window_band, ramp, ramp_to, stairs,
                   catwalk, twall, twall_run, decal, scatter_decals, barrel,
                   pallet_stack, spool, guard_post, floodlight, cover, device,
                   objs, ROOMS, LIGHTS, COVER, DEVICES)
import random

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BASE = args[0] if args else "."
UNITY = args[1] if len(args) > 1 else "."

HALF = 240.0
g1kit.NS = -1.0
g1kit.ROAD_CLEAR = 11.0
# A working facility, not a weathered one. The Sprawl's 0.20 dust coat is what
# makes it read as abandoned; dialling it down is most of what makes this place
# feel occupied before a single light is placed.
g1kit.DUSTINESS = 0.06
g1kit.DUST = (0.22, 0.22, 0.24)

rng = random.Random(9041)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

g1kit.MATS.update({
    "ground": M("cs_ground", (0.19, 0.20, 0.21), rough=1.0),
    "road": M("cs_road", (0.115, 0.12, 0.13), rough=1.0),
    "asphalt": M("cs_asphalt", (0.145, 0.15, 0.16), rough=1.0),
    "paint": M("cs_paint", (0.86, 0.87, 0.84), rough=0.85),
    "concrete": M("cs_concrete", (0.46, 0.47, 0.49)),
    "concrete_d": M("cs_concrete_d", (0.33, 0.34, 0.36)),
    "metal": M("cs_metal", (0.36, 0.38, 0.42), rough=0.42, metal=0.75),
    "steel_pale": M("cs_steel_pale", (0.60, 0.63, 0.67), rough=0.35, metal=0.8),
    "rust": M("cs_rust", (0.42, 0.24, 0.14), rough=0.9, metal=0.3),
    "hazard": M("cs_hazard", (0.84, 0.46, 0.06)),
    "warn_stripe": M("cs_warn", (0.88, 0.76, 0.08)),
    "glass": M("cs_glass", (0.30, 0.52, 0.60), rough=0.12),
    "glass_lab": M("cs_glass_lab", (0.62, 0.80, 0.84), rough=0.08),
    "wood": M("cs_wood", (0.40, 0.28, 0.16)),
    "olive": M("cs_olive", (0.25, 0.27, 0.18), rough=0.9),
    "canvas": M("cs_canvas", (0.34, 0.33, 0.24), rough=1.0),
    # district colours — you should be able to say where you are from the
    # colour of the nearest wall, the way HL1's chapters each own a palette
    "barrack_tan": M("cs_barrack", (0.52, 0.45, 0.33)),
    "depot_blue": M("cs_depot", (0.16, 0.30, 0.46)),
    "motor_green": M("cs_motor", (0.20, 0.34, 0.26)),
    "hq_grey": M("cs_hq", (0.40, 0.43, 0.48)),
    "power_ochre": M("cs_power", (0.54, 0.40, 0.12)),
    "robot_violet": M("cs_robot", (0.30, 0.24, 0.42)),
    "lab_white": M("cs_lab_white", (0.80, 0.83, 0.84), rough=0.55),
    "lab_trim": M("cs_lab_trim", (0.20, 0.52, 0.56)),
    "clean_floor": M("cs_clean_floor", (0.68, 0.72, 0.74), rough=0.35),
    # things that glow. These keep their colour — the dust coat skips emissives
    "lamp": M("cs_lamp", (1.0, 0.94, 0.80), emit=(1.0, 0.92, 0.75), estr=3.0),
    "signal_green": M("cs_sig_g", (0.10, 0.60, 0.28), emit=(0.06, 0.55, 0.26), estr=1.6),
    "signal_red": M("cs_sig_r", (0.62, 0.08, 0.08), emit=(0.75, 0.06, 0.06), estr=1.8),
    "signal_blue": M("cs_sig_b", (0.10, 0.34, 0.70), emit=(0.10, 0.35, 0.85), estr=1.8),
    "screen": M("cs_screen", (0.10, 0.42, 0.40), emit=(0.10, 0.62, 0.58), estr=2.2),
    "parasite": M("cs_parasite", (0.42, 0.62, 0.18), emit=(0.30, 0.70, 0.12), estr=1.5),
    # ground grime
    "oil": M("cs_oil", (0.06, 0.055, 0.05), rough=0.5),
    "scorch": M("cs_scorch", (0.10, 0.09, 0.085), rough=1.0),
    "tracks": M("cs_tracks", (0.13, 0.125, 0.12), rough=1.0),
})


# --------------------------------------------------------------- small kit
def lamp_post(name, x, y, h=9.0):
    cyl(f"{name}_pole", (x, y, h / 2), 0.16, h, "metal")
    box(f"{name}_head", (x, y, h + 0.2), (1.5, 0.7, 0.35), "lamp")
    LIGHTS.append({"x": x, "z": y * g1kit.NS, "y": h + 0.1,
                   "range": 26.0, "intensity": 2.0, "spot": True,
                   "color": [1.0, 0.93, 0.78]})


def strip_light(name, x, y, z, length=6.0, axis="x", color=(1.0, 0.95, 0.85),
                intensity=1.5, rng_=18.0):
    """A ceiling tube. Indoors this is the difference between a room you can
    fight in and a room you back out of."""
    dims = (length, 0.3, 0.16) if axis == "x" else (0.3, length, 0.16)
    box(f"{name}_tube", (x, y, z), dims, "lamp")
    LIGHTS.append({"x": x, "z": y * g1kit.NS, "y": z - 0.3,
                   "range": rng_, "intensity": intensity, "spot": False,
                   "color": list(color)})


def crate(name, x, y, s=1.2, key="wood", z0=0.0):
    box(name, (x, y, z0 + s / 2), (s, s, s), key)


def rack(name, x, y, w=10.0, d=1.4, levels=3, key="metal", yaw=0.0):
    """Warehouse racking: uprights and shelves. Solid enough to break a
    sightline and thin enough to shoot through the gap between levels."""
    for s in (-1, 1):
        for t in (-1, 1):
            box(f"{name}_up{s}{t}", (x + s * w / 2, y + t * d / 2, 2.6),
                (0.16, 0.16, 5.2), key, rot=(0, 0, yaw))
    for lv in range(levels):
        z = 0.9 + lv * 1.7
        box(f"{name}_sh{lv}", (x, y, z), (w, d, 0.12), key, rot=(0, 0, yaw))


def bunk(name, x, y, yaw=0.0):
    """Two-tier bed. Barracks are beds — a 'sleeping block' without them is a
    corridor with a sign on it."""
    for lv, z in enumerate((0.45, 1.35)):
        box(f"{name}_m{lv}", (x, y, z), (0.9, 2.0, 0.18), "canvas", rot=(0, 0, yaw))
        box(f"{name}_f{lv}", (x, y, z - 0.12), (0.94, 2.04, 0.08), "metal", rot=(0, 0, yaw))
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"{name}_p{sx}{sy}", (x + sx * 0.43, y + sy * 0.98, 0.95),
                (0.08, 0.08, 1.9), "metal", rot=(0, 0, yaw))


def locker(name, x, y, n=3, yaw=0.0):
    for i in range(n):
        box(f"{name}_{i}", (x + (i - (n - 1) / 2) * 0.52, y, 0.95),
            (0.5, 0.55, 1.9), "metal", rot=(0, 0, yaw))


def table(name, x, y, w=3.6, d=1.0, yaw=0.0):
    box(f"{name}_top", (x, y, 0.76), (w, d, 0.08), "steel_pale", rot=(0, 0, yaw))
    for sx in (-1, 1):
        box(f"{name}_leg{sx}", (x + sx * (w / 2 - 0.3), y, 0.38),
            (0.1, d * 0.8, 0.76), "metal", rot=(0, 0, yaw))
    for sy in (-1, 1):     # benches
        box(f"{name}_bench{sy}", (x, y + sy * (d / 2 + 0.42), 0.44),
            (w * 0.92, 0.34, 0.08), "wood", rot=(0, 0, yaw))


def console(name, x, y, yaw=0.0, key="steel_pale", tag=""):
    """A terminal you can walk up to and use. The screen is a separate emissive
    box so it reads as switched on from across a dark room."""
    box(f"{name}_body", (x, y, 0.55), (1.5, 0.8, 1.1), key, rot=(0, 0, yaw))
    box(f"{name}_desk", (x, y - 0.1, 1.14), (1.6, 1.0, 0.1), key, rot=(0, 0, yaw))
    box(f"{name}_scr", (x, y + 0.16, 1.62), (1.2, 0.1, 0.8), "screen", rot=(0, 0, yaw))
    box(f"{name}_hood", (x, y + 0.24, 2.06), (1.3, 0.34, 0.12), key, rot=(0, 0, yaw))
    device("terminal", x, y, 0.0, yaw, tag)


def reader(name, x, y, z=1.4, yaw=0.0, tag=""):
    """Card reader beside a door. Small, but it is the whole vocabulary of
    'this door is locked and there is a way to unlock it'."""
    box(f"{name}_plate", (x, y, z), (0.26, 0.14, 0.42), "metal", rot=(0, 0, yaw))
    box(f"{name}_led", (x, y - 0.09, z + 0.13), (0.12, 0.04, 0.08), "signal_red",
        rot=(0, 0, yaw))
    device("keycard", x, y, z, yaw, tag)


def rollup(name, x, y, w=6.0, h=4.5, yaw=0.0, tag=""):
    """A roll-up shutter: slats in a frame. Unity drives it upward on use."""
    box(f"{name}_frameL", (x - w / 2 - 0.2, y, h / 2), (0.4, 0.5, h), "metal", rot=(0, 0, yaw))
    box(f"{name}_frameR", (x + w / 2 + 0.2, y, h / 2), (0.4, 0.5, h), "metal", rot=(0, 0, yaw))
    box(f"{name}_head", (x, y, h + 0.35), (w + 0.9, 0.7, 0.7), "metal", rot=(0, 0, yaw))
    for i in range(int(h / 0.45)):
        box(f"{name}_slat{i}", (x, y, 0.22 + i * 0.45), (w, 0.16, 0.4),
            "hazard" if i % 4 == 0 else "steel_pale", rot=(0, 0, yaw))
    device("rollup", x, y, 0.0, yaw, tag)


def blast_door(name, x, y, w=4.0, h=3.4, yaw=0.0, tag=""):
    """Two leaves that part sideways. Research wing only — the sound of one of
    these opening is meant to mean you have left the part of the base where
    people sleep."""
    for s in (-1, 1):
        box(f"{name}_leaf{s}", (x + s * w / 4, y, h / 2), (w / 2, 0.36, h),
            "steel_pale", rot=(0, 0, yaw))
        box(f"{name}_chev{s}", (x + s * w / 4, y - 0.2, h * 0.62), (w / 2 - 0.3, 0.06, 0.3),
            "warn_stripe", rot=(0, 0, yaw))
    box(f"{name}_jambL", (x - w / 2 - 0.25, y, h / 2), (0.5, 0.7, h + 0.3), "concrete_d", rot=(0, 0, yaw))
    box(f"{name}_jambR", (x + w / 2 + 0.25, y, h / 2), (0.5, 0.7, h + 0.3), "concrete_d", rot=(0, 0, yaw))
    device("blastdoor", x, y, 0.0, yaw, tag)


def lift(name, x, y, floors, tag=""):
    """A shaft with a car in it. `floors` are the stop heights; the Unity side
    reads them off the manifest so the two can never disagree about where the
    third floor is."""
    top = max(floors) + 4.0
    for sx, sy, dx, dy in ((-1, 0, 0.4, 3.4), (1, 0, 0.4, 3.4), (0, 1, 3.4, 0.4)):
        box(f"{name}_wall{sx}{sy}", (x + sx * 1.7, y + sy * 1.7, top / 2),
            (dx, dy, top), "concrete_d")
    box(f"{name}_car", (x, y, floors[0] + 1.4), (2.8, 2.8, 0.16), "steel_pale")
    device("elevator", x, y, floors[0], 0.0,
           tag + "|" + ",".join(f"{f:.2f}" for f in floors))


def fabricator(name, x, y, yaw=0.0):
    box(f"{name}_base", (x, y, 0.6), (2.2, 1.4, 1.2), "metal", rot=(0, 0, yaw))
    box(f"{name}_hood", (x, y, 1.85), (2.0, 1.2, 1.3), "steel_pale", rot=(0, 0, yaw))
    box(f"{name}_win", (x, y - 0.62, 1.9), (1.5, 0.08, 0.9), "glass", rot=(0, 0, yaw))
    box(f"{name}_led", (x, y - 0.62, 2.62), (1.6, 0.1, 0.12), "signal_blue", rot=(0, 0, yaw))
    device("fabricator", x, y, 0.0, yaw)


def server_row(name, x, y, n=6, yaw=0.0):
    for i in range(n):
        cx = x + (i - (n - 1) / 2) * 1.3
        box(f"{name}_r{i}", (cx, y, 1.05), (1.1, 1.1, 2.1), "concrete_d", rot=(0, 0, yaw))
        box(f"{name}_f{i}", (cx, y - 0.58, 1.05), (0.9, 0.06, 1.8), "screen", rot=(0, 0, yaw))


def chassis(name, x, y, yaw=0.0, broken=False):
    """A robot body on a stand — the parasite's hardware, before a parasite got
    to it. Broken ones are the outbreak's handwriting on the walls."""
    z = 0.0 if broken else 1.0
    if not broken:
        box(f"{name}_stand", (x, y, 0.5), (1.6, 1.0, 1.0), "metal", rot=(0, 0, yaw))
    box(f"{name}_torso", (x, y, z + 0.9), (1.1, 0.7, 1.4), "steel_pale", rot=(0, 0, yaw))
    box(f"{name}_head", (x, y, z + 1.85), (0.6, 0.55, 0.5), "metal", rot=(0, 0, yaw))
    box(f"{name}_eye", (x, y - 0.3, z + 1.9), (0.36, 0.06, 0.12),
        "signal_red" if broken else "signal_blue", rot=(0, 0, yaw))
    for s in (-1, 1):
        box(f"{name}_arm{s}", (x + s * 0.75, y, z + 1.0), (0.28, 0.28, 1.2),
            "metal", rot=(0, 0, yaw))
    if broken:
        box(f"{name}_goo", (x + 0.9, y + 0.6, 0.06), (2.2, 1.8, 0.06), "parasite")


def footlocker(name, x, y, yaw=0.0):
    box(f"{name}_body", (x, y, 0.22), (0.90, 0.46, 0.44), "olive", rot=(0, 0, yaw))
    box(f"{name}_lid", (x, y, 0.455), (0.94, 0.50, 0.05), "metal", rot=(0, 0, yaw))
    box(f"{name}_latch", (x, y - 0.24, 0.30), (0.10, 0.04, 0.10), "steel_pale", rot=(0, 0, yaw))


def pegrail(name, x, y, length=3.0, yaw=0.0):
    """Coat pegs on a wall rail. Nobody looks at them and every barracks has
    them; it is the accumulation of things nobody looks at that separates a
    room somebody lives in from a room somebody modelled."""
    box(f"{name}_rail", (x, y, 1.72), (length, 0.06, 0.10), "wood",
        rot=(0, 0, yaw))
    n = max(2, int(length / 0.5))
    for i in range(n):
        o = (i - (n - 1) / 2) * (length / n)
        box(f"{name}_peg{i}", (x + o * math.cos(yaw), y + o * math.sin(yaw), 1.66),
            (0.04, 0.14, 0.04), "metal")


def noticeboard(name, x, y, yaw=0.0, w=1.8):
    box(f"{name}_back", (x, y, 1.55), (w, 0.06, 1.10), "wood", rot=(0, 0, yaw))
    box(f"{name}_cork", (x, y - 0.04, 1.55), (w - 0.14, 0.02, 0.96), "canvas", rot=(0, 0, yaw))
    for i in range(5):
        box(f"{name}_paper{i}", (x - w / 2 + 0.3 + i * (w - 0.6) / 4, y - 0.06,
                                 1.42 + (i % 3) * 0.22),
            (0.20, 0.01, 0.26), "paint", rot=(0, 0, yaw))


def stove(name, x, y):
    cyl(f"{name}_body", (x, y, 0.55), 0.28, 1.10, "metal", verts=12)
    box(f"{name}_door", (x, y - 0.27, 0.45), (0.28, 0.06, 0.30), "steel_pale")
    box(f"{name}_glow", (x, y - 0.30, 0.45), (0.16, 0.02, 0.16), "signal_red")
    cyl(f"{name}_flue", (x, y, 2.30), 0.09, 2.40, "metal", verts=10)


def sink(name, x, y, yaw=0.0):
    box(f"{name}_basin", (x, y, 0.82), (0.52, 0.42, 0.18), "lab_white",
        rot=(0, 0, yaw))
    box(f"{name}_pedestal", (x, y, 0.40), (0.16, 0.16, 0.72), "lab_white",
        rot=(0, 0, yaw))
    box(f"{name}_tap", (x, y + 0.16, 0.98), (0.05, 0.14, 0.12), "steel_pale", rot=(0, 0, yaw))


def cubicle(name, x, y, w=1.05, d=1.35, yaw=0.0):
    for sx in (-1, 1):
        box(f"{name}_side{sx}", (x + sx * w / 2, y, 1.05), (0.06, d, 2.10),
            "lab_white", rot=(0, 0, yaw))
    box(f"{name}_back", (x, y + d / 2, 1.05), (w, 0.06, 2.10), "lab_white", rot=(0, 0, yaw))
    box(f"{name}_door", (x, y - d / 2, 1.10), (w - 0.10, 0.05, 1.70),
        "depot_blue", rot=(0, 0, yaw))


def walkway(name, x0, y0, x1, y1, w=3.0):
    """The covered way between blocks. More than anything else in this quarter
    it is what says people walk this route every day in the rain."""
    dx, dy = x1 - x0, y1 - y0
    length = math.hypot(dx, dy)
    yaw = math.atan2(dy, dx)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    box(f"{name}_roof", (cx, cy, 2.85), (length, w, 0.14), "metal",
        rot=(0, 0, yaw))
    box(f"{name}_slab", (cx, cy, 0.05), (length, w, 0.10), "concrete",
        rot=(0, 0, yaw))
    n = max(2, int(length / 4.5))
    for i in range(n + 1):
        t = i / n
        px, py = x0 + dx * t, y0 + dy * t
        for s in (-1, 1):
            ox, oy = -math.sin(yaw) * s * w / 2, math.cos(yaw) * s * w / 2
            cyl(f"{name}_post{i}{s}", (px + ox, py + oy, 1.42), 0.07, 2.84, "metal",
                verts=8)


# ================================================================= build it
# --- ground, roads, lane markings
box("Ground", (0, 0, -0.25), (2 * HALF, 2 * HALF, 0.5), "ground")
box("Road_NS", (0, 0, 0.02), (14, 2 * HALF, 0.04), "road")
box("Road_EW", (0, -60, 0.02), (2 * HALF, 14, 0.04), "road")
box("Road_EW2", (0, 110, 0.02), (2 * HALF, 12, 0.04), "road")
box("Road_W", (-150, 0, 0.02), (12, 300, 0.04), "road")
box("Road_E", (150, 0, 0.02), (12, 300, 0.04), "road")
for i in range(-22, 23):          # centre line, so roads read as roads
    box(f"Lane_{i}", (0, i * 10, 0.05), (0.4, 5.0, 0.03), "paint")

# ------------------------------------------------------- gatehouse (south)
GY = -200
box("Gate_apron", (0, GY, 0.03), (70, 34, 0.06), "asphalt")
room("Gatehouse", -13, GY, 12, 9, 3.4, "hq_grey", doors="NE", floor="clean_floor")
window_band("Gatehouse", -13, GY, 12, 9, 3.4, "glass", sides="EW", z=1.5, tall=1.1)
strip_light("Gate_lt", -13, GY, 3.1, 6)
console("Gate_console", -13, GY + 2.6, tag="gate")
reader("Gate_reader", -6.6, GY - 3.0, 1.4, tag="gate")
for s in (-1, 1):                  # boom barriers across each carriageway
    box(f"Boom_post{s}", (s * 7.0, GY, 0.7), (0.5, 0.5, 1.4), "metal")
    box(f"Boom_arm{s}", (s * 7.0 + s * 4.5, GY, 1.25), (9.0, 0.28, 0.22), "warn_stripe")
    device("barrier", s * 7.0, GY, 0.0, 0.0, "gate")
box("Gate_sign", (0, GY + 15, 5.0), (22, 0.5, 2.2), "hq_grey")
box("Gate_sign_lit", (0, GY + 14.6, 5.0), (20, 0.1, 1.4), "signal_blue")
for s in (-1, 1):
    box(f"Gate_signleg{s}", (s * 10, GY + 15, 2.5), (0.6, 0.6, 5.0), "metal")
twall_run("Gate_wallW", -HALF + 6, GY, -24, GY, key="concrete_d", h=4.0)
twall_run("Gate_wallE", 24, GY, HALF - 6, GY, key="concrete_d", h=4.0)
guard_post("Gate_gp", 22, GY + 8)
lamp_post("Gate_lp1", -24, GY + 6)
lamp_post("Gate_lp2", 24, GY + 6)
cover(-7.6, GY + 1.2)
cover(7.6, GY + 1.2)

# --------------------------------------------------- barracks quarter (west)
BX, BY = -150, -110
box("Bar_apron", (BX, BY, 0.03), (96, 108, 0.06), "asphalt")
for i in range(3):                 # three sleeping blocks in a row
    y = BY - 34 + i * 30
    n = f"Sleep{i}"
    room(n, BX - 22, y, 26, 20, 3.6, "barrack_tan", doors="E", floor="concrete")
    window_band(n, BX - 22, y, 26, 20, 3.6, "glass", sides="NSW", z=1.5, tall=1.0)
    strip_light(f"{n}_lt1", BX - 29, y, 3.3, 8)
    strip_light(f"{n}_lt2", BX - 15, y, 3.3, 8)
    for j in range(4):             # bunks down both walls, aisle between
        by = y - 7.5 + j * 5.0
        bunk(f"{n}_bunkA{j}", BX - 30.5, by)
        bunk(f"{n}_bunkB{j}", BX - 13.5, by)
        locker(f"{n}_lockA{j}", BX - 32.8, by + 2.4, n=2)
        # a footlocker at the end of every bunk, which is where a soldier's
        # possessions actually live
        footlocker(f"{n}_ftA{j}", BX - 30.5, by - 1.45)
        footlocker(f"{n}_ftB{j}", BX - 13.5, by - 1.45)
    pegrail(f"{n}_pegs", BX - 22, y - 9.6, length=6.0)
    noticeboard(f"{n}_notice", BX - 11.4, y + 6.0, yaw=math.radians(90), w=1.6)
    stove(f"{n}_stove", BX - 22, y + 7.4)
    box(f"{n}_bin", (BX - 12.6, y - 7.8, 0.36), (0.44, 0.44, 0.72), "metal")
    # NCO's cubby at the door end: one bed, one desk, a wall between him and
    # everyone else. Every barracks block has one and it changes how the room
    # reads from "dormitory" to "unit".
    box(f"{n}_ncowall", (BX - 16.5, y + 5.2, 1.35), (0.16, 9.6, 2.70), "concrete")
    table(f"{n}_ncodesk", BX - 14.2, y + 4.4, w=1.8, d=0.8)
    device("bunkroom", BX - 22, y, 0.0, 0.0, f"sleep{i}")

# mess hall — the room that most says "people live here"
room("MessHall", BX + 16, BY - 22, 34, 24, 4.4, "barrack_tan", doors="WS", floor="concrete")
window_band("MessHall", BX + 16, BY - 22, 34, 24, 4.4, "glass", sides="NE", z=1.8, tall=1.4)
for r in range(3):
    for c in range(2):
        table(f"Mess_t{r}{c}", BX + 6 + c * 20, BY - 30 + r * 8)
box("Mess_counter", (BX + 16, BY - 10.5, 0.55), (26, 1.2, 1.1), "steel_pale")
box("Mess_counter_top", (BX + 16, BY - 10.5, 1.16), (26.6, 1.6, 0.12), "steel_pale")
for i in range(4):
    box(f"Mess_pot{i}", (BX + 6 + i * 6.5, BY - 10.5, 1.4), (1.4, 0.9, 0.4), "metal")
strip_light("Mess_lt1", BX + 8, BY - 22, 4.1, 12)
strip_light("Mess_lt2", BX + 24, BY - 22, 4.1, 12)
# the serving line, and the things that pile up at the end of one
box("Mess_traystack", (BX + 3.5, BY - 10.5, 1.32), (0.5, 0.7, 0.22), "steel_pale")
box("Mess_urn", (BX + 27.5, BY - 10.5, 1.55), (0.5, 0.5, 0.66), "steel_pale")
box("Mess_urn_tap", (BX + 27.5, BY - 10.9, 1.34), (0.08, 0.12, 0.08), "metal")
noticeboard("Mess_notice", BX + 16, BY - 33.6, w=2.4)
for i in range(3):
    box(f"Mess_bin{i}", (BX + 2.0 + i * 1.0, BY - 32.6, 0.42), (0.46, 0.46, 0.84),
        "metal")
box("Mess_servehatch", (BX + 16, BY - 9.6, 2.20), (12.0, 0.20, 1.10), "steel_pale")
cover(BX + 16, BY - 11.8)

# armoury — locked, and the only room on the map that is worth locking
room("Armoury", BX + 20, BY + 22, 22, 18, 3.8, "concrete_d", doors="W", floor="concrete")
strip_light("Arm_lt", BX + 20, BY + 22, 3.5, 8, color=(0.85, 0.9, 1.0))
blast_door("Arm_door", BX + 9, BY + 22, 3.6, 3.2, yaw=math.radians(90), tag="armoury")
reader("Arm_reader", BX + 9.6, BY + 17.6, 1.4, tag="armoury")
for i in range(4):                 # weapon racks along the back wall
    box(f"Arm_rack{i}", (BX + 28.5, BY + 15 + i * 4.4, 1.2), (1.0, 3.6, 2.4), "metal")
    box(f"Arm_rackbar{i}", (BX + 27.9, BY + 15 + i * 4.4, 1.9), (0.1, 3.4, 0.1), "steel_pale")
for i in range(6):
    crate(f"Arm_crate{i}", BX + 13 + (i % 3) * 2.6, BY + 16 + (i // 3) * 3.0,
          s=1.1, key="olive")
fabricator("Arm_fab", BX + 13.5, BY + 28.5)
device("ammo_cache", BX + 20, BY + 22, 0.0, 0.0, "armoury")

# ablutions + parade ground
room("Ablutions", BX + 18, BY - 44, 20, 14, 3.2, "barrack_tan", doors="N",
     floor="clean_floor")
strip_light("Abl_lt1", BX + 12, BY - 44, 2.9, 6, color=(0.9, 0.95, 1.0))
strip_light("Abl_lt2", BX + 24, BY - 44, 2.9, 6, color=(0.9, 0.95, 1.0))
# A washroom is the most domestic thing on a military base and the room that
# most says people were living here this morning. It was a bare shell.
for i in range(5):                 # sinks along the west wall, mirror over them
    sink(f"Abl_sink{i}", BX + 10.0, BY - 49.5 + i * 2.2)
box("Abl_mirror", (BX + 9.3, BY - 44, 1.55), (0.06, 11.0, 0.90), "glass_lab")
box("Abl_shelf", (BX + 9.6, BY - 44, 1.02), (0.22, 11.0, 0.05), "lab_white")
for i in range(4):                 # cubicles along the east wall
    cubicle(f"Abl_wc{i}", BX + 26.0, BY - 48.6 + i * 2.6, yaw=math.radians(180))
for i in range(4):                 # shower heads on the south wall
    box(f"Abl_showerhead{i}", (BX + 13.0 + i * 2.4, BY - 50.2, 2.30),
        (0.16, 0.16, 0.10), "steel_pale")
    cyl(f"Abl_showerpipe{i}", (BX + 13.0 + i * 2.4, BY - 50.5, 2.55), 0.035, 0.7,
        "steel_pale", verts=8)
    box(f"Abl_showerdrain{i}", (BX + 13.0 + i * 2.4, BY - 49.6, 0.07),
        (1.0, 1.0, 0.04), "metal")
box("Abl_duckboard", (BX + 16.6, BY - 49.6, 0.10), (9.0, 1.6, 0.08), "wood")
box("Parade", (BX, BY + 46, 0.04), (70, 34, 0.08), "asphalt")
for i in range(-3, 4):
    box(f"Parade_mark{i}", (BX + i * 9, BY + 46, 0.06), (0.3, 30, 0.04), "paint")
cyl("Flagpole", (BX, BY + 60, 8.0), 0.14, 16.0, "steel_pale")
box("Flag", (BX + 1.6, BY + 60, 14.6), (3.0, 0.08, 1.8), "depot_blue")
box("Flag_plinth", (BX, BY + 60, 0.30), (3.2, 3.2, 0.60), "concrete")
box("Flag_plinth2", (BX, BY + 60, 0.70), (2.4, 2.4, 0.24), "concrete_d")
# the dais the CO stands on, and the boards nobody has updated
box("Parade_dais", (BX - 22, BY + 60, 0.35), (5.0, 3.0, 0.70), "concrete")
box("Parade_dais_rail", (BX - 22, BY + 61.3, 1.20), (5.0, 0.10, 0.10), "metal")
for i in range(3):
    box(f"Parade_sign{i}", (BX + 12 + i * 8, BY + 60, 1.30), (3.0, 0.24, 1.30),
        "barrack_tan")
    box(f"Parade_signface{i}", (BX + 12 + i * 8, BY + 59.85, 1.30),
        (2.6, 0.04, 1.00), "paint")
    for s2 in (-1, 1):
        box(f"Parade_signleg{i}{s2}", (BX + 12 + i * 8 + s2 * 1.3, BY + 60, 0.33),
            (0.14, 0.14, 0.66), "metal")
# covered ways: the route between sleeping blocks and the mess, which is the
# walk everyone here makes three times a day
walkway("Bar_way1", BX - 9, BY - 34, BX - 9, BY + 26)
walkway("Bar_way2", BX - 9, BY - 22, BX + 2, BY - 22)
for i in range(6):                  # washing lines behind the blocks
    box(f"Bar_line{i}", (BX - 38, BY - 30 + i * 12, 2.05), (0.03, 9.0, 0.03),
        "metal")
    for s2 in (-1, 1):
        cyl(f"Bar_linepost{i}{s2}", (BX - 38, BY - 30 + i * 12 + s2 * 4.5, 1.05),
            0.06, 2.10, "metal", verts=8)
lamp_post("Bar_lp1", BX + 40, BY - 30)
lamp_post("Bar_lp2", BX + 40, BY + 10)
lamp_post("Bar_lp3", BX - 40, BY + 40)

# --------------------------------------------- warehouse & logistics (east)
WX, WY = 150, -110
box("Whs_apron", (WX, WY, 0.03), (100, 110, 0.06), "asphalt")
room("Warehouse", WX, WY, 62, 46, 11.0, "depot_blue", doors="W", floor="concrete",
     door_w=8.0, door_h=6.0, door_n=3)
# the shutters go on the bays the wall actually has, not on a spacing chosen
# separately from it
for i, c in enumerate(g1kit.bay_centres(46, 3, 8.0)):
    rollup(f"Whs_door{i}", WX - 31, WY + c, 7.4, 5.6,
           yaw=math.radians(90), tag=f"warehouse{i}")
for r in range(4):                 # racking aisles
    for c in range(2):
        rack(f"Whs_rack{r}{c}", WX - 12 + c * 26, WY - 16 + r * 11, w=20, d=2.4)
for i in range(14):
    pallet_stack(f"Whs_pal{i}", WX - 22 + rng.uniform(-4, 48), WY - 20 + rng.uniform(0, 40),
                 n=rng.randint(2, 4))
for i in range(5):
    strip_light(f"Whs_lt{i}", WX - 20 + i * 11, WY, 10.4, 14, intensity=1.9, rng_=26)
catwalk("Whs_cat", WX - 28, WY + 20, WX + 28, WY + 20, 6.2, "metal", width=2.4)
stairs("Whs_stair", WX + 26, WY + 12, 3.0, 7.0, 6.2, 12, "metal", axis="y")
console("Whs_console", WX + 26, WY - 20, tag="warehouse")
cover(WX - 12, WY + 19.0, 6.2)
cover(WX + 12, WY + 19.0, 6.2)

# loading dock + container yard
box("Dock", (WX - 40, WY, 0.6), (14, 40, 1.2), "concrete")
ramp_to("Dock_ramp", WX - 47, WY - 14, 8.0, 6.0, 1.2, "concrete", axis="x", side=-1)
for i in range(9):
    key = "container_a" if i % 2 else "container_b"
    if key not in g1kit.MATS:
        key = "depot_blue" if i % 2 else "motor_green"
    x = WX - 44 + (i % 3) * 13
    y = WY + 34 + (i // 3) * 7
    box(f"Cont{i}", (x, y, 1.3), (12.0, 2.5, 2.6), key)
    if i % 3 == 0:
        box(f"Cont{i}_b", (x, y, 3.95), (12.0, 2.5, 2.6), "rust")
lamp_post("Whs_lp1", WX - 44, WY - 30)
lamp_post("Whs_lp2", WX + 40, WY + 30)

# ------------------------------------------------------ motor pool (east)
MX, MY = 150, 60
box("Mot_apron", (MX, MY, 0.03), (92, 76, 0.06), "asphalt")
room("Garage", MX, MY, 54, 30, 7.5, "motor_green", doors="S", floor="concrete",
     door_w=9.0, door_h=5.4, door_n=3)
for i, c in enumerate(g1kit.bay_centres(54, 3, 9.0)):
    rollup(f"Mot_door{i}", MX + c, MY - 15, 8.4, 5.0, tag=f"garage{i}")
for i in range(3):                 # service pits — real garages have holes
    box(f"Mot_pit{i}", (MX - 18 + i * 18, MY + 4, 0.05), (5.0, 12.0, 0.1), "oil")
    for s in (-1, 1):
        box(f"Mot_pitedge{i}{s}", (MX - 18 + i * 18 + s * 2.7, MY + 4, 0.1),
            (0.4, 12.0, 0.2), "warn_stripe")
for i in range(4):
    strip_light(f"Mot_lt{i}", MX - 20 + i * 14, MY + 2, 7.0, 12, intensity=1.8, rng_=22)
console("Mot_console", MX + 22, MY + 10, tag="motorpool")
for i in range(6):
    barrel(f"Mot_bar{i}", MX - 24 + rng.uniform(0, 46), MY + 11 + rng.uniform(-2, 2))
box("Mot_bench", (MX + 20, MY + 12.5, 0.5), (10, 1.0, 1.0), "metal")
# fuel point outside the doors
for i in range(2):
    x = MX - 8 + i * 16
    box(f"Fuel_pump{i}", (x, MY - 26, 1.1), (1.2, 0.9, 2.2), "hazard")
    box(f"Fuel_scr{i}", (x, MY - 26.5, 1.9), (0.8, 0.1, 0.5), "screen")
    device("fuel", x, MY - 26, 0.0, 0.0, "motorpool")
cyl("Fuel_tank", (MX + 26, MY - 26, 2.4), 3.2, 4.8, "steel_pale",
    rot=(math.radians(90), 0, 0))
device("vehicle_spawn", MX - 18, MY - 34, 0.0, 0.0, "truck")
device("vehicle_spawn", MX, MY - 34, 0.0, 0.0, "truck")
device("vehicle_spawn", MX + 18, MY - 34, 0.0, 0.0, "truck")
lamp_post("Mot_lp1", MX - 40, MY - 30)
lamp_post("Mot_lp2", MX + 40, MY + 30)

# --------------------------------------------------- headquarters (centre)
# Five storeys with a lift. The Sprawl's command tower is a lobby with a roof
# you can reach; this one is a building you work your way up.
FLOOR_H = 4.2
HQ_FLOORS = [0.0, FLOOR_H, FLOOR_H * 2, FLOOR_H * 3, FLOOR_H * 4]
box("HQ_plaza", (0, 0, 0.04), (72, 62, 0.08), "concrete")
for f, z in enumerate(HQ_FLOORS):
    doors = "S" if f == 0 else ""
    room(f"HQ_L{f}", 0, 0, 44, 34, FLOOR_H, "hq_grey", doors=doors,
         floor="clean_floor", roof=(f == len(HQ_FLOORS) - 1), z0=z)
    window_band(f"HQ_L{f}", 0, 0, 44, 34, FLOOR_H, "glass",
                sides="NEW", z=z + 1.3, tall=2.0)
    strip_light(f"HQ_lt{f}a", -11, 0, z + FLOOR_H - 0.35, 10, intensity=1.6)
    strip_light(f"HQ_lt{f}b", 11, 0, z + FLOOR_H - 0.35, 10, intensity=1.6)
lift("HQ_lift", 17, 13, HQ_FLOORS, tag="hq")
stairs("HQ_stair", -17, -13, 3.2, 8.0, FLOOR_H, 14, "metal", axis="y")
# what each floor is for
console("HQ_recep", 0, -13, tag="hq_lobby")               # ground: reception
for i in range(4):                                         # 1F: ops room
    console(f"HQ_ops{i}", -12 + i * 8, 8, tag="hq_ops")
box("HQ_map_table", (0, -4, FLOOR_H + 0.8), (12, 6, 0.2), "steel_pale")
box("HQ_map_lit", (0, -4, FLOOR_H + 0.92), (11, 5, 0.06), "screen")
for r in range(2):                                         # 2F: briefing
    for c in range(3):
        table(f"HQ_brief{r}{c}", -12 + c * 12, -6 + r * 8)
for i in range(3):                                         # 3F: comms
    server_row(f"HQ_srv{i}", -10 + i * 10, FLOOR_H * 0 + 6, n=4)
console("HQ_comms", 0, -10, tag="hq_comms")
box("HQ_pad", (0, 0, HQ_FLOORS[-1] + FLOOR_H + 0.45), (18, 18, 0.2), "asphalt")
cyl("HQ_pad_ring", (0, 0, HQ_FLOORS[-1] + FLOOR_H + 0.6), 7.0, 0.12, "warn_stripe", verts=24)
device("helipad", 0, 0, HQ_FLOORS[-1] + FLOOR_H + 0.6, 0.0, "hq")
for s in (-1, 1):
    box(f"HQ_mast{s}", (s * 20, 15, HQ_FLOORS[-1] + FLOOR_H + 6), (0.4, 0.4, 12), "metal")
    box(f"HQ_beacon{s}", (s * 20, 15, HQ_FLOORS[-1] + FLOOR_H + 12.4), (0.8, 0.8, 0.8),
        "signal_red")
cover(-14, -16.0)
cover(14, -16.0)
lamp_post("HQ_lp1", -30, -26)
lamp_post("HQ_lp2", 30, -26)

# ---------------------------------------------------------- power hall (west)
PX, PY = -150, 30
room("PowerHall", PX, PY, 40, 26, 9.0, "power_ochre", doors="E", floor="concrete",
     door_w=5.0, door_h=4.2)
for i in range(3):                 # turbines
    x = PX - 12 + i * 12
    cyl(f"Turbine{i}", (x, PY, 2.2), 2.4, 9.0, "steel_pale", verts=16,
        rot=(0, math.radians(90), 0))
    box(f"Turbine{i}_base", (x, PY, 0.5), (10.0, 5.0, 1.0), "concrete_d")
    box(f"Turbine{i}_led", (x + 4.6, PY - 2.6, 3.4), (0.5, 0.1, 0.5), "signal_green")
for i in range(4):
    box(f"Switchgear{i}", (PX - 14 + i * 9, PY + 10, 1.3), (6.0, 1.6, 2.6), "metal")
    box(f"Switchgear{i}_scr", (PX - 14 + i * 9, PY + 9.1, 1.9), (2.0, 0.1, 0.6), "screen")
console("Power_console", PX + 14, PY - 8, tag="power")
device("breaker", PX, PY + 10, 0.0, 0.0, "power")
strip_light("Pow_lt1", PX - 10, PY, 8.4, 12, color=(1.0, 0.88, 0.6))
strip_light("Pow_lt2", PX + 10, PY, 8.4, 12, color=(1.0, 0.88, 0.6))
for i in range(6):
    cyl(f"Pow_pipe{i}", (PX - 15 + i * 6, PY - 15, 6.5), 0.5, 30, "metal",
        rot=(math.radians(90), 0, 0))
cover(PX + 18.0, PY - 8)

# --------------------------------------------------- robotics yard (north-west)
# Where the outbreak started. Half the chassis are still on the line; the other
# half are on the floor with something green under them.
RX, RY = -150, 130
box("Rob_apron", (RX, RY, 0.03), (86, 70, 0.06), "asphalt")
room("RoboticsBay", RX, RY, 52, 34, 9.5, "robot_violet", doors="S", floor="clean_floor",
     door_w=7.0, door_h=5.0)
blast_door("Rob_door", RX, RY - 17, 6.0, 4.6, tag="robotics")
reader("Rob_reader", RX + 4.2, RY - 17.8, 1.4, tag="robotics")
box("Rob_line", (RX, RY + 4, 0.55), (44, 2.6, 1.1), "metal")   # assembly line
for i in range(7):
    chassis(f"Rob_ch{i}", RX - 20 + i * 6.6, RY + 4, broken=(i in (2, 5)))
for i in range(4):                 # gantry arms over the line
    box(f"Rob_gantry{i}", (RX - 16 + i * 11, RY + 4, 6.0), (0.6, 12.0, 0.6), "steel_pale")
    box(f"Rob_armhead{i}", (RX - 16 + i * 11, RY + 8.5, 5.0), (0.9, 0.9, 2.0), "metal")
for i in range(6):
    chassis(f"Rob_wreck{i}", RX - 18 + rng.uniform(0, 36), RY - 9 + rng.uniform(-2, 3),
            yaw=rng.uniform(0, 3.1), broken=True)
for i in range(4):
    strip_light(f"Rob_lt{i}", RX - 18 + i * 12, RY, 9.0, 10, color=(0.8, 0.85, 1.0),
                intensity=1.4)
console("Rob_console", RX + 20, RY + 12, tag="robotics")
scatter_decals("Rob_goo", RX, RY - 6, 22, 8, "parasite", rng, wmin=1.6, wmax=4.0)
device("outbreak_origin", RX, RY, 0.0, 0.0, "robotics")
cover(RX - 22.0, RY + 4)
cover(RX + 22.0, RY + 4)
lamp_post("Rob_lp1", RX + 36, RY - 24)

# --------------------------------------------- research complex (north)
LX, LY = 0, 170
box("Lab_plaza", (LX, LY, 0.04), (150, 96, 0.08), "concrete")
# airlock spine: you enter through decontamination, not through a door
room("Airlock_A", LX, LY - 40, 14, 10, 3.6, "lab_white", doors="NS", floor="clean_floor")
blast_door("Airlock_out", LX, LY - 45, 4.2, 3.2, tag="lab_outer")
blast_door("Airlock_in", LX, LY - 35, 4.2, 3.2, tag="lab_inner")
reader("Airlock_reader", LX + 4.6, LY - 45.8, 1.4, tag="lab_outer")
strip_light("Airlock_lt", LX, LY - 40, 3.3, 6, color=(0.75, 0.9, 1.0), intensity=2.0)
box("Airlock_stripe", (LX, LY - 40, 0.1), (12, 8, 0.04), "warn_stripe")

# clean labs, glass-walled so you can see the thing before it sees you
for i, sx in enumerate((-42, 42)):
    n = f"CleanLab{i}"
    room(n, LX + sx, LY - 8, 34, 26, 4.2, "lab_white", doors="E" if sx < 0 else "W",
         floor="clean_floor")
    window_band(n, LX + sx, LY - 8, 34, 26, 4.2, "glass_lab", sides="NS", z=1.4, tall=2.2)
    strip_light(f"{n}_lt1", LX + sx - 8, LY - 8, 3.9, 12, color=(0.85, 0.95, 1.0), intensity=2.0)
    strip_light(f"{n}_lt2", LX + sx + 8, LY - 8, 3.9, 12, color=(0.85, 0.95, 1.0), intensity=2.0)
    for r in range(3):
        box(f"{n}_bench{r}", (LX + sx, LY - 17 + r * 8, 0.5), (26, 1.4, 1.0), "steel_pale")
        box(f"{n}_benchtop{r}", (LX + sx, LY - 17 + r * 8, 1.05), (26.6, 1.8, 0.1), "lab_trim")
        for c in range(4):
            box(f"{n}_glass{r}{c}", (LX + sx - 9 + c * 6, LY - 17 + r * 8, 1.35),
                (0.5, 0.5, 0.5), "glass_lab")
    console(f"{n}_console", LX + sx - 14, LY + 2, tag=f"lab{i}")
    device("sample", LX + sx, LY - 9, 1.2, 0.0, f"lab{i}")
    cover(LX + sx, LY - 17.8)

# server hall — the loudest room on the map, and the darkest
room("ServerHall", LX, LY + 6, 40, 30, 5.0, "lab_white", doors="S", floor="clean_floor")
for i in range(4):
    server_row(f"Srv{i}", LX, LY - 4 + i * 8, n=10)
    strip_light(f"Srv_lt{i}", LX, LY - 4 + i * 8, 4.7, 16, color=(0.5, 0.7, 1.0),
                intensity=1.1, rng_=14)
console("Srv_console", LX - 16, LY + 17, tag="servers")
device("mainframe", LX, LY + 17, 0.0, 0.0, "servers")
cover(LX - 18.0, LY + 6)

# containment hall: a two-storey drum with catwalks, the finale room
room("Containment", LX, LY + 46, 46, 34, 13.0, "lab_trim", doors="S",
     floor="clean_floor", door_w=6.0, door_h=4.6)
blast_door("Cont_door", LX, LY + 29, 6.2, 4.6, tag="containment")
reader("Cont_reader", LX + 4.8, LY + 28.2, 1.4, tag="containment")
cyl("Cont_drum", (LX, LY + 46, 5.0), 6.0, 10.0, "steel_pale", verts=24)
cyl("Cont_core", (LX, LY + 46, 5.0), 3.4, 9.6, "parasite", verts=16)
for i in range(6):
    a = i / 6 * math.tau
    box(f"Cont_rib{i}", (LX + math.cos(a) * 7.2, LY + 46 + math.sin(a) * 7.2, 5.0),
        (0.6, 0.6, 10.0), "metal")
catwalk("Cont_cat_w", LX - 21, LY + 46, LX - 7, LY + 46, 6.4, "metal", width=2.6)
catwalk("Cont_cat_e", LX + 7, LY + 46, LX + 21, LY + 46, 6.4, "metal", width=2.6)
catwalk("Cont_cat_n", LX, LY + 60, LX, LY + 53, 6.4, "metal", width=2.6)
stairs("Cont_stair", LX - 19, LY + 34, 3.0, 8.0, 6.4, 13, "metal", axis="y")
for i in range(3):
    console(f"Cont_console{i}", LX - 14 + i * 14, LY + 33, tag="containment")
strip_light("Cont_lt1", LX - 14, LY + 46, 12.4, 10, color=(0.7, 1.0, 0.8), intensity=1.6)
strip_light("Cont_lt2", LX + 14, LY + 46, 12.4, 10, color=(0.7, 1.0, 0.8), intensity=1.6)
device("reactor", LX, LY + 46, 0.0, 0.0, "containment")
cover(LX - 8.0, LY + 34)
cover(LX + 8.0, LY + 34)
lamp_post("Lab_lp1", LX - 60, LY - 30)
lamp_post("Lab_lp2", LX + 60, LY - 30)

# --------------------------------------------------------------- perimeter
for s in (-1, 1):
    twall_run(f"Peri_W{s}", s * (HALF - 4), -HALF + 4, s * (HALF - 4), HALF - 4,
              key="concrete_d", h=4.2)
twall_run("Peri_N", -HALF + 4, HALF - 4, HALF - 4, HALF - 4, key="concrete_d", h=4.2)
for cx, cy in ((-HALF + 14, -HALF + 14), (HALF - 14, -HALF + 14),
               (-HALF + 14, HALF - 14), (HALF - 14, HALF - 14)):
    for i in range(4):
        box(f"Tower{cx:.0f}{cy:.0f}_leg{i}",
            (cx + (4 if i % 2 else -4), cy + (4 if i > 1 else -4), 6.0),
            (0.7, 0.7, 12.0), "metal")
    box(f"Tower{cx:.0f}{cy:.0f}_deck", (cx, cy, 12.2), (11, 11, 0.4), "metal")
    for side, dx, dy, w, d in (("N", 0, 5.2, 11, 0.4), ("S", 0, -5.2, 11, 0.4),
                               ("E", 5.2, 0, 0.4, 11), ("W", -5.2, 0, 0.4, 11)):
        box(f"Tower{cx:.0f}{cy:.0f}_par{side}", (cx + dx, cy + dy, 12.9),
            (w, d, 1.0), "concrete_d")
    box(f"Tower{cx:.0f}{cy:.0f}_roof", (cx, cy, 15.6), (12, 12, 0.3), "metal")
    floodlight(f"Tower{cx:.0f}{cy:.0f}_fl", cx, cy, h=15.0)
    cover(cx, cy - 4.6, 12.4)
    ramp_to(f"Tower{cx:.0f}{cy:.0f}_ramp", cx, cy - 8, 14.0, 3.0, 12.2, "metal",
            axis="y", side=-1)

# ground grime, last so it lies on top of everything
for name, x, y, spread, n, key in (
        ("Grime_mot", MX, MY - 10, 34, 10, "oil"),
        ("Grime_whs", WX - 30, WY, 26, 8, "tracks"),
        ("Grime_rob", RX, RY, 30, 6, "scorch"),
        ("Grime_gate", 0, GY + 6, 30, 6, "tracks")):
    scatter_decals(name, x, y, spread, n, key, rng)


# ================================================================= export
# Chunk by district so Unity culls sensibly, with anything map-spanning (roads,
# perimeter walls) pooled separately — an object longer than a chunk drags that
# chunk's bounds across the level and defeats the culling it was meant to help.
CHUNK = 160.0
chunks, shell = {}, []
for ob, key in objs:
    ob.data.materials.append(g1kit.MATS[key])
    dim = max(ob.dimensions.x, ob.dimensions.y)
    if dim > CHUNK * 0.6:
        shell.append(ob)
    else:
        k = (int(math.floor(ob.location.x / CHUNK)),
             int(math.floor(ob.location.y / CHUNK)))
        chunks.setdefault(k, []).append(ob)

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

    # World-scale UVs.
    #
    # A generated box carries the default UV map, which spans 0..1 across each
    # face however big the face is — so a 400 m road and a 2 m crate get the
    # same one tile of texture, and the road's concrete is stretched two hundred
    # times. Cube-projecting after the join, with the projection cube fixed in
    # metres, makes one UV unit mean one metre everywhere on the map, so every
    # surface takes the same texture at the same real-world scale.
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=2.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    pieces.append(piece)


for (ix, iy), members in sorted(chunks.items()):
    _join(members, f"Cradle_{ix}_{iy}")
if shell:
    _join(shell, "Cradle_Shell")

bpy.ops.object.light_add(type="SUN", location=(120, -120, 240))
sun = bpy.context.active_object
sun.data.energy = 3.2
sun.rotation_euler = (math.radians(58), 0, math.radians(30))
bpy.ops.object.camera_add(location=(0, -12, 700))
cam = bpy.context.active_object
cam.data.type = "ORTHO"
cam.data.ortho_scale = 540
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
sc.render.filepath = f"{BASE}/renders/cradle_top.png"
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/cradle_station.blend")

os.makedirs(UNITY, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for piece in pieces:
    piece.select_set(True)
bpy.context.view_layer.objects.active = pieces[0]
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/CradleStation.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", use_space_transform=True,
    bake_space_transform=True, object_types={"MESH"}, mesh_smooth_type="FACE")

manifest = {"half": HALF, "rooms": ROOMS, "lights": LIGHTS,
            "cover": COVER, "devices": DEVICES}
with open(f"{UNITY}/CradleStation.manifest.json", "w") as fh:
    json.dump(manifest, fh, indent=1)

tris = sum((len(p.vertices) - 2) for ob in pieces for p in ob.data.polygons)
print(f"CRADLE STATION DONE — {int(2 * HALF)}x{int(2 * HALF)}m, ~{tris} tris, "
      f"{len(pieces)} chunks, {len(ROOMS)} interiors, {len(LIGHTS)} lights, "
      f"{len(COVER)} cover points, {len(DEVICES)} devices")
