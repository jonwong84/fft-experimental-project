# Early banner reveal and readiness — bounded ZOD-033 diagnostic

Date: 2026-10-24. Edition: `SCUS-94221`. Read-only reference HEAD:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
Managed base: `2ec1724`; working tree was clean at entry.

The earlier owner state now provides source-bound loader yields and initial
reveal observations. Sixteen original task-counter/crop observations match
managed replay. Nine of eleven selected displayed frames match actual C# CPU
composition at zero tolerance, including background preservation at the empty
first reveal. Two partial frames retain small, reproducible blue-channel
differences. **This diagnostic does not establish complete early-reveal pixel
equivalence.** The runtime renderer and its declared arithmetic remain unchanged.

The [metadata](../evidence/visual/early-banner-reveal-v1.json) records identities,
source addresses, observed geometry, comparison results and explicit limits.
Original files, saved states, pixels, palettes and generated pages remain ignored
under scratch. The previous [hide comparison](displayed-banner-hide.md) and owner
approval retain their original scope.

## Readiness comes before logical reveal

Pinned source: `src/event/require/entry_conditions.c`,
`src/event/require/conditions.c`, `src/event/require/screen_support.c`,
`src/main/file_loading.c`, and `include/fft/thread.h` in the read-only reference.
The dispatcher tags task 56, reads condition 8/hold 60, and enters the general
helper. `conditions_show_general` calls `conditions_load_banner_image(0)` before
initializing the six quads and entering its first reveal yield.

The complete 244-byte loader at `0x801c3ab0` matches SHA-256
`83bdf7ea18ea62c44d0b5bcf4f0d12ba8a9d4ad7b3827200fddb225bf364e6ae`.
The previously verified whole general-banner and scheduler fingerprints remain
required. The loader has four distinct source yield sites:

- `0x801c3ad0`: yield before attempting the `0x8000`-byte high-overlay reservation.
- `0x801c3b10`: yield before requesting the image read through the main-stack bridge.
- `0x801c3b40`: yield before polling shared file-load completion through that bridge.
- `0x801c3b80`: yield after both pixel/palette `LoadImage` calls, before freeing the reservation.

The request uses sector `0x16c0`, size `0x6800`, and the observed allocation
`0x801df000`. The selected upload is still 64x256 VRAM words and the existing
palette rectangle; the undefined reservation suffix remains unsupported.
The classifier checks the unique running condition-8/hold-60 task, in-slot
aligned stack, loader-to-general return `0x801c8fbc`, saved parent-stack pointer,
hold value and high-overlay allocation. No unknown PC becomes a loader phase.
These yields are not logical animation ticks. A pre-check yield alone does not
prove that an allocation attempt failed or that a file read remains busy.

Nine retained independent restores from the earlier owner anchor show allocation
yield at frontend budget 310, request yield at 312, completion yields at
314/320/328/332/334, post-upload yield at 336, then reveal counter zero with no
banner packets at 338. Source frame counters increase from 8965 through 8979.
The snapshots identify these boundaries; they do not enumerate every retry or
establish a portable loading duration. Two inspections of these same nine
captures reproduce identical reports; this is not a second emulator capture set
for the loader.

The selected texture and palette already match prepared bytes at the first
sampled loader yield. Their mere residency cannot serve as readiness evidence.
The accepted transition follows source control flow and saved caller/task state.
The managed preview continues to preload before logical scene creation; there is
no new disc-I/O emulation, loader clock or VM admission policy.

## Early submissions and displayed frames

The earlier budget-340 checkpoint is reveal counter 1. Its three degenerate
packets explain the old six-piece guard failure. Source loops submit pieces
2,1,0 during the first nine updates, then 5,4,3,2,1,0 starting at update ten.
Ordering-table head insertion yields ascending traversal 0..2, then 0..5.
The existing full-visible inspector remains strict by default. A separate
`early_reveal=True` path requires the exact source-selected piece list and crop
for the saved reveal counter, plus all existing stack, bank, XY and UV checks.
The explicit cropped DMA trace accepts only a first triplet or all six pieces;
its default still requires all six full pieces and the known preceding draw mode.

Counter zero has no submissions. Its saved initial packet parity remains
uninterpreted, preserving the source's uninitialized local. For each triplet,
the first update is degenerate, then visible heights 8, 16, 24 and 31. The second
triplet first joins at counter ten. Both XY and UV keep the bottom edge fixed.
Missing, extra, out-of-order or counter-inconsistent draws are rejected.

Displayed checkpoints are 1, 2, 3, 4, 5, 9, 10, 11, 12, 13 and 14. Each binds
the current front surface to a hash-bound queued submission two source frames
earlier, using the frame-submit stack and opposite draw/display environments.
The earlier state can be inside the first `v_wait` call from `VSync` (return
`0x8001dc40`); its short restores can instead be inside the second (return
`0x8001dc64`). Both precede `PutDispEnv`, `PutDrawEnv` and `DrawOTag`, as shown by
`src/platform/psyq/libetc/VSync.c` and `src/battle/frame.c`. Each admitted site
requires its own matching stack return, with the same complete function hashes.
The old hide classifier retains its second-wait default. This is a bounded
source history, not an offset search or universal frontend delay setting.

## Controlled backgrounds and measured differences

The comparison forks the first degenerate-submission state after the loader has
returned. It clears only the selected 16-word VRAM palette in a copy. No later
banner upload overwrites that intervention. Each original/control pair matches
all 28 non-GPU subsystem payloads, all GPU registers and the task/frame context.
The original and probe PNG crops match their entire displayed VRAM scanouts.
Outside the selected artwork footprint, original/control pixels stay unchanged.

The optional actual C# host's `--reveal-sequence` mode prevalidates all eleven
opaque RGB5 backgrounds and their hashes, advances the existing logical driver,
and uses `GeneralBannerPresentation.Project` and
`FiveBitGeneralBannerPresentation.ComposeOver`. Its receipt binds each phase,
counter, ordered crop list, background and output hash. The Python model has an
explicit partial-triplet option; the original full-banner default stays closed.
Omitted or zero-height artwork is excluded from the sensitivity model without
inventing an empty artwork comparison success.

Across 18,712 affected temporal samples, two frames differ:

- Displayed reveal 4: 21 changed pixels, blue only, original minus C# = +8.
- Displayed reveal 13: 14 changed pixels, blue only, original minus C# = +8.

All other selected full canvases match exactly. Reveal 1 has no artwork samples:
`compared=false`, `exactEquality=null`; its full-canvas result checks background
preservation. At both differing frames, the Python model matches C# exactly,
state/scanout/footprint checks pass, and red/green remain exact. Both differing
triplets have 24 visible rows. The remaining color-interpolation/triangle
initialization question needs its own source trace; the precise cause is not
established here. No tolerance, material, sampling rule or runtime precision
profile was changed to conceal these differences.

Final runs `scratch/banner-reveal-d` and `-e` reproduce 107 selected artifacts:
original PNGs/exit states, probe PNGs/exit states, raw backgrounds, cropped review
PNGs, native RGBA, native receipt and gallery. Source contexts, draw traces,
bindings, isolation and comparison results also match. Report paths/logs differ.
Earlier a/b/c exploratory diagnostics are retained separately.

## Reproduce, validate and review

Use the existing private RetroArch profile and Python/Pillow environment:

```powershell
python tools/verification/inspect_banner_readiness.py --plan scratch/banner-readiness-plan.json --output scratch/banner-readiness-c
dotnet restore tools/banner-blend-check/BannerBlendCheck.csproj --locked-mode
dotnet build tools/banner-blend-check/BannerBlendCheck.csproj -c Release --no-restore
python tools/verification/compare_banner_reveal.py --plan scratch/banner-033-comparison-plan/plan.json --profile scratch/retroarch-reference --output scratch/banner-reveal-f
python -m unittest discover -s tools/verification -p 'test_*.py'
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

Outputs must be fresh. The reveal verifier returns 0 for exact selected
comparisons, **1 for a valid difference report** (the expected result here), and
2 for invalid inputs/execution failure. The gallery at
`scratch/banner-reveal-d/review.html` shows original, controlled background and
actual C# at 2x. The owner confirmed "The gallery looks good to me" on
2026-10-24. This approval records visual acceptance of the displayed early-reveal
gallery; the two measured pixel differences remain open.

Locked restores, zero-warning Release builds, **1,466 .NET tests** (16 new Client
observation/replay cases) and **130 Python tests** (14 new authored cases) pass.
Five actual CPU-host checks reject duplicate fields, unordered checkpoints,
wrong background hashes, nonopaque backgrounds and existing outputs, preserving
captured files. These checks are separate from the Python unit-test count.
No runtime source, event admission, asset/save schema, package lock or solution
graph changes. No automatic commit.

Next investigate the two 24-row color differences before selecting a bounded
renderer correction. Physical Circle skipping, exact original event identity,
broader scene rendering and general PS1/hardware equivalence remain open.
Deferred CRT/aspect/backend/audio work does not become active here.
