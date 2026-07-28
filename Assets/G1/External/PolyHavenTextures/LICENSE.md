# Poly Haven textures — CC0

Twelve tiling PBR texture sets downloaded from https://polyhaven.com via its
public API on 2026-07-28. Poly Haven publishes everything under **CC0 1.0**:
public domain, no attribution required, commercial use permitted. Attribution
is recorded anyway.

Each folder holds three 1K JPEGs: `_Diffuse` (albedo), `_nor_gl` (OpenGL normal)
and `_arm` (ambient occlusion / roughness / metallic packed per channel).

concrete_wall_008, anti_slip_concrete, asphalt_02, metal_plate, corrugated_iron,
green_metal_rust, container_side, brick_4, gravel_floor, sandy_gravel,
wood_planks_dirt, floor_tiles_02

1K rather than 2K or 4K: these tile at two metres per UV unit across an
800x800 m map, so the limit on how sharp they look is the tiling rate, not the
texel count, and the whole set costs 25 MB instead of 400.
