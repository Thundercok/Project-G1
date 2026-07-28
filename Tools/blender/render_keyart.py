"""Render the key art: the protagonist bringing the crowbar down on an alien.

This is built from the game's own assets rather than drawn from scratch — the
rigged protagonist out of protagonist_rigged.blend and the crowbar out of
crowbar.blend — because the point of a key image is that it shows *this*
character, in *this* suit, holding the weapon the game actually starts you with.
An illustration that merely resembles them would be worth less than a screenshot.

The alien is built here rather than taken from the game, because the game has
never had one: what the code calls "Alien" is the Auditor's mesh tinted violet,
which is fine as a distant silhouette and useless a metre from the camera.

The moment chosen is the frame *before* contact. A crowbar already buried in
something is gore; a crowbar at the top of its arc is a decision, and the alien
recoiling underneath it tells you who is winning without showing the hit.

Run:  blender --background <project>/blender/protagonist_rigged.blend \
              --python render_keyart.py -- <project_dir>

It opens the rigged file directly rather than appending out of it. Appending
"Body" drags its parent armature in as a dependency, so the scene ends up with
two rigs: the one that was posed and the one the mesh is actually bound to. The
first version of this did exactly that and rendered a man standing to attention
while an invisible copy of him swung a crowbar.
"""
import bpy
import math
import os
import sys
from mathutils import Vector, Euler

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BASE = args[0] if args else "/Users/minhdang_work/halflife-like-game"

# the rigged file ships a turnaround studio; none of it is wanted here
for ob in list(bpy.data.objects):
    if ob.type in ("CAMERA", "LIGHT", "EMPTY") or ob.name == "ground":
        bpy.data.objects.remove(ob, do_unlink=True)

rig = bpy.data.objects["ProtagonistRig"]
if rig.animation_data:
    rig.animation_data.action = None
    for tr in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(tr)

# The character models face -Y. Turning him +90 about Z points that at -X,
# which is where the alien is; -90 pointed him at the opposite wall and had him
# swinging a crowbar at nothing.
rig.rotation_mode = "XYZ"
rig.rotation_euler = (0, 0, math.radians(90))
rig.location = (0.55, -0.10, 0.0)


def pose(bone, rx=0.0, ry=0.0, rz=0.0):
    pb = rig.pose.bones.get(bone)
    if pb is None:
        print("  ! no bone", bone)
        return
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), "XYZ")


bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE")

# A swing is a whole body, not an arm. Hips and spine counter-rotate into the
# blow, the lead leg is planted forward, the trailing arm swings back for
# balance — take any of those away and it reads as a man holding a stick up.
pose("hips", rx=-4, rz=22)
pose("spine", rx=-12, rz=-14)
pose("chest", rx=-10, rz=-20)
pose("neck", rx=16, rz=6)
pose("head", rx=20, rz=8)

# right arm high and cocked, a frame from coming down
pose("upper_arm.R", rx=-34, ry=-58, rz=-112)
pose("forearm.R", rx=-18, rz=-70)
pose("hand.R", rz=-12)

# left arm thrown back for counterweight
pose("upper_arm.L", rx=20, ry=46, rz=52)
pose("forearm.L", rx=-12, rz=-38)
pose("hand.L", rz=18)

# lead leg planted forward, rear leg driving
pose("thigh.R", rx=-36, rz=-6)
pose("shin.R", rx=28)
pose("foot.R", rx=10)
pose("thigh.L", rx=28, rz=6)
pose("shin.L", rx=-20)
pose("foot.L", rx=-10)

bpy.ops.object.mode_set(mode="OBJECT")

# ------------------------------------------------------------- the crowbar
# crowbar.blend keeps the tool as loose parts; append them all and join
BAR_PARTS = ["shaft", "hook0", "hook1", "hook2", "hook3", "hook4", "hook_tip",
             "chisel_neck", "chisel_blade"]
d = f"{BASE}/blender/crowbar.blend/Object/"
appended = []
for n in BAR_PARTS:
    before = set(bpy.data.objects)
    try:
        bpy.ops.wm.append(filepath=d + n, directory=d, filename=n)
    except Exception as e:
        print("  ! crowbar part", n, e)
        continue
    appended += [o for o in bpy.data.objects if o not in before]

if appended:
    bpy.ops.object.select_all(action="DESELECT")
    for o in appended:
        o.select_set(True)
    bpy.context.view_layer.objects.active = appended[0]
    if len(appended) > 1:
        bpy.ops.object.join()
    bar = bpy.context.active_object
    bar.name = "Crowbar"
    bar.parent = rig
    bar.parent_type = "BONE"
    bar.parent_bone = "hand.R"
    # bone parenting anchors at the bone TAIL, so the grip is pulled back along
    # the bone rather than left sitting where the wrist is
    bar.location = (0.0, -0.10, 0.0)
    bar.rotation_mode = "XYZ"
    bar.rotation_euler = (math.radians(84), math.radians(10), 0)
    print("crowbar attached from", len(appended), "parts")
else:
    print("  ! no crowbar parts appended")


# ---------------------------------------------------------------- the alien
def M(name, color, rough=0.6, metal=0.0, emit=None, estr=0.0, sss=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if sss and "Subsurface Weight" in b.inputs:
        b.inputs["Subsurface Weight"].default_value = sss
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = estr
    m.diffuse_color = (*color, 1)
    return m


alien_parts = []


def ab(name, loc, dims, mt, bevel=0.02, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector(dims)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        md = ob.modifiers.new("b", "BEVEL")
        md.width = bevel
        md.segments = 2
        md.limit_method = "ANGLE"
        md.angle_limit = math.radians(40)
    ob.data.materials.append(mt)
    bpy.ops.object.shade_flat()
    alien_parts.append(ob)
    return ob


def asph(name, loc, r, mt, scale=(1, 1, 1), rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=r,
                                         location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector(scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(mt)
    bpy.ops.object.shade_smooth()
    alien_parts.append(ob)
    return ob


def atap(name, p1, p2, r1, r2, mt, verts=10):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2,
                                    depth=d.length, location=(p1 + p2) / 2)
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = d.to_track_quat("Z", "Y")
    ob.data.materials.append(mt)
    bpy.ops.object.shade_smooth()
    alien_parts.append(ob)
    return ob


hide = M("al_hide", (0.115, 0.170, 0.098), rough=0.72, sss=0.25)
hide_d = M("al_hide_d", (0.075, 0.110, 0.070), rough=0.80)
carapace = M("al_carapace", (0.028, 0.046, 0.040), rough=0.30, metal=0.35)
claw = M("al_claw", (0.115, 0.100, 0.080), rough=0.28)
maw = M("al_maw", (0.34, 0.10, 0.13), rough=0.45, sss=0.4)
eye = M("al_eye", (0.95, 0.72, 0.10), rough=0.05, emit=(1.0, 0.72, 0.08), estr=6.0)
vent = M("al_vent", (0.30, 0.85, 0.35), rough=0.2, emit=(0.25, 1.0, 0.35), estr=3.0)

# Tall and lean, reeling backward with its head thrown back and one arm coming
# up too late. The first pass had it crouched, and a crouched creature next to a
# standing man is a shape on the floor: whatever detail goes into it, the
# silhouette says "sack". Height is what makes it read as a threat losing a
# fight rather than as scenery.
AX, AY = 0.0, 0.0   # built at the origin, placed as one object below

ab("al_pelvis", (AX, AY, 0.92), (0.40, 0.34, 0.28), hide, bevel=0.06)
atap("al_waist", (AX, AY, 1.02), (AX - 0.10, AY, 1.36), 0.21, 0.25, hide)
asph("al_thorax", (AX - 0.14, AY, 1.62), 0.31, hide, scale=(1.05, 0.80, 1.25))
ab("al_plate_a", (AX - 0.26, AY, 1.70), (0.40, 0.30, 0.52), carapace,
   bevel=0.06, rot=(0, math.radians(-14), 0))
ab("al_plate_b", (AX - 0.12, AY, 1.22), (0.36, 0.28, 0.34), carapace,
   bevel=0.05, rot=(0, math.radians(-6), 0))
ab("al_collar", (AX - 0.20, AY, 1.90), (0.44, 0.34, 0.14), carapace, bevel=0.05,
   rot=(0, math.radians(-18), 0))
for i, sy in enumerate((-1, 1)):       # dorsal vents: the only bright thing on it
    ab(f"al_vent{i}", (AX - 0.34, AY + sy * 0.13, 1.74), (0.10, 0.10, 0.30), vent,
       bevel=0.02, rot=(0, math.radians(-14), 0))

# head: thrown back and up, jaw open. Set high so it clears the shoulders —
# an alien whose head sits between its shoulders has no face from any angle.
HX, HY, HZ = AX - 0.30, AY, 2.14
asph("al_skull", (HX, HY, HZ), 0.22, hide, scale=(0.92, 0.86, 1.15),
     rot=(0, math.radians(28), 0))
ab("al_crest", (HX - 0.11, HY, HZ + 0.20), (0.24, 0.26, 0.22), carapace,
   bevel=0.05, rot=(0, math.radians(24), 0))
for c in (-1, 1):                      # swept horns off the crest
    atap(f"al_horn{c}", (HX - 0.10, HY + c * 0.13, HZ + 0.24),
         (HX - 0.40, HY + c * 0.24, HZ + 0.42), 0.045, 0.008, claw, verts=8)
atap("al_snout", (HX + 0.06, HY, HZ + 0.02), (HX + 0.30, HY, HZ + 0.16), 0.16, 0.09, hide)
ab("al_jaw_u", (HX + 0.22, HY, HZ + 0.14), (0.26, 0.17, 0.07), hide_d,
   bevel=0.02, rot=(0, math.radians(-30), 0))
ab("al_jaw_l", (HX + 0.20, HY, HZ - 0.03), (0.24, 0.16, 0.07), hide_d,
   bevel=0.02, rot=(0, math.radians(-2), 0))
ab("al_maw", (HX + 0.21, HY, HZ + 0.055), (0.20, 0.13, 0.13), maw, bevel=0.02)
for i in range(5):
    t = i * 0.045
    ab(f"al_tooth_u{i}", (HX + 0.16 + t, HY + 0.05 - i * 0.02, HZ + 0.12 + t * 0.5),
       (0.03, 0.03, 0.06), claw, bevel=0)
    ab(f"al_tooth_l{i}", (HX + 0.16 + t, HY + 0.05 - i * 0.02, HZ - 0.005 + t * 0.2),
       (0.03, 0.03, 0.06), claw, bevel=0)
for i, dz in enumerate((0.14, 0.06, -0.02)):   # three eyes down each temple
    for sy in (-1, 1):
        asph(f"al_eye{i}{sy}", (HX + 0.02 - i * 0.05, HY + sy * 0.17, HZ + dz),
             0.042 - i * 0.007, eye)

# arms: long. The near one is up between its face and the crowbar; the far one
# is down and braced, catching its own weight as it falls back.
for sy, hi in ((1, True), (-1, False)):
    sx, sy0 = AX - 0.14, AY + sy * 0.28
    if hi:
        elbow = (sx + 0.16, sy0 + 0.12, 2.16)
        wrist = (sx + 0.44, sy0 + 0.04, 2.44)
    else:
        elbow = (sx - 0.24, sy0 - 0.10, 1.16)
        wrist = (sx - 0.48, sy0 - 0.16, 0.72)
    atap(f"al_uarm{sy}", (sx, sy0, 1.76), elbow, 0.105, 0.078, hide)
    asph(f"al_elbow{sy}", elbow, 0.095, carapace)
    atap(f"al_farm{sy}", elbow, wrist, 0.078, 0.056, hide)
    asph(f"al_wrist{sy}", wrist, 0.068, hide)
    for c in range(3):
        a = (c - 1) * 0.44
        d = 0.22
        tip = ((wrist[0] + d * math.cos(a)) if hi else (wrist[0] - d * math.cos(a)),
               wrist[1] + d * math.sin(a) * 0.6,
               wrist[2] + (0.14 if hi else -0.12))
        atap(f"al_claw{sy}{c}", wrist, tip, 0.034, 0.007, claw, verts=8)

# legs: digitigrade, one skidding out from under it
for sy in (-1, 1):
    hip = (AX + 0.02, AY + sy * 0.19, 0.90)
    knee = (AX + 0.34 if sy > 0 else AX + 0.20, AY + sy * 0.25, 0.56)
    ankle = (AX + 0.02 if sy > 0 else AX - 0.10, AY + sy * 0.23, 0.24)
    atap(f"al_thigh{sy}", hip, knee, 0.15, 0.105, hide)
    asph(f"al_knee{sy}", knee, 0.105, carapace)
    atap(f"al_shin{sy}", knee, ankle, 0.095, 0.065, hide)
    ab(f"al_foot{sy}", (ankle[0] + 0.12, ankle[1], 0.07), (0.38, 0.19, 0.13), hide_d,
       bevel=0.04)
    for c in (-1, 1):
        atap(f"al_toe{sy}{c}", (ankle[0] + 0.24, ankle[1] + c * 0.06, 0.07),
             (ankle[0] + 0.40, ankle[1] + c * 0.09, 0.035), 0.042, 0.010, claw, verts=8)
ab("al_tail", (AX - 0.44, AY, 0.66), (0.72, 0.15, 0.15), hide_d, bevel=0.05,
   rot=(0, math.radians(-26), 0))

# Join it and place it as one object. Building it in place and trying to aim it
# by hand meant choosing between "facing him" and "recoiling from him" — the
# head offset controls both. As a single object the two are separate: yaw makes
# it face the blow, pitch makes it fall away from it.
bpy.ops.object.select_all(action="DESELECT")
for o in alien_parts:
    o.select_set(True)
bpy.context.view_layer.objects.active = alien_parts[0]
bpy.ops.object.join()
alien = bpy.context.active_object
alien.name = "Alien"
alien.rotation_mode = "XYZ"
alien.rotation_euler = (0, math.radians(9), 0)   # tipped back, away from the blow
alien.location = (-1.36, 0.05, 0.0)
alien.scale = (1.05, 1.05, 1.05)

# ------------------------------------------------------------------- set
ground = None
bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
ground = bpy.context.active_object
ground.name = "floor"
gm = M("kv_floor", (0.055, 0.052, 0.050), rough=0.92)
ground.data.materials.append(gm)

wallm = M("kv_wall", (0.085, 0.082, 0.078), rough=0.95)
pipem = M("kv_pipe", (0.14, 0.145, 0.15), rough=0.5, metal=0.7)
warn = M("kv_warn", (0.42, 0.30, 0.03), rough=0.85)
ab("wall_back", (-1.4, 2.8, 1.7), (11.0, 0.3, 3.4), wallm, bevel=0.04)
ab("wall_l", (-5.4, 0.4, 1.7), (0.3, 5.0, 3.4), wallm, bevel=0.04)
ab("wall_stripe", (-5.22, 0.4, 0.58), (0.06, 5.0, 0.30), warn, bevel=0)
ab("wall_stripe2", (-1.4, 2.62, 0.58), (11.0, 0.06, 0.30), warn, bevel=0)
for i in range(4):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.10, depth=8.4,
                                        location=(-1.4, 2.52, 2.55 - i * 0.26),
                                        rotation=(0, math.radians(90), 0))
    p = bpy.context.active_object
    p.data.materials.append(pipem)
    bpy.ops.object.shade_smooth()

# --------------------------------------------------------------- lighting
# A hard warm key from the protagonist's side and a cold rim from behind the
# alien: the two characters are lit by different lights so they separate from
# each other, which is the only thing that keeps a two-figure composition
# readable when both are dark green.
def lamp(name, kind, loc, energy, color, size=1.0, rot=(0, 0, 0)):
    bpy.ops.object.light_add(type=kind, location=loc, rotation=rot)
    l = bpy.context.active_object
    l.name = name
    l.data.energy = energy
    l.data.color = color
    if kind == "AREA":
        l.data.size = size
    elif kind == "SPOT":
        l.data.spot_size = math.radians(60)
        l.data.spot_blend = 0.4
    return l


lamp("key", "AREA", (3.4, -3.0, 3.4), 900, (1.0, 0.72, 0.42), size=2.4,
     rot=(math.radians(56), 0, math.radians(48)))
lamp("rim", "AREA", (-3.6, 2.2, 2.6), 700, (0.35, 0.62, 1.0), size=2.0,
     rot=(math.radians(72), 0, math.radians(-130)))
lamp("under", "AREA", (-1.4, -1.4, 0.5), 160, (0.30, 1.0, 0.40), size=1.2,
     rot=(math.radians(-40), 0, math.radians(-20)))
lamp("fill", "AREA", (1.0, -4.0, 1.2), 90, (0.5, 0.55, 0.7), size=4.0,
     rot=(math.radians(80), 0, 0))

world = bpy.data.worlds.new("kv")
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.020, 0.024, 0.032, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0

# ----------------------------------------------------------------- camera
# Low and to the front-left of the protagonist: low because looking up at a
# raised crowbar is what makes it read as raised, and off to the side because
# straight on would put him directly in front of the thing he is hitting.
bpy.ops.object.camera_add(location=(2.35, -3.75, 1.28))
cam = bpy.context.active_object
cam.rotation_euler = (math.radians(86), 0, math.radians(33))
cam.data.lens = 42
cam.data.dof.use_dof = True
cam.data.dof.focus_distance = 3.9
cam.data.dof.aperture_fstop = 2.6

sc = bpy.context.scene
sc.camera = cam
sc.render.engine = "CYCLES"
sc.cycles.samples = 220
sc.cycles.use_denoising = True
sc.render.resolution_x = 1600
sc.render.resolution_y = 900
sc.render.film_transparent = False
sc.view_settings.view_transform = "Filmic"
sc.view_settings.look = "High Contrast"
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
sc.render.filepath = f"{BASE}/renders/keyart_crowbar.png"
bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/keyart.blend")
bpy.ops.render.render(write_still=True)
print("KEYART DONE ->", sc.render.filepath)
