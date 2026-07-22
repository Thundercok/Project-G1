"""Build a large procedural facility map for Project G1: the Corvus Deep Research
Annex, Sub-Level C — a single-level, huge, explorable environment with a central
atrium, a labs wing, a cylindrical reactor hall, a records vault, and a motor-pool
gate. Structural columns, pipe runs, ramps to a mezzanine and a reactor catwalk.

Design rules that keep it playable once imported into Unity:
  * ONE ground floor at Z=0 (Blender Z is up; export flips it to Unity Y-up).
  * Walls are tall boxes; NO solid walkable ceiling, so Unity's NavMesh bakes
    cleanly over the floor + ramps and carves the walls as obstacles.
  * Ramps are < 35 deg so NavMesh agents can climb to the mezzanine/catwalk.
  * Everything is joined into a single "CorvusFacility" mesh and exported with the
    exact same FBX flags as the weapon/prop scripts (1 Blender metre = 1 Unity m).

Run:
  blender --background --python build_map.py -- <project_dir> <unity_env_models_dir>
e.g.
  blender --background --python Tools/blender/build_map.py -- . Assets/G1/Models/Environment
"""
import bpy
import math
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
BASE = args[0] if len(args) > 0 else "."
UNITY = args[1] if len(args) > 1 else "Assets/G1/Models/Environment"

parts = []


def M(name, color, rough=0.9, metal=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*color, 1)
    return m


def _finish(ob, mt):
    ob.data.materials.append(mt)
    bpy.ops.object.shade_flat()
    parts.append(ob)
    return ob


def box(name, loc, dims, mt, rot=(0, 0, 0)):
    """Axis-aligned (or Z-rotated) cuboid. loc = centre, dims = full size (m)."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector(dims)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(ob, mt)


def cyl(name, p1, p2, r, mt, verts=12):
    """Cylinder spanning p1->p2 (columns, pipes, silo shell segments)."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=max(d.length, 1e-4), location=(p1 + p2) / 2)
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = d.to_track_quat("Z", "Y")
    return _finish(ob, mt)


def wall(name, x1, y1, x2, y2, h, mt, t=0.4, base=0.0):
    """Wall segment between ground points (x1,y1)-(x2,y2), height h, thickness t."""
    dx, dy = x2 - x1, y2 - y1
    length = math.hypot(dx, dy)
    ang = math.atan2(dy, dx)
    box(name, ((x1 + x2) / 2, (y1 + y2) / 2, base + h / 2),
        (length, t, h), mt, rot=(0, 0, ang))


def ramp(name, x, y, along, width, rise, mt, axis="y"):
    """A sloped slab climbing `rise` over run `along`, centred at (x,y)."""
    run = math.hypot(along, rise)
    pitch = math.atan2(rise, along)
    if axis == "y":
        box(name, (x, y, rise / 2), (width, run, 0.25), mt, rot=(pitch, 0, 0))
    else:
        box(name, (x, y, rise / 2), (run, width, 0.25), mt, rot=(0, -pitch, 0))


# ---------------------------------------------------------------- clean slate
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

concrete = M("map_concrete", (0.62, 0.63, 0.66), rough=0.95)
floormat = M("map_floor",    (0.42, 0.44, 0.47), rough=0.8, metal=0.1)
steel    = M("map_steel",    (0.55, 0.57, 0.60), rough=0.4, metal=0.9)
hazard   = M("map_hazard",   (0.85, 0.55, 0.08), rough=0.7)
dark     = M("map_dark",     (0.14, 0.15, 0.17), rough=0.9)
green    = M("map_green",    (0.25, 0.45, 0.30), rough=0.85)

WALL_H = 6.0          # interior wall height
PERIM_H = 8.0         # perimeter height
MINX, MAXX = -60.0, 60.0
MINY, MAXY = -52.0, 52.0

# ---------------------------------------------------------------- ground floor
box("Floor", (0, 0, -0.25), (MAXX - MINX, MAXY - MINY, 0.5), floormat)

# ---------------------------------------------------------------- perimeter
# Solid perimeter, but a wide vehicle gate on the east wall (the exit to Level 2).
wall("Perim_S", MINX, MINY, MAXX, MINY, PERIM_H, concrete, t=0.6)
wall("Perim_N", MINX, MAXY, MAXX, MAXY, PERIM_H, concrete, t=0.6)
wall("Perim_W", MINX, MINY, MINX, MAXY, PERIM_H, concrete, t=0.6)
# East wall split around a 10 m gate at y[-5,5]
wall("Perim_E1", MAXX, MINY, MAXX, -5, PERIM_H, concrete, t=0.6)
wall("Perim_E2", MAXX, 5, MAXX, MAXY, PERIM_H, concrete, t=0.6)
box("GateLintel", (MAXX, 0, PERIM_H - 1.0), (0.6, 10, 2.0), hazard)

# ---------------------------------------------------------------- ATRIUM (core)
# Open central hub x[-16,16] y[-16,16]. Four big columns + a north mezzanine.
for sx in (-1, 1):
    for sy in (-1, 1):
        cyl(f"AtriumCol_{sx}_{sy}", (sx * 12, sy * 12, 0), (sx * 12, sy * 12, WALL_H + 2),
            0.7, steel, verts=16)
# Roof trusses across the atrium for silhouette (thin, non-walkable).
for i in range(-14, 15, 7):
    cyl(f"Truss_{i}", (i, -16, WALL_H + 2), (i, 16, WALL_H + 2), 0.15, steel, verts=6)

# Mezzanine platform on the atrium's north side + two ramps up to it.
box("Mezzanine", (0, 12, 2.6), (30, 8, 0.4), steel)
box("MezzRail", (0, 8.2, 3.4), (30, 0.2, 1.0), steel)
ramp("MezzRampW", -12, 4, along=8, width=4, rise=2.6, mt=steel, axis="y")
ramp("MezzRampE", 12, 4, along=8, width=4, rise=2.6, mt=steel, axis="y")

# Atrium boundary walls with doorway gaps to each wing (N labs, S reactor, W, E).
def gapped(nx, prefix, x1, y1, x2, y2, gap_at, gap_w=4.0):
    """Wall with a doorway gap centred at gap_at (a fraction 0..1 along it)."""
    L = math.hypot(x2 - x1, y2 - y1)
    ux, uy = (x2 - x1) / L, (y2 - y1) / L
    g0 = gap_at * L - gap_w / 2
    g1 = gap_at * L + gap_w / 2
    wall(f"{prefix}_a", x1, y1, x1 + ux * g0, y1 + uy * g0, WALL_H, concrete)
    wall(f"{prefix}_b", x1 + ux * g1, y1 + uy * g1, x2, y2, WALL_H, concrete)

gapped(0, "AtriumN", -16, 16, 16, 16, 0.5)   # to Labs
gapped(0, "AtriumS", -16, -16, 16, -16, 0.5)  # to Reactor
gapped(0, "AtriumW", -16, -16, -16, 16, 0.5)  # to Records
gapped(0, "AtriumE", 16, -16, 16, 16, 0.5)    # to Motor pool

# ---------------------------------------------------------------- LABS (north)
# A run of lab rooms y[16,48] either side of a central corridor.
wall("Labs_N", -44, 48, 44, 48, WALL_H, concrete)
wall("Labs_W", -44, 16, -44, 48, WALL_H, concrete)
wall("Labs_E", 44, 16, 44, 48, WALL_H, concrete)
# central corridor walls with doorways into each room
for i, x in enumerate((-30, -15, 15, 30)):
    gapped(0, f"LabDiv_{i}", x, 16, x, 48, 0.35)
# a few benches per lab (low boxes)
for x in (-37, -22, 22, 37):
    box(f"LabBench_{x}", (x, 40, 0.5), (5, 1.2, 1.0), steel)
    box(f"LabBench2_{x}", (x, 24, 0.5), (5, 1.2, 1.0), steel)
# skylight columns
for x in (-30, 0, 30):
    cyl(f"LabCol_{x}", (x, 32, 0), (x, 32, WALL_H), 0.5, concrete, verts=10)

# ---------------------------------------------------------------- REACTOR (south)
# Cylindrical silo in a hall y[-48,-16], ringed by a raised catwalk + ramps.
wall("Reactor_S", -24, -48, 24, -48, WALL_H, concrete)
wall("Reactor_W", -24, -48, -24, -16, WALL_H, concrete)
wall("Reactor_E", 24, -48, 24, -16, WALL_H, concrete)
# silo shell (ring of tall segments) + core
CX, CY, R = 0, -32, 8
seg_n = 16
for i in range(seg_n):
    a0 = 2 * math.pi * i / seg_n
    a1 = 2 * math.pi * (i + 1) / seg_n
    p0 = (CX + R * math.cos(a0), CY + R * math.sin(a0))
    p1 = (CX + R * math.cos(a1), CY + R * math.sin(a1))
    wall(f"Silo_{i}", p0[0], p0[1], p1[0], p1[1], WALL_H + 1.5, steel, t=0.5)
cyl("SiloCore", (CX, CY, 0), (CX, CY, WALL_H + 3), 3.0, hazard, verts=20)
# catwalk ring (square approximation) at height 2.5 with ramp up from atrium side
for (n, x1, y1, x2, y2) in (("cwN", -12, -20, 12, -20), ("cwS", -12, -44, 12, -44),
                            ("cwW", -12, -44, -12, -20), ("cwE", 12, -44, 12, -20)):
    box(n, ((x1 + x2) / 2, (y1 + y2) / 2, 2.5),
        (abs(x2 - x1) + 1.5 if x1 != x2 else 1.5,
         abs(y2 - y1) + 1.5 if y1 != y2 else 1.5, 0.3), steel)
ramp("ReactorRamp", 0, -18, along=6, width=4, rise=2.5, mt=steel, axis="y")
# pipe runs along the hall walls
for x in (-22, 22):
    cyl(f"Pipe_{x}", (x, -47, 4.5), (x, -18, 4.5), 0.3, steel, verts=10)

# ---------------------------------------------------------------- RECORDS (west)
# Vault of shelving rows x[-60,-16] y[-16,16].
wall("Rec_N", -60, 16, -16, 16, WALL_H, concrete)
wall("Rec_S", -60, -16, -16, -16, WALL_H, concrete)
for k, x in enumerate(range(-52, -20, 6)):
    box(f"Shelf_{k}", (x, 0, 1.2), (1.0, 22, 2.4), dark)
box("VaultDoor", (-16, 0, WALL_H / 2), (0.5, 4, WALL_H), steel)

# ---------------------------------------------------------------- MOTOR POOL (east)
# Open bay x[16,60] y[-16,16] leading to the perimeter gate. A couple of trucks.
wall("Motor_N", 16, 16, 60, 16, WALL_H, concrete)
wall("Motor_S", 16, -16, 60, -16, WALL_H, concrete)
for k, y in enumerate((-9, 9)):
    box(f"Truck_body_{k}", (34, y, 1.4), (10, 4, 2.4), green)
    box(f"Truck_cab_{k}", (40, y, 1.2), (3, 3.6, 2.0), dark)
# hazard chevrons framing the exit gate
for k, y in enumerate((-4.5, 4.5)):
    box(f"GateChevron_{k}", (58, y, 1.5), (0.4, 1.5, 3.0), hazard)

# ---------------------------------------------------------------- studio + export
sun = M("map_sun", (1, 1, 1))
bpy.ops.object.light_add(type="SUN", location=(0, 0, 40))
bpy.context.active_object.data.energy = 3.0

bpy.ops.object.select_all(action="DESELECT")
for ob in parts:
    ob.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
facility = bpy.context.active_object
facility.name = "CorvusFacility"
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/corvus_facility.blend")

bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/CorvusFacility.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", use_space_transform=True,
    bake_space_transform=True, object_types={"MESH"}, mesh_smooth_type="FACE")
print("CORVUS FACILITY MAP DONE — %d parts joined" % len(parts))
