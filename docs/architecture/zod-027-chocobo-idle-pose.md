# ZOD-027 — One Chocobo idle pose

Date: 2026-10-23. Target: North American PS1 `SCUS-94221`.
Reference checkout: `C:\repositories\fft_decomp`, read-only, observed HEAD
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

**Recommendation:** prepare primary SHP frame **9**, the first frame of Chocobo
idle sequence **6**, using SPR palette bank **0**. Start with the static pose;
optionally prepare the same sequence's five distinct frames as a bounded idle loop.
This extends the [existing SPR proof](../sprite-attack-image-decoder-poc.md) without
requiring attack images, EVTCHR slots, a general animation interpreter or original media
in the repository. ZOD-028 owns implementation; this ticket changes documentation only.

Evidence is **source-derived analysis plus local file-structure inspection**, not an
executing C oracle, emulator observation, rendered comparison or original-game equivalence.
The previous attack-image fixture retains its historical reference revision and meaning.

Implementation follow-up: [ZOD-028](../development/prepared-sprite-pose.md) now prepares the
static pose and loads its prepared representation. The original comparison, broader animation
and rendering limitations below remain applicable.

## Selected preconditions and file identity

Select spritesheet ID 134 (`SPRITE_SHEET_ID_CHOCOBO`), dry/unmounted primary frame set,
healthy idle animation 3, and camera-relative direction 0. Set sequence/frame bias and
sequence flip flags to zero, normal scale/rotation, no status/weather/terrain tint,
no weapon/effect/shadow overlays, and explicit palette bank 0. These are controlled
preview inputs, not recovered encounter/unit records. Palette 0 is an explicit choice;
this investigation does not establish every monster job's palette assignment.

Inputs were read from ignored `scratch/all-data-extraction/` and checked against the
previous extracted inventory. The local image profile is SHA-256
`29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9`.

- `BATTLE/CYOKO.SPR`: LBA 57494, 49,572 bytes, SHA-256
  `7f7a94e5615137f9a4fa6e1dcfbad8b2760a6b66099154171b0e1feb137632d2`.
- `BATTLE/CYOKO.SHP`: LBA 57026, 7,316 bytes, SHA-256
  `235b36f2e2990993a38c9d30fcd927aefef645af9de8c49394dc07e5f8a1ae27`.
- `BATTLE/CYOKO.SEQ`: LBA 57053, 3,068 bytes, SHA-256
  `f746461ad326c8bb1a12c0d430495046330649b2952dae72db50b94873395b6b`.
- `BATTLE.BIN`: 1,397,096 bytes, SHA-256
  `7af579c7bcfd46f21f8d6886acb6aae156474dace667553e76079be7c7d6db94`.
  Offline provenance for the selection/size tables only; do not make it a runtime input.

## Source map at the pinned revision

Paths below are relative to the reference checkout; symbols, addresses and serialized
offsets identify the evidence independently of moving source line numbers.

- `include/fft/sprite_sheet.h`: spritesheet 134, SHP type 2 and SEQ type 2 are Chocobo.
  `target/battle.yaml` maps `g_sprite_sheet_data` to `0x80094748`;
  record `134 * 4` is `(shp=2, seq=2, flying=0, graphic_height=36)`.
- The same symbol map places `s_seq_files` at `0x80094c04`, `s_shp_files` at
  `0x80094c4c`, and `s_sheet_files` at `0x80094cd4`. Eight-byte extent records
  2, 2 and 134 resolve to the three LBAs above. Their read sizes are respectively
  `0x1000`, `0x2000`, `0xc800`, larger than the ISO file lengths. Preserve file
  bounds in managed parsing rather than reproducing the sector-rounded overreads.
  Address-to-file offset for these tables is `address - 0x80067000`.
- `src/battle/units/sprite_runtime.c`: `sprite_shp_seq_load_update` installs shared
  SHP/SEQ files via `unit_shp_data_unpack` / `unit_seq_data_unpack`;
  `sprite_queued_load_update` selects a unit's files and invokes `runtime_unit_init`.
  `src/battle/units/sprite_state.c` resolves its resident tables through
  `sprite_sheet_get_unit_shp` / `sprite_sheet_get_unit_seq`.
- `runtime_unit_init` initializes idle sequence `ANIMATION_IDLE << 1`, biases 0,
  script position 0 and calls `unit_seq_run`. `unit_wep_eff_update` selects later
  views using `(camera_yaw + facing) & 0xfff`, quadrant `/1024`, and the back/flip
  tables. Quadrant 0 gives sequence `2 * 3 + 0 = 6` without mirroring.
  `ANIMATION_STAND` is a special directional lookup; do not apply the general
  doubling rule to that animation. See `include/fft/animation.h`.
- `frame_unit_shp_select` chooses the primary/submerged set;
  `sprite_unit_frame_load` expands the selected SHP frame into display parts.
  `include/fft/sprite.h` defines their signed shifts, attributes and flags.
- `vram_sheet_load`, `sprite_palette_tint_unit` and
  `sprite_display_construct_polygon_data` establish upload, palette selection,
  piece transforms and drawing. `include/psx/libgpu.h` defines `addPrim` as
  head insertion into the ordering list.

## Exact sequence and frame selection

Unit SEQ files have little-endian unsigned 16-bit attack/SP2 thresholds at `+0/+2`,
256 signed 32-bit payload-relative offsets at `+4`, a 16-bit payload byte count
at `+0x404`, and data at `+0x406`. CYOKO thresholds are 120/255; payload size is
2,038, ending exactly at the file length. Sequence 6's entry is at `+0x1c`, value
`0x0c`, so its first instruction is at **`+0x412`**.

`unit_seq_run` reads ordinary instructions as `(frame byte, wait byte)`. Sequence 6 is:

```text
frame 9 / wait 6
frame 10 / wait 8
frame 11 / wait 10
frame 10 / wait 8
frame 9 / wait 6
frame 12 / wait 8
frame 13 / wait 10
frame 12 / wait 8
RESTART (prefix 0xff, opcode 0xd5)
```

The loop is 64 source presentation wait units, not 64 simulation/CT ticks.
`unit_wep_eff_update` decrements a nonzero wait in a loop controlled by
`g_system_vsync_interval`; reaching zero executes the next instruction.
`RESTART` sets script position to zero and increments a 16-bit restart counter.
A zero wait holds a frame; it must not become a zero-duration busy loop.
For this static slice the first wait is recorded as **6** and playback is frozen.
No wall-clock/frame-rate equivalence has been observed; do not infer it from the
Windows host's nominal 60 Hz presentation rate.

Unit SHP files have a signed 32-bit second-set offset at `+0`, unsigned 16-bit
attack/SP2 thresholds at `+4/+6`, then the primary set at `+8`. Each set has 256
signed 32-bit frame-data offsets, a 16-bit byte count at set `+0x400`, then data
at set `+0x402`. Original native tables install only the first `0xd0` entries;
`-1` offsets map to payload offset 0, not an absent frame. Validate all lengths
and referenced records; never translate that sentinel into an unchecked pointer.

CYOKO has second-set offset `0x0e4c`, thresholds 61/255, and primary data length
2,626 at file `+0x408`. Primary data starts at **`+0x40a`**, ending at the second
set. Frame 9's offset entry at `+0x2c` is `0xea`; its record is at **`+0x4f4`**.
Header `(5,0)` means six pieces, rotation index 0, frame flags 0. The rotation
table's index 0 resolves to 0. Frame 9 is below attack threshold 61, so it uses
the ordinary 256x256 image, without the compressed attack tail.

## Frame 9 composition

Each piece is `(signed i8 x, signed i8 y, little-endian u16 attributes)`.
Coordinates are relative to the unit's projected sprite origin, not the texture
rectangle's center. `tile = attributes & 0x3ff`, `u=(tile & 31)*8`,
`v=(tile >> 5)*8`, size index `(attributes & 0x3c00)>>10`, flip flags
`(attributes & 0xc000)>>13` (horizontal 2, vertical 4).

`g_sprite_part_sizes` at `0x800946c8` stores pairs of signed 32-bit dimensions in
8-pixel units. Needed entries: 0 = 8x8 pixels, 2 = 16x16, 5 = 24x16, 6 = 24x24.
`g_sprite_y_rotations` is at `0x80094508`. Values below were resolved from the
hash-verified local BATTLE.BIN using that symbol map:

```text
record index   shift x,y   source u,v   size w,h   attributes
0              -5,-22     64,64        24,16      0x1508
1             -15,-31    216,0         16,16      0x081b
2             -16,-18     32,32        24,16      0x1484
3              24,-8     240,0          8,8       0x001e
4             -16,-7      56,32        24,16      0x1487
5              -7,-35    200,88        24,24      0x1979
```

All six have zero flip flags, zero frame semi-transparency/blend bits and zero
rotation. All source rectangles fit the main image. The geometric union is
`x=-16..32`, `y=-35..9` (exclusive maxima), including record 3, whose 64 texels
are all index zero in this asset. Preserve the original piece and anchor even
when it contributes no visible pixels.

`sprite_display_construct_polygon_data` submits indices 0 through 5 with
`addPrim`, which prepends each polygon to the same ordering bucket for these
pieces. A forward painter must therefore draw **5,4,3,2,1,0** to reproduce the
intra-unit list traversal for this selected case. This is a source-derived
ordering expectation, still awaiting an independent rendered comparison.
It does not establish map/unit occlusion or ordering with overlays/other units.

General flips combine piece flags with whole-sprite flags by XOR for texture
orientation; a whole-sprite horizontal flip also changes placement to `-x-width`
(vertical: `-y-height`). Size index 14 has special 48x48 splitting/near-depth
behavior and returns early from frame expansion; it is absent here and should
be rejected explicitly by the first bounded converter rather than approximated.

## Image, palette and transparency boundary

`unit_spr_file_t` / existing `UnitSpriteSheet` locate 16 banks of 16 raw BGR555/STP
colors at `+0`, ordinary 4bpp data at `+0x200` (32,768 bytes), portrait/body strip
at `+0x8200`, and attack stream at `+0x9200`. `vram_sheet_load` sends ordinary
bytes unchanged to `LoadTPage(tp=0)`; `src/platform/psyq/libgpu/LoadTPage.c`
uploads 64 VRAM words per 256-pixel row. The source's packed-image accessor
`gfx_read_packed_nibble` in `src/lib/menu/graphics/image_pixels.c` selects the
**low nibble for even x**, high nibble for odd x. Use that native ordinary-image
packing; do not reuse the earlier attack decoder fixture's explicitly high-first
managed output convention as an ordinary-sheet contract.

The selected palette is SPR bank 0 (`+0..31` bytes), not a palette selected by
the SHP frame or SEQ instruction. `runtime_unit_init` accepts the load descriptor's
palette, overrides it from stats for generic human job sheets, and maps values
at least 6 to 0. Chocobo 134 is outside that human override. The ordinary palette
update ultimately reads `clut[stored_palette]`; palette/weather/status/tile modulation
is a distinct path. This preview requires no modulation and neutral display RGB
`0x80,0x80,0x80` (`sprite_display_init_unit`).

`vram_sheet_load` sets bit 15 on colors 1..15 in every bank and leaves color 0
unchanged. In selected bank 0, entry 0 is zero and all remaining entries have
nonzero raw colors. For this frame, primitive semi-transparency is disabled:
zero texels are transparent, other selected colors are opaque. Preserve raw
color/STP provenance; setting STP is not by itself an instruction to alpha-blend
every texel. An RGBA preview may use a documented 5-to-8-bit expansion, but that
does not establish PS1 modulation, dithering, rounding or blend equivalence.

## Smallest ZOD-028 implementation

1. Add bounded offline unit SHP/SEQ readers to `Fft.AssetTool`, with explicit
   little-endian widths, copied immutable data, checked offsets/counts, selected
   native frame-index bounds and source `-1 -> 0` handling. Distinguish the
   optional second set even though this pose selects the primary set.
2. Resolve sheet 134 / SHP 2 / SEQ 2 explicitly and prepare frame 9 plus its
   first wait 6 and explicit bank 0. A small declarative prepared record should
   carry texture ID, source rectangles, signed anchor offsets, resolved draw
   order, rotation/flip/blend limitations and provenance hashes/revision.
   Runtime consumers load prepared files by manifest ID; no runtime SHP/SEQ,
   BATTLE.BIN, importer, BIN, C, or native PS1 pointer tables are needed.
3. Prepare only the main indexed atlas and palette needed by this slice, with
   an explicit pixel-packing/color-expansion contract. Preserve raw palette/STP
   metadata. Do not decode attack data simply because the existing proof can.
4. If idle playback is included, permit only bounded frame/wait instructions
   and restart, with loop/instruction budgets; prepare frames 9..13 and all eight
   durations above. Unsupported opcodes, transforms or data paths must fail
   clearly. This is not a generic interpreter or animation catalog.
5. Add synthetic/truncated/negative/overflowing-offset and rectangle tests,
   signed-shift and flip tests, reverse-order/overlap regressions, palette and
   low-first pixel vectors, zero-wait handling, and deterministic export/load
   checks. Keep copyrighted sample bytes out of tests. Use hash-verified local
   integration separately under `scratch/`.

Prepared view metadata belongs in FFT content/presentation; pure simulation and
`Zodiac.Engine` gain no animation timing, graphics package or file-format dependency.
ZOD-031 consumes the prepared pose after map preparation. ZOD-026 supplies the
independent comparison workflow; neither task is completed by this source trace.

## Uncertainty and validation

The source map and local metadata checks establish this one selection and composition.
Local inspection verified source-file hashes against the extracted inventory, table
selection/extents, payload bounds, sequence instructions, six signed pieces and their
source rectangles. No C routine or emulator was executed and no pose image was rendered.
Only documentation changed; the 656-test result from ZOD-025 is not a new validation run.

Still unverified: screen-origin calibration against the original camera, exact real-time
cadence and phase changes during camera turns, GPU texture-edge rasterization, color
rounding/dithering, ordering against map geometry and effects, and original observations
of this selected frame. Submerged/riding variants, other directions, attacks/EVTCHR/SP2,
humanoid/weapon/effect formats, status replacement sheets, semi-transparent primitives,
special 48x48 splitting, and arbitrary sequences remain outside the selected slice.
