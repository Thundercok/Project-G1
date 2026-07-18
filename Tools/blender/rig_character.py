"""Rig a built character .blend: armature, rigid skinning, idle + walk actions,
FBX export for Unity.

Run: blender --background <char>.blend --python rig_character.py -- <protagonist|villain> <project_dir> <unity_models_dir>
"""
import bpy
import math
import sys

argv = sys.argv
args = argv[argv.index("--") + 1:]
CHAR = args[0]
BASE = args[1]
UNITY = args[2]

sc = bpy.context.scene
sc.render.fps = 30

# ------------------------------------------------------------- bone layout
def mirror(bones):
    out = {}
    for n, (h, t, parent, connect) in bones.items():
        out[n] = (h, t, parent, connect)
        if n.endswith(".L"):
            r = n[:-2] + ".R"
            pr = parent[:-2] + ".R" if parent.endswith(".L") else parent
            out[r] = ((-h[0], h[1], h[2]), (-t[0], t[1], t[2]), pr, connect)
    return out


if CHAR == "protagonist":
    BONES = mirror({
        "root": ((0, 0, 0), (0, 0, 0.15), "", False),
        "hips": ((0, 0, 0.95), (0, 0, 1.10), "root", False),
        "spine": ((0, 0, 1.10), (0, 0, 1.30), "hips", True),
        "chest": ((0, 0, 1.30), (0, 0, 1.47), "spine", True),
        "neck": ((0, 0, 1.47), (0, 0, 1.56), "chest", True),
        "head": ((0, 0, 1.56), (0, 0, 1.80), "neck", True),
        "upper_arm.L": ((0.27, 0, 1.41), (0.385, 0, 1.15), "chest", False),
        "forearm.L": ((0.385, 0, 1.15), (0.45, 0, 0.90), "upper_arm.L", True),
        "hand.L": ((0.45, 0, 0.90), (0.48, -0.01, 0.78), "forearm.L", True),
        "thigh.L": ((0.12, 0, 0.98), (0.13, 0, 0.57), "hips", False),
        "shin.L": ((0.13, 0, 0.57), (0.135, 0, 0.14), "thigh.L", True),
        "foot.L": ((0.135, 0, 0.14), (0.135, -0.22, 0.05), "shin.L", True),
    })
else:
    BONES = mirror({
        "root": ((0, 0, 0), (0, 0, 0.15), "", False),
        "hips": ((0, 0, 0.95), (0, 0, 1.10), "root", False),
        "spine": ((0, 0, 1.10), (0, 0, 1.30), "hips", True),
        "chest": ((0, 0, 1.30), (0, 0, 1.48), "spine", True),
        "neck": ((0, 0, 1.48), (0, 0, 1.58), "chest", True),
        "head": ((0, 0, 1.58), (0, 0, 1.80), "neck", True),
        "upper_arm.L": ((0.215, 0, 1.43), (0.235, 0, 1.14), "chest", False),
        "forearm.L": ((0.235, 0, 1.14), (0.25, 0, 0.88), "upper_arm.L", True),
        "hand.L": ((0.25, 0, 0.88), (0.26, 0, 0.76), "forearm.L", True),
        "thigh.L": ((0.10, 0, 0.99), (0.105, 0, 0.57), "hips", False),
        "shin.L": ((0.105, 0, 0.57), (0.105, 0, 0.10), "thigh.L", True),
        "foot.L": ((0.105, 0, 0.10), (0.105, -0.18, 0.04), "shin.L", True),
    })


def side_of(name):
    if name.endswith("-1"):
        return ".R"
    if name.endswith("1"):
        return ".L"
    return ""


def bone_for(n):
    s = side_of(n)
    if n in ("device", "device_screen"):
        return "forearm.R"
    if n in ("handle", "case") or n.startswith("latch"):
        return "hand.L"
    for pre in ("head", "hair", "nose", "goatee", "ear", "lens", "garm",
                "gbridge", "eye", "brow", "mouth"):
        if n.startswith(pre):
            return "head"
    if n.startswith(("neckseat", "neck", "collar")):
        return "neck"
    if n.startswith("jacket_low"):
        return "hips"
    for pre in ("chest", "emblem", "mark_", "backpack", "vent_", "pauldron",
                "shoulder", "jacket", "shirt", "tie", "lapel"):
        if n.startswith(pre):
            return "chest"
    if n.startswith("abdomen"):
        return "spine"
    for pre in ("pelvis", "crotch", "belt", "pouch"):
        if n.startswith(pre):
            return "hips"
    if n.startswith("uarm"):
        return "upper_arm" + s
    for pre in ("farm", "elbow", "cuff"):
        if n.startswith(pre):
            return "forearm" + s
    if n.startswith("hand"):
        return "hand" + s
    if n.startswith(("thigh",)):
        return "thigh" + s
    if n.startswith(("shin", "knee")):
        return "shin" + s
    for pre in ("boot", "toecap", "shoe"):
        if n.startswith(pre):
            return "foot" + s
    raise RuntimeError("no bone mapping for part: " + n)


# ------------------------------------------------------- skin: groups + join
parts = [o for o in bpy.data.objects
         if o.type == "MESH" and o.name != "ground"]
for ob in parts:
    bone = bone_for(ob.name)
    vg = ob.vertex_groups.new(name=bone)
    vg.add(list(range(len(ob.data.vertices))), 1.0, "REPLACE")

bpy.ops.object.select_all(action="DESELECT")
for ob in parts:
    ob.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.convert(target="MESH")   # apply bevel modifiers
bpy.ops.object.join()
body = bpy.context.active_object
body.name = "Body"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# ------------------------------------------------------------- armature
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
arm = bpy.context.active_object
arm.name = CHAR.capitalize() + "Rig"
arm.data.name = arm.name
eb = arm.data.edit_bones
eb.remove(eb[0])
created = {}
for name, (h, t, parent, connect) in BONES.items():
    b = eb.new(name)
    b.head, b.tail, b.roll = h, t, 0.0
    created[name] = b
for name, (h, t, parent, connect) in BONES.items():
    if parent:
        created[name].parent = created[parent]
        created[name].use_connect = connect
bpy.ops.object.mode_set(mode="OBJECT")

body.parent = arm
mod = body.modifiers.new("Armature", "ARMATURE")
mod.object = arm

# ------------------------------------------------------------- animation
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
        fc.modifiers.new("CYCLES")
    return act, length


R = lambda x=0, y=0, z=0: (x, y, z)

if CHAR == "protagonist":
    A, S, ARM_A = 27, 50, 18       # thigh swing, shin bend, arm swing
else:
    A, S, ARM_A = 18, 38, 6

walk_keys = {
    "thigh.L": [(1, R(-A), None), (8, R(8), None), (16, R(A * 0.75), None),
                (23, R(0), None), (31, R(-A), None)],
    "thigh.R": [(1, R(A * 0.75), None), (8, R(0), None), (16, R(-A), None),
                (23, R(8), None), (31, R(A * 0.75), None)],
    "shin.L": [(1, R(8), None), (8, R(4), None), (16, R(25), None),
               (23, R(S), None), (31, R(8), None)],
    "shin.R": [(1, R(25), None), (8, R(S), None), (16, R(8), None),
               (23, R(4), None), (31, R(25), None)],
    "foot.L": [(1, R(0), None), (16, R(12), None), (31, R(0), None)],
    "foot.R": [(1, R(12), None), (16, R(0), None), (23, R(6), None),
               (31, R(12), None)],
    "upper_arm.L": [(1, R(ARM_A), None), (16, R(-ARM_A), None),
                    (31, R(ARM_A), None)],
    "upper_arm.R": [(1, R(-ARM_A), None), (16, R(ARM_A), None),
                    (31, R(-ARM_A), None)],
    "forearm.L": [(1, R(12), None)],
    "forearm.R": [(1, R(12), None)],
    "hips": [(1, None, (0, -0.015, 0)), (8, None, (0, 0.015, 0)),
             (16, None, (0, -0.015, 0)), (23, None, (0, 0.015, 0)),
             (31, None, (0, -0.015, 0))],
    "chest": [(1, R(4), None)],
}
if CHAR == "villain":
    walk_keys["upper_arm.L"] = [(1, R(3), None)]   # briefcase arm stays stiff
    walk_keys["forearm.L"] = [(1, R(4), None)]
    walk_keys["chest"] = [(1, R(2), None)]

walk, walk_len = make_action("Walk", walk_keys, 30)

if CHAR == "protagonist":
    idle_keys = {
        "chest": [(1, R(0), None), (45, R(2.5), None), (91, R(0), None)],
        "head": [(1, R(0), None), (45, R(-1.5), None), (91, R(0), None)],
        "upper_arm.L": [(1, R(2), None), (45, R(3.5), None), (91, R(2), None)],
        "upper_arm.R": [(1, R(2), None), (45, R(3.5), None), (91, R(2), None)],
        "forearm.L": [(1, R(8), None)],
        "forearm.R": [(1, R(8), None)],
    }
    idle, idle_len = make_action("Idle", idle_keys, 90)
else:
    idle_keys = {
        "chest": [(1, R(0), None), (60, R(1.2), None), (121, R(0), None)],
        "head": [(1, R(0, 0, 0), None), (40, R(0, 14, 0), None),
                 (80, R(0, -14, 0), None), (121, R(0, 0, 0), None)],
        "forearm.L": [(1, R(4), None)],
        "forearm.R": [(1, R(4), None)],
    }
    idle, idle_len = make_action("Idle", idle_keys, 120)

# push both actions onto NLA so the FBX exporter writes them as clips
ad = arm.animation_data
ad.action = None
for act, length in ((idle, idle_len), (walk, walk_len)):
    tr = ad.nla_tracks.new()
    tr.name = act.name
    strip = tr.strips.new(act.name, 1, act)
    strip.action_frame_start = 1
    strip.action_frame_end = length + 1

bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/{CHAR}_rigged.blend")

# ------------------------------------------------- verification renders
sc.render.resolution_x = 640
sc.render.resolution_y = 960
ad.action = walk
for frame, label in ((1, "walk_f1"), (8, "walk_f8")):
    sc.frame_set(frame)
    sc.camera = bpy.data.objects["cam_threequarter"]
    sc.render.filepath = f"{BASE}/renders/{CHAR}_rig_{label}.png"
    bpy.ops.render.render(write_still=True)
ad.action = None
sc.frame_set(1)

# ------------------------------------------------------------- FBX export
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=f"{UNITY}/{CHAR.capitalize()}.fbx",
    use_selection=True,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y",
    object_types={"MESH", "ARMATURE"},
    use_armature_deform_only=True, add_leaf_bones=False,
    mesh_smooth_type="FACE",
    bake_anim=True, bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=False, bake_anim_use_all_bones=True,
    bake_anim_force_startend_keying=True)
print("RIG DONE", CHAR)
