"""Generate a HUGE battlefield map for Project G1 — the 'Corvus Sprawl': a
~220x220 m military-industrial complex with distinct districts for a two-faction
battle. Low-poly, flat-shaded, exported as one Unity-ready FBX with a walkable
ground plane and building obstacles.

Districts:
  - Central Command Tower (map center)
  - Allied Base           (west)   — barracks, sandbags, helipad
  - Lab Complex           (north)  — connected blocks + dome
  - Hangar / Motor Pool   (east)   — big open shed + vehicles
  - Alien Breach Ruins    (south)  — broken walls, glowing pods, craters
  - Perimeter wall + 4 corner watchtowers, cross roads, catwalks, cover

Run:  blender --background --python build_huge_map.py -- <project_dir> <unity_models_dir>
"""
import bpy
import math
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BASE = args[0] if args else "."
UNITY = args[1] if len(args) > 1 else "."

objs = []  # (object, material_key)


# ------------------------------------------------------------- materials
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


# ------------------------------------------------------------- primitives
def box(name, loc, dims, key, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector((dims[0] / 2, dims[1] / 2, dims[2] / 2))
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_flat()
    objs.append((ob, key))
    return ob


def cyl(name, loc, radius, height, key, verts=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius,
                                        depth=height, location=loc)
    ob = bpy.context.active_object
    ob.name = name
    bpy.ops.object.shade_flat()
    objs.append((ob, key))
    return ob


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


# ------------------------------------------------------------- build
clear()
MATS.update({
    "ground": M("map_ground", (0.16, 0.17, 0.18), rough=1.0),
    "road": M("map_road", (0.10, 0.10, 0.11), rough=1.0),
    "concrete": M("map_concrete", (0.42, 0.43, 0.45)),
    "metal": M("map_metal", (0.32, 0.34, 0.37), rough=0.5, metal=0.7),
    "hazard": M("map_hazard", (0.80, 0.42, 0.06)),
    "allied": M("map_allied", (0.20, 0.34, 0.55)),
    "lab": M("map_lab", (0.72, 0.75, 0.78)),
    "alien": M("map_alien", (0.10, 0.55, 0.55), emit=(0.06, 0.5, 0.5), estr=1.2),
    "wood": M("map_wood", (0.38, 0.26, 0.14)),
    "glass": M("map_glass", (0.15, 0.4, 0.5), rough=0.2),
})

HALF = 110.0

# ground + roads
box("Ground", (0, 0, -0.25), (2 * HALF, 2 * HALF, 0.5), "ground")
box("Road_NS", (0, 0, 0.02), (10, 2 * HALF, 0.04), "road")
box("Road_EW", (0, 0, 0.02), (2 * HALF, 10, 0.04), "road")
box("Plaza", (0, 0, 0.03), (36, 36, 0.05), "road")

# perimeter wall (four runs, gap for south gate) + corner towers
wall_h, wall_t = 7.0, 1.2
box("Wall_N", (0, HALF - 1, wall_h / 2), (2 * HALF, wall_t, wall_h), "concrete")
box("Wall_E", (HALF - 1, 0, wall_h / 2), (wall_t, 2 * HALF, wall_h), "concrete")
box("Wall_W", (-(HALF - 1), 0, wall_h / 2), (wall_t, 2 * HALF, wall_h), "concrete")
box("Wall_S_L", (-(HALF / 2 + 6), -(HALF - 1), wall_h / 2),
    (HALF - 12, wall_t, wall_h), "concrete")
box("Wall_S_R", (HALF / 2 + 6, -(HALF - 1), wall_h / 2),
    (HALF - 12, wall_t, wall_h), "concrete")
for sx in (-1, 1):
    for sy in (-1, 1):
        cx, cy = sx * (HALF - 3), sy * (HALF - 3)
        box(f"Tower_{sx}_{sy}", (cx, cy, 6), (7, 7, 12), "concrete")
        box(f"TowerTop_{sx}_{sy}", (cx, cy, 12.6), (8.4, 8.4, 1.2), "metal")
        box(f"TowerRail_{sx}_{sy}", (cx, cy, 13.6), (8.4, 8.4, 1.0), "hazard")
# gate posts + hazard beam
box("GatePost_L", (-6, -(HALF - 1), 4), (2, 2, 8), "hazard")
box("GatePost_R", (6, -(HALF - 1), 4), (2, 2, 8), "hazard")
box("GateBeam", (0, -(HALF - 1), 8.5), (14, 1.4, 1.2), "hazard")

# --- CENTRAL COMMAND TOWER
box("CmdBase", (0, 0, 3), (18, 18, 6), "concrete")
box("CmdMid", (0, 0, 9), (14, 14, 6), "metal")
box("CmdTop", (0, 0, 15), (10, 10, 6), "concrete")
box("CmdAntenna", (0, 0, 21), (1, 1, 6), "metal")
for a in range(4):
    ang = a * math.pi / 2
    box(f"CmdWin_{a}", (math.cos(ang) * 7.05, math.sin(ang) * 7.05, 9),
        (0.2 if a % 2 == 0 else 8, 8 if a % 2 == 0 else 0.2, 3), "glass")

# --- ALLIED BASE (west)
bx = -70
box("BarracksA", (bx, 14, 3.5), (22, 12, 7), "allied")
box("BarracksB", (bx, -14, 3.5), (22, 12, 7), "allied")
box("BarracksRoofA", (bx, 14, 7.3), (23, 13, 0.6), "metal")
box("BarracksRoofB", (bx, -14, 7.3), (23, 13, 0.6), "metal")
box("Helipad", (bx - 2, 0, 0.08), (16, 16, 0.16), "concrete")
cyl("HelipadRing", (bx - 2, 0, 0.1), 6.5, 0.05, "hazard", verts=24)
# sandbag barriers (allied front line facing center)
for i in range(6):
    box(f"Sandbag_{i}", (bx + 16, -12 + i * 5, 0.6),
        (1.2, 3.2, 1.2), "wood", rot=(0, 0, 0.15 * i))
box("AlliedFlag", (bx - 10, 0, 6), (0.3, 0.3, 12), "metal")
box("AlliedBanner", (bx - 8.5, 0, 10), (3, 0.2, 3), "allied")

# --- LAB COMPLEX (north)
ly = 72
box("LabBlock1", (-16, ly, 4), (20, 18, 8), "lab")
box("LabBlock2", (16, ly, 4), (20, 18, 8), "lab")
box("LabBridge", (0, ly, 6), (14, 4, 3), "lab")
cyl("LabDome", (0, ly + 12, 5), 9, 10, "glass", verts=20)
box("LabHazard", (0, ly - 10, 1.5), (44, 1.0, 3), "hazard")
for i in range(4):
    box(f"LabTank_{i}", (-24 + i * 16, ly + 12, 3), (3, 3, 6), "metal")

# --- HANGAR / MOTOR POOL (east)
hx = 72
box("HangarFloor", (hx, 0, 0.06), (44, 40, 0.12), "concrete")
box("HangarWallN", (hx, 19, 5), (44, 1, 10), "metal")
box("HangarWallS", (hx, -19, 5), (44, 1, 10), "metal")
box("HangarWallE", (hx + 21, 0, 5), (1, 40, 10), "metal")
box("HangarRoof", (hx, 0, 10.2), (46, 42, 0.6), "metal")
box("HangarStripe", (hx - 21, 0, 5), (1, 40, 10), "hazard")
# vehicles (blocky)
for i in range(3):
    vx = hx - 10 + i * 12
    box(f"Truck_{i}_body", (vx, -8, 1.6), (5, 9, 3.2), "allied")
    box(f"Truck_{i}_cab", (vx, -13, 1.4), (5, 3, 2.8), "metal")
box("FuelTank", (hx + 10, 12, 3), (6, 6, 6), "hazard")

# --- ALIEN BREACH RUINS (south)
sy = -70
box("RuinFloor", (0, sy, -0.1), (60, 44, 0.3), "alien")
# broken walls
for i in range(7):
    x = -28 + i * 9
    h = 2 + (i % 3) * 2
    box(f"Ruin_{i}", (x, sy + 8 - (i % 2) * 16, h / 2),
        (4, 3, h), "concrete", rot=(0.1 * (i % 3), 0, 0.2 * i))
# glowing alien pods
for i in range(6):
    ang = i * math.pi / 3
    px, py = math.cos(ang) * 16, sy + math.sin(ang) * 12
    cyl(f"Pod_{i}", (px, py, 2.2), 2.0, 4.4, "alien", verts=10)
# the breach ring (emissive) at the ruins center
for i in range(12):
    a = i * math.pi / 6
    box(f"BreachRing_{i}", (math.cos(a) * 5, sy + math.sin(a) * 5, 4.5 + math.sin(a) * 0),
        (0.6, 0.6, 0.6), "alien", rot=(0, 0, a))
box("Crater", (0, sy, 0.05), (18, 18, 0.1), "road")

# --- ROADS / catwalks / scattered cover across the sprawl
# elevated catwalk connecting command tower to lab
box("CatwalkNS", (0, 36, 4), (3, 32, 0.4), "metal")
box("CatwalkRailL", (-1.4, 36, 4.7), (0.2, 32, 1.0), "hazard")
box("CatwalkRailR", (1.4, 36, 4.7), (0.2, 32, 1.0), "hazard")
box("RampToCatwalk", (0, 20, 2), (4, 8, 0.4), "metal", rot=(-0.4, 0, 0))

# scattered cover crates & barriers along the roads
cover_spots = [
    (-30, 4), (-30, -4), (30, 4), (30, -4), (4, 30), (-4, 30),
    (4, -30), (-4, -30), (-45, 20), (45, -20), (20, 45), (-20, -45),
    (0, 44), (0, -44), (48, 0), (-48, 0),
]
for i, (x, y) in enumerate(cover_spots):
    box(f"Cover_{i}", (x, y, 0.9), (1.8, 1.8, 1.8), "wood")

# lamp posts for scale/atmosphere
for i in range(8):
    ang = i * math.pi / 4
    lx, ly2 = math.cos(ang) * 90, math.sin(ang) * 90
    box(f"Lamp_{i}", (lx, ly2, 4), (0.4, 0.4, 8), "metal")
    box(f"LampHead_{i}", (lx, ly2, 8.2), (1.6, 1.6, 0.6), "hazard")

# ------------------------------------------------------------- assign mats
for ob, key in objs:
    ob.data.materials.append(mat(key))

# keep Ground a separate object (clean walkable navmesh surface); join the
# rest by material into a few meshes to keep the FBX tidy.
ground = next(ob for ob, k in objs if ob.name == "Ground")
rest = [ob for ob, k in objs if ob.name != "Ground"]
bpy.ops.object.select_all(action="DESELECT")
for ob in rest:
    ob.select_set(True)
bpy.context.view_layer.objects.active = rest[0]
bpy.ops.object.join()
sprawl = bpy.context.active_object
sprawl.name = "CorvusSprawl"

# ------------------------------------------------------------- studio/render
bpy.ops.object.light_add(type="SUN", location=(60, -60, 120))
sun = bpy.context.active_object
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(55), 0, math.radians(35))
bpy.ops.object.camera_add(location=(0, -8, 300))
cam = bpy.context.active_object
cam.data.type = "ORTHO"
cam.data.ortho_scale = 250
cam.rotation_euler = (0, 0, 0)
sc = bpy.context.scene
sc.camera = cam
sc.render.engine = "CYCLES"
sc.cycles.samples = 24
sc.cycles.use_denoising = True
sc.render.resolution_x = 900
sc.render.resolution_y = 900
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

import os
os.makedirs(f"{BASE}/renders", exist_ok=True)
os.makedirs(f"{BASE}/blender", exist_ok=True)
sc.render.filepath = f"{BASE}/renders/huge_map_top.png"
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/huge_map.blend")

# ------------------------------------------------------------- export FBX
os.makedirs(UNITY, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
ground.select_set(True)
sprawl.select_set(True)
bpy.context.view_layer.objects.active = sprawl
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/HugeMap.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", use_space_transform=True,
    bake_space_transform=True, object_types={"MESH"}, mesh_smooth_type="FACE")
tris = sum((len(p.vertices) - 2) for ob in (ground, sprawl) for p in ob.data.polygons)
print(f"HUGE MAP DONE — ~{tris} tris")
