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
def M(name, color, rough=0.7, metal=0.0, emit=None, estr=0.0,
      wear=0.0, grime=0.0, wear_color=None, grime_color=(0.055, 0.048, 0.040)):
    """A material that has been somewhere.

    A flat Principled colour is the last thing keeping this model looking like
    a toy: real equipment is dark in every crevice and rubbed back to bare
    metal on every edge it has been dragged past. Both of those follow from
    one signal — the *curvature* of the surface — so the whole effect comes
    off `Geometry > Pointiness` with no UVs, no textures and no unwrapping:

        crevices (concave)  ->  dirt collects
        flats                ->  the colour as issued
        edges (convex)      ->  paint worn through

    A little noise is added to the mask so the wear is uneven rather than a
    perfect outline of the geometry, and the same mask roughens the dirty
    parts and polishes the worn ones.

    `wear`/`grime` are 0..1 amounts, so the Auditor can be handed 0 for both
    and stay conspicuously immaculate while everyone else is filthy.
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

    if wear <= 0.0 and grime <= 0.0:
        b.inputs["Base Color"].default_value = (*color, 1)
        b.inputs["Roughness"].default_value = rough
        return m

    # Where the dirt is, from two signals that both survive low-poly geometry:
    #
    #   occlusion  — a crevice is a crevice whatever the vertex count, so this
    #                darkens seams, the insides of straps and under every plate
    #   height     — you walk through it, so boots are filthy and the collar is
    #                nearly clean; a vertical gradient does most of the work of
    #                selling a suit as worn
    #
    # (Pointiness was the obvious choice for edge wear and is the wrong tool
    # here: on a beveled cube almost every vertex is a corner, so entire flat
    # panels register as "edge" and the suit bleaches to pale tan.)
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.samples = 8
    ao.only_local = False
    ao.inputs["Distance"].default_value = 0.16

    cav = nt.nodes.new("ShaderNodeMath")        # invert: occluded -> dirty
    cav.operation = "SUBTRACT"
    cav.inputs[0].default_value = 1.0
    nt.links.new(ao.outputs["AO"], cav.inputs[1])

    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Position"], sep.inputs["Vector"])
    height = nt.nodes.new("ShaderNodeMapRange")   # 1 at the boots, 0 at the collar
    height.inputs["From Min"].default_value = 0.05
    height.inputs["From Max"].default_value = 1.62
    height.inputs["To Min"].default_value = 1.0
    height.inputs["To Max"].default_value = 0.0
    height.clamp = True
    nt.links.new(sep.outputs["Z"], height.inputs["Value"])

    noise = nt.nodes.new("ShaderNodeTexNoise")    # patchiness, not a clean ramp
    noise.inputs["Scale"].default_value = 6.0
    noise.inputs["Detail"].default_value = 8.0

    hw = nt.nodes.new("ShaderNodeMath")
    hw.operation = "MULTIPLY"
    hw.inputs[1].default_value = 1.25
    nt.links.new(height.outputs[0], hw.inputs[0])

    cw = nt.nodes.new("ShaderNodeMath")
    cw.operation = "MULTIPLY"
    cw.inputs[1].default_value = 1.30
    nt.links.new(cav.outputs[0], cw.inputs[0])

    acc = nt.nodes.new("ShaderNodeMath")
    acc.operation = "ADD"
    nt.links.new(hw.outputs[0], acc.inputs[0])
    nt.links.new(cw.outputs[0], acc.inputs[1])

    nz = nt.nodes.new("ShaderNodeMath")
    nz.operation = "MULTIPLY_ADD"
    nz.inputs[1].default_value = 0.45
    nz.inputs[2].default_value = -0.18
    nt.links.new(noise.outputs["Fac"], nz.inputs[0])

    dirt = nt.nodes.new("ShaderNodeMath")
    dirt.operation = "ADD"
    dirt.use_clamp = True
    nt.links.new(acc.outputs[0], dirt.inputs[0])
    nt.links.new(nz.outputs[0], dirt.inputs[1])

    gain = nt.nodes.new("ShaderNodeMath")         # per-material susceptibility
    gain.name = gain.label = "G1_DIRT"            # rig_character.py bakes this
    gain.operation = "MULTIPLY"
    gain.use_clamp = True
    gain.inputs[1].default_value = grime
    nt.links.new(dirt.outputs[0], gain.inputs[0])

    ramp = nt.nodes.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    e[0].position = 0.0
    e[0].color = (*color, 1)                      # clean: exactly as issued
    e[1].position = 1.0
    e[1].color = (*[c * 0.14 + g * 0.86 for c, g in zip(color, grime_color)], 1)
    nt.links.new(gain.outputs[0], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])

    rramp = nt.nodes.new("ShaderNodeValToRGB")    # grime is matte
    re = rramp.color_ramp.elements
    re[0].position = 0.0
    re[0].color = (rough,) * 3 + (1,)
    re[1].position = 1.0
    re[1].color = (min(1.0, rough + 0.30),) * 3 + (1,)
    nt.links.new(gain.outputs[0], rramp.inputs["Fac"])
    nt.links.new(rramp.outputs["Color"], b.inputs["Roughness"])
    return m


def _finish(ob, mt, bevel, smooth=False):
    if bevel:
        md = ob.modifiers.new("bev", "BEVEL")
        md.width = bevel
        md.segments = 2
        md.limit_method = "ANGLE"
        md.angle_limit = math.radians(40)
    ob.data.materials.append(mt)
    # Flat shading on a 12-sided cylinder is why limbs read as faceted pipes.
    # Smoothing only the round primitives — and only across shallow angles, so
    # hard edges stay hard — is the cheapest single upgrade to how this looks.
    if smooth:
        try:
            bpy.ops.object.shade_auto_smooth(angle=math.radians(50))
        except Exception:
            bpy.ops.object.shade_smooth()
    else:
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
    _finish(ob, mt, bevel, smooth=True)
    return ob, d.to_track_quat("Z", "Y")


def sph(name, loc, r, mt, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=14, ring_count=10, radius=r, location=loc)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector(scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(ob, mt, bevel=0, smooth=True)


def panel(name, loc, dims, mt):
    """A recessed seam line. Detail at a consistent, small scale is most of
    what separates a shape that reads as manufactured hardware from a shape
    that reads as a primitive with a colour on it."""
    return box(name, loc, dims, mt, bevel=0)


def rivets(name, p1, p2, n, r, mt, side=""):
    """A row of fasteners.

    `side` goes last in the name on purpose: the rig reads left/right off the
    final characters, so `shin_rivet-1` + index would produce `shin_rivet-11`
    and be read as a LEFT part on the right leg.
    """
    p1, p2 = Vector(p1), Vector(p2)
    for i in range(n):
        t = (i + 0.5) / n
        sph(f"{name}{i}{side}", p1 + (p2 - p1) * t, r, mt)


def strap(name, loc, dims, mt, buckle_mt, rot=(0, 0, 0), buckle=True):
    """Flat webbing with a buckle, for running across a chest or a back."""
    box(name, loc, dims, mt, bevel=0.004, rot=rot)
    if buckle:
        box(f"{name}_buckle", (loc[0], loc[1] - dims[1] * 0.75, loc[2]),
            (dims[0] * 0.30, dims[1] * 0.9, dims[2] * 1.4), buckle_mt,
            bevel=0.004, rot=rot)


def band(name, p1, p2, r, mt, mt_buckle=None, verts=12):
    """A strap that wraps a limb.

    Has to be a ring, not a box: a cube scaled to a limb's diameter is a
    square shelf sticking out either side, which is exactly what the first
    pass of this model looked like at the thighs.
    """
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r,
                                        depth=d.length, location=(p1 + p2) / 2)
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = d.to_track_quat("Z", "Y")
    _finish(ob, mt, bevel=0.004, smooth=True)
    if mt_buckle is not None:
        mid = (p1 + p2) / 2
        box(f"{name}_clip", (mid.x, mid.y - r * 0.95, mid.z),
            (r * 0.7, r * 0.35, d.length * 1.5), mt_buckle, bevel=0.003)
    return ob


def taper(name, p1, p2, r1, r2, mt, verts=12):
    """A limb segment that is thicker at one end. Straight cylinders are the
    main reason low-poly limbs read as pipes."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2,
                                    depth=d.length, location=(p1 + p2) / 2)
    ob = bpy.context.active_object
    ob.name = name
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = d.to_track_quat("Z", "Y")
    _finish(ob, mt, bevel=0.006, smooth=True)
    return ob, d.to_track_quat("Z", "Y")


def oval(name, z0, z1, w0, d0, w1, d1, mt, verts=20, y=0.0):
    """One ring-to-ring section of a torso: elliptical in cross-section and
    tapered along its length.

    A chest is not a rectangle seen from above and it is not a circle either.
    Stacking a few of these — hips narrow, waist pinched, chest broad and
    flatter front-to-back — is what stops the body reading as a filing
    cabinet, which is exactly what three stacked cubes looked like.
    """
    h = z1 - z0
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=1.0,
                                    radius2=max(0.02, w1 / w0), depth=h,
                                    location=(0, y, (z0 + z1) / 2))
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = Vector((w0, (d0 + d1) / 2, 1.0))
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(ob, mt, bevel=0, smooth=True)


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
    """The hazard-suit engineer.

    Rebuilt to a grounded near-future industrial look rather than a primitive
    with a colour on it. Four ideas do most of the work:

      * Layered construction — a soft ribbed under-suit with hard shell plates
        bolted over it, so edges and seams exist to catch light. The old model
        was one flat orange on plain boxes.
      * Material contrast — matte rubber, brushed steel, worn webbing, tinted
        glass. Silhouette reads at distance, materials read up close.
      * A sealed respirator helmet instead of a bare face. A low-poly face is
        a box with a nose box on it and always looks it; a visor and a filter
        block give a distinctive silhouette and suit the fiction better.
      * An asymmetric loadout — the wrist unit on one arm, a plated bracer on
        the other, pouches placed unevenly. Perfect mirror symmetry is the
        tell of a model rather than of issued equipment.

    Joint positions are unchanged, because rig_character.py binds each part
    rigidly to one bone and the animations are built against that skeleton.
    """
    # Wear amounts are the characterisation: painted plate is scuffed to bare
    # metal on every edge and filthy in every seam, webbing just gets dirty
    # (fabric does not rub back to metal), and machined trim polishes rather
    # than wears.
    shell = M("suit_shell", (0.58, 0.19, 0.035), rough=0.45,
              wear=0.45, grime=0.68, wear_color=(0.46, 0.44, 0.41))
    shell_w = M("suit_worn", (0.40, 0.135, 0.03), rough=0.74,
                wear=0.55, grime=0.80, wear_color=(0.44, 0.42, 0.39))
    under = M("suit_under", (0.075, 0.075, 0.085), rough=0.92,
              wear=0.20, grime=0.72, wear_color=(0.20, 0.20, 0.21))
    rubber = M("rubber", (0.035, 0.035, 0.04), rough=0.95,
               wear=0.25, grime=0.62, wear_color=(0.16, 0.16, 0.17))
    webbing = M("webbing", (0.24, 0.22, 0.15), rough=0.9,
                wear=0.15, grime=0.88, wear_color=(0.32, 0.30, 0.24))
    steel = M("steel", (0.44, 0.45, 0.48), rough=0.34, metal=0.9,
              wear=0.40, grime=0.58, wear_color=(0.66, 0.67, 0.69))
    alu = M("alu_trim", (0.66, 0.67, 0.70), rough=0.28, metal=1.0,
            wear=0.35, grime=0.46, wear_color=(0.82, 0.83, 0.85))
    visor = M("visor", (0.03, 0.05, 0.07), rough=0.12)
    lamp = M("suit_lamp", (0.2, 0.9, 0.95), rough=0.2,
             emit=(0.15, 0.85, 0.95), estr=2.2)
    screen = M("screen", (0.05, 0.30, 0.08), rough=0.3,
               emit=(0.20, 0.85, 0.30), estr=1.4)
    pale = M("emblem_ring", (0.85, 0.83, 0.78), rough=0.4)

    # ---------------------------------------------------------------- torso
    # The body itself: a stack of elliptical, tapered rings from crotch to
    # collar. Narrow at the hips, pinched at the waist, broad and flatter
    # front-to-back at the chest — the proportions of a person rather than of
    # a stack of crates.
    oval("crotch", 0.855, 0.955, 0.130, 0.115, 0.168, 0.126, under)
    oval("pelvis", 0.955, 1.055, 0.168, 0.126, 0.156, 0.116, under)
    oval("abdomen_seg_a", 1.055, 1.165, 0.156, 0.116, 0.140, 0.106, under)
    oval("abdomen_seg_b", 1.165, 1.255, 0.140, 0.106, 0.163, 0.116, under)
    oval("chest_seg_a", 1.255, 1.360, 0.163, 0.116, 0.192, 0.128, under)
    oval("chest_seg_b", 1.360, 1.470, 0.192, 0.128, 0.176, 0.118, under)
    box("pelvis_plate", (0, -0.104, 1.005), (0.26, 0.05, 0.12), shell, bevel=0.02)
    for s in (-1, 1):                       # hip plates
        box(f"hipplate{s}", (0.155 * s, -0.015, 0.985), (0.05, 0.17, 0.13),
            shell, bevel=0.015)

    for i in range(3):                      # ribbed under-suit at the waist
        panel(f"abdomen_rib{i}", (0, -0.104, 1.075 + i * 0.048),
              (0.24, 0.018, 0.015), rubber)

    # one plate, seated flush on the chest. Splitting it in two and tilting
    # them left a gap you could see daylight through — it read as a picture
    # frame hovering off the ribs rather than as armour bolted to a torso.
    # Armour follows the ribcage: a centre panel plus two side panels angled
    # back around the curve. One flat slab across a round chest always reads
    # as a clipboard taped to the front.
    box("chest_plate", (0, -0.108, 1.352), (0.175, 0.040, 0.215), shell,
        bevel=0.026)
    for sgn in (-1, 1):
        box(f"chest_plate_side{sgn}", (0.128 * sgn, -0.076, 1.348),
            (0.115, 0.040, 0.195), shell, bevel=0.024,
            rot=(0, 0, math.radians(38) * sgn))
        panel(f"chest_plate_gap{sgn}", (0.083 * sgn, -0.102, 1.348),
              (0.012, 0.030, 0.19), shell_w)
    box("chest_plate_top", (0, -0.092, 1.451), (0.285, 0.075, 0.042), shell,
        bevel=0.018, rot=(0.5, 0, 0))
    panel("chest_seam", (0, -0.130, 1.262), (0.30, 0.012, 0.012), shell_w)
    rivets("chest_rivet", (-0.125, -0.128, 1.448), (0.125, -0.128, 1.448), 6,
           0.008, alu)

    # collar ring: the seal the helmet locks into
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.115, depth=0.05,
                                        location=(0, 0.005, 1.485))
    _finish(bpy.context.active_object, steel, bevel=0.006)
    bpy.context.active_object.name = "collar_ring"
    box("collar_clamp", (0, -0.10, 1.487), (0.09, 0.05, 0.055), alu, bevel=0.008)

    # chest emblem, recessed into the plate
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.055, depth=0.016,
                                        location=(0, -0.136, 1.335),
                                        rotation=(math.pi / 2, 0, 0))
    _finish(bpy.context.active_object, pale, bevel=0)
    bpy.context.active_object.name = "emblem_outer"
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.034, depth=0.02,
                                        location=(0, -0.138, 1.335),
                                        rotation=(math.pi / 2, 0, 0))
    _finish(bpy.context.active_object, under, bevel=0)
    bpy.context.active_object.name = "emblem_inner"
    box("mark_l", (-0.011, -0.150, 1.333), (0.013, 0.008, 0.048), shell,
        bevel=0, rot=(0, 0.45, 0))
    box("mark_r", (0.011, -0.150, 1.333), (0.013, 0.008, 0.048), shell,
        bevel=0, rot=(0, -0.45, 0))

    # webbing across the chest, off-centre
    strap("strap_chest", (0, -0.140, 1.238), (0.33, 0.02, 0.038),
          webbing, steel, rot=(0, 0, 0.05))
    # one shoulder only, and thin — the first pass made this a slab the size
    # of a briefcase hanging off the collarbone
    strap("strap_shoulder", (0.112, -0.122, 1.372), (0.033, 0.033, 0.25),
          webbing, steel, rot=(0, 0, 0.16), buckle=False)

    # back unit: rebreather stack rather than a slab
    box("backpack", (0, 0.145, 1.33), (0.30, 0.10, 0.31), shell, bevel=0.03)
    box("backpack_lid", (0, 0.145, 1.487), (0.27, 0.095, 0.035), shell_w, bevel=0.012)
    for s in (-1, 1):
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=12, radius=0.038, depth=0.24,
            location=(0.095 * s, 0.215, 1.32))
        _finish(bpy.context.active_object, steel, bevel=0.006)
        bpy.context.active_object.name = f"backpack_tank{s}"
        box(f"vent_{s}", (0.095 * s, 0.22, 1.452), (0.062, 0.042, 0.026), alu,
            bevel=0.004)
    for i in range(4):
        panel(f"backpack_fin{i}", (0, 0.205, 1.21 + i * 0.042),
              (0.20, 0.025, 0.012), rubber)
    box("backpack_lamp", (0, 0.205, 1.483), (0.042, 0.025, 0.016), lamp, bevel=0)

    # belt + asymmetric pouches
    box("belt", (0, 0, 0.955), (0.375, 0.275, 0.07), webbing, bevel=0.008)
    box("belt_buckle", (0, -0.152, 0.955), (0.05, 0.022, 0.045), steel, bevel=0.005)
    box("pouch_a", (-0.135, -0.135, 0.945), (0.10, 0.07, 0.11), webbing,
        bevel=0.01)
    box("pouch_b", (0.115, -0.145, 0.955), (0.08, 0.06, 0.08), webbing,
        bevel=0.01)
    box("pouch_c", (0.185, 0.02, 0.94), (0.06, 0.11, 0.12), webbing, bevel=0.01)
    box("pouch_a_flap", (-0.135, -0.172, 0.985), (0.10, 0.02, 0.05), rubber,
        bevel=0.004)

    # ----------------------------------------------------------------- head
    # sealed respirator helmet
    box("neckseat", (0, 0.012, 1.512), (0.125, 0.125, 0.075), rubber, bevel=0.024)
    box("neck_col", (0, 0.012, 1.556), (0.10, 0.10, 0.05), under, bevel=0.02)
    sph("head", (0, 0.008, 1.668), 0.089, shell, scale=(0.93, 1.05, 0.98))
    sph("head_back", (0, 0.046, 1.656), 0.075, shell, scale=(0.96, 0.86, 1.0))
    sph("head_crown", (0, 0.010, 1.733), 0.070, shell_w,
        scale=(1.03, 1.12, 0.40))
    box("head_brow", (0, -0.072, 1.716), (0.158, 0.040, 0.022), shell,
        bevel=0.01, rot=(-0.30, 0, 0))
    box("head_jaw", (0, -0.030, 1.588), (0.128, 0.132, 0.046), rubber, bevel=0.024)

    # visor: one wide band, dark, with a lit lower edge
    box("visor_glass", (0, -0.082, 1.682), (0.138, 0.046, 0.055), visor,
        bevel=0.016)
    panel("visor_lip", (0, -0.100, 1.650), (0.132, 0.013, 0.009), alu)
    panel("visor_glow", (0, -0.103, 1.643), (0.086, 0.006, 0.004), lamp)

    # respirator block and filters
    box("resp_block", (0, -0.078, 1.600), (0.078, 0.054, 0.044), steel, bevel=0.012)
    for s in (-1, 1):
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=12, radius=0.024, depth=0.046,
            location=(0.075 * s, -0.054, 1.606), rotation=(0, math.pi / 2, 0))
        _finish(bpy.context.active_object, alu, bevel=0.005)
        bpy.context.active_object.name = f"filter{s}"
        box(f"commpod{s}", (0.086 * s, 0.018, 1.682), (0.020, 0.064, 0.060),
            under, bevel=0.012)
    box("resp_grille", (0, -0.103, 1.600), (0.054, 0.011, 0.030), rubber, bevel=0)
    # headlamp, off to one side like it was clipped on
    box("lamp_body", (0.066, -0.032, 1.748), (0.040, 0.046, 0.030), steel, bevel=0.008)
    box("lamp_lens", (0.066, -0.056, 1.748), (0.029, 0.011, 0.023), lamp, bevel=0)

    # ----------------------------------------------------------------- arms
    for s in (-1, 1):
        # pauldron: shell cap over a soft joint
        sph(f"pauldron{s}", (0.255 * s, 0, 1.435), 0.088, under,
            scale=(1.2, 1.05, 0.9))
        sph(f"pauldron_cap{s}", (0.243 * s, -0.004, 1.428), 0.079, shell,
            scale=(1.12, 1.16, 0.86))
        sph(f"pauldron_lip{s}", (0.259 * s, -0.004, 1.379), 0.073, shell_w,
            scale=(1.06, 1.10, 0.46))
        panel(f"pauldron_seam{s}", (0.286 * s, -0.052, 1.452),
              (0.075, 0.075, 0.010), shell_w)

        taper(f"uarm{s}", (0.27 * s, 0, 1.41), (0.385 * s, 0, 1.15),
              0.062, 0.05, under)
        box(f"armband{s}", (0.325 * s, 0, 1.28), (0.115, 0.115, 0.10), shell,
            bevel=0.015, rot=(0, 0.42 * s, 0))
        sph(f"elbow{s}", (0.385 * s, 0, 1.14), 0.058, rubber)
        box(f"elbow_cup{s}", (0.395 * s, -0.035, 1.14), (0.10, 0.07, 0.10),
            shell, bevel=0.014)

        fore, fq = taper(f"farm{s}", (0.39 * s, 0, 1.13), (0.45 * s, 0, 0.90),
                         0.055, 0.045, under)
        mid = Vector(((0.39 * s + 0.45 * s) / 2, 0, (1.13 + 0.90) / 2))

        if s == -1:     # wrist computer, one arm only
            box("device", mid + Vector((0, -0.055, 0.01)), (0.115, 0.06, 0.15),
                steel, bevel=0.014, quat=fq)
            box("device_screen", mid + Vector((0, -0.088, 0.02)),
                (0.075, 0.012, 0.095), screen, bevel=0, quat=fq)
            box("device_strap", mid + Vector((0, 0.02, 0.01)),
                (0.13, 0.10, 0.03), webbing, bevel=0.006, quat=fq)
        else:           # plated bracer on the other
            box(f"bracer{s}", mid + Vector((0, -0.04, 0.0)),
                (0.115, 0.075, 0.17), shell, bevel=0.016, quat=fq)
            panel(f"bracer_seam{s}", mid + Vector((0, -0.078, 0.0)),
                  (0.075, 0.012, 0.14), shell_w)

        box(f"cuff{s}", (0.442 * s, 0, 0.925), (0.10, 0.10, 0.045), alu,
            bevel=0.008, quat=fq)

        # glove: palm, knuckle plate, thumb
        box(f"hand{s}", (0.462 * s, -0.005, 0.855), (0.075, 0.10, 0.115),
            rubber, bevel=0.018, quat=fq)
        box(f"hand_knuckle{s}", (0.468 * s, -0.045, 0.815), (0.07, 0.05, 0.05),
            shell, bevel=0.01, quat=fq)
        box(f"hand_thumb{s}", (0.425 * s, -0.03, 0.845), (0.03, 0.045, 0.06),
            rubber, bevel=0.008, quat=fq)

    # ----------------------------------------------------------------- legs
    for s in (-1, 1):
        taper(f"thigh{s}", (0.12 * s, 0, 0.99), (0.13 * s, 0, 0.58),
              0.090, 0.072, under)
        box(f"thigh_plate{s}", (0.138 * s, -0.088, 0.80), (0.135, 0.055, 0.26),
            shell, bevel=0.016)
        panel(f"thigh_seam{s}", (0.138 * s, -0.118, 0.80),
              (0.09, 0.012, 0.20), shell_w)
        band(f"thigh_strap{s}", (0.133 * s, 0, 0.648), (0.134 * s, 0, 0.682),
             0.086, webbing, steel)
        box(f"thigh_pouch{s}", (0.185 * s, -0.05, 0.72), (0.05, 0.095, 0.13),
            webbing, bevel=0.01)

        sph(f"knee{s}", (0.13 * s, 0, 0.565), 0.068, rubber)
        box(f"knee_cap{s}", (0.133 * s, -0.055, 0.565), (0.115, 0.07, 0.115),
            shell, bevel=0.018)

        taper(f"shin{s}", (0.13 * s, 0, 0.55), (0.135 * s, 0, 0.16),
              0.070, 0.058, under)
        box(f"shin_plate{s}", (0.135 * s, -0.062, 0.37), (0.115, 0.055, 0.32),
            shell, bevel=0.014)
        rivets("shin_rivet", (0.135 * s, -0.092, 0.25),
               (0.135 * s, -0.092, 0.49), 4, 0.007, alu, side=str(s))

        # boot: sole, upper, ankle brace, reinforced toe
        box(f"boot{s}", (0.135 * s, -0.03, 0.105), (0.165, 0.30, 0.13),
            rubber, bevel=0.02)
        box(f"boot_sole{s}", (0.135 * s, -0.035, 0.028), (0.175, 0.33, 0.055),
            rubber, bevel=0.012)
        panel(f"boot_tread{s}", (0.135 * s, -0.035, 0.006),
              (0.16, 0.30, 0.012), under)
        box(f"boot_ankle{s}", (0.135 * s, 0.02, 0.185), (0.145, 0.14, 0.08),
            shell, bevel=0.014)
        box(f"toecap{s}", (0.135 * s, -0.155, 0.075), (0.15, 0.10, 0.10),
            shell_w, bevel=0.014)


def build_villain():
    """The Auditor.

    Deliberately built to the opposite brief from the hazard suit. Everyone
    else in the Sprawl is filthy; he is handed wear=0 and grime=0 and stays
    immaculate — pressed, brushed, unmarked — because a man who is never
    dirty in a place where everything is dirty is not from that place. It is
    the cheapest characterisation in the whole project and it costs two
    arguments.

    He is also the one character who shows a face, which is the point: he
    presents as human and the survivors do not get to.
    """
    suit = M("suit_wool", (0.055, 0.058, 0.075), rough=0.66)
    suit_l = M("suit_light", (0.085, 0.090, 0.112), rough=0.60)
    shirt = M("shirt", (0.80, 0.80, 0.78), rough=0.62)
    tiemat = M("tie", (0.26, 0.03, 0.045), rough=0.42)
    skin = M("pale_skin", (0.60, 0.50, 0.44), rough=0.72)
    hair = M("hair_dark", (0.055, 0.045, 0.042), rough=0.86)
    shoe = M("shoe", (0.022, 0.022, 0.026), rough=0.22)
    case = M("briefcase", (0.075, 0.042, 0.026), rough=0.40)
    steel = M("steel_v", (0.62, 0.63, 0.66), rough=0.20, metal=1.0)
    eye = M("eye", (0.06, 0.09, 0.10), rough=0.22)

    # ---- body: a narrower, straighter loft than the engineer's. A suit is
    # cut to hang, so the taper is gentler and the shoulders are square.
    oval("crotch", 0.855, 0.955, 0.112, 0.098, 0.140, 0.108, suit)
    oval("jacket_low", 0.955, 1.075, 0.140, 0.108, 0.138, 0.104, suit)
    oval("jacket_a", 1.075, 1.200, 0.138, 0.104, 0.140, 0.104, suit)
    oval("jacket_b", 1.200, 1.330, 0.140, 0.104, 0.156, 0.110, suit)
    oval("jacket_c", 1.330, 1.470, 0.156, 0.110, 0.150, 0.104, suit)

    # shirt front and tie, set into the jacket opening
    box("shirt_front", (0, -0.106, 1.352), (0.072, 0.024, 0.198), shirt,
        bevel=0.010)
    box("tie_knot", (0, -0.116, 1.458), (0.032, 0.026, 0.036), tiemat,
        bevel=0.008)
    box("tie_blade", (0, -0.116, 1.348), (0.036, 0.020, 0.172), tiemat,
        bevel=0.006)
    cyl("shirt_band", (0, 0.010, 1.470), (0, 0.008, 1.528), 0.058, shirt)
    for cs in (-1, 1):
        box(f"shirt_collar{cs}", (0.038 * cs, -0.052, 1.500), (0.052, 0.036, 0.048),
            shirt, bevel=0.006, rot=(0, 0, math.radians(22) * cs))

    # lapels: two angled panels, the silhouette that says "suit" at any range
    for sgn in (-1, 1):
        box(f"lapel{sgn}", (0.064 * sgn, -0.104, 1.372), (0.078, 0.026, 0.204),
            suit_l, bevel=0.008, rot=(0, 0, math.radians(13) * sgn))
        box(f"jacket_shoulder{sgn}", (0.116 * sgn, 0.004, 1.448),
            (0.135, 0.165, 0.062), suit, bevel=0.028)
        panel(f"jacket_seam{sgn}", (0.140 * sgn, -0.050, 1.360),
              (0.010, 0.055, 0.190), suit_l)
    box("jacket_vent", (0, 0.108, 1.150), (0.012, 0.020, 0.170), suit_l, bevel=0)
    box("pocket_l", (-0.098, -0.088, 1.180), (0.070, 0.020, 0.016), suit_l,
        bevel=0.004)
    box("pocket_r", (0.098, -0.088, 1.180), (0.070, 0.020, 0.016), suit_l,
        bevel=0.004)

    # ---- head: the one face in the game
    cyl("neckseat", (0, 0.012, 1.448), (0, 0.006, 1.610), 0.052, skin)
    sph("head", (0, 0.004, 1.664), 0.085, skin, scale=(0.92, 1.02, 1.10))
    sph("hair_top", (0, 0.014, 1.722), 0.077, hair, scale=(1.01, 1.06, 0.55))
    box("hair_back", (0, 0.066, 1.670), (0.146, 0.045, 0.146), hair, bevel=0.030)
    box("brow", (0, -0.068, 1.698), (0.116, 0.028, 0.018), skin, bevel=0.008)
    box("nose", (0, -0.080, 1.656), (0.023, 0.025, 0.040), skin, bevel=0.006)
    box("mouth", (0, -0.074, 1.600), (0.036, 0.013, 0.009), hair, bevel=0)
    for sgn in (-1, 1):
        sph(f"eye{sgn}", (0.033 * sgn, -0.070, 1.674), 0.012, eye)
        box(f"ear{sgn}", (0.078 * sgn, 0.008, 1.662), (0.013, 0.034, 0.045),
            skin, bevel=0.006)

    # ---- arms, held down and still
    for sgn in (-1, 1):
        sph(f"pauldron{sgn}", (0.196 * sgn, 0.004, 1.432), 0.064, suit,
            scale=(1.12, 1.22, 0.98))
        taper(f"uarm{sgn}", (0.215 * sgn, 0, 1.430), (0.235 * sgn, 0, 1.140),
              0.052, 0.044, suit)
        taper(f"farm{sgn}", (0.235 * sgn, 0, 1.140), (0.250 * sgn, 0, 0.880),
              0.044, 0.038, suit)
        box(f"cuff{sgn}", (0.248 * sgn, 0, 0.892), (0.076, 0.076, 0.026),
            shirt, bevel=0.006)
        box(f"hand{sgn}", (0.256 * sgn, -0.006, 0.828), (0.062, 0.086, 0.098),
            skin, bevel=0.020)

    # the briefcase, in the left hand
    box("case", (-0.320, 0.010, 0.700), (0.075, 0.230, 0.290), case, bevel=0.014)
    box("handle", (-0.300, 0.010, 0.858), (0.020, 0.090, 0.036), case, bevel=0.010)
    box("latch_l", (-0.281, -0.060, 0.792), (0.016, 0.036, 0.026), steel,
        bevel=0.004)
    box("latch_r", (-0.281, 0.080, 0.792), (0.016, 0.036, 0.026), steel,
        bevel=0.004)

    # ---- legs: trousers with a break over the shoe
    for sgn in (-1, 1):
        taper(f"thigh{sgn}", (0.100 * sgn, 0, 0.990), (0.105 * sgn, 0, 0.570),
              0.078, 0.062, suit)
        taper(f"shin{sgn}", (0.105 * sgn, 0, 0.570), (0.105 * sgn, 0, 0.120),
              0.062, 0.058, suit)
        panel(f"thigh_crease{sgn}", (0.105 * sgn, -0.060, 0.780),
              (0.010, 0.010, 0.400), suit_l)
        box(f"shoe{sgn}", (0.105 * sgn, -0.040, 0.055), (0.105, 0.250, 0.075),
            shoe, bevel=0.020)
        box(f"shoe_toe{sgn}", (0.105 * sgn, -0.140, 0.048), (0.092, 0.080, 0.058),
            shoe, bevel=0.024)


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
