"""Render the game's trailer straight out of the game's own assets.

Built to the structure the trade actually uses:

  * **the hook is the first three seconds** and it is visceral, not
    informational — a parasitised chassis turning its head into camera, in the
    dark. No logo, no studio card. Logo-first wastes the only three seconds
    that decide whether anyone watches the rest.
  * **thirty seconds**, not sixty. Shorter is better when the hook is clear.
  * **the cuts land on the beat.** The music bed is generated at 84 BPM by
    Tools/audio/make_trailer_bed.py, and every shot boundary below is computed
    from that beat rather than eyeballed — which is the whole reason the bed is
    generated instead of licensed.
  * **one idea per shot**, and the title lands on the single loudest moment.

Rendered in EEVEE rather than Cycles: at 720 frames, Cycles would take
somewhere north of a day and the trailer does not need path tracing to sell a
low-poly base at dusk. Blender's built-in FFmpeg writes the MP4 and muxes the
audio, so no external encoder is needed.

Run:  blender --background <project>/blender/huge_map.blend \\
              --python render_trailer.py -- <project_dir> <out.mp4>
"""
import bpy
import math
import os
import sys

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BASE = args[0] if args else "/Users/minhdang_work/halflife-like-game"
OUT = args[1] if len(args) > 1 else os.path.expanduser("~/Desktop/CorvusSprawl_Trailer.mp4")

FPS = 24
BPM = 84.0
BEAT = 60.0 / BPM
BAR = BEAT * 4


def f_at(bars):
    """Frame number of a musical position. Every cut in this file goes through
    here, so the edit cannot drift off the beat."""
    return int(round(bars * BAR * FPS)) + 1


# ------------------------------------------------------------------ assets
def append(blend, name):
    d = f"{BASE}/blender/{blend}/Object/"
    before = set(bpy.data.objects)
    try:
        bpy.ops.wm.append(filepath=d + name, directory=d, filename=name)
    except Exception as e:
        print("  ! append failed:", blend, name, e)
        return None
    new = [o for o in bpy.data.objects if o not in before]
    return new[0] if new else None


def place(ob, loc, rot_z=0.0, scale=1.0, name=None):
    if ob is None:
        return None
    if name:
        ob.name = name
    ob.location = loc
    ob.rotation_mode = "XYZ"
    ob.rotation_euler = (0, 0, rot_z)
    ob.scale = (scale, scale, scale)
    return ob


# The map file is already open. Everything else is brought in beside it.
# NOTE: this is Blender space — the game's Unity z is this file's -y.
terrain = append("terrain.blend", "Terrain")

soldier_a = place(append("soldier_rigged.blend", "Body"),
                  (-14, 352, 0), rot_z=math.radians(190), name="TrailerSoldierA")
soldier_b = place(append("soldier_rigged.blend", "Body"),
                  (6, 344, 0), rot_z=math.radians(205), name="TrailerSoldierB")
robot_hero = place(append("robot_rigged.blend", "Body"),
                   (0, 0, 0), rot_z=math.radians(14), name="TrailerRobotHero")
robot_b = place(append("robot_rigged.blend", "Body"),
                (-152, -168, 0), rot_z=math.radians(150), name="TrailerRobotB")
auditor = place(append("villain_rigged.blend", "Body"),
                (6.5, -5.0, 38.6), rot_z=math.radians(35), name="TrailerAuditor")

truck = place(append("../Project-G1/Assets/G1/Models/Vehicles/Truck.fbx", "Truck"),
              (0, 0, 0)) if False else None
# vehicles live as FBX, not .blend — rebuild them here instead of importing
for fbx, nm, loc, rz in (
        ("Truck.fbx", "TrailerTruck", (18, 250, 0), math.radians(184)),
        ("Tank.fbx", "TrailerTank", (-300, -18, 0), math.radians(96)),
        ("Apc.fbx", "TrailerApc", (-276, -46, 0), math.radians(92))):
    path = f"{BASE}/Project-G1/Assets/G1/Models/Vehicles/{fbx}"
    if not os.path.exists(path):
        print("  ! missing vehicle", path)
        continue
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    if new:
        place(new[0], loc, rot_z=rz, name=nm)


# --------------------------------------------------------------- lighting
for l in [o for o in bpy.data.objects if o.type == "LIGHT"]:
    bpy.data.objects.remove(l, do_unlink=True)

bpy.ops.object.light_add(type="SUN", location=(300, 300, 300))
sun = bpy.context.active_object
sun.data.energy = 2.4
sun.data.color = (1.0, 0.62, 0.33)              # the game's dusk
sun.data.angle = math.radians(2.5)
sun.rotation_euler = (math.radians(74), 0, math.radians(-38))

world = bpy.data.worlds.new("trailer_world")
bpy.context.scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (0.055, 0.052, 0.070, 1)
bg.inputs[1].default_value = 1.0

# a hard rim on the hero robot, so the hook shot has a shape in the dark
bpy.ops.object.light_add(type="AREA", location=(2.4, -3.0, 2.6))
rim = bpy.context.active_object
rim.data.energy = 95
rim.data.color = (0.40, 0.95, 0.35)
rim.data.size = 1.6
rim.rotation_euler = (math.radians(62), 0, math.radians(38))


# ---------------------------------------------------------------- cameras
# Linear, not bezier. An eased camera inside a two-second shot reads as a
# stutter at each end rather than as a move — and Blender 5 restructured
# Actions into layers and slots, so the old trick of walking
# `action.fcurves` afterwards to fix the handles no longer exists. Setting the
# default before inserting anything is both simpler and version-proof.
try:
    bpy.context.preferences.edit.keyframe_new_interpolation_type = "LINEAR"
except Exception as e:
    print("  ! could not set default interpolation:", e)


def shot(name, start_bars, end_bars, eye_a, eye_b, look_a, look_b, lens=38.0):
    """One camera, keyframed from A to B, bound to a timeline marker.

    Markers with a camera attached are how Blender cuts between cameras on a
    single timeline; the alternative is rendering each shot separately and
    concatenating, which needs an external tool this machine does not have.
    """
    cam_data = bpy.data.cameras.new(name)
    cam_data.lens = lens
    cam = bpy.data.objects.new(name, cam_data)
    bpy.context.collection.objects.link(cam)

    tgt = bpy.data.objects.new(name + "_target", None)
    bpy.context.collection.objects.link(tgt)
    con = cam.constraints.new("TRACK_TO")
    con.target = tgt
    con.track_axis = "TRACK_NEGATIVE_Z"
    con.up_axis = "UP_Y"

    f0, f1 = f_at(start_bars), f_at(end_bars)
    for f, eye, look in ((f0, eye_a, look_a), (f1, eye_b, look_b)):
        cam.location = eye
        cam.keyframe_insert("location", frame=f)
        tgt.location = look
        tgt.keyframe_insert("location", frame=f)
    m = bpy.context.scene.timeline_markers.new(name, frame=f0)
    m.camera = cam
    return cam


# Blender space. Unity z = -(this y). The Sprawl fills ±400; the breach ruins
# are at y = -165 here; the south gate at y = +352; Cradle Station at x = 1100.
S = [
    # 1 — HOOK. Two bars, in tight on the parasite, pushing in fast. It is dark
    # and green and it is the only thing on screen.
    ("hook", 0, 2, (1.35, -2.0, 1.85), (0.62, -1.05, 1.78),
     (0, 0, 1.66), (0, 0, 1.70), 62.0),
    # 2 — the place. Hard cut wide, and the scale does the talking.
    ("wide", 2, 4.5, (-470, 470, 190), (-250, 300, 132),
     (0, 0, 14), (0, 0, 10), 34.0),
    # 3 — the breach.
    ("breach", 4.5, 7, (66, -250, 96), (24, -196, 30),
     (0, -165, 6), (0, -165, 3), 40.0),
    # 4 — soldiers on the gate road, low and close.
    ("gate", 7, 9, (-30, 372, 2.6), (-6, 360, 2.2),
     (-14, 352, 1.5), (-10, 350, 1.4), 50.0),
    # 5 — the truck.
    ("truck", 9, 11.5, (44, 262, 5.4), (22, 240, 3.2),
     (18, 250, 1.6), (18, 248, 1.4), 42.0),
    # 6 — armour in the tank park.
    ("armour", 11.5, 14, (-262, -6, 7.6), (-286, -34, 4.4),
     (-300, -18, 1.6), (-292, -30, 1.6), 40.0),
    # 7 — the second base, revealed over the ridge. This is the "there is more"
    # beat and it is the last thing before the title.
    ("cradle", 14, 17, (430, 40, 110), (700, 16, 74),
     (1100, 0, 16), (1100, 0, 10), 46.0),
    # 8 — the Auditor, alone on the tower roof, tiny against the sky.
    ("auditor", 17, 19, (22, -26, 41.4), (13.5, -15.5, 40.4),
     (6.5, -5.0, 39.6), (6.5, -5.0, 39.4), 62.0),
    # 9 — the title, on the impact.
    ("title", 19, 22, (-120, 210, 70), (-96, 186, 62),
     (0, 60, 16), (0, 60, 14), 40.0),
]
for a in S:
    shot(*a)

# ------------------------------------------------------------------ title
# Parented to the title camera, not placed in the world.
#
# World-space 3D text was cropped to "E CORVUS SPRAW" the moment the camera
# was anywhere but the one spot it had been eyeballed from, and it inherited
# the ground's perspective so it sheared across the frame. Hanging it off the
# camera at a fixed local offset means it is centred and level by construction,
# whatever the shot behind it is doing.
title_cam = bpy.data.objects["title"]

tm = bpy.data.materials.new("title_mat")
tm.use_nodes = True
tn = tm.node_tree.nodes["Principled BSDF"]
tn.inputs["Base Color"].default_value = (0.96, 0.95, 0.92, 1)
tn.inputs["Emission Color"].default_value = (0.96, 0.95, 0.92, 1)
tn.inputs["Emission Strength"].default_value = 3.2

def card(body, size, y_off, z_off):
    bpy.ops.object.text_add()
    t = bpy.context.active_object
    t.data.body = body
    t.data.size = size
    t.data.align_x = "CENTER"
    t.data.align_y = "CENTER"
    t.data.materials.append(tm)
    t.parent = title_cam
    t.parent_type = "OBJECT"
    # In front of the lens, and NOT rotated. A text object already lies in its
    # own XY plane with its normal along +Z, and a camera looks down its own -Z
    # — so parented with an identity rotation it is already square to the lens.
    # The 90-degree X spin that felt obviously right stood it on edge, and the
    # title rendered as a thin white bar.
    t.location = (0.0, y_off, z_off)
    t.rotation_euler = (0.0, 0.0, 0.0)
    return t

TITLE_Z = -3.4                      # metres in front of the lens
titles = [card("THE CORVEX", 0.255, 0.15, TITLE_Z),
          card("SOMETHING GOT OUT.  THE ARMY SEALED THE VALLEY.", 0.078, -0.13, TITLE_Z)]

# they exist only for the last three bars
for ob in titles:
    for fr, hide in ((1, True), (f_at(19) - 1, True), (f_at(19), False)):
        ob.hide_viewport = ob.hide_render = hide
        ob.keyframe_insert("hide_render", frame=fr)
        ob.keyframe_insert("hide_viewport", frame=fr)

# ------------------------------------------------------------------ scene
sc = bpy.context.scene
sc.frame_start = 1
sc.frame_end = f_at(22)
sc.render.fps = FPS
sc.render.resolution_x = 1280
sc.render.resolution_y = 720
sc.render.resolution_percentage = 100

for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        sc.render.engine = engine
        break
    except Exception:
        continue
try:
    sc.eevee.taa_render_samples = 24
    sc.eevee.use_bloom = True
except Exception:
    pass

sc.view_settings.view_transform = "Filmic"
sc.view_settings.look = "Medium High Contrast"

# fog, so the 800 m map reads as depth rather than as a diorama
try:
    mist = sc.world.mist_settings
    mist.use_mist = True
    mist.start = 60
    mist.depth = 620
except Exception:
    pass

# --------------------------------------------------------------- the audio
sc.sequence_editor_create()
se = sc.sequence_editor


def sound(path, frame, chan, volume=1.0):
    if not os.path.exists(path):
        print("  ! no audio at", path)
        return None
    try:
        s = se.sequences.new_sound(os.path.basename(path), path, chan, frame)
    except AttributeError:
        s = se.strips.new_sound(os.path.basename(path), path, chan, frame)
    s.volume = volume
    return s


sound(f"{BASE}/Project-G1/renders/trailer_bed.wav", 1, 1, 0.85)

# Three lines, placed in the gaps the bed leaves. Any more and the trailer is
# explaining itself, which is the mistake the research warns about twice.
VO = f"{BASE}/Project-G1/Assets/Resources/Audio/Voice"
import hashlib


def line(text, at_bars, vol=1.35):
    k = hashlib.sha1(text.encode("utf-8")).hexdigest()[:16]
    return sound(os.path.join(VO, k + ".wav"), f_at(at_bars), 2, vol)


line("Something got out of the research station east of here. It came down "
     "this road. Whatever it is, it does not stay in one body.", 2.2)
line("Warning. The army has sealed this valley. They are firing on anyone "
     "leaving it. Forty-one people are still alive inside the wire.", 7.1)
line("The leak is out there, past the far ridge. Shut it off and I can go "
     "home. Do try to keep the survivors alive. It reads better.", 14.2)

# ------------------------------------------------------------------ output
# Blender 5 split still-image and video formats behind `media_type`: until it
# is set to VIDEO, the file_format enum contains only PNG, EXR and friends and
# assigning "FFMPEG" raises. On older builds the property does not exist and
# FFMPEG is available directly.
try:
    sc.render.image_settings.media_type = "VIDEO"
except Exception:
    pass
sc.render.image_settings.file_format = "FFMPEG"
sc.render.ffmpeg.format = "MPEG4"
sc.render.ffmpeg.codec = "H264"
sc.render.ffmpeg.constant_rate_factor = "HIGH"
sc.render.ffmpeg.ffmpeg_preset = "GOOD"
sc.render.ffmpeg.audio_codec = "AAC"
sc.render.ffmpeg.audio_bitrate = 192
sc.render.filepath = OUT
sc.render.use_file_extension = False

print(f"TRAILER: {sc.frame_end} frames at {FPS}fps = "
      f"{sc.frame_end / FPS:.1f}s, {len(S)} shots, engine {sc.render.engine}")
bpy.ops.wm.save_as_mainfile(filepath=f"{BASE}/blender/trailer.blend")
if os.environ.get("G1_TRAILER_STILLS") != "1":
    bpy.ops.render.render(animation=True)
    print("TRAILER DONE ->", OUT)
else:
    print("TRAILER SCENE READY (stills mode)")
