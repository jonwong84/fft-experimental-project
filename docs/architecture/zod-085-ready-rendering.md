# ZOD-085 — Ready artwork, readiness and rendering policy

Date: 2026-10-25. Edition: `SCUS-94221`. Managed starting revision:
`68c97dd87fdba4307d4299c0633bdc78c9d98431`; working tree clean on entry.
Read-only FFT reference HEAD: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

Select a separate **Ready RGB5 CPU compositor**, presenting its completed opaque
256x240 buffer through the Windows host. It consumes immutable prepared artwork
and accepted [ZOD-084 snapshots](../development/ready-display-transactions.md).
The renderer is future ZOD-086 work. This spike changes no runtime API, renderer,
asset decoder, package or test suite. The existing general-banner schemas and
RGB8/RGB5 policies remain unchanged.

The [study metadata](../evidence/events/ready-rendering-source-v1.json) binds the
source files, offline checks and retained observations. The
[32 presentation expectations](../evidence/events/ready-presentation-contract-v1.json)
are independently authored future implementation checks. They supplement
RDY-031 from the [historical Ready contract](../evidence/events/ready-display-contract-v1.json);
they do not claim that RDY-031 or a new runtime compositor now executes.

## Source trace and selected artwork

The [ZOD-083 trace](zod-083-ready-display.md) identifies the condition-0 dispatcher,
message 0 and resident image 0. `conditions_special_thread` in
`src/event/require/conditions.c` selects range `[0,6)`, six signed-halfword quad
records and four RGB corners per 20-byte color record. The unused four-byte prefix
and each corner's fourth byte do not influence rendering. No general-banner
tpage table is consulted. `gfx_init_poly_gt4_array_32(32,0)` initializes page 6,
modulation enabled and semi-transparency enabled; palette origin `(384,254)` plus
32 words selects CLUT `(416,254)`, value `0x3f9a`, **bank 2**.

The selected extraction inputs retain the verified whole-file hashes from
[ZOD-083 metadata](../evidence/events/ready-display-source-v1.json): BONUS.BIN is
958,464 bytes and REQUIRE.OUT is 127,684 bytes at load base `0x801bf000`. Image 0
is `[0,0x6800)`. Its pixel region `[0,0x6400)` contains 256x200 packed 4bpp texels;
the low nibble is the left texel. Sixteen little-endian RGB555/STP words at
`[0x6440,0x6460)` form bank 2. Loader reservation/upload suffixes are excluded.

REQUIRE selections are range bytes at `0x801d153c` (2 bytes), six quad records at
`0x801d0ee8` (72 bytes), six color records at `0x801d1548` (120 bytes) and the
palette rectangle at `0x801d0070` (8 bytes). Their complete-range hashes remain
linked in the historical metadata; raw tables and decoded artwork stay in scratch.

For source `(sx,sy,w,h,dx,dy)`, `setUV4` stores `(sx,sy)` through `(sx+w,sy+h)`
as bytes, and `setXY4` stores `(dx+256,dy+120)` through the far edges as signed
halfwords. Unlike the general banner, **Ready does not subtract 128 from source
V**. Applying the selected battle horizontal draw offset -128 gives normalized
screen `(dx+128,dy+120)`. This is a native canvas translation, unrelated to CRT
aspect correction or widescreen layout.

The verified glyphs are 16/17 pixels wide and 24 high; their union lies in
half-open screen bounds `(42,98)..(224,122)`. Sampled atlas coordinates are within
U `[32,240)` and V `[0,48)`, safely inside the initialized pixel region. All four
corner colors must survive preparation. Left/right colors can differ and the
gradient is piecewise triangular, not a separable vertical or bilinear gradient.

Both packet banks copy the initialized geometry. `buffer=0`, followed by a toggle
before each submission, makes bank 1 first. Source submission visits indices
0..5; `src/lib/menu/display.c::gfx_draw_or_append_gpu_primitive` uses immediate
DrawPrim or ordering-table head insertion. Select the verified queued branch:
traversal **5,4,3,2,1,0**, without other interleaved effects. Immediate mode and
arbitrary shared-pool packet histories are outside this preview. Packet-bank
parity need not enter logical state because both copies carry the same geometry
and every submitted corner color is refreshed.

## Arithmetic and rasterization contract

`gfx_scale_poly_gt4_colors` multiplies each unsigned-byte channel by signed-32
scale, divides by 128 toward zero, then stores a byte. For selected nonnegative
scale 48..128 this is integer floor. Scale applies **to the four corners before
interpolation**. For example, channel 148 at scale 48 becomes 55, and channel 255
becomes 95. Scaling an interpolated sample or the completed framebuffer can differ.

The separately pinned implementation study is
`libretro/beetle-psx-libretro@dda8cf71d420b1972d92324a5edecf2b92b1ec92`, using
the already retained `gpu_polygon.c`, `gpu_common.h` and `gpu_uv_phase.h`.
Their file identities are verified against the
[triangle-color evidence](../evidence/visual/banner-triangle-colors-v1.json).
This revision is not identified as the installed DLL's build. These files are
source-study inputs only; no emulator implementation/library is copied into or
invoked by the application.

The parser splits TL/TR/BL/BR into triangles TL/TR/BL and TR/BL/BR. Its unsorted-X
core selection and stable Y sorting seed their colors at TL and BL respectively.
`CalcIDeltas` truncates signed slope divisions; color seed bias is half a color
unit. For scaled channel corners A/B/C/D, positive width W/height H, and integer
sample `(x,y)` inside the rectangle, the bounded Q12 reduction is:

```text
first triangle when x*H < W*(H-y):
    dx = trunc((B-A)*4096/W); dy = trunc((C-A)*4096/H)
    color = (A*4096 + 2048 + dx*x + dy*y) >> 12
second triangle, including the shared diagonal:
    dx = trunc((D-C)*4096/W); dy = trunc((D-B)*4096/H)
    color = (C*4096 + 2048 + dx*x + dy*(y-H)) >> 12
```

The prior [triangle-color study](../development/banner-triangle-colors.md) traces
the Q32 edge walk and its second-triangle diagonal ownership. Its predicate
depends on geometry, so it applies to these positive rectangles even with four
different colors. Coverage is half-open `[0,W) x [0,H)`; the diagonal is shaded
once. One-to-one UV slopes are exact, so point sampling uses `(U+x,V+y)` with no
filtering, wrapping, perspective correction or half-pixel geometry offset. The
half-unit color/UV initialization does not introduce a second texel at integer
one-to-one samples.

Use signed, checked arithmetic at validated bounds. Byte channels and scales up
to 128 give products at most 32,640. For W=16/17, H=24 the cross-product numerator
times 4096 is at most `255*24*4096 = 25,067,520`; reduced seed/step expressions
fit signed 32-bit integers. Absolute screen-coordinate wrapping is unnecessary
after the bounded local-coordinate reduction. Do not infer a general PS1 GPU or
arbitrary-triangle overflow policy from this proof.

For each palette word, zero discards the texel. Any nonzero word with STP clear
writes the modulated source opaquely; STP set enables average blending. In
particular, `0x8000` is visible STP-black, and index 0 is not intrinsically
transparent if a synthetic palette assigns a nonzero word there.

**Bank 2 has one zero word, 14 nonzero STP-clear words and one STP-set word.**
Across the selected glyph rectangles, 2,328 texel samples include 1,049
transparent, 1,202 opaque and 77 STP samples. The previous general-banner bank-4
policy of blending every nonzero texel would be wrong for most Ready pixels.

With dither explicitly disabled, channel arithmetic is:

```text
source5 = clamp(((texture5 * interpolatedColor8) >> 4) >> 3, 0, 31)
output5 = STP ? (background5 + source5) >> 1 : source5
output8 = output5 << 3
```

The packed average in the pinned `gpu_common.h` reduces to the joint sum shifted
once. Thus `(1+1)>>1 = 1`; separately shifting the two operands would produce 0.
Quantize/modulate before blending, preserve each draw's intermediate RGB5 result,
and encode opaque output RGBA with zero low RGB bits and alpha 255. No RGB8 alpha
blend, bit replication, gamma transform or framebuffer-wide fade is selected.

## Explicit presentation state and accepted snapshots

Select a named Ready-only policy, `ready-glyph-rgb5-v1`. Its state declares point
sampling, modulated 4bpp texture, semi-transparency enabled with average mode,
zero texture window, dithering disabled, mask evaluation/set disabled, a full
256x240 local clip, normalized draw origin and an explicit physical framebuffer
origin fitting VRAM (X 0..768, Y 0..272). The standard authored preview uses
physical origin `(0,0)`. Unsupported state must reject before drawing; there is
no inherited GPU state. Dither-on, texture windows, mask-bit framebuffer effects,
interlacing, multisampling and alternate graph types require later policies.

An optional background is exactly 256x240 opaque RGBA with zero low RGB bits;
invalid length/alpha/encoding rejects before composition. Without it, use the
existing declared 16-pixel checker, quantized to RGB5, as a synthetic background.
It is not an original battle scene. Compose into a new buffer, preserving caller
background/artwork data. With dithering off, valid physical origin changes do
not change local pixels.

Projection reads `ReadyDisplayState.DrawScale`. A nonnull accepted value submits
all six full glyphs in reverse index order; a null value submits none. Birth draw
48 (128 for H=0), early release jump to 128, seven fade-out submissions and empty
extra yield come from the existing aggregate. There are no wipe crops or triplet
selection. At R+7 no previous draw may be cached as current artwork, even while
the child/dispatcher is still active. Completed/cancelled states also draw none.

CPU composition and GPU upload/presentation are read-only consumers of accepted
state. Paints, repeated snapshot reads, device/resource rebuilds and output
captures cannot tick the scene, consume Circle, allocate tasks or acknowledge
completion. The C# CPU buffer is the arithmetic oracle for the finite host's GPU
readback; nearest-neighbor display of that buffer does not prove a native GPU
quad implementation.

## Offline preparation and strict prepared files

ZOD-086 should add a distinct versioned artwork format `fft.ready-artwork`, schema
1, and a separate authored event profile. Do not widen `fft.general-banner` or
reuse its bank-4/triplet/crop/material validators. New preparation is offline:

1. Verify the selected edition/import manifest, BONUS/REQUIRE whole-file length
   and hash, reference revision, selected image/range/quads/colors/palette rectangle
   and range hashes before decoding. Reject truncation, alternate selections or
   mismatched provenance. Read only the selected bounded ranges; never decode
   an uninitialized upload suffix or silently wrap source UV endpoints.
2. Emit three distinct stable logical IDs for artwork, indices and palette in
   the existing length/SHA-256 loose manifest. Indices are exactly 51,200 bytes
   (`index4-unpacked-u8`, each 0..15); palette exactly 32 bytes
   (`rgb555-stp-le16`, bank 2). Atlas dimensions are 256x200. Artwork identifies
   condition 0/image 0 and retains six source-indexed pieces, normalized XY/UV,
   width/height, four byte RGB corners and named average material. Hold belongs
   to the logical/event definition, not the reusable artwork.
3. The schema permits widths 16/17 and height 24, indices 0..5 exactly once in
   source order, and four corners in TL/TR/BL/BR order. Require
   `0<=X<=256-W`, `0<=Y<=240-H`, `0<=U<=255-W`, `0<=V<=200-H`. These safe bounds
   reject far-U byte wrapping as well as invalid sampled ranges. Authored tests
   may use synthetic corners/palettes and overlapping valid rectangles to expose
   order sensitivity. Verified-retail conversion must preserve the selected
   layout/colors exactly. No raw packet addresses, buffers, unknown material,
   scale animation, sampler state or old triplet/crop fields enter artwork.
4. Use closed JSON validation: metadata at most 32 KiB/depth 12; required exact
   fields/types/version/formats; reject duplicate/unknown/null fields at every
   depth, fractions, invalid IDs, duplicate child IDs, bad counts and oversized
   arrays before allocation. Allow only explicit authored-test provenance with
   no retail hashes or the pinned verified-retail source identities. A manifest
   hash authenticates file consistency, not the historical truth of a provenance
   label; original evidence remains separately hash-bound.
5. Publish a fresh prepared root only after validation; preserve existing output
   on failure. Use the current manifest/store path containment and hash checks.
   Load immutable artwork/indices/palette from prepared files before creating
   scene state. All source BIN/REQUIRE/BONUS reads remain offline under scratch.

The host then validates the authored event and matching logical
definition/artwork/owner binding, builds CPU data and creates every required GPU
resource, and only then creates the scene/consumer and runs admission. Missing or
bad files, an unsupported profile or failed resource creation produces no scene,
task allocation, logical tick or partial success receipt. This is a preloaded
composition boundary, not a model of the original loader's duration. Later device
rebuilds use retained validated data and preserve the logical clock/state.

Use small Ready-specific decoder, Content model, projection and compositor types.
Reuse proven low-level unpacking, manifest, immutable-store and texture-upload
facilities where their contracts match. No broad graphics abstraction or rewrite
of the old general-banner renderer is needed for this bounded integration.

## Evidence, validation and remaining gates

The offline study reruns the existing ZOD-083 inspector against its two hash-bound
independent states. Resident selected code/tables/VRAM and six queued GT4 packets
still match; generated source reports equal the historical report. Source frame
9003 queues scale 48/bank 1; independent frame 9110 queues scale 128/bank 0, both
with preceding queued draw mode `0xE1000006`. These are not a duration pair.

Saved GPU fields separately show texture window/masks/dither zero and draw offset
X=-128 in both snapshots. Their draw/clip origin Y is respectively 240/0 while
display starts at 0/240. This demonstrates why queued Ready draws must not be
compared directly to the same state's displayed scanout. The Ready worker does
not emit every GPU environment command; the selected standalone policy makes all
required state explicit without claiming it is universal original state.

Two final offline reports repeat byte-for-byte. The reduced four-corner formulas
agree with a separately expressed cross-product/unsigned-Q24 calculation for all
97,776 channel samples from the six glyphs, all 14 distinct lifecycle scales and
three channels. Six authored asymmetric/falling-gradient vectors supplement that
comparison. Another 3,072 component checks reduce the packed average expression.
These validate the math derivation, not original Ready scanout or a runtime
implementation. The failed exploratory report directory is retained under scratch.

The local study scripts and source copies are hash-bound by the metadata and
retained under ignored scratch. With those verified local inputs available, use
fresh output roots to repeat the study without launching RetroArch:

```powershell
python scratch/trace-ready-rendering-085.py scratch/ready-rendering-085-d
python scratch/check-ready-085-evidence.py
python tools/verification/verify_baseline_coverage.py --reference-root C:/repositories/fft_decomp
python -m unittest discover -s tools/verification -p 'test_*.py'
```

Validation on 2026-10-25: pinned source/input/report hashes, clean reference,
contract shape/IDs/authored numeric vectors, coverage inventory/view, documentation
links and whitespace checks pass. All **163 Python regressions** pass. No runtime
or test code changed; the last full locked restore/zero-warning Release build and
**1,682 .NET tests** remain ZOD-084's validation, not a new .NET run.

ZOD-086 can now implement the selected authored prepared preview, execute
RPR-001..032 together with RDY-029..032, and check repeated finite receipts,
CPU/GPU readback and unchanged legacy outputs. It must keep original events 4/401,
condition 1, WORLD/tutorial/speed changes and combined general→Ready retail
playback closed.

Original Ready visual acceptance remains a separate gate: retain hash-bound
queued-to-displayed continuations with matching code/tables/texture/palette,
draw state and frame/worker history; recover background-sensitive STP pixels
with independently bound background evidence; compare accepted C# samples at
birth/full/fade-out/empty frames and retain an owner-review gallery. Circle,
natural release/clear and original loading/timing claims need their own paired
continuations. No emulator, new capture, renderer, original comparison or visual
equivalence result is claimed by this spike. CRT/4:3/shaders/backend/audio remain
the owner's later presentation work.
