"""Generate the terrain the two bases sit on.

Both maps stand on a perfectly flat slab, which is the single loudest thing
still saying "this is a diagram". A real installation is levelled — a graded
pad is what a bulldozer is for — but the land it was cut into is not, and the
horizon in every direction being a dead straight line is what gives it away.

So this does exactly what a site engineer does: it leaves the built ground
flat and gives everything outside the wire its shape back.

    inside the Sprawl footprint          flat
    inside Cradle Station's footprint    flat
    along the road that links them       flat, a graded cut through the hills
    everywhere else                      rolling ground, rising with distance

The flat mask is not decoration — it is a correctness requirement. Every
building, wall, vehicle spawn and cover point on both maps was authored at
y = 0. Terrain that rose under a barracks block would leave it buried to the
windows, and nothing in the build would report it.

The mesh sits at y = -0.3 where it is flat, which is inside the maps' own
ground slabs (whose top face is y = 0), so under the bases it is simply not
visible. It only ever emerges past the perimeter.

Run:  blender --background --python build_terrain.py -- <project_dir> <unity_env_models_dir>
"""
import bpy
import bmesh
import json
import math
import os
import sys

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BASE = args[0] if args else "."
UNITY = args[1] if len(args) > 1 else "."

# The world in Unity coordinates. The Sprawl is 800x800 centred on the origin;
# Cradle Station is 480x480 centred on x = 1100. Everything here is authored in
# Unity space and converted on export, because unlike the map generators this
# has to agree with two maps at once and one axis convention is enough.
X0, X1 = -760.0, 1860.0
Z0, Z1 = -760.0, 760.0
STEP = 9.0                  # metres between vertices
# Sixteen metres was fine for gentle ground. At the new gradient a
# hillside crossed a whole vertex in one step and read as origami.

FLAT_Y = -0.3               # baseline: hidden inside the maps' ground slabs

# The two site footprints. Inside these the ground is *graded* rather than flat:
# it still moves, just gently, because a base cut into a hillside is levelled
# building by building and not one plate at a time. Outside them the land is
# whatever it was.
SPRAWL = (0.0, 0.0, 430.0, 430.0)       # cx, cz, half-x, half-z
CRADLE = (1100.0, 0.0, 270.0, 270.0)
ROAD_HALF = 26.0            # the link road corridor stays graded
BLEND = 210.0               # how far the ground takes to become itself again

# How much the ground is allowed to move inside the wire. Everything the map
# generators place — walls, crates, lamp posts, cover — sits at y = 0, so this
# is a budget rather than a taste: any more and props start floating.
YARD_AMPLITUDE = 4.2
PAD = 13.0                  # flat apron kept around every building
ROAD_PAD = 9.0              # and either side of every road


def hash2(ix, iz, seed=0):
    """Deterministic value noise. A build has to be repeatable — a map whose
    hills move between runs makes every screenshot and every placement probe
    unreproducible."""
    h = ix * 374761393 + iz * 668265263 + seed * 1442695040888963407
    h = (h ^ (h >> 13)) * 1274126177
    h ^= h >> 16
    return ((h & 0xFFFFFFFF) / 0xFFFFFFFF) * 2.0 - 1.0


def smooth(t):
    return t * t * (3.0 - 2.0 * t)


def value_noise(x, z, cell, seed=0):
    fx, fz = x / cell, z / cell
    ix, iz = math.floor(fx), math.floor(fz)
    tx, tz = smooth(fx - ix), smooth(fz - iz)
    a = hash2(ix, iz, seed)
    b = hash2(ix + 1, iz, seed)
    c = hash2(ix, iz + 1, seed)
    d = hash2(ix + 1, iz + 1, seed)
    return (a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + d * tx) * tz


def ridged(x, z):
    """Four octaves, the largest ridged so the ground gets a spine rather than
    an even lumpiness. Even noise reads as a crumpled sheet; one dominant
    direction reads as land.

    The first pass topped out at twenty metres over a six-hundred-metre
    wavelength, which is a gradient of about one in thirty — technically not
    flat and visually indistinguishable from flat. Height alone was never the
    problem: a hill reads as a hill because of how fast it *changes*, so the
    fix is a shorter wavelength on the big octave as much as a taller one.
    Sixty-five metres over three hundred is roughly one in five, which is
    steep enough to hide a truck behind and still drivable."""
    n = 0.0
    n += (1.0 - abs(value_noise(x, z, 300.0, 1))) * 62.0
    n += value_noise(x, z, 150.0, 2) * 26.0
    n += value_noise(x, z, 62.0, 3) * 9.0
    n += value_noise(x, z, 26.0, 4) * 3.0
    return n - 34.0


def box_mask(x, z, box):
    """0 inside the rectangle, 1 beyond the blend band, smooth between."""
    cx, cz, hx, hz = box
    dx = abs(x - cx) - hx
    dz = abs(z - cz) - hz
    d = max(dx, dz)
    if d <= 0.0:
        return 0.0
    return smooth(min(1.0, d / BLEND))


def road_mask(x, z):
    """The link road runs east along z = 0 from the Sprawl to x = 1100, then
    north to Cradle's gatehouse. Keep a corridor graded either side of it or
    the road would climb a hill and leave the tarmac in mid air."""
    m = 1.0
    # The link road starts at the Sprawl's east wall, not at the west edge of
    # the world. Running the corridor the whole width laid a 26 m flat strip
    # across four hundred metres of hillside that no road has ever been on.
    if 360.0 <= x <= 1110.0:
        m = min(m, smooth(min(1.0, max(0.0, abs(z) - ROAD_HALF) / BLEND)))
    if 0.0 <= z <= 260.0:
        m = min(m, smooth(min(1.0, max(0.0, abs(x - 1100.0) - ROAD_HALF) / BLEND)))
    return m


def load_pads():
    """Every building on both maps, as a flat rectangle in world coordinates.

    Read from the manifests rather than typed here, for the same reason the
    lights and the loot are: the generator that put the wall up is the only
    thing that knows where it is. Typing fifty rectangles on this side would be
    wrong the first time a district moved, and the failure mode is a barracks
    block buried to its windows with nothing reporting it.
    """
    pads = []
    for rel, off in (("HugeMap.manifest.json", 0.0),
                     ("CradleStation.manifest.json", 1100.0)):
        path = os.path.join(UNITY, rel)
        if not os.path.exists(path):
            print("  ! no manifest at", path, "- yard will stay flat there")
            continue
        data = json.load(open(path))
        for r in data.get("rooms", []):
            pads.append((r["x"] + off, r["z"],
                         r["w"] / 2 + PAD, r["d"] / 2 + PAD))
    return pads


PADS = load_pads()


def yard_mask(x, z):
    """0 on a building pad or a road, 1 out in the open ground between them."""
    m = 1.0
    for pad in PADS:
        m = min(m, box_mask_pad(x, z, pad, 42.0))
        if m <= 0.0:
            return 0.0
    # the Sprawl's cross roads and ring roads, and Cradle's grid
    for cx, half in ((0.0, ROAD_PAD),):
        m = min(m, smooth(min(1.0, max(0.0, abs(x - cx) - half) / 40.0)))
        m = min(m, smooth(min(1.0, max(0.0, abs(z - cx) - half) / 40.0)))
    return m


def box_mask_pad(x, z, pad, blend):
    cx, cz, hx, hz = pad
    d = max(abs(x - cx) - hx, abs(z - cz) - hz)
    if d <= 0.0:
        return 0.0
    return smooth(min(1.0, d / blend))


def yard(x, z):
    """The gentle roll of the open ground inside a base. Two octaves only —
    this is a graded yard settling over the years, not a landscape."""
    n = value_noise(x, z, 130.0, 7) * 0.7 + value_noise(x, z, 52.0, 8) * 0.3
    return n * YARD_AMPLITUDE


def height(x, z):
    m = min(box_mask(x, z, SPRAWL), box_mask(x, z, CRADLE), road_mask(x, z))
    if m <= 0.0:
        # inside the wire: dips and rises between the buildings, nothing under
        # them. This is what the yard was missing — eight hundred metres of
        # billiard table between districts you spend the whole game crossing.
        return FLAT_Y + yard(x, z) * yard_mask(x, z)
    h = ridged(x, z)
    # never dig below the slab: a hollow beside the perimeter reads as a hole in
    # the world, and the player can fall into it
    inner = yard(x, z) * yard_mask(x, z)
    return FLAT_Y + max(0.0, h) * m + inner * (1.0 - m)


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

nx = int((X1 - X0) / STEP) + 1
nz = int((Z1 - Z0) / STEP) + 1

bm = bmesh.new()
verts = []
for j in range(nz):
    row = []
    z = Z0 + j * STEP
    for i in range(nx):
        x = X0 + i * STEP
        # Blender +Y exports to Unity -Z, so the Unity z authored above becomes
        # Blender -z here. Doing the flip at construction keeps every mask and
        # footprint above readable in the coordinates the rest of the project
        # talks in.
        row.append(bm.verts.new((x, -z, height(x, z))))
    verts.append(row)
bm.verts.ensure_lookup_table()

for j in range(nz - 1):
    for i in range(nx - 1):
        bm.faces.new((verts[j][i], verts[j][i + 1],
                      verts[j + 1][i + 1], verts[j + 1][i]))

mesh = bpy.data.meshes.new("TerrainMesh")
bm.to_mesh(mesh)
bm.free()
ob = bpy.data.objects.new("Terrain", mesh)
bpy.context.collection.objects.link(ob)
bpy.context.view_layer.objects.active = ob
ob.select_set(True)

# world-scale UVs, same rule as the maps: one UV unit is one metre
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.cube_project(cube_size=2.0, correct_aspect=True, scale_to_bounds=False)
bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.shade_flat()

m = bpy.data.materials.new("terrain_ground")
m.use_nodes = True
b = m.node_tree.nodes["Principled BSDF"]
b.inputs["Base Color"].default_value = (0.145, 0.135, 0.105, 1)
b.inputs["Roughness"].default_value = 1.0
m.diffuse_color = (0.145, 0.135, 0.105, 1)
mesh.materials.append(m)

os.makedirs(f"{BASE}/blender", exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/terrain.blend")

os.makedirs(UNITY, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
ob.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/Terrain.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", use_space_transform=True,
    bake_space_transform=True, object_types={"MESH"}, mesh_smooth_type="FACE")

hi = max(v.co.z for v in mesh.vertices)
print(f"TERRAIN DONE — {nx}x{nz} grid at {STEP:.0f}m, "
      f"{len(mesh.polygons)} quads, highest point {hi:.1f}m")
