"""Build the crowbar weapon: worn red paint over steel, hex stock, curved hook.
Pivot sits at the grip (mid-shaft) for easy viewmodel mounting.

Run: blender --background --python build_crowbar.py -- <project_dir> <unity_models_dir>
"""
import bpy
import math
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
BASE = args[0]
UNITY = args[1]

parts = []


def M(name, color, rough=0.6, metal=0.0):
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
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector(dims)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(ob, mt)


def cyl(name, p1, p2, r, mt, verts=6):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=d.length, location=(p1 + p2) / 2)
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = d.to_track_quat("Z", "Y")
    return _finish(ob, mt)


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

red = M("worn_red", (0.30, 0.035, 0.03), rough=0.55)
steel = M("bare_steel", (0.42, 0.42, 0.45), rough=0.35, metal=1.0)

# shaft (hex stock) with chisel end at the bottom
cyl("shaft", (0, 0, -0.20), (0, 0, 0.21), 0.016, red)
box("chisel_neck", (0, 0, -0.225), (0.030, 0.018, 0.06), steel)
box("chisel_blade", (0, 0, -0.263), (0.040, 0.008, 0.038), steel)

# curved hook over the top
C = Vector((0, -0.045, 0.21))
angles = [0, 40, 80, 120, 160, 200]
pts = [C + 0.045 * Vector((0, math.cos(math.radians(a)),
                           math.sin(math.radians(a)))) for a in angles]
for i in range(5):
    mt = red if i < 2 else steel
    r = 0.0155 - i * 0.001
    cyl(f"hook{i}", pts[i], pts[i + 1], r, mt)
tang = Vector((0, math.sin(math.radians(200)) * -1,
               math.cos(math.radians(200))))
box("hook_tip", pts[-1] + tang * 0.015, (0.034, 0.008, 0.042), steel,
    rot=(math.radians(200), 0, 0))

# studio
gm = M("ground", (0.55, 0.56, 0.58), rough=1.0)
bpy.ops.mesh.primitive_plane_add(size=10, location=(0, 0, -0.30))
bpy.context.active_object.data.materials.append(gm)
bpy.ops.object.empty_add(location=(0, 0, 0.02))
target = bpy.context.active_object
for name, loc, e in (("key", (0.8, -0.8, 1.0), 25), ("fill", (-0.9, -0.5, 0.5), 8),
                     ("rim", (0.2, 0.9, 0.8), 14)):
    bpy.ops.object.light_add(type="AREA", location=loc)
    li = bpy.context.active_object
    li.data.energy = e
    li.data.size = 1.2
    li.constraints.new("TRACK_TO").target = target
bpy.ops.object.camera_add(location=(0.85, -1.15, 0.38))
cam = bpy.context.active_object
cam.data.lens = 60
cam.constraints.new("TRACK_TO").target = target

sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = 40
sc.cycles.use_denoising = True
sc.render.resolution_x = 800
sc.render.resolution_y = 600
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
sc.camera = cam
sc.render.filepath = f"{BASE}/renders/crowbar.png"
bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/crowbar.blend")

bpy.ops.object.select_all(action="DESELECT")
for ob in parts:
    ob.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
joined = bpy.context.active_object
joined.name = "Crowbar"
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/Crowbar.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", use_space_transform=True,
    bake_space_transform=True, object_types={"MESH"}, mesh_smooth_type="FACE")
print("CROWBAR DONE")
