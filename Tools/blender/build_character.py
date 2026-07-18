"""Build a retro low-poly game character (protagonist or villain), render a
turnaround, and export a Unity-ready FBX.

Run:  blender --background --python build_character.py -- <protagonist|villain> <project_dir>
"""
import bpy
import math
import sys
from mathutils import Vector

argv = sys.argv
args = argv[argv.index("--") + 1:] if "--" in argv else []
CHAR = args[0] if args else "protagonist"
BASE = args[1] if len(args) > 1 else "/Users/minhdang_work/halflife-like-game"

parts = []  # every mesh belonging to the character (not studio props)


# ---------------------------------------------------------------- helpers
def M(name, color, rough=0.7, metal=0.0, emit=None, estr=0.0):
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


def _finish(ob, mt, bevel):
    if bevel:
        md = ob.modifiers.new("bev", "BEVEL")
        md.width = bevel
        md.segments = 2
        md.limit_method = "ANGLE"
        md.angle_limit = math.radians(40)
    ob.data.materials.append(mt)
    bpy.ops.object.shade_flat()
    parts.append(ob)
    return ob


def box(name, loc, dims, mt, bevel=0.012, rot=(0, 0, 0), quat=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector(dims)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if quat is not None:
        ob.rotation_mode = "QUATERNION"
        ob.rotation_quaternion = quat
    return _finish(ob, mt, bevel)


def cyl(name, p1, p2, r, mt, verts=12, bevel=0.008):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=d.length, location=(p1 + p2) / 2)
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = d.to_track_quat("Z", "Y")
    _finish(ob, mt, bevel)
    return ob, d.to_track_quat("Z", "Y")


def sph(name, loc, r, mt, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=14, ring_count=10, radius=r, location=loc)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector(scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(ob, mt, bevel=0)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for blocks in (bpy.data.meshes, bpy.data.materials, bpy.data.lights,
                   bpy.data.cameras):
        for b in list(blocks):
            if b.users == 0:
                blocks.remove(b)


# ---------------------------------------------------------------- characters
def build_protagonist():
    orange = M("hazard_orange", (0.60, 0.17, 0.02), rough=0.55)
    dark = M("dark_polymer", (0.055, 0.055, 0.06), rough=0.9)
    grey = M("grey_metal", (0.25, 0.26, 0.28), rough=0.5, metal=0.6)
    skin = M("skin", (0.62, 0.40, 0.28), rough=0.8)
    hair = M("hair_brown", (0.10, 0.055, 0.025), rough=0.9)
    glass = M("lens", (0.02, 0.02, 0.03), rough=0.15)
    pale = M("emblem_ring", (0.85, 0.83, 0.78), rough=0.4)
    screen = M("screen", (0.05, 0.30, 0.08), rough=0.3,
               emit=(0.20, 0.85, 0.30), estr=0.8)

    # torso
    box("pelvis", (0, 0, 1.00), (0.36, 0.26, 0.16), orange, bevel=0.02)
    box("crotch", (0, 0, 0.91), (0.22, 0.22, 0.12), dark)
    box("abdomen", (0, 0, 1.12), (0.32, 0.22, 0.16), orange, bevel=0.02)
    box("chest", (0, 0, 1.33), (0.46, 0.27, 0.28), orange, bevel=0.025)
    box("chest_plate", (0, -0.15, 1.34), (0.38, 0.06, 0.24), orange, bevel=0.02)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.062, depth=0.018,
                                        location=(0, -0.185, 1.345),
                                        rotation=(math.pi / 2, 0, 0))
    _finish(bpy.context.active_object, pale, bevel=0)
    bpy.context.active_object.name = "emblem_outer"
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.038, depth=0.022,
                                        location=(0, -0.186, 1.345),
                                        rotation=(math.pi / 2, 0, 0))
    _finish(bpy.context.active_object, dark, bevel=0)
    bpy.context.active_object.name = "emblem_inner"
    # lambda-style mark on the emblem
    box("mark_l", (-0.012, -0.200, 1.343), (0.014, 0.008, 0.052), orange,
        bevel=0, rot=(0, 0.45, 0))
    box("mark_r", (0.012, -0.200, 1.343), (0.014, 0.008, 0.052), orange,
        bevel=0, rot=(0, -0.45, 0))
    box("backpack", (0, 0.17, 1.32), (0.36, 0.12, 0.34), orange, bevel=0.02)
    box("vent_l", (-0.08, 0.235, 1.32), (0.09, 0.012, 0.22), dark, bevel=0)
    box("vent_r", (0.08, 0.235, 1.32), (0.09, 0.012, 0.22), dark, bevel=0)
    box("belt", (0, 0, 0.96), (0.42, 0.32, 0.07), dark)
    box("pouch_l", (-0.11, -0.15, 0.955), (0.09, 0.06, 0.09), grey)
    box("pouch_r", (0.11, -0.15, 0.955), (0.09, 0.06, 0.09), grey)

    # head
    box("neckseat", (0, 0.01, 1.50), (0.10, 0.10, 0.12), dark, bevel=0.02)
    box("head", (0, 0, 1.67), (0.17, 0.19, 0.23), skin, bevel=0.045)
    box("hair_top", (0, 0.015, 1.76), (0.185, 0.20, 0.075), hair, bevel=0.03)
    box("hair_back", (0, 0.09, 1.68), (0.185, 0.05, 0.16), hair, bevel=0.02)
    box("nose", (0, -0.105, 1.66), (0.028, 0.025, 0.045), skin, bevel=0.006)
    box("goatee", (0, -0.088, 1.575), (0.075, 0.025, 0.06), hair, bevel=0.008)
    box("ear_l", (-0.088, 0.01, 1.665), (0.018, 0.045, 0.055), skin, bevel=0.006)
    box("ear_r", (0.088, 0.01, 1.665), (0.018, 0.045, 0.055), skin, bevel=0.006)
    for s in (-1, 1):
        box(f"lens{s}", (0.045 * s, -0.102, 1.685), (0.065, 0.012, 0.045),
            glass, bevel=0.004)
        box(f"garm{s}", (0.083 * s, -0.03, 1.688), (0.008, 0.13, 0.012),
            dark, bevel=0)
    box("gbridge", (0, -0.102, 1.69), (0.03, 0.01, 0.012), dark, bevel=0)

    # arms (A-pose)
    for s in (-1, 1):
        sph(f"pauldron{s}", (0.26 * s, 0, 1.44), 0.095, orange,
            scale=(1.25, 1.05, 0.85))
        cyl(f"uarm{s}", (0.27 * s, 0, 1.41), (0.385 * s, 0, 1.15), 0.06, orange)
        sph(f"elbow{s}", (0.385 * s, 0, 1.14), 0.065, dark)
        fore, fq = cyl(f"farm{s}", (0.39 * s, 0, 1.13), (0.45 * s, 0, 0.90),
                       0.056, orange)
        box(f"hand{s}", (0.465 * s, -0.01, 0.84), (0.08, 0.11, 0.13), dark,
            bevel=0.02, quat=fq)
        if s == -1:  # geiger counter unit on the left forearm
            mid = Vector((0.39 * s + 0.45 * s, 0, 1.13 + 0.90)) / 2
            box("device", mid, (0.15, 0.15, 0.18), dark, bevel=0.02, quat=fq)
            box("device_screen", mid + Vector((0, -0.105, 0.01)),
                (0.08, 0.014, 0.10), screen, bevel=0)

    # legs
    for s in (-1, 1):
        cyl(f"thigh{s}", (0.12 * s, 0, 0.99), (0.13 * s, 0, 0.58), 0.088, orange)
        box(f"thigh_plate{s}", (0.135 * s, -0.095, 0.78), (0.13, 0.05, 0.24),
            orange, bevel=0.015)
        sph(f"knee{s}", (0.13 * s, 0, 0.565), 0.075, dark)
        cyl(f"shin{s}", (0.13 * s, 0, 0.55), (0.135 * s, 0, 0.16), 0.072, orange)
        box(f"boot{s}", (0.135 * s, -0.04, 0.08), (0.17, 0.32, 0.16), dark,
            bevel=0.02)
        box(f"toecap{s}", (0.135 * s, -0.17, 0.065), (0.15, 0.10, 0.11),
            orange, bevel=0.015)


def build_villain():
    suit = M("suit", (0.035, 0.04, 0.055), rough=0.75)
    shirt = M("shirt", (0.75, 0.75, 0.72), rough=0.8)
    tiemat = M("tie", (0.25, 0.02, 0.035), rough=0.6)
    skin = M("pale_skin", (0.55, 0.46, 0.40), rough=0.8)
    hair = M("hair_dark", (0.05, 0.04, 0.04), rough=0.9)
    shoe = M("shoe", (0.02, 0.02, 0.02), rough=0.3)
    case = M("briefcase", (0.055, 0.030, 0.018), rough=0.5)
    steel = M("steel", (0.6, 0.6, 0.62), rough=0.3, metal=1.0)
    eye = M("eye", (0.05, 0.10, 0.09), rough=0.3,
            emit=(0.45, 0.85, 0.78), estr=0.55)
    dark = M("dark_detail", (0.05, 0.05, 0.05), rough=0.9)
    mouth = M("mouth", (0.30, 0.14, 0.12), rough=0.8)

    # torso / suit
    box("pelvis", (0, 0, 1.00), (0.30, 0.20, 0.16), suit)
    box("crotch", (0, 0, 0.92), (0.17, 0.18, 0.12), suit)
    box("jacket_low", (0, 0, 1.10), (0.37, 0.23, 0.22), suit, bevel=0.02)
    box("jacket", (0, 0, 1.33), (0.40, 0.23, 0.30), suit, bevel=0.025)
    box("shirt_v", (0, -0.113, 1.40), (0.11, 0.012, 0.12), shirt, bevel=0)
    box("tie_knot", (0, -0.121, 1.42), (0.055, 0.02, 0.045), tiemat, bevel=0.006)
    box("tie", (0, -0.119, 1.28), (0.05, 0.014, 0.26), tiemat, bevel=0.006)
    box("tie_clip", (0, -0.128, 1.30), (0.03, 0.006, 0.012), steel, bevel=0)
    for s in (-1, 1):
        box(f"lapel{s}", (0.075 * s, -0.118, 1.38), (0.055, 0.016, 0.20),
            suit, bevel=0.006, rot=(0, s * 0.26, 0))
        sph(f"shoulder{s}", (0.20 * s, 0, 1.44), 0.07, suit,
            scale=(1.15, 1.0, 0.85))

    # arms straight down; right hand carries the briefcase
    for s in (-1, 1):
        cyl(f"uarm{s}", (0.215 * s, 0, 1.43), (0.235 * s, 0, 1.15), 0.052, suit)
        sph(f"elbow{s}", (0.235 * s, 0, 1.14), 0.055, suit)
        cyl(f"farm{s}", (0.24 * s, 0, 1.13), (0.25 * s, 0, 0.88), 0.047, suit)
        cyl(f"cuff{s}", (0.25 * s, 0, 0.895), (0.25 * s, 0, 0.872), 0.051, shirt)
        hz = 0.80 if s == 1 else 0.82
        box(f"hand{s}", (0.255 * s, 0, hz), (0.065, 0.085, 0.11), skin,
            bevel=0.015)
    box("handle", (0.26, 0, 0.735), (0.035, 0.11, 0.028), dark, bevel=0.006)
    box("case", (0.26, 0, 0.585), (0.10, 0.40, 0.28), case, bevel=0.02)
    box("latch_f", (0.312, -0.10, 0.70), (0.012, 0.035, 0.022), steel, bevel=0)
    box("latch_b", (0.312, 0.10, 0.70), (0.012, 0.035, 0.022), steel, bevel=0)

    # legs
    for s in (-1, 1):
        cyl(f"thigh{s}", (0.10 * s, 0, 0.99), (0.105 * s, 0, 0.57), 0.068, suit)
        sph(f"knee{s}", (0.105 * s, 0, 0.555), 0.06, suit)
        cyl(f"shin{s}", (0.105 * s, 0, 0.545), (0.105 * s, 0, 0.10), 0.058, suit)
        box(f"shoe{s}", (0.105 * s, -0.055, 0.05), (0.105, 0.27, 0.10), shoe,
            bevel=0.015)

    # head
    cyl("neck", (0, 0.01, 1.46), (0, 0.01, 1.60), 0.045, skin)
    cyl("collar", (0, 0.01, 1.475), (0, 0.01, 1.508), 0.058, shirt)
    box("head", (0, 0, 1.68), (0.155, 0.185, 0.225), skin, bevel=0.04)
    box("hair_top", (0, 0.02, 1.77), (0.17, 0.19, 0.06), hair, bevel=0.025)
    box("hair_back", (0, 0.095, 1.70), (0.17, 0.05, 0.14), hair, bevel=0.015)
    box("nose", (0, -0.10, 1.665), (0.026, 0.028, 0.05), skin, bevel=0.005)
    box("ear_l", (-0.082, 0.01, 1.675), (0.016, 0.04, 0.05), skin, bevel=0.005)
    box("ear_r", (0.082, 0.01, 1.675), (0.016, 0.04, 0.05), skin, bevel=0.005)
    box("mouth", (0, -0.096, 1.61), (0.05, 0.006, 0.007), mouth, bevel=0)
    for s in (-1, 1):
        box(f"eye{s}", (0.040 * s, -0.096, 1.70), (0.030, 0.010, 0.020), eye,
            bevel=0)
        box(f"brow{s}", (0.042 * s, -0.095, 1.722), (0.032, 0.008, 0.008),
            hair, bevel=0)


# ---------------------------------------------------------------- studio
def build_studio():
    ground_mat = M("ground", (0.55, 0.56, 0.58), rough=1.0)
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = "ground"
    ground.data.materials.append(ground_mat)

    bpy.ops.object.empty_add(location=(0, 0, 0.92))
    target = bpy.context.active_object
    target.name = "target"

    def light(name, kind, loc, energy, size=2.5):
        bpy.ops.object.light_add(type=kind, location=loc)
        ob = bpy.context.active_object
        ob.name = name
        ob.data.energy = energy
        if kind == "AREA":
            ob.data.size = size
        c = ob.constraints.new("TRACK_TO")
        c.target = target
        return ob

    light("key", "AREA", (2.6, -2.6, 3.2), 280)
    light("fill", "AREA", (-3.0, -1.8, 1.8), 90)
    light("rim", "AREA", (0.5, 3.2, 3.0), 170)

    cams = {}
    views = {"front": (0, -3.1, 0.95), "side": (3.1, 0, 0.95),
             "back": (0, 3.1, 0.95), "threequarter": (2.2, -2.2, 1.55)}
    for name, loc in views.items():
        bpy.ops.object.camera_add(location=loc)
        cam = bpy.context.active_object
        cam.name = f"cam_{name}"
        cam.data.lens = 60
        c = cam.constraints.new("TRACK_TO")
        c.target = target
        cams[name] = cam
    return cams


def setup_render():
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 40
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 640
    sc.render.resolution_y = 960
    sc.view_settings.view_transform = "Standard"
    world = sc.world or bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.70, 0.72, 0.74, 1)
        bg.inputs[1].default_value = 1.0
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "METAL"
        prefs.get_devices()
        for d in prefs.devices:
            d.use = True
        sc.cycles.device = "GPU"
        print("Cycles: using METAL GPU")
    except Exception as e:
        sc.cycles.device = "CPU"
        print("Cycles: falling back to CPU:", e)


# ---------------------------------------------------------------- main
clear_scene()
if CHAR == "protagonist":
    build_protagonist()
else:
    build_villain()
cams = build_studio()
setup_render()

bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/{CHAR}.blend")

for name, cam in cams.items():
    bpy.context.scene.camera = cam
    bpy.context.scene.render.filepath = f"{BASE}/renders/{CHAR}_{name}.png"
    bpy.ops.render.render(write_still=True)
    print("rendered", name)

# join a copy-free single mesh and export FBX for Unity
bpy.ops.object.select_all(action="DESELECT")
for ob in parts:
    ob.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.convert(target="MESH")
bpy.ops.object.join()
joined = bpy.context.active_object
joined.name = CHAR.capitalize()
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
tris = sum(len(p.vertices) - 2 for p in joined.data.polygons)
print(f"{joined.name}: {len(joined.data.polygons)} faces, ~{tris} tris")

bpy.ops.export_scene.fbx(
    filepath=f"{BASE}/exports/{joined.name}.fbx",
    use_selection=True, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y",
    use_space_transform=True, bake_space_transform=True,
    object_types={"MESH"}, mesh_smooth_type="FACE")
print("DONE", CHAR)
