"""Build the drivable vehicles: a military cargo truck, a wheeled APC and a tank.

The trucks in the game were six boxes and four cylinders assembled in C# at
build time, which is why they read as crates on wheels. Nothing about that was
fixable by downloading models — the freely-licensed vehicle packs are civilian
and cartoon-shaped, and a rounded ambulance next to a HECU rifleman looks worse
than the boxes did. So these are built the same way the characters were.

What separates a vehicle that reads as real from one that does not, roughly in
order of how far away it reads:

    proportion      a cab is shorter and narrower than its load bed
    wheel arches    the single strongest cue that a thing is a vehicle
    the greeble     mirrors, exhaust stack, steps, tow hitch, jerry cans;
                    individually invisible, collectively the difference
    tread           a wheel is a cylinder, a tyre is a cylinder with lugs

Each vehicle exports its own FBX with the origin on the ground between the
wheels, so Unity can drop one in without a wrapper transform, and the driving
code keeps working unchanged.

Run:  blender --background --python build_vehicles.py -- <project_dir> <unity_models_dir> [truck|apc|tank|all]
"""
import bpy
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BASE = args[0] if args else "."
UNITY = args[1] if len(args) > 1 else "."
WHICH = args[2] if len(args) > 2 else "all"

parts = []


# ---------------------------------------------------------------- materials
def M(name, color, rough=0.8, metal=0.0, emit=None, estr=0.0, grime=0.0):
    """Vehicle paint that has been outdoors.

    Same trick as the characters: occlusion darkens every seam and panel gap,
    and a height gradient means the sills and wheel arches are filthy while the
    roof is merely dull. On a vehicle the height cue does most of the work,
    because road grime really does climb from the bottom up.
    """
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = estr
    m.diffuse_color = (*color, 1)

    if grime <= 0.0 or emit:
        b.inputs["Base Color"].default_value = (*color, 1)
        b.inputs["Roughness"].default_value = rough
        return m

    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.samples = 8
    ao.only_local = False
    ao.inputs["Distance"].default_value = 0.35
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    nt.links.new(ao.outputs["AO"], inv.inputs[1])

    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Position"], sep.inputs["Vector"])
    hgt = nt.nodes.new("ShaderNodeMapRange")
    hgt.inputs["From Min"].default_value = 0.05
    hgt.inputs["From Max"].default_value = 2.60
    hgt.inputs["To Min"].default_value = 1.0
    hgt.inputs["To Max"].default_value = 0.0
    hgt.clamp = True
    nt.links.new(sep.outputs["Z"], hgt.inputs["Value"])

    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 3.5
    noise.inputs["Detail"].default_value = 7.0

    acc = nt.nodes.new("ShaderNodeMath")
    acc.operation = "ADD"
    nt.links.new(hgt.outputs[0], acc.inputs[0])
    nt.links.new(inv.outputs[0], acc.inputs[1])

    nz = nt.nodes.new("ShaderNodeMath")
    nz.operation = "MULTIPLY_ADD"
    nz.inputs[1].default_value = 0.5
    nz.inputs[2].default_value = -0.2
    nt.links.new(noise.outputs["Fac"], nz.inputs[0])

    tot = nt.nodes.new("ShaderNodeMath")
    tot.operation = "ADD"
    tot.use_clamp = True
    nt.links.new(acc.outputs[0], tot.inputs[0])
    nt.links.new(nz.outputs[0], tot.inputs[1])

    gain = nt.nodes.new("ShaderNodeMath")
    gain.operation = "MULTIPLY"
    gain.use_clamp = True
    gain.inputs[1].default_value = grime
    nt.links.new(tot.outputs[0], gain.inputs[0])

    ramp = nt.nodes.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    e[0].position = 0.0
    e[0].color = (*color, 1)
    e[1].position = 1.0
    e[1].color = (*[c * 0.25 + g * 0.75 for c, g in
                    zip(color, (0.10, 0.085, 0.065))], 1)
    nt.links.new(gain.outputs[0], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])

    rr = nt.nodes.new("ShaderNodeValToRGB")
    re = rr.color_ramp.elements
    re[0].color = (rough,) * 3 + (1,)
    re[1].color = (min(1.0, rough + 0.28),) * 3 + (1,)
    nt.links.new(gain.outputs[0], rr.inputs["Fac"])
    nt.links.new(rr.outputs["Color"], b.inputs["Roughness"])
    return m


# ------------------------------------------------------------------ shapes
def _finish(ob, mt, bevel, smooth=False):
    if bevel:
        md = ob.modifiers.new("bev", "BEVEL")
        md.width = bevel
        md.segments = 2
        md.limit_method = "ANGLE"
        md.angle_limit = math.radians(40)
    ob.data.materials.append(mt)
    if smooth:
        try:
            bpy.ops.object.shade_auto_smooth(angle=math.radians(45))
        except Exception:
            bpy.ops.object.shade_smooth()
    else:
        bpy.ops.object.shade_flat()
    parts.append(ob)
    return ob


def box(name, loc, dims, mt, bevel=0.02, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector(dims)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(ob, mt, bevel)


def cyl(name, loc, r, h, mt, verts=16, rot=(0, 0, 0), bevel=0.01, smooth=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h,
                                        location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    return _finish(ob, mt, bevel, smooth=smooth)


def wedge(name, loc, dims, mt, taper=0.5, bevel=0.02, rot=(0, 0, 0)):
    """A truncated pyramid: a box whose top face is `taper` of its base.

    Turret sides, superstructure, engine covers. The first version of this took
    an axis argument and got the order of operations wrong — Blender applies
    scale in local space *before* rotation, so scaling a shape that was going to
    be rotated 90 degrees stretched it along the wrong axis and turned the APC's
    hull roof into an eight-metre pyramid. It now bakes the orientation in
    first, so local axes are world axes by the time `dims` is applied and the
    numbers mean what they say.

    Sloped *plates* — a glacis, a bow — are not this shape. They are a flat slab
    at an angle, so they are a `box` with a rotation, which is also what they
    are in real life.
    """
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.7071,
                                    radius2=0.7071 * taper, depth=1.0,
                                    location=loc, rotation=(0, 0, math.radians(45)))
    ob = bpy.context.active_object
    ob.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    ob.scale = Vector(dims)
    ob.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return _finish(ob, mt, bevel)


def wheel(name, x, y, r=0.52, width=0.34, mt=None, tread=None, lugs=12, hub=None):
    """A tyre, not a cylinder.

    The lugs are twelve small boxes around the circumference. At any distance
    they resolve into a texture rather than individual blocks, and their whole
    job is that the wheel stops looking turned on a lathe.
    """
    cyl(f"{name}_tyre", (x, y, r), r, width, mt, verts=20,
        rot=(0, math.radians(90), 0), bevel=0.04)
    if hub is not None:
        cyl(f"{name}_hub", (x, y, r), r * 0.46, width * 1.06, hub, verts=12,
            rot=(0, math.radians(90), 0), bevel=0.015)
        for i in range(5):
            a = i / 5 * math.tau
            box(f"{name}_nut{i}", (x + width * 0.55 * (1 if x > 0 else -1),
                                   y + math.cos(a) * r * 0.22,
                                   r + math.sin(a) * r * 0.22),
                (0.03, 0.05, 0.05), hub, bevel=0.004)
    if tread is not None:
        for i in range(lugs):
            a = i / lugs * math.tau
            box(f"{name}_lug{i}", (x, y + math.cos(a) * r * 0.98,
                                   r + math.sin(a) * r * 0.98),
                (width * 1.02, 0.09, 0.07), tread, bevel=0.012,
                rot=(-a, 0, 0))


def light(name, x, y, z, mt, r=0.10, guard=None):
    cyl(name, (x, y, z), r, 0.10, mt, verts=12, rot=(math.radians(90), 0, 0))
    if guard is not None:
        for i in range(3):
            box(f"{name}_bar{i}", (x, y - 0.07, z - r + (i + 0.5) * r * 2 / 3),
                (r * 2.1, 0.02, 0.022), guard, bevel=0)
        cyl(f"{name}_ring", (x, y - 0.07, z), r * 1.14, 0.03, guard, verts=14,
            rot=(math.radians(90), 0, 0))


# =============================================================== the truck
def build_truck():
    """A 6x6 cargo truck. Bonnet, crew cab, canvas tilt over hoops."""
    olive = M("veh_olive", (0.155, 0.170, 0.110), rough=0.88, grime=0.75)
    olive_d = M("veh_olive_d", (0.105, 0.118, 0.078), rough=0.9, grime=0.8)
    canvas = M("veh_canvas", (0.255, 0.240, 0.170), rough=1.0, grime=0.7)
    steel = M("veh_steel", (0.36, 0.37, 0.40), rough=0.42, metal=0.85, grime=0.5)
    dark = M("veh_dark", (0.045, 0.045, 0.050), rough=0.92, grime=0.4)
    tyre = M("veh_tyre", (0.055, 0.055, 0.058), rough=0.97, grime=0.35)
    glass = M("veh_glass", (0.10, 0.14, 0.16), rough=0.10, metal=0.2)
    lamp = M("veh_lamp", (0.95, 0.92, 0.75), rough=0.1,
             emit=(1.0, 0.94, 0.72), estr=2.4)
    rear = M("veh_rear", (0.6, 0.05, 0.04), rough=0.2, emit=(0.9, 0.05, 0.04), estr=1.6)

    # ---- ladder chassis. Two rails and cross-members, visible under the body,
    # which is what makes it read as a truck rather than a bus.
    for s in (-1, 1):
        box(f"rail{s}", (s * 0.46, 0.15, 0.62), (0.14, 6.10, 0.22), steel, bevel=0.012)
    for i, y in enumerate((-2.5, -1.1, 0.4, 1.9, 3.0)):
        box(f"crossmem{i}", (0, y, 0.60), (1.02, 0.14, 0.14), steel, bevel=0.01)
    box("fuel_tank", (-0.70, 0.20, 0.66), (0.26, 1.5, 0.42), steel, bevel=0.06)
    cyl("air_tank", (0.72, 0.10, 0.70), 0.16, 0.9, steel, verts=12,
        rot=(0, math.radians(90), 0))

    # ---- bonnet and front. A cab-over would be one box; a bonnet is two
    # volumes with a step between them and instantly reads as a lorry.
    box("bonnet", (0, -2.28, 1.28), (1.72, 1.30, 0.62), olive, bevel=0.05)
    box("bonnet_slope", (0, -2.92, 1.40), (1.70, 0.52, 0.30), olive, bevel=0.04,
        rot=(math.radians(-38), 0, 0))
    box("grille", (0, -2.94, 1.12), (1.44, 0.14, 0.52), dark, bevel=0.02)
    for i in range(6):
        box(f"grille_bar{i}", (0, -3.00, 0.92 + i * 0.085), (1.40, 0.06, 0.035),
            steel, bevel=0)
    box("bumper", (0, -3.16, 0.72), (2.00, 0.22, 0.24), steel, bevel=0.03)
    for s in (-1, 1):
        box(f"towhook{s}", (s * 0.42, -3.30, 0.72), (0.12, 0.24, 0.16), steel, bevel=0.02)
        light(f"head{s}", s * 0.68, -3.02, 1.20, lamp, r=0.14, guard=steel)
        box(f"fender{s}", (s * 0.98, -2.30, 1.04), (0.30, 1.66, 0.09), olive_d, bevel=0.03)
        box(f"fender_lip{s}", (s * 1.12, -2.30, 0.96), (0.06, 1.66, 0.20), olive_d, bevel=0.02)

    # ---- cab: doors with frames, mirrors, steps, roof hatch
    box("cab", (0, -1.20, 1.62), (1.94, 1.30, 1.30), olive, bevel=0.05)
    box("cab_roof", (0, -1.20, 2.30), (1.98, 1.34, 0.10), olive_d, bevel=0.03)
    box("windshield", (0, -1.86, 1.92), (1.66, 0.10, 0.62), glass, bevel=0.02,
        rot=(math.radians(-8), 0, 0))
    box("wiper", (0, -1.92, 1.62), (0.9, 0.04, 0.03), dark, bevel=0)
    box("hatch", (0, -1.05, 2.38), (0.70, 0.70, 0.08), olive_d, bevel=0.02)
    for s in (-1, 1):
        box(f"door{s}", (s * 0.99, -1.16, 1.58), (0.06, 1.02, 1.10), olive_d, bevel=0.02)
        box(f"doorwin{s}", (s * 1.02, -1.30, 1.92), (0.04, 0.62, 0.44), glass, bevel=0.01)
        box(f"handle{s}", (s * 1.05, -0.86, 1.56), (0.05, 0.16, 0.05), steel, bevel=0.01)
        # mirror on an arm — small, and one of the strongest "this is a truck"
        # cues there is
        box(f"mirror_arm{s}", (s * 1.14, -1.74, 2.02), (0.22, 0.05, 0.05), steel, bevel=0.01)
        box(f"mirror{s}", (s * 1.26, -1.74, 1.86), (0.06, 0.16, 0.34), dark, bevel=0.02)
        box(f"step{s}", (s * 1.00, -1.16, 0.80), (0.30, 0.52, 0.06), steel, bevel=0.01)
    # exhaust stack up the back of the cab
    cyl("stack", (0.88, -0.62, 2.10), 0.09, 1.85, steel, verts=12)
    cyl("stack_cap", (0.88, -0.62, 3.06), 0.11, 0.12, dark, verts=12)

    # ---- cargo bed with a canvas tilt over hoops
    box("bed_floor", (0, 1.35, 1.04), (2.00, 3.60, 0.14), olive_d, bevel=0.02)
    for s in (-1, 1):
        box(f"bed_side{s}", (s * 0.97, 1.35, 1.36), (0.10, 3.60, 0.56), olive, bevel=0.02)
    box("bed_front", (0, -0.42, 1.36), (2.00, 0.10, 0.56), olive, bevel=0.02)
    box("tailgate", (0, 3.12, 1.30), (2.00, 0.10, 0.50), olive_d, bevel=0.02)
    for i in range(5):                       # tilt hoops
        y = -0.30 + i * 0.86
        cyl(f"hoop{i}_l", (-0.92, y, 2.00), 0.045, 1.15, steel, verts=8)
        cyl(f"hoop{i}_r", (0.92, y, 2.00), 0.045, 1.15, steel, verts=8)
        cyl(f"hoop{i}_top", (0, y, 2.56), 0.045, 1.84, steel, verts=8,
            rot=(0, math.radians(90), 0))
    box("tilt", (0, 1.35, 2.30), (1.96, 3.56, 1.10), canvas, bevel=0.10)
    box("tilt_roof", (0, 1.35, 2.60), (2.04, 3.62, 0.12), canvas, bevel=0.05)
    for i in range(6):                       # lashing ropes down the side
        for s in (-1, 1):
            box(f"lash{i}{s}", (s * 1.00, -0.05 + i * 0.58, 1.78),
                (0.03, 0.05, 0.55), dark, bevel=0)

    # ---- kit strapped to the outside: this is where a truck gets its character
    box("spare_mount", (-1.02, 0.10, 1.28), (0.10, 0.62, 0.62), steel, bevel=0.02)
    wheel("spare", -1.28, 0.10, r=0.50, width=0.30, mt=tyre, tread=tyre, hub=steel)
    for i in range(2):
        box(f"jerry{i}", (1.04, 2.30 + i * 0.42, 1.52), (0.14, 0.36, 0.46),
            olive_d, bevel=0.03)
    box("shovel", (-1.03, 2.10, 1.60), (0.05, 1.00, 0.16), steel, bevel=0.01)
    box("tarp_roll", (0, 3.12, 1.66), (1.60, 0.30, 0.30), canvas, bevel=0.12)
    for s in (-1, 1):
        box(f"rearlight{s}", (s * 0.80, 3.20, 1.20), (0.14, 0.06, 0.20), rear, bevel=0.01)
    box("plate", (0, 3.22, 0.92), (0.44, 0.03, 0.16), steel, bevel=0)

    # ---- 6x6: one steering axle forward, two driven at the back
    for x, y in ((-1.02, -2.30), (1.02, -2.30),
                 (-1.02, 1.30), (1.02, 1.30),
                 (-1.02, 2.55), (1.02, 2.55)):
        wheel(f"w{x:.0f}{y:.0f}", x, y, r=0.60, width=0.36,
              mt=tyre, tread=tyre, hub=steel)
        # arch over each wheel, which is the cue that carries furthest
        box(f"arch{x:.0f}{y:.0f}", (x, y, 1.22), (0.44, 1.36, 0.10), olive_d, bevel=0.03)


# ================================================================= the APC
def build_apc():
    """An 8x8 wheeled armoured personnel carrier. Sloped everything."""
    hull = M("apc_hull", (0.175, 0.190, 0.150), rough=0.82, grime=0.6)
    hull_d = M("apc_hull_d", (0.120, 0.132, 0.104), rough=0.86, grime=0.65)
    steel = M("apc_steel", (0.34, 0.35, 0.38), rough=0.4, metal=0.85, grime=0.45)
    dark = M("apc_dark", (0.04, 0.04, 0.045), rough=0.92, grime=0.35)
    tyre = M("apc_tyre", (0.05, 0.05, 0.053), rough=0.97, grime=0.3)
    vision = M("apc_vision", (0.08, 0.12, 0.14), rough=0.08, metal=0.3)
    lamp = M("apc_lamp", (0.95, 0.92, 0.75), rough=0.1, emit=(1.0, 0.94, 0.72), estr=2.2)

    box("hull_mid", (0, 0.20, 1.30), (2.60, 6.40, 0.90), hull, bevel=0.05)
    box("glacis", (0, -3.20, 1.34), (2.58, 1.10, 0.26), hull, bevel=0.04,
        rot=(math.radians(-42), 0, 0))
    box("bow", (0, -3.06, 0.98), (2.56, 0.90, 0.26), hull, bevel=0.04,
        rot=(math.radians(40), 0, 0))
    wedge("hull_upper", (0, 0.10, 1.90), (2.56, 5.90, 0.36), hull, taper=0.86)
    box("hull_rear", (0, 3.44, 1.44), (2.56, 0.24, 1.14), hull_d, bevel=0.04)
    box("ramp", (0, 3.56, 1.24), (1.70, 0.14, 1.30), hull_d, bevel=0.03)
    box("ramp_hinge", (0, 3.58, 0.66), (1.74, 0.16, 0.14), steel, bevel=0.02)
    # side skirts hide the top of the wheels; without them a wheeled APC reads
    # as a van
    for s in (-1, 1):
        box(f"skirt{s}", (s * 1.34, 0.20, 1.06), (0.10, 6.20, 0.52), hull_d, bevel=0.02)
        for i in range(6):
            box(f"stow{s}{i}", (s * 1.36, -2.2 + i * 0.95, 1.86),
                (0.14, 0.72, 0.26), hull_d, bevel=0.03)
        for i in range(4):
            box(f"vision{s}{i}", (s * 1.14, -2.0 + i * 0.9, 2.14),
                (0.06, 0.34, 0.16), vision, bevel=0.01)
        light(f"apc_head{s}", s * 0.92, -3.62, 1.42, lamp, r=0.12, guard=steel)
        box(f"apc_fender{s}", (s * 1.40, -2.60, 1.44), (0.26, 1.10, 0.08), hull_d, bevel=0.02)

    # turret: a small sloped drum with a gun and a commander's hatch
    cyl("turret", (0, -0.30, 2.42), 0.86, 0.62, hull, verts=14, bevel=0.05)
    box("turret_face", (0, -1.02, 2.42), (1.20, 0.50, 0.62), hull, bevel=0.05,
        rot=(math.radians(-20), 0, 0))
    cyl("gun_mantlet", (0, -1.32, 2.42), 0.22, 0.34, steel, verts=12,
        rot=(math.radians(90), 0, 0))
    cyl("gun", (0, -2.30, 2.42), 0.075, 2.00, steel, verts=12,
        rot=(math.radians(90), 0, 0))
    cyl("gun_brake", (0, -3.24, 2.42), 0.105, 0.28, dark, verts=12,
        rot=(math.radians(90), 0, 0))
    cyl("hatch", (0.30, 0.20, 2.78), 0.34, 0.10, hull_d, verts=12)
    box("periscope", (-0.34, -0.14, 2.82), (0.22, 0.16, 0.16), vision, bevel=0.02)
    box("antenna_base", (-0.68, 0.60, 2.20), (0.14, 0.14, 0.16), steel, bevel=0.02)
    cyl("antenna", (-0.68, 0.60, 3.10), 0.022, 1.80, steel, verts=6)
    for i in range(4):                       # smoke dischargers
        for s in (-1, 1):
            cyl(f"smoke{s}{i}", (s * (0.52 + i * 0.14), -0.96, 2.62), 0.055, 0.26,
                dark, verts=8, rot=(math.radians(-70), 0, 0))

    for x, y in ((-1.30, -2.60), (1.30, -2.60), (-1.30, -1.15), (1.30, -1.15),
                 (-1.30, 1.55), (1.30, 1.55), (-1.30, 2.95), (1.30, 2.95)):
        wheel(f"aw{x:.0f}{y:.0f}", x, y, r=0.68, width=0.40,
              mt=tyre, tread=tyre, hub=steel)


# ================================================================ the tank
def build_tank():
    """A tracked main battle tank: hull, running gear, turret, long gun."""
    hull = M("tk_hull", (0.150, 0.162, 0.128), rough=0.84, grime=0.7)
    hull_d = M("tk_hull_d", (0.100, 0.110, 0.086), rough=0.88, grime=0.75)
    steel = M("tk_steel", (0.32, 0.33, 0.36), rough=0.4, metal=0.85, grime=0.5)
    track = M("tk_track", (0.075, 0.072, 0.070), rough=0.9, metal=0.5, grime=0.6)
    dark = M("tk_dark", (0.04, 0.04, 0.045), rough=0.92, grime=0.4)
    canvas = M("tk_canvas", (0.24, 0.225, 0.160), rough=1.0, grime=0.7)
    vision = M("tk_vision", (0.08, 0.12, 0.14), rough=0.08, metal=0.3)
    lamp = M("tk_lamp", (0.95, 0.92, 0.75), rough=0.1, emit=(1.0, 0.94, 0.72), estr=2.2)

    # hull: a low tub with a steeply sloped glacis
    box("tub", (0, 0, 1.02), (3.10, 6.60, 0.72), hull, bevel=0.05)
    box("glacis", (0, -3.46, 1.06), (3.06, 1.30, 0.26), hull, bevel=0.04,
        rot=(math.radians(-58), 0, 0))
    box("bow", (0, -3.22, 0.60), (3.00, 0.60, 0.22), hull, bevel=0.04,
        rot=(math.radians(28), 0, 0))
    box("deck", (0, 0.70, 1.40), (3.06, 5.00, 0.10), hull_d, bevel=0.03)
    box("engine_deck", (0, 2.60, 1.46), (3.00, 1.90, 0.16), hull_d, bevel=0.03)
    for i in range(5):
        box(f"louvre{i}", (0, 2.05 + i * 0.26, 1.56), (2.70, 0.16, 0.06), steel, bevel=0.01)
    box("driver_hatch", (-0.70, -2.60, 1.50), (0.66, 0.66, 0.10), hull_d, bevel=0.03)
    box("driver_scope", (-0.70, -2.88, 1.60), (0.44, 0.14, 0.12), vision, bevel=0.02)

    # running gear: road wheels inside a track run, with a return run on top.
    # Tracks are what the eye checks first on a tank, so they get the detail.
    for s in (-1, 1):
        x = s * 1.44
        for i in range(6):
            y = -2.35 + i * 0.94
            cyl(f"road{s}{i}", (x, y, 0.62), 0.42, 0.34, track, verts=14,
                rot=(0, math.radians(90), 0), bevel=0.03)
        cyl(f"sprocket{s}", (x, 3.12, 0.74), 0.44, 0.34, track, verts=12,
            rot=(0, math.radians(90), 0), bevel=0.03)
        cyl(f"idler{s}", (x, -3.10, 0.70), 0.40, 0.34, track, verts=12,
            rot=(0, math.radians(90), 0), bevel=0.03)
        for i in range(3):
            cyl(f"return{s}{i}", (x, -1.5 + i * 1.6, 1.28), 0.16, 0.28, steel, verts=10,
                rot=(0, math.radians(90), 0), bevel=0.02)
        # the track itself: bottom run, top run, and links round the ends
        box(f"track_bot{s}", (x, 0.0, 0.16), (0.46, 6.60, 0.20), track, bevel=0.03)
        box(f"track_top{s}", (x, 0.0, 1.46), (0.46, 6.30, 0.18), track, bevel=0.03)
        for i in range(7):                     # front curve
            a = math.pi * (0.5 + i / 6 * 0.5)
            box(f"track_f{s}{i}", (x, -3.10 + math.sin(a) * 0.48,
                                   0.70 - math.cos(a) * 0.48),
                (0.46, 0.24, 0.18), track, bevel=0.02, rot=(a, 0, 0))
        for i in range(7):                     # rear curve
            a = math.pi * (1.5 + i / 6 * 0.5)
            box(f"track_r{s}{i}", (x, 3.12 + math.sin(a) * 0.52,
                                   0.74 - math.cos(a) * 0.52),
                (0.46, 0.24, 0.18), track, bevel=0.02, rot=(a, 0, 0))
        box(f"skirt{s}", (x + s * 0.20, 0.0, 1.10), (0.08, 5.60, 0.66), hull_d, bevel=0.02)
        box(f"fender{s}", (x, -0.2, 1.62), (0.60, 5.60, 0.08), hull_d, bevel=0.02)
        for i in range(4):
            box(f"bin{s}{i}", (x, -2.0 + i * 1.3, 1.79), (0.44, 0.88, 0.26),
                hull_d, bevel=0.04)
        light(f"tk_head{s}", s * 1.10, -3.86, 1.28, lamp, r=0.13, guard=steel)

    # turret: sloped, long gun, stowage basket on the back
    box("turret", (0, 0.30, 2.34), (2.16, 2.80, 0.76), hull, bevel=0.06)
    box("turret_front", (0, -1.32, 2.38), (2.14, 0.94, 0.28), hull, bevel=0.05,
        rot=(math.radians(-52), 0, 0))
    box("turret_cheek", (0, -1.16, 2.06), (2.12, 0.80, 0.26), hull, bevel=0.05,
        rot=(math.radians(38), 0, 0))
    box("turret_rear", (0, 1.86, 2.36), (2.06, 0.50, 0.66), hull_d, bevel=0.05,
        rot=(math.radians(24), 0, 0))
    box("basket", (0, 2.48, 2.28), (2.00, 0.72, 0.52), steel, bevel=0.03)
    box("basket_load", (0, 2.48, 2.54), (1.82, 0.60, 0.30), canvas, bevel=0.10)
    cyl("mantlet", (0, -1.84, 2.34), 0.42, 0.50, steel, verts=14,
        rot=(math.radians(90), 0, 0))
    cyl("barrel", (0, -3.66, 2.34), 0.105, 3.20, steel, verts=14,
        rot=(math.radians(90), 0, 0))
    cyl("bore_evac", (0, -3.86, 2.34), 0.20, 0.60, hull, verts=14,
        rot=(math.radians(90), 0, 0))
    cyl("muzzle", (0, -5.20, 2.34), 0.135, 0.34, dark, verts=14,
        rot=(math.radians(90), 0, 0))
    cyl("cupola", (0.52, 0.62, 2.86), 0.42, 0.28, hull, verts=12, bevel=0.03)
    cyl("cupola_hatch", (0.52, 0.62, 3.04), 0.38, 0.10, hull_d, verts=12, bevel=0.02)
    box("mg", (0.52, 0.10, 3.12), (0.10, 0.90, 0.10), dark, bevel=0.02)
    box("loader_hatch", (-0.60, 0.70, 2.76), (0.62, 0.62, 0.10), hull_d, bevel=0.03)
    box("commander_scope", (0.52, 0.20, 3.00), (0.26, 0.18, 0.18), vision, bevel=0.02)
    box("gunner_scope", (-0.52, -1.06, 2.76), (0.30, 0.20, 0.20), vision, bevel=0.02)
    box("antenna_base", (-0.96, 1.90, 2.74), (0.14, 0.14, 0.16), steel, bevel=0.02)
    cyl("antenna", (-0.96, 1.90, 3.68), 0.022, 1.90, steel, verts=6)
    for i in range(4):
        for s in (-1, 1):
            cyl(f"smoke{s}{i}", (s * (0.62 + i * 0.16), -1.26, 2.62), 0.06, 0.28,
                dark, verts=8, rot=(math.radians(-70), 0, 0))


# ================================================================== export
def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for blocks in (bpy.data.meshes, bpy.data.materials):
        for b in list(blocks):
            if b.users == 0:
                blocks.remove(b)


def studio():
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = "ground"
    gm = bpy.data.materials.new("studio_ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.35, 0.35, 0.36, 1)
    ground.data.materials.append(gm)

    bpy.ops.object.light_add(type="SUN", location=(6, -8, 12))
    sun = bpy.context.active_object
    sun.data.energy = 3.4
    sun.rotation_euler = (math.radians(52), 0, math.radians(38))

    bpy.ops.object.camera_add(location=(7.6, -9.4, 4.4))
    cam = bpy.context.active_object
    cam.rotation_euler = (math.radians(74), 0, math.radians(40))
    sc = bpy.context.scene
    sc.camera = cam
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 28
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 1000
    sc.render.resolution_y = 640
    sc.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("w")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.58, 0.62, 1)
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "METAL"
        prefs.get_devices()
        for d in prefs.devices:
            d.use = True
        sc.cycles.device = "GPU"
    except Exception:
        sc.cycles.device = "CPU"
    return ground


def export(name):
    ground = studio()
    os.makedirs(f"{BASE}/renders", exist_ok=True)
    bpy.context.scene.render.filepath = f"{BASE}/renders/vehicle_{name}.png"
    bpy.ops.render.render(write_still=True)

    bpy.ops.object.select_all(action="DESELECT")
    for ob in parts:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = name.capitalize()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    # Put the origin on the ground between the wheels.
    #
    # `join` keeps the ACTIVE object's origin, and the active object is whichever
    # part happened to be built first — for the truck that is a chassis rail
    # 0.62 m up and half a metre off the centreline. Unity then drops that point
    # on the ground and the whole vehicle sinks by exactly that much, which is
    # what buried the wheels. Everything here is authored around x=y=0 with z=0
    # as the ground, so the world origin is the right pivot.
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    tris = sum(len(p.vertices) - 2 for p in joined.data.polygons)

    os.makedirs(UNITY, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    joined.select_set(True)
    bpy.context.view_layer.objects.active = joined
    bpy.ops.export_scene.fbx(
        filepath=f"{UNITY}/{joined.name}.fbx",
        use_selection=True, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y",
        use_space_transform=True, bake_space_transform=True,
        object_types={"MESH"}, mesh_smooth_type="FACE")
    print(f"VEHICLE {name}: {len(joined.data.polygons)} faces, ~{tris} tris")


for which, fn in (("truck", build_truck), ("apc", build_apc), ("tank", build_tank)):
    if WHICH not in ("all", which):
        continue
    clear()
    parts.clear()
    fn()
    export(which)
print("VEHICLES DONE")
