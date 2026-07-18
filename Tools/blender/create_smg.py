"""9mm submachine gun: low-poly mesh with separate bolt,
trigger, and magazine parts, a 4-bone armature, and Idle / Fire / Reload
actions exported as FBX takes for Unity.

Grip sits at the origin; barrel points -Y in Blender (= +Z forward in Unity
with the export settings below, so the viewmodel mounts with no rotation).

Run: blender --background --python create_smg.py -- <project_dir> <unity_models_dir>
"""
import bpy
import math
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
BASE = args[0]
UNITY = args[1]

RAKE = math.radians(-12)          # grip rake-back angle
MAG_RAKE = math.radians(8)        # mag rake-forward angle
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


# Reset scene
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

polymer = M("polymer", (0.035, 0.035, 0.04), rough=0.75)
steel = M("receiver_steel", (0.15, 0.15, 0.17), rough=0.4, metal=0.8)
darkst = M("dark_steel", (0.06, 0.06, 0.07), rough=0.45, metal=0.6)

# ---- static frame (bone: root)
# Upper receiver tube
cyl("receiver_upper", (0, 0.15, 0.08), (0, -0.15, 0.08), 0.016, steel, "root")
# Lower receiver/trigger housing
box("receiver_lower", (0, -0.02, 0.045), (0.026, 0.15, 0.04), polymer, "root")
# Pistol grip
box("grip", (0, 0.06, -0.015), (0.028, 0.036, 0.11), polymer, "root", rot=(RAKE, 0, 0))
# Fixed stock
box("stock", (0, 0.22, 0.04), (0.024, 0.14, 0.06), polymer, "root")
# Handguard
box("handguard", (0, -0.11, 0.065), (0.034, 0.12, 0.042), polymer, "root")
# Barrel
cyl("barrel", (0, -0.15, 0.08), (0, -0.28, 0.08), 0.0075, darkst, "root")
# Muzzle / flash hider
cyl("muzzle", (0, -0.28, 0.08), (0, -0.31, 0.08), 0.009, darkst, "root")
# Sights
box("sight_front", (0, -0.26, 0.11), (0.006, 0.01, 0.022), darkst, "root")
box("sight_rear", (0, 0.12, 0.105), (0.016, 0.012, 0.012), darkst, "root")
# Trigger guard
box("guard_bottom", (0, 0.005, -0.025), (0.022, 0.06, 0.006), polymer, "root")
box("guard_front", (0, -0.025, -0.008), (0.022, 0.006, 0.032), polymer, "root")

# ---- bolt (bone: bolt)
# Bolt handle on the left side (standard MP5 charging handle)
cyl("bolt_handle", (-0.015, -0.06, 0.088), (-0.033, -0.06, 0.088), 0.0045, darkst, "bolt")

# ---- trigger (bone: trigger)
box("trigger", (0, 0.005, 0.0), (0.009, 0.006, 0.022), darkst, "trigger",
    rot=(math.radians(10), 0, 0), bevel=0.001)

# ---- magazine (bone: magazine)
box("mag_body", (0, -0.07, -0.06), (0.02, 0.032, 0.15), darkst, "magazine",
    rot=(MAG_RAKE, 0, 0))
box("mag_clamp", (0, -0.074, -0.002), (0.025, 0.036, 0.024), polymer, "magazine",
    rot=(MAG_RAKE, 0, 0))

# ---- armature ----------------------------------------------------------
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
arm = bpy.context.active_object
arm.name = "SmgRig"
arm.data.name = "SmgRig"
eb = arm.data.edit_bones
eb.remove(eb[0])

BONES = {
    "root": ((0, 0.05, -0.03), (0, 0.05, 0.03), ""),
    "bolt": ((0, 0.08, 0.08), (0, -0.08, 0.08), "root"),
    "trigger": ((0, 0.01, 0.01), (0, 0.0, -0.01), "root"),
    "magazine": ((0, -0.06, 0.04), (0, -0.08, -0.15), "root"),
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
body.name = "SmgMesh"
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

# Idle: breathing hold sway (loops)
idle, idle_len = make_action("Idle", {
    "root": [(1, R(0), Z3), (31, R(0.6, 0, 0.3), (0, 0, 0.0008)),
             (61, R(0), Z3)],
}, 60)

# Fire: quick cyclic bolt pull and muzzle kick (4f = 0.13s loop)
fire, fire_len = make_action("Fire", {
    "trigger": [(1, R(0), Z3), (2, R(-12), Z3), (4, R(0), Z3)],
    "bolt": [(1, None, Z3), (2, None, (0, -0.035, 0)), (4, None, Z3)],
    "root": [(1, R(0), Z3), (2, R(-3.2), (0, 0.003, 0.004)), (4, R(0), Z3)],
}, 4)

# Reload: mag slides down raked front, seats new mag, charging handle pulled back (60f = 2.0s)
reload_, reload_len = make_action("Reload", {
    "magazine": [(1, None, Z3), (12, None, (0, 0.22, 0)), (32, None, (0, 0.22, 0)),
                 (42, None, (0, 0.005, 0)), (44, None, Z3)],
    "root": [(1, R(0), Z3), (12, R(5, 0, -6), Z3), (42, R(5, 0, -6), Z3), (48, R(0), Z3)],
    "bolt": [(44, None, Z3), (50, None, (0, -0.035, 0)), (54, None, Z3)],
}, 60)

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
bpy.ops.object.camera_add(location=(0.42, -0.6, 0.25))
cam = bpy.context.active_object
cam.data.lens = 65
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

bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/smg.blend")

sc.render.filepath = f"{BASE}/renders/smg.png"
bpy.ops.render.render(write_still=True)
ad.action = reload_
sc.frame_set(12)
sc.render.filepath = f"{BASE}/renders/smg_reload_f12.png"
bpy.ops.render.render(write_still=True)
ad.action = None
sc.frame_set(1)

# ---- export ------------------------------------------------------------
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/Smg.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y",
    object_types={"MESH", "ARMATURE"},
    use_armature_deform_only=True, add_leaf_bones=False,
    mesh_smooth_type="FACE",
    bake_anim=True, bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True)
print("SMG DONE")
