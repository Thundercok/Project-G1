"""12-Gauge Shotgun: low-poly mesh with separate pump slide and trigger parts,
a 3-bone armature, and Idle / Fire / Reload actions exported as FBX takes for Unity.

Grip sits at the origin; barrel points -Y in Blender (= +Z forward in Unity
with the export settings below, so the viewmodel mounts with no rotation).

Run: blender --background --python create_shotgun.py -- <project_dir> <unity_models_dir>
"""
import bpy
import math
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
BASE = args[0]
UNITY = args[1]

sc = bpy.context.scene
sc.render.fps = 30

RAKE = math.radians(-12)          # grip rake-back angle
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
# Receiver tube (thick steel block)
box("receiver", (0, 0.0, 0.05), (0.032, 0.22, 0.05), steel, "root")
# Pistol grip (wood/polymer)
box("grip", (0, 0.09, -0.02), (0.03, 0.04, 0.11), polymer, "root", rot=(RAKE, 0, 0))
# Stock (wood/polymer)
box("stock", (0, 0.28, 0.04), (0.026, 0.22, 0.07), polymer, "root")
# Barrel (long dark steel tube)
cyl("barrel", (0, -0.11, 0.08), (0, -0.65, 0.08), 0.01, darkst, "root")
# Magazine tube (under barrel)
cyl("mag_tube", (0, -0.11, 0.05), (0, -0.55, 0.05), 0.0085, darkst, "root")
# Muzzle tip
cyl("muzzle", (0, -0.65, 0.08), (0, -0.67, 0.08), 0.0105, darkst, "root")
# Sights
box("sight_front", (0, -0.63, 0.095), (0.006, 0.01, 0.012), darkst, "root")
# Trigger guard
box("guard_bottom", (0, 0.04, -0.01), (0.024, 0.06, 0.006), polymer, "root")
box("guard_front", (0, 0.01, 0.01), (0.024, 0.006, 0.032), polymer, "root")

# ---- pump slide (bone: pump)
# Wooden/polymer ribbed forearm pump grip sliding on the mag tube
box("pump_slide", (0, -0.22, 0.05), (0.038, 0.16, 0.038), polymer, "pump")

# ---- trigger (bone: trigger)
box("trigger", (0, 0.04, 0.01), (0.009, 0.006, 0.022), darkst, "trigger",
    rot=(math.radians(10), 0, 0), bevel=0.001)

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
body.name = "ShotgunMesh"

# Armature creation
bpy.ops.object.armature_add(enter_editmode=True, align="WORLD", location=(0, 0, 0))
arm = bpy.context.active_object
arm.name = "ShotgunRig"
arm.data.display_type = "STICK"
eb = arm.data.edit_bones
eb.remove(eb[0])  # remove default bone

b_root = eb.new("root")
b_root.head = (0, 0, 0)
b_root.tail = (0, -0.2, 0.08)

b_pump = eb.new("pump")
b_pump.head = (0, -0.22, 0.05)
b_pump.tail = (0, -0.38, 0.05)
b_pump.parent = b_root

b_trig = eb.new("trigger")
b_trig.head = (0, 0.04, 0.01)
b_trig.tail = (0, 0.04, -0.01)
b_trig.parent = b_root

bpy.ops.object.editmode_toggle()

# Bind mesh to armature
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type="ARMATURE")

# Add armature animation data
arm.animation_data_create()


# ---- animations --------------------------------------------------------
def make_action(name, keys, length):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data.action = act
    for bone_name, keyframes in keys.items():
        # Pose bone location/rotation
        pb = arm.pose.bones[bone_name]
        pb.rotation_mode = "XYZ"
        for frame, loc, rot in keyframes:
            if loc is not None:
                pb.location = loc
                pb.keyframe_insert("location", frame=frame)
            if rot is not None:
                pb.rotation_euler = rot
                pb.keyframe_insert("rotation_euler", frame=frame)
    cb = act.layers[0].strips[0].channelbag(act.slots[0])
    for fc in cb.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    return act, length


# Helpers
Z3 = (0, 0, 0)
# Idle Action (60 frames)
idle_keys = {
    "root": [(1, Z3, Z3), (30, (0, 0, 0.001), (math.radians(0.3), 0, 0)), (60, Z3, Z3)],
    "pump": [(1, Z3, Z3)],
    "trigger": [(1, Z3, Z3)],
}
idle, idle_len = make_action("Idle", idle_keys, 60)

# Fire Action (24 frames = 0.8s pump cycle)
# root kicks back and up quickly, slide Y moves back on frame 5-10, forward on 10-15
fire_keys = {
    "root": [
        (1, Z3, Z3),
        (3, (0, 0.02, 0.015), (math.radians(-6), 0, math.radians(1))), # muzzle kick
        (12, (0, 0.005, 0.002), (math.radians(-1), 0, 0)),
        (24, Z3, Z3)
    ],
    "pump": [
        (1, Z3, Z3),
        (5, Z3, Z3),
        (10, (0, 0.06, 0), Z3), # slide back Y (0.06m)
        (12, (0, 0.06, 0), Z3),
        (17, Z3, Z3), # slide back forward
        (24, Z3, Z3)
    ],
    "trigger": [
        (1, Z3, Z3),
        (3, Z3, (math.radians(-25), 0, 0)),
        (6, Z3, Z3)
    ]
}
fire, fire_len = make_action("Fire", fire_keys, 24)

# Reload Action (18 frames = 0.6s single shell load loop)
reload_keys = {
    "root": [
        (1, Z3, Z3),
        (5, (0, -0.01, -0.01), (math.radians(8), math.radians(12), 0)), # tilt side/down
        (12, (0, -0.01, -0.01), (math.radians(8), math.radians(12), 0)),
        (18, Z3, Z3)
    ],
    "pump": [(1, Z3, Z3)],
    "trigger": [(1, Z3, Z3)]
}
reload_, reload_len = make_action("Reload", reload_keys, 18)

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

bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/shotgun.blend")

sc.render.filepath = f"{BASE}/renders/shotgun.png"
bpy.ops.render.render(write_still=True)
ad.action = reload_
sc.frame_set(8)
sc.render.filepath = f"{BASE}/renders/shotgun_reload_f8.png"
bpy.ops.render.render(write_still=True)
ad.action = None
sc.frame_set(1)

# ---- export ------------------------------------------------------------
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/Shotgun.fbx", use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y",
    object_types={"MESH", "ARMATURE"},
    use_armature_deform_only=True, add_leaf_bones=False,
    mesh_smooth_type="FACE",
    bake_anim=True, bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True)
print("SHOTGUN DONE")
