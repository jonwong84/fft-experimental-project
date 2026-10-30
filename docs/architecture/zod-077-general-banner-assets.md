# ZOD-077 — General-banner assets and readiness

Date: 2026-10-23. Edition: North American PS1 `SCUS-94221`.
Read-only reference HEAD: `11464943bbdbd2a0af022d90cb7f10455cf911df`.
Managed base: `c84e0c4` (ZOD-076); working tree clean at entry.

**Select a prepared condition-8/banner-0 preview, driven by the existing logical
effect, with assets loaded before scene creation.** Prepare indexed pixels,
palette bank 4 and six normalized shaded pieces. Use a small deterministic CPU
compositor in Client for the first presentation proof; MonoGame displays its
output. This avoids extending the opaque map material into an unproved general
PS1 GPU renderer. ZOD-078 owns implementation; this ticket changes documentation
and summarized evidence only.

Implementation follow-up: [ZOD-078](../development/prepared-general-banner.md) now
implements this bounded pipeline and preview, with regressions for all 20 authored
expectations and repeatable local preparation/Windows captures. The unexecuted
statements below preserve this investigation's historical evidence boundary.

Evidence is static source tracing and hash-verified local asset inspection, not
original execution or an original timing/visual comparison. The
[source metadata](../evidence/events/general-banner-assets-v1.json) contains hashes
and structural summaries, not original table or pixel payloads. The
[authored expectations](../evidence/events/general-banner-presentation-contract-v1.json)
are specification-only, unexecuted examples; they are not a prepared-file schema.

## Selection and verified local inputs

The [BattleStart trace](zod-066-battle-start-event.md) selects event 401,
DisplayConditions at offset 164, condition 8, hold 60. The interpreter passes byte
operands to `script_conditions_display`; REQUIRE tags its task 56 and calls
`conditions_show_general`. That helper selects image `condition - 8`, hence 0.
The later Ready condition 0 follows a different routine and is excluded. The
`CONDITIONS_BANNER_READY = 0` special-message enum is not the general-banner layout
selector: the general helper always uses its own range row 0.

The local BIN was rehashed: 541,315,152 bytes, SHA-256
`29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9`.
The selected extracted files were compared byte-for-byte with bounded Mode 2 Form 1
payloads read directly from that image, checking sector sync, mode, repeated
subheaders and Form 1. `SYSTEM.CNF` selects `SCUS_942.21`, whose header is PS-X EXE.
The existing ISO inventory supplies the named extents; direct payload comparison
and the whole-image identity bind those extents to this input rather than trusting
an unverified extraction directory.

- `EVENT/BONUS.BIN`: LBA 5824 (`0x16c0`), 958,464 bytes, SHA-256
  `c5d881b1fb62c04d1d7f0e5238faff4131676165c08e35b5cadb68b21c89165f`.
  Exactly 36 records of 26,624 (`0x6800`) bytes. Only image 0 is selected.
- Image 0: first 13 sectors / bytes `[0, 0x6800)`, SHA-256
  `5ba6d5c56bbffa7a4c1bdaea392fcf08f265f545588ccc28496462b63d521ea4`.
- `EVENT/REQUIRE.OUT`: LBA 2192, 127,684 bytes, SHA-256
  `97112c50c0b037b2cb5c9d93ae9e2a7506b582c5edbd86a1debfb7ab8e777630`.
  Matches the pinned `target/event.yaml` module identity; load base `0x801bf000`.
  File offsets below are symbol address minus that base, with no executable-header
  adjustment. Every selected range lies within the file.

Raw records, decoded palette words/pieces, the one-off audit script and diagnostic
PNG remain ignored under `scratch/zod-077/`. The PNG is an atlas inspection with
one palette, not the animated/composited game view. No original asset is committed.

## Source map and serialized layouts

All paths here are relative to the pinned reference checkout; symbols and offsets
identify evidence independently of moving line numbers.

- `src/event/require/entry_conditions.c`, `conditions.c`, `screen_support.c`:
  dispatch, general animation, buffer setup and banner loading.
- `include/fft/after_battle.h`, `gfx.h`: banner families, 16-word palettes,
  `sprite_sheet_frame_t`, battle screen-origin constant.
- `target/event.yaml`: REQUIRE module base/hash and addresses of the five records
  below. The six declared quads occupy 72 bytes; the following 24-byte gap is not
  two additional pieces or part of the selected decoder input.
- `include/psx/libgpu.h`: signed-16 RECT, packed unsigned-byte UVs, signed-16 screen
  coordinates, page/CLUT masks, modulation/semitransparency flags and ABR modes.
- `src/lib/menu/display.c::gfx_draw_or_append_gpu_primitive`: immediate DrawPrim
  versus ordering-table head insertion via `addPrim`.
- `src/battle/entry.c::battle_entrypoint`, `src/battle/frame.c::battle_display_init`,
  `src/platform/psyq/libgpu/SetDefDrawEnv.c`: 256x240 display, projection 512,
  horizontal draw origin -128, second framebuffer at Y 240, dithering enabled.

Image record: pixels `[0,0x6400)` are a 256x200 4bpp atlas, 128 bytes per row.
Low nibble is the left pixel; high nibble is the next pixel. Eight little-endian
16-color RGB555/STP palettes occupy `[0x6400,0x6500)`; `[0x6500,0x6800)` is unused
by the selected file struct. This is not a TIM container.

The upload rectangle is `(384,0,64,256)` in VRAM words: page 6, 4bpp, 256 texels
wide. The palette rectangle at REQUIRE address `0x801d0070`, offset `0x11070`,
8 bytes, is `(384,254,64,2)`: four 16-color banks per VRAM row. Initialization
calls `getClut(384,255)`, selecting bank 4, byte offset `0x6480` in the image.
Neither palette bank 0 nor a palette chosen from the tpage record is correct.

The loader reserves `0x8000` bytes but reads `0x6800`; its 256-row upload consumes
`0x8000` bytes. Rows 200..207 cover the file's palette/padding bytes, rows 208..255
include the uninitialized reservation suffix, and the explicit palette upload
overwrites rows 254/255. **Do not manufacture those suffix pixels.** Selected UV
vertices lie at rows 128..191; normalized half-open crops cover rows 128..190,
within the initialized 200-row pixel section even at their far vertex endpoints.
A 256x200 prepared atlas suffices for this slice; reject
references outside it. This does not establish safety for other banner layouts.

Selected REQUIRE records, with complete range hashes in the metadata:

- Quad frames: address `0x801d0df4`, offset `0x11df4`, 6 x 12 bytes. Each record is
  six little-endian signed halfwords: source x/y/w/h, then offset x/y.
- Texture-page records: `0x801d0e54`, offset `0x11e54`, 6 x 4 bytes. Only byte +2
  is used; retain its `0x60` blend bits and OR page 6. Other bytes/bits are not
  texture coordinates, palette selectors or additional supported flags.
- Range: `0x801d0e6c`, offset `0x11e6c`, **2 bytes**, start 0 / end 6. The following
  two alignment bytes are excluded from its hash and meaning.
- Colors: `0x801d0e70`, offset `0x11e70`, 6 x 20 bytes. Four unused bytes precede
  four RGB/code-byte vertices. Only RGB is read; a nonzero unused prefix is not
  an execution flag. Scale is signed-32 `channel * 128 / 128` here, then stored
  to unsigned-byte channels, so the selected source RGB is unchanged.

## Geometry, palette and ordering interpretation

All six selected pieces are 232x31. Three overlap for the first text row, three
for the second. Each triplet uses the same atlas crop; one shadow is displaced
one pixel diagonally. Pieces 0/1 and 3/4 subtract with neutral RGB 128; pieces
2/5 add with equal top-vertex colors and equal, lighter bottom-vertex colors.
Keep the four colors and piece identities in the prepared representation rather
than flattening the composite into a single opaque image.

For a source frame `(sx,sy,w,h,dx,dy)` the full primitive has left/top
`(dx+256,dy+120)`, right/bottom plus `(w,h)`. Its UV bounds are
`(sx, sy-128)` to `(sx+w, sy-128+h)`, assigned to **u8**, hence modulo 256.
Here source Y values 0 and 32 become **128 and 160**, not negative atlas rows.
Screen X subtracts the battle draw origin 128, giving row origins around
`(6,20)` and `(6,56)`; the displaced pieces reach X 239 / Y 88 at their far edge.
This translation is separate from window scaling, pixel-aspect correction and
widescreen UI design. Use a 256x240 logical inspection canvas in ZOD-078.

Wipe arithmetic uses signed-32 intermediates, u8 UV stores and signed-16 position
stores. Selected values fit the screen-coordinate domain; a managed prepared
validator must reject overflow/out-of-atlas geometry rather than allow accidental
wrapping beyond the explicitly normalized source UV conversion.

- Reveal amount R keeps each bottom edge fixed, moves top screen Y to
  `dy+120+h-R`, and top UV V to `(sy+h-128-R)&255`. Bottom UV is
  `(sy+h-128)&255`. This crops; it does not scale the entire glyph into R rows.
- Hide amount D keeps the same bottom edge, moves top screen Y to `dy+120+D`
  and top UV V to `(sy-128+D)&255`. At D=h the quad is degenerate.
- Reveal loops clamp the **shared** offset as each piece is visited, then add 8
  after the triplet. Do not substitute per-piece independent offsets. Source
  variables call indices 2..0 “bottom” and 5..3 “top”; their names do not describe
  their actual top-to-bottom screen positions in this selected table.

For these equal 31-pixel heights, reveal update 1 draws indices 2,1,0 at height 0;
updates 2..5 draw 8,16,24,31 rows. Indices 5,4,3 first join on update 10 at 0 rows,
then reach 31 on update 14. Each subsequent reveal update remains full. Hold
submits cached quads, without recomputing full geometry. Both packet buffers have
settled to full geometry before hold for the selected equal-height data. Hide
updates use D=0,8,16,24,31,31,31,31,31,31; its first update is still full.

Source call order is 2,1,0 before update 10; afterwards 5,4,3,2,1,0, also for
hold/hide. With ordering-table head insertion, traversal reverses that list:
0,1,2,3,4,5. Immediate mode preserves call order. The selected preview explicitly
uses the ordering-table interpretation with no other interleaved primitives;
END-graphic immediate mode and complete BattleStart draw-state reachability are
not proved by the block bytes. This distinction matters because saturated
subtraction/addition is order dependent.

The ordinary graph layout gives pages `0x46` (subtract) and `0x26` (add), with
page base `(384,0)`; GraphType 1/2 moves blend bits two places higher. Normalize to
named add/subtract operations, rather than expose hardware page words at runtime.
All 15 nonzero colors of selected bank 4 have STP set; color word zero is
transparent. Keep RGB555 and STP separately: index 0 alone is not a universal
transparency rule, STP is not generic half-alpha, and STP-black `0x8000` is not
the transparent word. The selected neutral/subtractive and gradient/additive
passes therefore need texture modulation and blending, unlike the current map's
opaque material. Native ABR modes are named in `include/psx/libgpu.h`; GPU
rasterization, dithering, channel precision and edge coverage are not validated
by this source inspection.

The source local `buffer` is uninitialized before its first parity toggle. Do not
declare which packet buffer draws first or silently fix that uncertainty. Both
initial buffers copy the same geometry for the selected six pieces, and both
converge before hold, so a normalized visual projection need not model parity in
this bounded preview. The setup initializes 32 packets and copies 40 (`0x820`
bytes); the other packets are not six additional banner draws. Unequal/larger
piece sets and general persistent packet history remain unsupported.

## Proposed prepared and runtime boundary

ZOD-078 should use the existing loose-file manifest with length/SHA-256 verification
and distinct stable IDs, for example `fft.ps1-us:banner/general-0`, its `/indices`
and `/palette-4` children. These are proposed names, not shipped APIs.

1. A versioned `banner.json` identifies condition 8/image 0, indices/palette IDs,
   atlas dimensions, six normalized pieces in source-index order, their UV/screen
   rectangles, four RGB vertices, add/subtract material, triplet grouping and
   source provenance. Preserve each piece's identity and source selection; do not
   store only a composited screenshot. Hold count belongs to the separately
   selected effect definition, not the reusable artwork.
2. `indices.bin` is 51,200 row-major bytes, one validated index 0..15 per pixel.
   `palette-4.bin` is 32 bytes, sixteen little-endian RGB555/STP words. Prepared
   metadata makes their formats explicit; no VRAM suffix, original struct padding,
   C pointers or native packet buffers are runtime inputs.
3. CLI bridges AssetTool parsing/selection and Content serialization. Verify the
   input profile, named files and selected ranges before conversion. Emit all
   generated payloads/manifests/provenance under a new scratch directory. A failed
   preparation must not publish a partially valid root or overwrite prior output.
4. Content validates the closed schema, unique IDs, referenced formats/hashes,
   counts, positive bounded dimensions, normalized UV/screen ranges, RGB and the
   selected six-piece/31-height profile. Return immutable presentation data.
   Reject unknown fields/versions/materials, duplicate/missing references, hash or
   length mismatches, bad indices and unsupported layout values explicitly.
   Authored test artwork must use separately labeled provenance; it must never
   pretend to have a retail file hash. No original-format parsing enters Content.
5. The composition root associates the loaded artwork with an existing
   `GeneralBannerDefinition(Id,8,H)`; Content may project those three logical
   values only. No texture, palette, file, shader, readiness callback or renderer
   reference enters Core or Zodiac. The simulation retains the existing task
   identity/authority, effect counters and atomic transition contract.

The first Client preview should compose on the CPU into an opaque RGBA8 256x240
inspection canvas, against an explicitly authored background. Use point sampling,
half-open rectangles, 5-to-8 bit replication, RGB modulation with 128 neutral,
and saturating RGB8 add/subtract in explicit ordering-table traversal order.
Interpolate the selected vertical gradient at pixel centers, round down after
modulation, and clamp to 0..255. This is a declared modern preview policy: it does
not reproduce PS1 5-bit intermediate rounding, dithering, affine raster rules,
pixel aspect or a recovered battle background. Record the policy in checks and
output, and do not advertise it as original visual equivalence. No GPU blend
implementation, new package or shader compiler is required for this first slice.
Discard the zero palette word; a nonzero word with STP clear replaces the
background opaquely, while STP-set texels use the piece's add/subtract operation.
Include both cases in authored material checks even though selected nonzero
retail colors all have STP set.

Render from an accepted effect tick/snapshot, with a separately defined pure
presentation projection. RunUntilBlocked/rejected ticks must not advance it.
Revealing/Holding/Hiding transitions need the **completed counters**, not phase
alone: the reveal's last draw precedes Holding/Hiding with zero hide completions;
the hold's last draw precedes Hiding. Circle-skipped ExitYield submits nothing;
natural ExitYield follows the final degenerate hide draw; terminal/cancelled and
initial zero-progress states submit nothing. No stale cached picture should be
mistaken for a new worker submission. Repainting an accepted snapshot is safe and
does not consume another tick. Keep event-exit/restart retention as in ZOD-076.

## Readiness and original latency

The host loads and validates all prepared data and establishes its presentation
resources **before** creating the logical scene/attaching the ready effect. If
readiness fails, show a useful load error; do not allocate the scene task, consume
ticks or acknowledge completion. Device loss after start may pause/rebuild the
view; it cannot complete/cancel the task or change its logical history. The host
chooses explicit ticks and Circle samples; rendering may repeat or omit pictures
without driving task completion.

Retail startup is a different boundary. `conditions_load_banner_image` yields
before reservation attempts, read requests, load-state polls and once after
image/palette uploads before freeing memory. Each loop runs at least once, but
the number of retries, scheduler context, disc state and presentation completion
are not established here. Task 56 is already running during this work. Preloading
is a declared preview policy, **not** evidence of original zero latency; do not
invent a four-frame/fixed loading delay. H+31 remains post-readiness lifetime.
Original opcode-78 integration needs its own startup/scheduling decision and
original observations; no existing event profile/exporter gains that capability.

## Independent expectations and implementation child

The companion authored JSON fixes nibble order, u8 wrap, screen translation,
reveal/hide cropping, order-dependent shared clamps, blend order and RGB8 material
vectors, selected equal-height milestones, skip/cancel/rollback, readiness and
invalid-input expectations. Geometry/color examples are invented, not copied
retail tables. Timing expectations reuse the independently traced ZOD-075 contract.
They are not executed tests or captured game frames.

**ZOD-078 — Prepare and preview the bounded general banner:** implement the narrow
AssetTool decoder/CLI preparation, strict Content representation/loading and a
Client CPU-composited preview consuming the existing Core effect. Convert these
independent expectations into tests, including failed-tick presentation stability,
terminal/restart boundaries, no-original-files runtime and unchanged logical
replay with rendering skipped/repeated. Locally prepare twice and compare output
hashes; verify the pinned source range hashes and selected dimensions/materials.
Provide a finite Windows preview/check with documented synthetic background and
repeatable captures; keep output under scratch and original comparison pending.

Exclude other conditions/images, arbitrary packet/parity emulation, EventEnd,
full opcode admission/playback, original startup latency, camera/map integration,
save/schema changes, widescreen UI work and engine extraction. A further milestone
must decide the next source-traced scene capability after this preview is reviewed.

## Verification performed

Rehashed the BIN and selected files; checked their direct sector payloads and
REQUIRE's pinned module identity; bounded/hashed all five metadata ranges; decoded
image 0 locally and checked all selected source crops lie within initialized
pixels and use bank 4. Verified source symbols/paths, authored expectation
arithmetic, local documentation links, coverage generated view and whitespace.
No C reference, original game/emulator or GPU preview executed. No runtime/tests,
project/package files or original payloads changed. The latest implementation
validation remains ZOD-076's zero-warning Release build and **1,302 passing tests**;
the .NET suite was not rerun for this documentation milestone.
