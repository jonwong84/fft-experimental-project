# ZOD-033 — First observed general-banner comparison

Date: 2026-10-24. Managed entry: `baa37d5`, clean working tree.
Read-only decompilation HEAD freshly checked:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

The frozen original state and managed preview now have an explicit, bounded
**fully visible artwork comparison**. The text appears at the same native scale
and positions after removing measured emulator borders. Strict sampled color
equality fails: the managed modern RGB8 preview is brighter in the selected core
pixels. No tolerance was relaxed, fidelity established or runtime code changed.
ZOD-033 remains partial.

Subsequent [color precision study](../architecture/general-banner-native-color-study.md)
now explains the selected foreground discrepancy: the frozen GPU state has
dithering off, and a source-informed five-bit gradient/material/scanout model
matches all 1,577 samples exactly. The historical modern comparison below remains
unchanged. This is a developer study, not a production renderer or timing proof.

## Inputs and alignment

The [original state observation](../evidence/visual/original-banner-state-v1.json)
binds the SCUS-94221 edition, private emulator environment, frozen state and
repeat-restored image. Select `restore-004-a/frame.png`, SHA-256
`4fc1a51f516efbd1c94493476f0b382e901e2c0e0baf7cc865a391e4463d65aa`.
The second restore has identical bytes. This is not the owner's earlier screenshot
at an unverified frame, and BSV playback remains unverified.

The original raw image is 280x240 RGB. All pixels in columns `[0,12)` and
`[268,280)` are black; the remaining content spans `[12,268)`. Declare
`originalRect=[12,0,256,240]`, `candidateRect=[0,0,256,240]`. The comparator checks
those borders and exact dimensions. It does not search offsets, resize, resample,
correct color, discard rows or change the source PNG. This is a recorded viewport
mapping, separate from camera alignment of the unmatched backgrounds.

The managed image is `scratch/banner-078-preview-a/tick-020.png`, checked against
its capture receipt's raw RGBA digest. Tick 20 is `Holding`, with all six pieces
at CropTop 0 / VisibleRows 31. Its source revision and zero renderer task
acknowledgments are checked. The original visually shows both complete text lines;
its logical tick, hold age, event path, simulation state and frame-to-logical-tick
relationship remain unknown. **The anchor matches visible artwork, not clocks.**

The prepared definition, indices and palette must match the previously reviewed
[ZOD-078 identities](prepared-general-banner.md). Their bytes stay local. The
existing source normalization places the quads near `(6,20)` and `(6,56)`;
the fresh read of `src/event/require/conditions.c` and `entry_conditions.c` at the
recorded revision introduces no new behavior translation.

## Separate background effects from color diagnostics

Each line reports its full 233x32 half-open primitive-union rectangle, including
the displaced shadow. These exact raw diagnostics visibly differ because the
original battle and managed checker backgrounds are different. They are not a
whole-scene fidelity test, and their declared category does not diagnose a cause.

For more useful color measurements, the tool computes the **managed material's
background sensitivity** from the prepared indexed artwork. It applies the
documented RGB8 add/subtract/opaque operations to black and white endpoints.
Those operations are monotone in each background channel. Equal RGB endpoints
therefore identify pixels whose managed output is independent of any background.
The model uses the existing point sampling, RGB555 bit expansion, pixel-center
gradient, neutral-128 modulation and flooring policy. Every selected pixel must
agree exactly with the actual managed capture before its mask is used.

The mask is a diagnostic selection derived from the managed policy. It is **not
an independent original geometry oracle**, and its invariance does not prove the
original GPU's material/raster behavior. It cannot detect every extra/missing
pixel outside the selection. All touched texture-support pixels and the raw
rectangles remain separately reported; background-sensitive support is counted.
Opaque screenshots do not independently resolve transparency/order.

No original/managed color acceptance tolerance is chosen. The diagnostic checks
exact equality, preserving every difference. The first line has 969 invariant
samples, of which 893 differ; channel maxima are `[16,16,15]`. The second line
has 608 samples, of which 567 differ; maxima are `[16,15,14]`. Across all **1,577
samples, 1,460 differ**. Original-minus-managed deltas are never positive in this
subset: the original is equal or darker. The remaining **1,551 touched pixels**
are background-sensitive in the managed model and receive separate diagnostics.

This direction and magnitude are consistent with the already documented native
precision/dithering limitations, but are not proof of their cause. Do not subtract
a guessed brightness offset, change the declared modern renderer or widen a
tolerance to make this result pass. A separately traced native material/gradient
precision investigation is needed before any compatibility correction.

## Manual inspection and limits

The original restored PNG and managed tick-20 PNG were inspected locally. Both
show the same complete two-line artwork, at matching apparent native size and
line positions after the explicit border mapping. No missing line or large
placement shift was observed. The original is darker; backgrounds and shadows
visibly differ. This is a bounded human inspection, not a proof of pixel-perfect
geometry, full-scene rendering, event-401 activation or timing fidelity.

The generated local `review.html` presents both raw inputs at 2x nearest-neighbor
display scale with the declared border mapping and expandable raw metrics. CSS
display scaling does not enter report calculations. It embeds only local image
bytes and has no network dependency.

## Reproduction and contract

Use Python 3.12+ and the existing pinned Pillow 12.3.0 developer environment.
No .NET runtime dependency or emulator execution is added by these tools:

```powershell
python tools/verification/prepare_observed_banner_comparison.py --managed-root scratch/banner-078-preview-a --prepared-root scratch/banner-078-a --output scratch/banner-033-comparison-plan
python tools/verification/compare_observed_banner.py --plan scratch/banner-033-comparison-plan/plan.json --output scratch/banner-033-comparison-a
python -m unittest discover -s tools/verification -p 'test_*.py'
```

Choose fresh output names on repeats. The preparer snapshots the committed state
observation into its scratch plan directory and validates inputs before atomic
publication. It intentionally selects this one reviewed original checkpoint and
managed tick-20 anchor. The comparator's closed `fft.observed-banner-comparison`
v1 contract records explicit original PNG/observation/state/profile, managed
PNG/receipt/tick, prepared definition/indices/palette, alignment and rationale.
All referenced files have safe scratch paths, SHA-256 identities and size budgets.
Original observation bindings, prepared pinned bytes, full-visible receipt and raw
pixel hash, PNG dimensions/mode/opacity and border contents must agree.

The separate `fft.observed-banner-report` v1 contract explicitly records
`originalComparisonPerformed=true` but `originalFidelityEstablished=false`, no
simulation or timing comparison and no automatic cause diagnosis. Exit **0** means
strict equality of the sampled invariant pixels only; **1** writes a valid
difference report (the observed result here); **2** rejects invalid/unavailable
evidence without publishing a report. Existing outputs are preserved. The
synthetic-only v1 comparison tool remains unchanged and still rejects original
evidence; no original image is relabeled synthetic.

Two report/review runs are byte-identical. See
[committed comparison metadata](../evidence/visual/observed-general-banner-comparison-v1.json)
for their identities and scoped findings. All **52 Python tests pass** (22 new
authored cases for masks/materials, signed metrics, explicit borders, image guards,
provenance/receipt/payload binding, exact differences, report repeatability,
frozen preparation and output preservation). Tests need no game files, emulator,
GPU or reference checkout. No C# changed; the full .NET suite was not rerun and
its last recorded result remains 1,388 tests.

Next after the linked precision study: select a bounded precision profile with
explicit draw-state provenance before changing rendering; obtain a
pre-banner anchor and documented inputs for reveal/hide timing. Broader scene
state/camera comparisons and justified original fidelity thresholds remain open.
