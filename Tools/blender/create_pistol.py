"""9mm semi-automatic service pistol: low-poly mesh with separate slide,
trigger, and magazine parts, a 4-bone armature, and Idle / Fire / Reload
actions exported as FBX takes for Unity.

Grip sits at the origin; barrel points -Y in Blender (= +Z forward in Unity
with the export settings below, so the viewmodel mounts with no rotation).

Run: blender --background --python create_pistol.py -- <project_dir> <unity_models_dir>
"""
import bpy
import math
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
BASE = args[0]
UNITY = args[1]

RAKE = math.radians(-15)          # grip/mag rake-back angle
parts = []                        # (object, bone_name)


def M(name, color, rough=0.6, metal=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*color, 1)
    return m


def box(name, loc, dims, mt, bone, rot=(0, 0, 0), bevel=0.0025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector(dims)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        md = ob.modifiers.new("bev", "BEVEL")
        md.width = bevel
        md.segments = 2
        md.limit_method = "ANGLE"
    ob.data.materials.append(mt)
    bpy.ops.object.shade_flat()
    parts.append((ob, bone))
    return ob


def cyl(name, p1, p2, r, mt, bone, verts=10):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=r, depth=d.length, location=(p1 + p2) / 2)
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = d.to_track_quat("Z", "Y")
    ob.data.materials.append(mt)
    bpy.ops.object.shade_flat()
    parts.append((ob, bone))
    return ob


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

polymer = M("polymer", (0.035, 0.035, 0.04), rough=0.75)
steel = M("slide_steel", (0.16, 0.16, 0.18), rough=0.35, metal=0.85)
darkst = M("dark_steel", (0.06, 0.06, 0.07), rough=0.45, metal=0.6)

# ---- static frame (bone: root)
box("frame", (0, -0.030, 0.048), (0.028, 0.150, 0.022), polymer, "root")
box("grip", (0, 0.026, 0.000), (0.030, 0.036, 0.105), polymer, "root",
    rot=(RAKE, 0, 0))
box("guard_bottom", (0, -0.024, 0.013), (0.024, 0.052, 0.007), polymer, "root")
box("guard_front", (0, -0.048, 0.025), (0.024, 0.007, 0.021), polymer, "root")
box("beavertail", (0, 0.052, 0.062), (0.026, 0.024, 0.010), polymer, "root")
cyl("muzzle", (0, -0.126, 0.075), (0, -0.144, 0.075), 0.0085, darkst, "root")

# ---- slide (bone: slide)
box("slide", (0, -0.035, 0.078), (0.031, 0.190, 0.032), steel, "slide")
box("sight_front", (0, -0.122, 0.098), (0.006, 0.008, 0.008), darkst, "slide")
box("sight_rear", (0, 0.052, 0.098), (0.020, 0.010, 0.008), darkst, "slide")
box("ejection_port", (0.0158, -0.005, 0.082), (0.002, 0.040, 0.016), darkst,
    "slide", bevel=0)
box("hammer", (0, 0.065, 0.070), (0.010, 0.012, 0.016), darkst, "slide")

# ---- trigger (bone: trigger)
box("trigger", (0, -0.020, 0.028), (0.011, 0.007, 0.021), darkst, "trigger",
    rot=(math.radians(8), 0, 0), bevel=0.001)

# ---- magazine (bone: magazine)
box("mag_body", (0, 0.030, -0.005), (0.024, 0.030, 0.100), darkst, "magazine",
    rot=(RAKE, 0, 0))
box("mag_plate", (0, 0.044, -0.058), (0.030, 0.042, 0.011), polymer,
    "magazine", rot=(RAKE, 0, 0))

# ---- armature ----------------------------------------------------------
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
arm = bpy.context.active_object
arm.name = "PistolRig"
arm.data.name = "PistolRig"
eb = arm.data.edit_bones
eb.remove(eb[0])
BONES = {
    "root": ((0, 0.02, -0.03), (0, 0.02, 0.03), ""),
    "slide": ((0, 0.060, 0.078), (0, -0.060, 0.078), "root"),
    "trigger": ((0, -0.014, 0.040), (0, -0.024, 0.016), "root"),
    "magazine": ((0, 0.016, 0.040), (0, 0.044, -0.062), "root"),
}
made = {}
for n, (h, t, p) in BONES.items():
    b = eb.new(n)
    b.head, b.tail, b.roll = h, t, 0.0
    made[n] = b
for n, (h, t, p) in BONES.items():
    if p:
        made[n].parent = made[p]
bpy.ops.object.mode_set(mode="OBJECT")

# ---- skin: rigid vertex groups, join, bind ----------------------------
for ob, bone in parts:
    vg = ob.vertex_groups.new(name=bone)
    vg.add(list(range(len(ob.data.vertices))), 1.0, "REPLACE")
bpy.ops.object.select_all(action="DESELECT")
for ob, _ in parts:
    ob.select_set(True)
bpy.context.view_layer.objects.active = parts[0][0]
bpy.ops.object.convert(target="MESH")
bpy.ops.object.join()
body = bpy.context.active_object
body.name = "PistolMesh"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
body.parent = arm
body.modifiers.new("Armature", "ARMATURE").object = arm

# ---- actions -----------------------------------------------------------
sc = bpy.context.scene
sc.render.fps = 30
arm.animation_data_create()


def make_action(name, keys, length):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data.action = act
    for bname, frames in keys.items():
        pb = arm.pose.bones[bname]
        pb.rotation_mode = "XYZ"
        for frame, rot, loc in frames:
            if rot is not None:
                pb.rotation_euler = [math.radians(a) for a in rot]
                pb.keyframe_insert("rotation_euler", frame=frame)
            if loc is not None:
                pb.location = loc
                pb.keyframe_insert("location", frame=frame)
    cb = act.layers[0].strips[0].channelbag(act.slots[0])
    for fc in cb.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
    return act, length


R = lambda x=0, y=0, z=0: (x, y, z)
Z3 = (0, 0, 0)

# Idle: barely-alive hold sway (loops)
idle, idle_len = make_action("Idle", {
    "root": [(1, R(0), Z3), (31, R(0.8, 0, 0.4), (0, 0, 0.0012)),
             (61, R(0), Z3)],
}, 60)

# Fire: trigger pull, slide cycles back and returns, muzzle kick (10f = 0.33s)
# slide bone points -Y (forward): recoil backward = local +Y negative... the
# bone's local Y runs head->tail (forward), so rearward travel is -Y.
fire, fire_len = make_action("Fire", {
    "trigger": [(1, R(0), Z3), (2, R(-16), Z3), (6, R(0), Z3)],
    "slide": [(1, None, Z3), (3, None, (0, -0.030, 0)), (7, None, Z3)],
    "root": [(1, R(0), Z3), (2, R(-4.5), (0, 0.004, 0.006)), (9, R(0), Z3)],
}, 10)

# Reload: mag drops out along its rake, pause, new mag seats, slide racks
reload_, reload_len = make_action("Reload", {
    "magazine": [(1, None, Z3), (8, None, (0, 0.16, 0)), (22, None, (0, 0.16, 0)),
                 (30, None, (0, 0.004, 0)), (33, None, Z3)],
    "root": [(1, R(0), Z3), (8, R(6, 0, -7), Z3), (30, R(6, 0, -7), Z3),
             (38, R(0), Z3)],
    "slide": [(33, None, Z3), (38, None, (0, -0.030, 0)), (43, None, Z3)],
}, 48)

ad = arm.animation_data
ad.action = None
for act, length in ((idle, idle_len), (fire, fire_len), (reload_, reload_len)):
    tr = ad.nla_tracks.new()
    tr.name = act.name
    strip = tr.strips.new(act.name, 1, act)
    strip.action_frame_start = 1
    strip.action_frame_end = length + 1

# ---- studio + render check --------------------------------------------
bpy.ops.object.empty_add(location=(0, -0.02, 0.05))
target = bpy.context.active_object
for name, loc, e in (("key", (0.5, -0.5, 0.6), 12), ("fill", (-0.5, -0.3, 0.3), 4),
                     ("rim", (0.1, 0.5, 0.5), 7)):
    bpy.ops.object.light_add(type="AREA", location=loc)
    li = bpy.context.active_object
    li.data.energy = e
    li.data.size = 0.8
    li.constraints.new("TRACK_TO").target = target
bpy.ops.object.camera_add(location=(0.38, -0.5, 0.22))
cam = bpy.context.active_object
cam.data.lens = 70
cam.constraints.new("TRACK_TO").target = target
sc.camera = cam
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

bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/pistol.blend")

sc.render.filepath = f"{BASE}/renders/pistol.png"
bpy.ops.render.render(write_still=True)
ad.action = reload_
sc.frame_set(8)
sc.render.filepath = f"{BASE}/renders/pistol_reload_f8.png"
bpy.ops.render.render(write_still=True)
ad.action = None
sc.frame_set(1)

# ---- export ------------------------------------------------------------
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/Pistol.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y",
    object_types={"MESH", "ARMATURE"},
    use_armature_deform_only=True, add_leaf_bones=False,
    mesh_smooth_type="FACE",
    bake_anim=True, bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True)
print("PISTOL DONE")
