# General-banner color precision study — ZOD-033

Date: 2026-10-24. Managed entry `89630c2`, clean working tree.
Read-only FFT reference HEAD freshly checked:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

**Result:** A source-informed five-bit model matches all **1,577 previously
selected foreground pixels exactly**, with zero tolerance. The frozen state's
GPU records contain `dtd=0`; its displayed VRAM pixels also match the restored
screenshot exactly on this subset. The earlier modern RGB8 preview still differs
at 1,460 samples. This explains the selected checkpoint's color discrepancy
without applying a brightness offset or relaxing the comparison.

This is a developer study, not a new runtime renderer, hardware-equivalence proof
or replacement for the historical [observed comparison](../development/observed-general-banner-comparison.md).
ZOD-033 remains partial. A bounded compatibility precision profile is now justified
as follow-up work; the declared modern RGB8 compositor has not violated its own
policy and is not silently rewritten here.

Subsequent [five-bit implementation](../development/five-bit-general-banner.md)
now supplies that explicit profile and reconstructs the frozen command writer:
the REQUIRE dark-screen worker's mode precedes all six banner draws. Its actual
C# capture matches the selected samples; the historical study below remains
separate evidence rather than being relabeled implementation validation.

## FFT source trace and draw-state boundary

At the pinned FFT revision, `src/event/require/conditions.c` initializes textured
Gouraud quads with modulation and semi-transparency enabled. The selected
palette, six normalized pieces, vertex colors, UV stores and head-insertion
traversal remain those traced by [ZOD-077](zod-077-general-banner-assets.md).
`gfx_scale_poly_gt4_colors` uses signed-32 scale arithmetic, then byte color
stores. The selected scale is 128/128. Full visible geometry has equal left/right
colors per row; the studied color gradient therefore has no horizontal slope.
The additive/subtractive operations use source palette STP; texel word zero
discards. No new asset format or rule is translated into the game.

`src/battle/frame.c::battle_display_init` initially enables dithering.
That is not proof that every later primitive is dithered. Subsequent draw-mode
commands can replace the state: `src/lib/menu/display.c` has menu texture-page
helpers using `SetDrawMode(...,0,0,...)`; `src/battle/map/runtime.c` has explicit
overlay-dither enable/disable and indoor/outdoor branches. The ordinary-GPU
`src/platform/psyq/libgpu/get_mode.c` places the dither flag in bit 9 of GP0(E1).
The exact command/caller responsible for the captured banner state is untraced.
Do not promote initial battle setup into an always-enabled material policy or
the captured snapshot into a universal always-disabled policy.

The [PSX-SPX GPU documentation](https://psx-spx.consoledev.net/graphicsprocessingunitgpu/)
distinguishes draw-mode dithering from the texture-page bits supplied by a
textured polygon. Its [dithering notes](https://psx-spx.consoledev.net/dev/ps1/gpu/misc/)
describe conversion to five-bit channels with a position-dependent offset.
These are secondary hardware-reference context; the actual checkpoint evidence
is the saved state and original screenshot, and the interpolation study below
uses separately identified emulator implementation research.

## Emulator implementation study

Source-study checkout identity:
`libretro/beetle-psx-libretro@dda8cf71d420b1972d92324a5edecf2b92b1ec92`.
Files, URLs, lengths and hashes are retained in the
[evidence metadata](../evidence/visual/general-banner-precision-study-v1.json).
Source copies and its GPL-2.0 license text stay under ignored
`scratch/banner-precision-sources`. No emulator code/library is imported into
the application or added as a dependency. **This revision is not identified as
the source build of the installed DLL**; their relationship is unverified.

The pinned [polygon interpolator](https://github.com/libretro/beetle-psx-libretro/blob/dda8cf71d420b1972d92324a5edecf2b92b1ec92/mednafen/psx/gpu_polygon.c)
quantizes attribute slopes with 12 fractional bits and starts colors with a
half-unit color bias. For the selected vertical gradients, the study reduces
this to `step=trunc((bottom-top)*4096/height)` and
`vertex=(top*4096+2048+step*row)>>12`, with signed division toward zero.
This half-unit is in color space; it is not the modern preview's half-pixel
spatial sample. General triangles, unequal-height/cropped gradients and arbitrary
fixed-point wrap are outside this reduced model.

The pinned [texture/material implementation](https://github.com/libretro/beetle-psx-libretro/blob/dda8cf71d420b1972d92324a5edecf2b92b1ec92/mednafen/psx/gpu_common.h)
works from the five-bit texture channel. The study computes
`modulated8=(texel5*vertex8)>>4`, then applies the selected dither offset,
right-shifts three bits and clamps to 0..31. Each subtract/add pass saturates in
that five-bit domain before the next pass. It does not expand the texture to
RGB8 before modulation or defer quantization until final composition.

The pinned [GPU output path](https://github.com/libretro/beetle-psx-libretro/blob/dda8cf71d420b1972d92324a5edecf2b92b1ec92/mednafen/psx/gpu.c)
encodes its raw 15-bit scanout as `channel5<<3`, with zero low bits. The existing
modern preview instead uses bit replication when expanding source colors.
Every RGB channel in this original screenshot is a multiple of eight, consistent
with zero-padded scanout. This is an emulator capture encoding observation,
not a statement about analog CRT colorimetry or a reason to darken all assets.

## Frozen state inspection

The tool reads the existing hash-bound state without changing or executing it.
The supported wrapper is RZIP v1 with 128-KiB zlib chunks, then RASTATE v1 and
its aligned `MEM ` block, then labeled `MDFNSVST` subsystem/field records.
The formats were checked against RetroArch Git `59e4fc7`
([RZIP stream](https://github.com/libretro/RetroArch/blob/59e4fc7/libretro-common/streams/rzip_stream.c),
[state blocks](https://github.com/libretro/RetroArch/blob/59e4fc7/tasks/task_save.c))
and the pinned Beetle [state serialization](https://github.com/libretro/beetle-psx-libretro/blob/dda8cf71d420b1972d92324a5edecf2b92b1ec92/mednafen/state.c).
This is an optional developer inspector for this labeled format, not original
memory-card/save interoperability or a runtime dependency. Replay blocks are
skipped, not played. Parsing enforces allocation/decompression limits, chunk and
block lengths, required records, duplicate rejection and supported display mode.

The decoded local state is 17,113,728 bytes. GPU `dtd` is 0; `DisplayMode` is 0;
the displayed framebuffer starts at `(0,240)` in the 1024x512 VRAM. The VRAM
record hash is `97191d6c51d35f4cc3a1656f2e9b3fa1944c7479008fe0d7be04ac0492fadb6d`.
On the selected 1,577 samples, its zero-padded RGB output matches the restored
original image exactly. It therefore provides an additional frozen-frame check
beyond the earlier repeat-restored PNGs. It does not supply a per-primitive GPU
command trace; the original logical tick and event path remain unverified.

## Experiments and interpretation

The source-informed model uses the six prepared pieces in the already declared
order. Black/white five-bit endpoint evaluation confirms that all 1,577 selected
samples remain background-independent for the state-selected, dither-off model.
The matching model has **zero changed pixels and zero RGB deltas** at both lines.
The same frozen input/plan/tool run twice produces byte-identical reports.

Diagnostic counterfactuals retain exact comparisons rather than widening a
tolerance. Bit-replicated output differs at 1,441/1,577 samples. Omitting the
half-unit color bias differs at 192/1,577. Forcing dithering on leaves only 1,433
background-invariant samples, of which 657 differ; 144 newly background-sensitive
pixels are explicitly excluded and counted, not compared against a guessed
background. These are mechanism checks, not interchangeable masks or a general
search for fitted parameters. Early exploratory integer/pixel-center variants
remain under scratch; the retained study parameters have source provenance and
the dither choice comes from the frozen state.

The measured difference has a concrete precision/output-encoding explanation
for this foreground subset. Full primitive coverage, background-sensitive
shadows, blending with the original scene, triangle boundaries and other GPU
states still need their own evidence. Neither an exact subset nor current
emulator source establishes original-hardware equivalence. Raw native captures,
display aspect and later CRT filtering retain their separate policies.

## Reproduction and next implementation gate

Use the existing Python 3.12+/Pillow 12.3.0 developer environment and existing
ignored comparison plan. No network or emulator execution is needed to rerun:

```powershell
python tools/verification/analyze_banner_precision.py --plan scratch/banner-033-comparison-plan/plan.json --output scratch/banner-precision-e
python -m unittest discover -s tools/verification -p 'test_*.py'
```

Choose fresh output names; e/f already contain the demonstrated runs. The tool
reuses all input/provenance and managed receipt checks from the observed
comparison, records state flags, sample masks, exact signed deltas and tool/input
hashes, and preserves prior outputs. Exit 0 means the state-selected model matches
the selected samples; 1 retains a valid difference report; 2 rejects invalid
inputs without publishing a report. None is a global fidelity verdict.

All **66 Python tests pass**, including 14 new authored precision/state-format
regressions. No C# changed or .NET suite reran; its last result remains 1,388 tests.
The raw screenshots, state, source copies, decoded memory and generated reports
are not committed. No automatic commit was made.

Next implementation should select an explicitly named bounded five-bit banner
profile, with authored arithmetic tests and original comparisons. Carry per-draw
dither state explicitly; establish its banner-time provenance before deciding a
compatibility default. Keep modern RGB8 expectations identifiable. Do not make
every banner/material inherit `dtd=0` from this one checkpoint. Pre-banner input
anchors are still needed for reveal/hide timing; ZOD-079 remains deferred.
