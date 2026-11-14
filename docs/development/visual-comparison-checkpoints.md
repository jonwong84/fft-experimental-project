# ZOD-033 — Bounded visual comparison infrastructure

Date: 2026-10-24. Managed baseline: `c561efc`.

**Implemented:** headless authored/same-environment PNG comparisons, strict
checkpoint bindings, regional RGBA diagnostics, a synthetic Windows capture
adapter and a separate [bounded observed banner diagnostic](observed-general-banner-comparison.md).
**Still pending:** matched original timing/scene state and reviewed fidelity
thresholds. The existing RetroArch installation now has a
[private boot-checked reference profile](retroarch-reference-capture.md), but no matched
full battle comparison yet. The owner has supplied a raw banner screenshot/state;
[two bounded restores](retroarch-reference-capture.md#owner-captured-banner-checkpoint)
produce identical banner scene images. ZOD-026's executable decoder oracle does not
supply a rendered scene reference. ZOD-033 remains partial.

No original source rule, engine/game simulation, managed project/package graph,
save schema, event capability or client behavior changes in this milestone. The
optional Python tools are development validation only. PNGs, receipts, manifests,
plans and reports stay under ignored `scratch/`; no captured media is committed.

## Reproduction

Use Python **3.12+** with **Pillow 12.3.0**, pinned in
[`requirements-visual-comparison.txt`](../../tools/verification/requirements-visual-comparison.txt).
The bundled workspace Python 3.12.14 already contained that Pillow version; no
installation was performed. On this workstation its executable is
`C:/Users/Jonathan/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`.
Replace `python` below with your selected interpreter if it is not on PATH.
The tests and comparison command require no GPU, BIN, BIOS, extracted game files,
reference checkout or C toolchain. Only the two optional Windows capture commands
need a desktop/GPU and a built Client.

```powershell
dotnet build Fft.sln -c Release
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --window-check scratch/visual-033-a
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --window-check scratch/visual-033-b
python tools/verification/prepare_synthetic_visual_comparison.py --reference-root scratch/visual-033-a --candidate-root scratch/visual-033-b --output scratch/visual-033-plan
python tools/verification/compare_visual_checkpoints.py --plan scratch/visual-033-plan/plan.json --reference scratch/visual-033-plan/reference.json --candidate scratch/visual-033-plan/candidate.json --output scratch/visual-033-report-a
python -m unittest discover -s tools/verification -p test_visual_checkpoints.py
```

Each generated directory must be fresh; choose new names on repeat runs. The
adapter binds native 960x640 `--window-check` receipts at presentation frame 120.
It rejects failed flags, other viewports/sizes, other evidence kinds, missing files
and existing output. Original sprite/map/banner inputs are not needed. The generic
comparison tool accepts explicitly authored RGBA expectations too; the tests create
their tiny images from invented bytes at runtime, without using GPU screenshots
as independent authored expectations.

## Contracts and reporting

[`compare_visual_checkpoints.py`](../../tools/verification/compare_visual_checkpoints.py)
owns the closed `fft.visual-comparison`, `fft.visual-capture` and
`fft.visual-comparison-report` v1 contracts. The plan and capture validators in that
file are the authoritative field lists. All unknown/missing/duplicate JSON fields,
versions, bad IDs/hash shapes, unsupported modes/categories, duplicate/missing/extra
checkpoints, invalid timing domains, dimensions and regional bounds reject.
Metadata is bounded to 64 KiB; canvases to 4096 per axis and 4,194,304 pixels;
plans to 64 checkpoints and 16 regions; source PNGs to 32 MiB. Native PNG RGB8/RGBA8
is required: no palette/gray/16-bit conversion, resizing, resampling, alignment
search or implicit color-management correction. RGB8 gains opaque alpha only.
Regions use half-open `[x,y,width,height]` rectangles.

Each capture binds its PNG and source receipt by local path and SHA-256, with
environment, view binding, explicit checkpoint/tick/timing domain and simulation
projection hash. Plan/capture metadata hashes, full validated metadata, tool
versions and comparator source hash are retained in the report. Paths must remain
below repository scratch; traversal, absolute and reparse-point paths reject.
Failed validation publishes no report root; successful validation publishes a
staged new directory. Existing output is preserved. Reports are deterministic
for identical input bytes and tool source/version; source line-ending changes
also change the recorded harness hash.

Exit codes: **0** comparison passed; **1** valid evidence differed (report written);
**2** invalid/unavailable evidence (no comparison report published). A missing
reference cannot become a successful comparison. `originalComparisonPerformed`
is always false. Original-capture modes/kinds are explicitly rejected in v1;
the separate observed banner tool now owns its own capture/context contract.

Before comparing pixels, the tool checks:

- canvas and view-binding compatibility;
- the recorded environment in same-environment mode;
- simulation-projection hash agreement;
- each side's tick **and** timing domain against the declared checkpoint.

Simulation/timing/view/environment failures are reported independently and prevent
pixel comparison at that checkpoint. A presentation frame is not a logical tick.
For compatible inputs, regions report raw changed-pixel counts, RGB-only and
alpha-change counts, maximum/summed absolute RGBA deltas, changed bounding boxes,
and pixels above the declared per-channel tolerance. The threshold test passes if
that last count is at most `maxChangedPixels`. Inclusive boundary behavior is
tested. Raw differences remain visible even when an explicit tolerance admits
them. Every region requires a nonblank rationale, retained with the results.

Region categories (`geometry`, `palette`, `transparency-order`) are **declared
inspection targets**, not automatic cause diagnoses. An opaque screenshot's alpha
cannot expose every transparency bug: a blend/order error may appear as changed
RGB. Human inspection and targeted scene controls still determine the cause.

## Synthetic adapter and justified thresholds

[`prepare_synthetic_visual_comparison.py`](../../tools/verification/prepare_synthetic_visual_comparison.py)
projects the existing host's turn receipt into an explicit versioned scalar
record: logical tick, phase/active ID, RNG state/draws and ordered unit IDs,
Speed/CT/tie keys/turn/movement/action flags. SHA-256 covers sorted-key compact ASCII
JSON of that specified projection, not object hash codes or an arbitrary serialized
.NET object graph. `simulationSha256` means this **bounded turn projection**, not
all battle state. HP, position and definitions are not present in the legacy
receipt and are not cross-capture simulation assertions here; each host run still
checks its full existing immutable-snapshot diagnostic. A future multi-state battle
capture needs additional explicit projections before it can claim those checks.

The view binding names the synthetic host/profile, viewport/canvas and projection
version. Recorded environment comprises runtime, OS and adapter; it does not
invent a graphics-driver version absent from the source receipt. This is a local
same-recorded-environment regression, not a cross-driver determinism guarantee.

The selected regions are the full 960x640 canvas, first-unit/background rectangle
`[456,228,48,72]`, and its authored head-palette rectangle `[468,231,24,24]`.
**All tolerances are zero:** this static authored point-sampled scene, fixed state,
native canvas and matching recorded environment admit no expected pixel drift.
The full-canvas region catches changes outside the diagnostic regions. No threshold
was relaxed after inspecting a failure. Thresholds for original/modern raster,
color, lighting and camera differences remain unselected; no broad whole-frame
tolerance is guessed to hide those known differences.

## Validation performed and next gate

[Committed evidence metadata](../evidence/visual/synthetic-checkpoint-comparison-v1.json)
records capture/report hashes and the limited evidence classification without media.

**22 Python tests pass**, including malformed contracts, distinct timing/state/view/
environment failures, exact regional metrics, alpha versus RGB, threshold boundaries,
half-open crops, explicit turn projection, rejected original claims, format/size
guards, safe paths, identical reports, wrong hashes, preserved output and valid
one-pixel fault injection returning a visual failure. Tests use authored bytes and
temporary files only. No .NET source changed; the last complete managed validation
remains the earlier zero-warning Release build and **1,388 passing tests**. It was
not rerun for these Python-only changes.

Two fresh Windows synthetic runs, `scratch/visual-033-a` and `scratch/visual-033-b`,
each captured after 120 presentation frames with unchanged snapshot, logical tick
0 and RNG 1/0. Native 960x640, AMD Radeon RX 6600, .NET 10.0.13, Windows
10.0.26200. Both image files have SHA-256
`3dfa7cd09c6a30b6af4f0b4942aa3c99d07f7a323af12245bc5146c22641adc6`.
Two report runs have identical bytes, SHA-256
`f6f091c1cb6ed0481cd2c8cc42b000cf4eceb893bf552acd5f35abb98f5ea7d4`.
All three regions have zero changed pixels and zero maximum RGBA deltas.
The local screenshot was visually inspected: readable labels, two authored sprites,
four map tiles, palette accents and transparent corners; no original media involved.

The remaining original comparison needs an actual game checkpoint/input sequence
plus matched frame/camera/palette state. The private RetroArch setup now reaches
introductory game content, and the owner's banner state now reproduces a rendered
battle checkpoint in two bounded restores. The exact original/managed timing,
context remain unverified. The separate full-visible-artwork profile now records
matching apparent text placement and strict sampled color differences without
claiming fidelity or timing equivalence. ZOD-026's original attack-decoder byte agreement
is retained separately; it cannot calibrate rendered thresholds. MAP003 lighting/
depth/camera approximations and banner RGB8 precision/parity limits remain those in
their existing guides. The subsequent original game boot/state observations do not
establish visual equivalence. No automatic commit was made.
