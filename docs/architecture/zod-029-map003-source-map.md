# ZOD-029 — MAP003 base scene and terrain

Date: 2026-10-23. Target: North American PS1 `SCUS-94221`.
Read-only reference: `C:\repositories\fft_decomp`, observed HEAD
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

**Select MAP003, day/dry, default arrangement, with presentation animations frozen.**
Its primary map is 9x13 tiles with two source terrain layers, one primary geometry
chunk, and a separate texture file. It has no additional animated mesh-part chunks.
Use the four rug tiles `(3,1)..(4,2)`, lower layer, as the first geometry/terrain
alignment checkpoint. This is a numeric asset selection, not a claim about an
encounter's campaign setup, unit roster or localized location name.

Evidence is **source-derived analysis and hash-verified local structure inspection**.
No C routine, original executable, emulator or original-game visual comparison ran.
This ticket produces a converter recommendation, not map parsing or rendering code.

## Files and selection

The input profile is the previously verified local disc image SHA-256
`29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9`.
Files were read from ignored `scratch/all-data-extraction/` and their lengths/hashes
checked against `scratch/fft-decomp-work/build/extracted/inventory.json`:

- `MAP/MAP003.GNS`: LBA 12938, 568 bytes, SHA-256
  `3df4a492a1ae6c6aca645a9b58b5984cb8dbf13b2631a3819215d8a516327d57`.
- `MAP/MAP003.8`: LBA 12658, 131,072 bytes, SHA-256
  `746af063e812f591e8c72f0510764316e005621eab73ae5fbe99565eac9fa5a4`.
  Complete external texture; its suffix is a filename, not a resource-type ID.
- `MAP/MAP003.9`: LBA 12722, 41,852 bytes, SHA-256
  `227172783f50289e4a7425a63966fad7cbfd4599af80b76870ea094ff2f56ff4`.
  Primary mesh file containing geometry, palettes, terrain and presentation metadata.

`target/battle.yaml` places `g_battle_map_gns_lba` at `0x800f5c74`. Entry 3 is
12938 in the verified local BATTLE.BIN. The module loads at `0x80067000`, so table
file offsets are `address - 0x80067000 + index * 4`. BATTLE.BIN identity is
SHA-256 `7af579c7bcfd46f21f8d6886acb6aae156474dace667553e76079be7c7d6db94`;
it is offline selection evidence, never a runtime dependency.

The loader's relevant source map, all at the pinned revision:

- `include/fft/battle_map_load.h`: packed GNS rows, command prefixes, resource IDs,
  49-entry mesh header and terrain resource layout.
- `src/battle/map/gns.c::battle_map_gns_move_find_load`: map ID to GNS sector;
  `battle_map_file_load_start`: bounded file-read request in the original loader.
- `src/battle/map/loading.c::battle_map_load_data`: select resources, load/dispatch
  them, finish mesh records and execute GNS commands.
- `src/battle/map/runtime.c::battle_map_gns_record_matches_state` and
  `battle_map_gns_resource_dispatch`: state comparison and resource application.

For the selected cold load, script variable `0x22` (weather/time layout) and
`0x30` (map arrangement) are 0; weather `0x23` is 0 and time-of-day `0x24` is 0.
A resource request packs `(scriptValue & 0xfff) | ((weather & 7)<<12) |
((timeOfDay & 1)<<15)`. Comparisons use the source's signed 16-bit state values;
do not generalize unsigned ordering from the selected equality comparisons.

The first two 20-byte GNS records are:

```text
row   variable  state   comparison  resource  LBA    requested bytes
0     0x22      0       equal       0x17      12658  0x20000 (texture .8)
1     0x22      0       equal       0x2e      12722  0x0a800 (primary mesh .9)
```

Rows 2..7 select weather/night textures and small alternate mesh files for states
`0x1000`, `0x8000`, `0x9000`. They do not match the selected request. Rows 8..17
are filler type `0x31`, state 1; they also do not match. The loader makes ordered
passes over primary mesh, override mesh, default texture, then remaining matching
rows. ZOD-030 should validate this exact selection rather than introduce a general
GNS state machine in its first converter.

The command separator is at `0x168`; it is a 20-byte separator, not a command whose
byte `+8` can be interpreted as a stride. Commands begin at `0x17c`: four pairs
of `SET_INDOOR (0x85)` / `CLEAR_SNOW (0x88)`, then a 28-byte `0x8a` record at
`0x21c`, ending at file length `0x238`. These commands are unconditional in
`battle_map_gns_apply_command`; `0x8a` is ignored by the dispatcher. No camera
position/rotation command is present. There is no complete zero command after
that final row in the ISO file; a managed reader must stop safely at the bounded
EOF after the complete final record.

Original read requests can exceed file length: GNS requests 0x1000 bytes and this
mesh requests 0xa800. Do not reproduce disc-neighbor/padding reads or the native
150-row buffers as managed format requirements. Resolve sectors to verified named
files offline and validate each referenced range against its actual file length.

## Primary mesh structure and geometry

The first 196 bytes are 49 little-endian unsigned 32-bit offsets, indexed by
resource ID. Zero means absent. All nonzero offsets in MAP003.9 are:

```text
resource   file offset  consumed data
0x10       0x00c4       primary geometry, ends at 0x7ac8
0x11       0x7ac8       16 palettes x 16 little-endian colors (512 bytes)
0x19       0x7cc8       packed lighting (45 bytes, then alignment padding)
0x1a       0x7cf8       terrain (4098 bytes, then alignment padding)
0x1b       0x8cfc       32 texture-animation records x 20 bytes
0x1c       0x8f7c       palette-animation frames (512 bytes)
0x1f       0x917c       marked-tile palettes (512 bytes)
0x2c       0x937c       polygon flags (4096 bytes; ends at EOF 0xa37c)
```

Geometry starts with unsigned 16-bit counts: **80 textured triangles, 430 textured
quads, 0 untextured triangles, 26 untextured quads**. It then stores:

1. Positions, grouped by those four kinds: three or four vertices per polygon,
   each vertex three signed little-endian 16-bit XYZ values, without native padding.
2. Normals for textured triangles then textured quads: three signed 16-bit
   components per vertex. No corresponding untextured-normal section is read here.
3. Textured triangle material records (10 bytes each), textured quad records
   (12 bytes), then four-byte records for each untextured triangle/quad.
4. Unsigned 16-bit terrain tags for textured triangles, then textured quads.

`battle_map_mesh_append_geometry` and `mesh_render_records_init` in `loading.c`
consume this layout. Native eight-byte vertex/vector records and GPU packet
strides are runtime representations, not serialized strides. The geometry span is
31,236 bytes, independently calculated from counts and checked against the next chunk.
Textured triangle materials begin at file `0x5f1c`, quad materials at `0x623c`,
triangle terrain tags at `0x76cc`, quad tags at `0x776c`.

Material halfwords are `(UV0, CLUT, UV1, TPAGE, UV2[, UV3])`; low UV byte is U,
high byte V. The CLUT selector is `rawClut & 0x3f`, remapped to the map CLUT row
by `mesh_render_records_init`. Preserve all original material words as provenance.
The untextured four-byte records are skipped by that initializer, which initializes
untextured packet colors to `(0,0,1)`; do not label those four bytes as verified
vertex colors without a separate trace.

Textured terrain tags encode `X=tag>>8`, grid Y/Z=`(tag>>1)&0x7f`, layer=`tag&1`;
`0xfffe` marks no terrain association. Tags annotate visual polygons; they do not
define movement validity. Quad corner order is a strip-like GPU order, not a
perimeter loop: preserve it and explicitly validate managed triangulation/winding
after coordinate conversion. A generic perimeter fan can produce a crossed or
incomplete floor even when all four XYZ values are correct.

Polygon flag data has an unused `0x380` prefix, then reserved arrays of 512
triangle flags, 768 quad flags, 64 untextured-triangle flags and 256 untextured-quad
flags. Flags are unsigned raw 16-bit words even where native fields are signed.
`include/fft/battle_map_mesh.h` identifies lighting and yaw-dependent visibility
bits; `runtime.c` applies them and draws through `battle_map_mesh_weather_draw`.
Preserve flags and normals; a static renderer must state its visibility/lighting
approximation rather than silently treating every polygon as always visible/unlit.

## Texture, palette and frozen presentation

Resource `0x17` dispatches four consecutive `0x8000`-byte texture quarters to VRAM
word X `0x300`, `0x340`, `0x380`, `0x3c0`, Y 0, width 64 words, height 256.
Each is a 256x256 4bpp page. An offline representation can keep four pages or
assemble a **1024x256 indexed atlas**; concatenating their bytes and treating them
as ordinary 1024-pixel scanlines would interleave rows incorrectly.

These pages correspond to the low TPAGE selectors 12..15. For the checkpoint's
TPAGE `0x000e`, use quarter 2, file `0x10000 + v*128 + floor(u/2)`. Ordinary
native pixels use low nibble for even U, high nibble for odd U, as also traced for
[ZOD-027](zod-027-chocobo-idle-pose.md). Palette selection is **per polygon**;
the single-palette sprite RGBA contract cannot directly represent the entire map.

Map palettes use raw `ABBBBBGGGGGRRRRR`. Unlike unit-sheet loading,
`palettes.c::palette_colors_extract` copies the STP bit without forcing it on
nonzero indices. Keep raw 16-bit palettes and explicit transparency/blend semantics.
Do not reuse the unit importer's STP transformation. Exact PS1 modulation,
semi-transparency, affine rasterization, edge sampling and dithering remain render
comparison work, not established by source-file inspection.

The lighting chunk contains two 3x3 signed 16-bit matrices, ambient RGB and two
background-gradient RGB values. Selected ambient is `(32,16,16)` and both gradient
colors are zero. The original applies these through its GTE-related light path;
raw matrices and source normals should survive preparation even if the first
renderer uses a documented simplified light model.

This map has texture-animation records: rows 0/1 are palette loops (mode 3,
duration 12, three/four frames), targeting palette banks 4/5; rows 2..7 are
play-once UV operations (mode 5), initially inactive. `runtime.c` activates the
palette loops when installing the records. The checkpoint uses bank 8, away from
those two loops. Choose the base uploaded texture/palette state with **no animation
updates** for the first prepared snapshot; this is an explicit frozen comparison
state, not a promise that the whole original map is static.

## Terrain and coordinate alignment checkpoint

Resource `0x1a` is width byte, depth byte, then two fixed **256-record layers**.
Each record is eight bytes. `terrain.c::battle_map_tile_data_copy` copies this
block; `battle_map_tile_get_pointer` bounds-checks and indexes
`layer*256 + y*width + x`. For this map only 117 records per layer are in bounds.
Layer 1 begins 2048 bytes after layer 0, not after 117 records. Convert valid cells
to the existing dense `BattleMapDefinition` layout explicitly; retain source bytes
and unused-field provenance separately from simulation semantics.

The record fields from `include/fft/battle_map.h` are:

- Byte 0: six-bit surface ID, depth-bias bit 6, unused bit 7; byte 1 is unknown.
- Byte 2: whole height. Byte 3: five-bit half-height and three-bit water depth.
- Byte 4: four two-bit slope sides, east/west/south/north from the low bits.
- Byte 5: five-bit ceiling depth plus transient movement/target marks.
- Byte 6: blocked, untargetable, two-bit shading, three unknown bits, cannot-stop.
- Byte 7: camera-block masks for flat and steep pitches.

Checkpoint: lower-layer tiles **(3,1), (4,1), (3,2), (4,2)**. Their records at
file `0x7d5a`, `0x7d62`, `0x7da2`, `0x7daa` are all
`1d 00 01 00 00 00 20 00`: Rug surface 29, height 1, dry, flat, zero ceiling,
not blocked/untargetable/cannot-stop, normal shading. Preserve the unknown flag
bits represented by raw byte `0x20`; do not discard them as if decoded.
The corresponding upper-layer records at `0x855a`, `0x8562`, `0x85a2`, `0x85aa`
are `00 00 00 00 00 00 01 00` (blocked). Surface appearance does not override that flag.

The matching textured quads have source indices **217, 218, 226, 227**, tags
`0x0304`, `0x0404`, `0x0402`, `0x0302` respectively. Their bounds are:

```text
quad  tile   world X     world Z     all vertex Y
217   3,2    84..112     56..84      -12
218   4,2   112..140     56..84      -12
226   4,1   112..140     28..56      -12
227   3,1    84..112     28..56      -12
```

All four have upward source normals `(0,-4096,0)`, flags `0x8000` (lit), TPAGE
`0x000e`, and CLUT word `0x7808` (bank 8; native map CLUT row Y `0x1e0`).
Quad 227 is a useful exact vector:

```text
vertices: (84,-12,28), (84,-12,56), (112,-12,28), (112,-12,56)
UVs:      (88,227),    (88,208),    (107,227),    (107,208)
```

Grid `(x,y)` maps to world `(X,Z)`; one tile is 28 units, whole height step is
12 units toward negative world Y. For these flat dry cells the unit center is
`(28*x+14, -12*height, 28*y+14)`: tile (3,1) gives **(98,-12,42)**. Source:
`terrain_projection.c::battle_map_slope_height_calculate` and
`unit_height_calculate_from_tile`. Slopes, water, standing on units, mounts and
Float alter that path; the flat formula must not become a general height rule.

The current bounded attack snapshot requires nonblocked tiles at height 10.
This map is not immediately a supported M1 combat setup. **Do not rewrite native
height 1 to 10** to make it pass; any playable integration requires an explicit,
source-traced expansion of that supported domain. Geometry, terrain and movement
policy remain separate data/behavior contracts.

## Minimum ZOD-030 preparation and ZOD-031 scene setup

1. Verify the three selected files and pinned source identity. Parse GNS rows and
   command boundaries only far enough to validate the named day/dry/default file
   selection; reject other states initially. Preserve the selection/provenance.
2. Parse the eight selected mesh chunks with checked count/offset arithmetic,
   exact source strides and explicit bounds. Prepare immutable primary positions,
   textured normals/materials, terrain tags and polygon flags. Keep untextured
   records/raw metadata explicit. There are no additional mesh parts in this file.
3. Prepare indexed texture pages, raw palettes, lighting and frozen animation
   metadata. Select a typed rendering representation that supports per-polygon
   palette references; do not flatten all polygons through one palette. Any RGBA
   expansion must name its channel/STP/blend policy and retain raw provenance.
4. Prepare the two dense in-bounds terrain layers with stable FFT surface IDs,
   original dimensions/heights/slopes/flags and source records. Do not infer
   traversability from a mesh or introduce the original fixed capacity into Engine.
5. Publish a manifest-addressable prepared map package under ignored scratch.
   Runtime reads that package and the already prepared Chocobo pose; no GNS/ISO,
   original binary, importer or reference C code enters runtime simulation.
6. Test malformed GNS strides/separators/EOF, offset/count overflow, overlapping
   chunks, truncated sections, polygon tags without terrain, page/CLUT bounds,
   native quad ordering, signed XYZ/normals, two-layer source stride, packed flags,
   deterministic preparation and prepared-only loading. Add source-derived local
   vectors for the four checkpoint cells/quads without committing game bytes.

The original `init.c::camera_defaults_init` starts with pitch `0x12e`, yaw `0xe00`,
roll 0, zoom ONE (`0x1000`), real position zero and screen offset `(0x100,0xa0,0x280)`.
MAP003's GNS supplies no camera overrides; encounter/event panning can still change
the real view. For the first Windows fixed scene, explicitly record authored camera
target/position, projection, viewport, scale, animation phase and pose-facing choice.
A view aimed at the checkpoint center `(112,-12,56)` and a Chocobo anchor at
`(98,-12,42)` provides a bounded alignment check. It is an authored inspection view,
not a recovered encounter camera or screen-coordinate equivalence fixture.

## Validation and remaining limits

Local checks verified all three source hashes/lengths against the extracted inventory,
the GNS LBA table entry, selected file rows, eight chunk offsets, geometry span,
primitive counts, checkpoint terrain, matching quad vertices/materials/normals/flags,
and nonzero texture-animation rows. The generated structure summary is local only:
`scratch/zod-029-map003-source-audit.json`.

No production code or tests changed; the previous 720-test result is historical and
was not rerun for this documentation ticket. No decoded media or source inventories
are committed. ZOD-026 remains the independent comparison gate.

Unsupported by the first static scene: alternate weather/night/arrangement meshes,
weather effects, palette/texture animation playback, event terrain modifications,
animated meshes in other maps, complete slope/water movement, original event camera,
map/sprite occlusion equivalence, exact PS1 lighting/GTE arithmetic, affine texturing,
semi-transparency, dithering, and raster edge rules. Retain raw input metadata and
report differences explicitly when ZOD-031/033 add rendering comparisons.

Implementation follow-up (2026-10-23): [ZOD-030](../development/prepared-map-slice.md)
implements the bounded preparation/load pipeline and records repeatable output hashes,
the native terrain/quad checkpoint and synthetic validation. Rendering remains ZOD-031.
