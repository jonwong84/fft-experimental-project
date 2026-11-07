# Bounded banner triangle color correction

Date: 2026-10-24. Edition: `SCUS-94221`. Managed base: `dd38c49`; clean at entry.
Read-only FFT reference HEAD: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

The two differences retained by the [early-reveal diagnostic](early-banner-reveal.md)
are explained by triangle-dependent color initialization. The corrected actual
C# CPU compositor now matches all eleven selected original canvases exactly,
including all 18,712 affected temporal samples across the ten nonempty anchors.
Only the earlier 21 and 14 blue-channel pixels change. The empty first anchor
still checks background preservation, without claiming an empty artwork success.
The [new metadata](../evidence/visual/banner-triangle-colors-v1.json) preserves
the original diagnostic and binds the corrected outputs separately.

## Source trace and reduction

At the pinned FFT revision, `src/event/require/conditions.c` emits `POLY_GT4`
vertices in top-left, top-right, bottom-left, bottom-right order, with matching
UV corners. `gfx_scale_poly_gt4_colors` writes top colors to vertices 0/1 and
bottom colors to 2/3. The selected scale is 128/128; byte stores retain the source
RGB. Reveal crops move the top XY and UV while preserving the bottom edge and
the original top/bottom vertex colors. The existing whole-function instruction
fingerprints and per-vertex packet checks still bind this interpretation to the
saved original states. No original asset table or pointer becomes runtime input.

The separately pinned emulator implementation study remains
`libretro/beetle-psx-libretro@dda8cf71d420b1972d92324a5edecf2b92b1ec92`.
Its [polygon source](https://github.com/libretro/beetle-psx-libretro/blob/dda8cf71d420b1972d92324a5edecf2b92b1ec92/mednafen/psx/gpu_polygon.c)
and [interpolation constants](https://github.com/libretro/beetle-psx-libretro/blob/dda8cf71d420b1972d92324a5edecf2b92b1ec92/mednafen/psx/gpu_uv_phase.h)
provide the implementation context. Their file identities are recorded; copies
and GPL license text remain ignored under scratch. This revision is **not
identified as the source build of the installed DLL**. Original rendered
captures independently corroborate the selected arithmetic reduction. No emulator
source is copied into the application or compiled/invoked by it.

The command parser retains vertices 1/2 of the first triangle, then consumes
vertex 3 for the second: triangles are TL/TR/BL and TR/BL/BR. `DrawTriangle`
chooses its color seed using the unsorted X comparisons, tracks that vertex
through stable Y sorting, and initializes its interpolants at the selected core
vertex. For these positive-width, positive-height rectangles with equal colors
on each horizontal edge, the first triangle's core is TL; the second's is BL.
Both have zero horizontal color slope and the same quantized vertical slope.
Zero-height draws are already skipped by the bounded compositor.

`CalcIDeltas` computes signed cross-product ratios with truncation toward zero.
`COORD_FBS=12` and `COORD_POST_PADDING=12`; all selected seeds, slopes and steps
are multiples of the post-padding unit. The extra 12 zero bits therefore cancel
in this bounded reduction. Original unsigned 32-bit interpolant subtraction and
addition wrap while removing/adding absolute screen coordinates; they reduce to
the following local, bounded integer expressions:

```text
step = trunc((bottom - top) * 4096 / height)
first  = (top    * 4096 + 2048 + step * row)            >> 12
second = (bottom * 4096 + 2048 + step * (row - height)) >> 12
```

Using the first expression for the entire quad was the error. Truncating the
slope means that its extrapolation from the top need not equal interpolation
backward from the bottom. The half-unit bias is in color space, not a half-pixel
spatial sample. For supported channel values 0..255 and dimensions 1..256, the
reduced signed intermediates fit a C# `int`; the actual prepared quads are 232
pixels wide and crops are at most 31 rows. No arbitrary triangle/overflow model
is inferred from this bounded equivalence.

The shared edge runs from TR to BL. The source's biased Q32 edge walk assigns
integer columns at or right of `ceil(width * (height-row) / height)` to the
second triangle. This is exactly the integer predicate
`column * height >= width * (height-row)` here. Unit checks compare the threshold
against an independent Q32 walk for every width 1..256, height 1..31 and row.
The diagonal itself belongs to the second triangle. Local coordinates ensure
screen placement, crop offset and physical dither origin do not alter this
partition; physical origin still controls dithering separately.

At the observed 24-row blue gradient, top=10 and bottom=120:
`step=18773`. On local row 6, the top seed gives 37 while the bottom seed gives
38. The diagonal threshold is local X 174 for width 232. A texture channel of
31 then modulates/quantizes to blue 64 versus 72. This predicts the sign,
channel, row and triangle side of all 35 retained differences. Rising/falling
gradient and exact-diagonal vectors supplement the original comparison.

## Implementation and independent checks

`FiveBitGeneralBannerPresentation.QuadVertexChannel` selects the seed for each
sample in the already validated rectangular draw. The existing `VertexChannel`
top-seed helper remains available for explicit arithmetic tests/counterfactuals.
`Compose` and `ComposeOver` use the corrected helper; the five-bit policy text
now says `Q12 triangle-seeded vertical colors`. Texture sampling, STP handling,
per-pass blending/saturation, order and dither policy are unchanged. The RGB8
profile remains separate. Content, simulation, input and loading contracts are
not changed.

The new [verifier](../../tools/verification/verify_banner_triangle_colors.py)
reuses the historical hash-bound original/control captures. It verifies all 107
artifact bindings in the original audit, reconstructs the task/frame and queued
draw histories, rechecks probe isolation and palette intervention, then invokes
the actual rebuilt C# host with the retained backgrounds. This run launches no
emulator. Current original/probe PNGs still match their saved VRAM scanouts.
An independently implemented Python material model matches the actual C# output.
The model and C# both follow the source reduction, rather than fitting a per-frame
brightness offset or changing masks/tolerances.

Two corrected runs (`scratch/banner-triangle-colors-a` and `-b`) reproduce all
57 selected review/raw-output artifacts byte-for-byte. Both reports preserve the
earlier comparison failures and record exactly 21/14 changed pixels in the two
corrected frames, with zero changes to the other nine. All original comparisons
are now exact at zero tolerance. Inherited original background pixels do not
validate managed scene rendering.

The actual C# full-visible and five-hide outputs/receipts are byte-identical to
the previously exact artifacts, preserving 3,128 and 6,917 affected temporal
samples respectively. Two finite hidden Windows five-bit previews also repeat
and preserve all fifteen historical PNGs with exact CPU/GPU readback. Their
receipts differ from the old receipt only in the declared policy text. That
standard GPU schedule excludes reveal ticks 4/13; the new original/C# CPU
comparison, not those GPU PNGs, checks the corrected frames. A finite RGB8
Windows preview preserves all sixteen historical files, including its receipt.

## Reproduce and review

Use the existing prepared files and retained original evidence, with fresh
output directories:

```powershell
dotnet restore tools/banner-blend-check/BannerBlendCheck.csproj --locked-mode
dotnet build tools/banner-blend-check/BannerBlendCheck.csproj -c Release --no-restore
python tools/verification/verify_banner_triangle_colors.py --plan scratch/banner-033-comparison-plan/plan.json --output scratch/banner-triangle-colors-c
python -m unittest discover -s tools/verification -p 'test_*.py'
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

Exit 0 means all selected comparisons are exact; 1 retains a valid difference
report; 2 rejects unavailable/invalid inputs or failed execution. The corrected
gallery is `scratch/banner-triangle-colors-a/review.html`. The owner confirmed
"Looks good to me" on 2026-10-24. This records visual acceptance of the corrected
early-reveal gallery. Physical Circle input, original event identity and wider
rendering fidelity remain open. Original artifacts are preserved.

Locked restores, zero-warning Release builds, **1,486 .NET tests** (20 new Client
cases) and **135 Python tests** (5 new authored cases) pass. The native host and
all package locks/solution references remain unchanged. No automatic commit.

This closes the selected 24-row color discrepancy. Physical Circle skipping,
original event identity and complete BattleStart integration remain separate
gates, alongside wider rasterization/scene fidelity. The matched software-core
captures are not a hardware-equivalence proof. Next select a bounded original
Circle-input validation method; do not infer physical input timing from injected
RAM or a logical managed skip test. CRT/aspect/backend/audio remain deferred.
