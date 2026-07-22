"""Generate a MASSIVE battlefield map for Project G1 — the 'Corvus Sprawl': a
~600x600 m military-industrial complex with many districts for a large
two-faction battle. Low-poly, flat-shaded, exported as one Unity-ready FBX with
a walkable ground plane and building obstacles.

Districts (spread across a huge footprint):
  - Central Command Tower (map center, very tall)
  - Allied Base           (west)   — barracks rows, sandbags, helipad
  - Lab Complex           (north)  — connected blocks + dome + tanks
  - Hangar / Motor Pool   (east)   — big open shed + vehicles + fuel
  - Alien Breach Ruins    (south)  — broken walls, glowing pods, breach ring
  - Living Quarters       (NW)     — apartment blocks
  - Warehouse Yard        (NE)     — container stacks
  - Comms Array           (SE)     — dish + antenna masts
  - Fuel Depot            (SW)     — tank farm
  - Perimeter wall + corner & mid watchtowers, ring + cross roads, cover

Run:  blender --background --python build_huge_map.py -- <project_dir> <unity_models_dir>
"""
import bpy
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BASE = args[0] if args else "."
UNITY = args[1] if len(args) > 1 else "."

objs = []


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
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector((dims[0] / 2, dims[1] / 2, dims[2] / 2))
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


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

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
    "container_a": M("map_cont_a", (0.55, 0.28, 0.16)),
    "container_b": M("map_cont_b", (0.20, 0.45, 0.40)),
})

HALF = 300.0

# --- ground + roads (cross + ring)
box("Ground", (0, 0, -0.25), (2 * HALF, 2 * HALF, 0.5), "ground")
box("Road_NS", (0, 0, 0.02), (16, 2 * HALF, 0.04), "road")
box("Road_EW", (0, 0, 0.02), (2 * HALF, 16, 0.04), "road")
R = 200
box("Ring_N", (0, R, 0.02), (2 * R + 16, 12, 0.04), "road")
box("Ring_S", (0, -R, 0.02), (2 * R + 16, 12, 0.04), "road")
box("Ring_E", (R, 0, 0.02), (12, 2 * R + 16, 0.04), "road")
box("Ring_W", (-R, 0, 0.02), (12, 2 * R + 16, 0.04), "road")
box("Plaza", (0, 0, 0.03), (60, 60, 0.05), "road")

# --- perimeter wall + towers (corner + mid-wall)
wall_h, wall_t = 8.0, 1.6
edge = HALF - 2
box("Wall_N", (0, edge, wall_h / 2), (2 * HALF, wall_t, wall_h), "concrete")
box("Wall_E", (edge, 0, wall_h / 2), (wall_t, 2 * HALF, wall_h), "concrete")
box("Wall_W", (-edge, 0, wall_h / 2), (wall_t, 2 * HALF, wall_h), "concrete")
box("Wall_S_L", (-(HALF / 2 + 8), -edge, wall_h / 2), (HALF - 16, wall_t, wall_h), "concrete")
box("Wall_S_R", (HALF / 2 + 8, -edge, wall_h / 2), (HALF - 16, wall_t, wall_h), "concrete")
tower_spots = [(-1, -1), (-1, 1), (1, -1), (1, 1), (0, 1), (0, -1), (1, 0), (-1, 0)]
for sx, sy in tower_spots:
    cx = sx * (HALF - 6) if sx else 0
    cy = sy * (HALF - 6) if sy else 0
    box(f"Tower_{sx}_{sy}", (cx, cy, 8), (9, 9, 16), "concrete")
    box(f"TowerTop_{sx}_{sy}", (cx, cy, 16.8), (11, 11, 1.6), "metal")
    box(f"TowerRail_{sx}_{sy}", (cx, cy, 18.2), (11, 11, 1.4), "hazard")
box("GatePost_L", (-9, -edge, 5), (3, 3, 10), "hazard")
box("GatePost_R", (9, -edge, 5), (3, 3, 10), "hazard")
box("GateBeam", (0, -edge, 11), (21, 2, 1.6), "hazard")

# --- CENTRAL COMMAND TOWER (very tall)
box("CmdBase", (0, 0, 4), (30, 30, 8), "concrete")
box("CmdT2", (0, 0, 12), (24, 24, 8), "metal")
box("CmdT3", (0, 0, 20), (18, 18, 8), "concrete")
box("CmdT4", (0, 0, 28), (12, 12, 8), "metal")
box("CmdTop", (0, 0, 35), (8, 8, 6), "concrete")
box("CmdAntenna", (0, 0, 44), (1.4, 1.4, 12), "metal")
for a in range(4):
    ang = a * math.pi / 2
    box(f"CmdWin_{a}", (math.cos(ang) * 12.1, math.sin(ang) * 12.1, 12),
        (0.3 if a % 2 == 0 else 12, 12 if a % 2 == 0 else 0.3, 4), "glass")


def district(cx, cy):
    return cx, cy


# --- ALLIED BASE (west)
bx, by = -160, 0
for i, oy in enumerate((-30, -10, 10, 30)):
    box(f"Barracks_{i}", (bx, by + oy, 4), (30, 14, 8), "allied")
    box(f"BarracksRoof_{i}", (bx, by + oy, 8.3), (31, 15, 0.6), "metal")
box("Helipad", (bx - 26, by, 0.08), (22, 22, 0.16), "concrete")
cyl("HelipadRing", (bx - 26, by, 0.1), 9, 0.05, "hazard", verts=24)
for i in range(10):
    box(f"Sandbag_{i}", (bx + 22, by - 24 + i * 5.4, 0.8), (1.6, 4.2, 1.6),
        "wood", rot=(0, 0, 0.12 * i))
box("AlliedFlag", (bx - 12, by, 9), (0.5, 0.5, 18), "metal")
box("AlliedBanner", (bx - 10, by, 15), (4, 0.3, 5), "allied")

# --- LAB COMPLEX (north)
lx, ly = 0, 165
box("LabBlock1", (-26, ly, 6), (28, 26, 12), "lab")
box("LabBlock2", (26, ly, 6), (28, 26, 12), "lab")
box("LabBridge", (0, ly, 9), (24, 6, 5), "lab")
cyl("LabDome", (0, ly + 20, 7), 14, 14, "glass", verts=22)
box("LabHazard", (0, ly - 16, 2), (64, 1.4, 4), "hazard")
for i in range(5):
    box(f"LabTank_{i}", (-36 + i * 18, ly + 20, 5), (5, 5, 10), "metal")

# --- HANGAR / MOTOR POOL (east)
hx, hy = 165, 0
box("HangarFloor", (hx, hy, 0.06), (64, 60, 0.12), "concrete")
box("HangarWallN", (hx, hy + 29, 8), (64, 1.4, 16), "metal")
box("HangarWallS", (hx, hy - 29, 8), (64, 1.4, 16), "metal")
box("HangarWallE", (hx + 31, hy, 8), (1.4, 60, 16), "metal")
box("HangarRoof", (hx, hy, 16.3), (66, 62, 0.8), "metal")
box("HangarStripe", (hx - 31, hy, 8), (1.4, 60, 16), "hazard")
for i in range(4):
    vx = hx - 18 + i * 12
    box(f"Truck_{i}_body", (vx, hy - 12, 2), (6, 12, 4), "allied")
    box(f"Truck_{i}_cab", (vx, hy - 19, 1.8), (6, 4, 3.4), "metal")
box("FuelTankH", (hx + 16, hy + 18, 4), (8, 8, 8), "hazard")

# --- ALIEN BREACH RUINS (south)
sx, sy = 0, -165
box("RuinFloor", (sx, sy, -0.1), (90, 66, 0.3), "alien")
for i in range(10):
    x = -42 + i * 9.4
    h = 3 + (i % 3) * 3
    box(f"Ruin_{i}", (x, sy + 12 - (i % 2) * 24, h / 2), (6, 4, h),
        "concrete", rot=(0.08 * (i % 3), 0, 0.15 * i))
for i in range(9):
    ang = i * (2 * math.pi / 9)
    px, py = math.cos(ang) * 26, sy + math.sin(ang) * 18
    cyl(f"Pod_{i}", (px, py, 3.2), 3.0, 6.4, "alien", verts=10)
for i in range(14):
    a = i * (2 * math.pi / 14)
    box(f"BreachRing_{i}", (math.cos(a) * 8, sy + math.sin(a) * 8, 7),
        (1.0, 1.0, 1.0), "alien", rot=(0, 0, a))
box("Crater", (sx, sy, 0.05), (28, 28, 0.1), "road")

# --- LIVING QUARTERS (NW)
qx, qy = -150, 150
for i in range(3):
    for j in range(2):
        box(f"Quarters_{i}_{j}", (qx + i * 22, qy + j * 22, 9),
            (16, 16, 18), "concrete")
        box(f"QRoof_{i}_{j}", (qx + i * 22, qy + j * 22, 18.3),
            (17, 17, 0.6), "metal")

# --- WAREHOUSE YARD (NE) — container stacks
wxc, wyc = 150, 150
for i in range(4):
    for j in range(3):
        key = "container_a" if (i + j) % 2 == 0 else "container_b"
        h = 3.2
        stack = 1 + ((i + j) % 3)
        for s in range(stack):
            box(f"Cont_{i}_{j}_{s}", (wxc + i * 14, wyc + j * 8, 1.7 + s * (h + 0.1)),
                (12, 6, h), key)
box("WarehouseShed", (wxc + 20, wyc + 26, 7), (40, 20, 14), "metal")

# --- COMMS ARRAY (SE)
ax, ay = 155, -150
cyl("DishBase", (ax, ay, 6), 3, 12, "metal", verts=12)
cyl("Dish", (ax, ay + 5, 14), 10, 2, "lab", verts=20, rot=(math.radians(60), 0, 0))
for i in range(4):
    box(f"Mast_{i}", (ax - 24 + i * 16, ay - 20, 12), (1.2, 1.2, 24), "metal")
    box(f"MastLight_{i}", (ax - 24 + i * 16, ay - 20, 24.4), (2, 2, 0.8), "hazard")

# --- FUEL DEPOT (SW) — tank farm
fx, fy = -155, -150
for i in range(3):
    for j in range(2):
        cyl(f"FuelTank_{i}_{j}", (fx + i * 20, fy + j * 20, 7), 8, 14, "hazard", verts=16)
box("PumpHouse", (fx - 24, fy, 5), (14, 14, 10), "concrete")
box("PipeRun", (fx, fy - 24, 1.5), (60, 1.4, 1.4), "metal")

# --- scattered cover across the whole sprawl
import random as _r
_r.seed(7)
for i in range(60):
    ang = _r.uniform(0, 2 * math.pi)
    rad = _r.uniform(30, 240)
    x, y = math.cos(ang) * rad, math.sin(ang) * rad
    box(f"Cover_{i}", (x, y, 1.0), (2.2, 2.2, 2.0), "wood")

# --- lamp posts ringing the sprawl
for i in range(16):
    ang = i * (2 * math.pi / 16)
    lx2, ly2 = math.cos(ang) * 250, math.sin(ang) * 250
    box(f"Lamp_{i}", (lx2, ly2, 6), (0.6, 0.6, 12), "metal")
    box(f"LampHead_{i}", (lx2, ly2, 12.4), (2.2, 2.2, 0.8), "hazard")

# --- catwalk network near the center
box("CatwalkNS", (0, 45, 6), (4, 50, 0.5), "metal")
box("CatwalkRailL", (-1.8, 45, 6.9), (0.3, 50, 1.2), "hazard")
box("CatwalkRailR", (1.8, 45, 6.9), (0.3, 50, 1.2), "hazard")

# ------------------------------------------------------------- assign mats
for ob, key in objs:
    ob.data.materials.append(mat(key))

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
bpy.ops.object.light_add(type="SUN", location=(120, -120, 240))
sun = bpy.context.active_object
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(55), 0, math.radians(35))
bpy.ops.object.camera_add(location=(0, -12, 780))
cam = bpy.context.active_object
cam.data.type = "ORTHO"
cam.data.ortho_scale = 680
cam.rotation_euler = (0, 0, 0)
sc = bpy.context.scene
sc.camera = cam
sc.render.engine = "CYCLES"
sc.cycles.samples = 20
sc.cycles.use_denoising = True
sc.render.resolution_x = 1000
sc.render.resolution_y = 1000
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
sprawl.select_set(True)
bpy.context.view_layer.objects.active = sprawl
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/HugeMap.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", use_space_transform=True,
    bake_space_transform=True, object_types={"MESH"}, mesh_smooth_type="FACE")
tris = sum((len(p.vertices) - 2) for ob in (ground, sprawl) for p in ob.data.polygons)
print(f"HUGE MAP DONE — 600x600m, ~{tris} tris")
