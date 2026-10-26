# ZOD-098: Prepared combined introduction and battle handoff

Implementation entry: 2026-10-26, managed
`182802a32bcd4ad9decfe557bdef5a541fa88db6`, clean working tree.
Read-only reference freshly checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [ZOD-096 contract](../architecture/zod-096-combined-dark-screen-introduction.md)
and [ZOD-097 behavior](../behavior/dark-screen-introduction.md) remain authoritative.

## Finite native plan recorded before execution

Use exactly seven successful Windows game processes, each at most 4096 logical
ticks and 120 seconds. Do not expand the plan with repeated exploratory windows.
Compile and headless-test first; readiness rejection probes run before window
construction and do not launch graphics processes. No emulator or physical input
automation is selected. A failed native scenario is recorded and investigated;
any need to exceed seven launches requires a revised explicit plan before launch.

1. `normal`: holds 60/60, no Circle; expected availability tick 275.
2. `repeat`: identical prepared inputs/schedule; content artifacts match normal.
3. `zero`: holds 0/0, no Circle; expected availability tick 155.
4. `maximum`: holds 255/255, no Circle; expected availability tick 665.
5. `skip`: holds 60/60, Circle at 1,55,56,57,59,60; expected availability tick 127.
6. `hide`: holds 60/60, Circle at 138 (general hide); expected availability 275.
7. `isolated`: normal descriptor/resources copied alongside the built application
   to a fresh prepared-only root; normal content artifacts match. No original
   archive, BIN, state, source tree or emulator is an application input.

Each scenario captures every accepted tick including zero and exit, with actual
native 256x240 CPU/GPU equality and independent full-frame composition. It then
runs the unchanged programmed battle plan: twelve accepted/two rejected commands,
headless/replay agreement, six unchanged historical battle PNGs and resize.
Recreate resources at tick 5, exercise focus handlers at tick 6 and handoff,
baseline queued/held input, and assert frozen battle through the handoff call.
Programmed calls do not establish physical controller or display latency.

Before running, hash retained standalone banner/Ready/DarkScreen and old authored
intro artifacts; recheck them afterward. Headless regressions preserve old APIs,
profiles, input routes and traces. Native processes are reserved for the seven
new scenarios rather than re-running every old graphics host.

Probe missing/corrupt descriptor and each resource manifest/payload, wrong
identity/owner/provenance/policy and existing output preservation before graphics.
Headless injected initial/recreation allocation failures must not publish a scene
or simulation progress; actual resource recreation is exercised in native runs.

## Selected implementation

The separate closed `fft.prepared-dark-screen-introduction` schema binds three
prepared resources and one authored owner/profile. It rejects malformed, unknown,
duplicate, missing/null and over-budget fields, colliding derived IDs and invalid
holds. The dark package's standalone hold timer is deliberately unused.

The client loads all prepared data before window construction, allocates graphics
before admission and reuses the one-window battle controller/router/input latch.
An internal presentation adapter shares the host between the two typed intro
sessions; it is not a public engine/framework replacement contract.

Every paint starts with a fresh opaque RGB5 checker, applies the accepted dark
quads, then general or Ready through the existing ComposeOver policy. Worker
progress and battle ownership never depend on rendering. The synthetic switch
to the existing battle does not implement original EventEnd or a map transition.

## Validation and review status

Locked restore and the final Release build pass with zero warnings/errors.
**2,173 .NET tests pass** (62 new): Engine 24, Framework 805, Content 576,
AssetTool 202, CLI 211 and Client 355. **278 Python tests pass** (9 new).
An initial sandboxed Python run encountered temporary-file permission errors;
the unchanged suite passes outside the sandbox. Initial test-fixture mistakes
were corrected before the final complete managed run.

All seven planned native windows completed in 8.4..18.0 seconds each. The 2,054
accepted intro frames cover **126,197,760 pixels** at zero tolerance across CPU,
actual GPU readback and an independent Python composition model. Every run also
matches the six retained battle PNGs and the existing 12-accepted/2-rejected
headless/replay outcome. Reversed layer order changes sensitive general samples
in six scenarios; the early-skip scenario has no sensitive general draw.

Normal/repeat and normal/prepared-only each repeat all 282 PNGs, accepted frame
metadata, final introduction and trace hash. The isolated application copy runs
with prepared inputs only and no repository working directory. This is a
prepared-only deployment check, not an OS filesystem-access-denial experiment.
All **581 retained historical files** rehash identically. Their old native hosts
were not relaunched; existing managed tests and new battle PNG equality provide
the current regression checks.

Twenty-seven distinct final pre-window negatives reject every missing/corrupt
manifest and manifest-listed payload,
wrong dark owner/provenance and an existing output. They create no new check
output or preserve its marker. Seven preliminary headless probes also rejected
before a harness palette-filename mistake was corrected; none opened a window.
The raw logs remain under scratch. Initial/recreation allocation-failure tests
use injected callbacks, not forced GPU/driver loss. Native resource recreation,
focus handlers and post-handoff resize execute; pre/during/post snapshot resize
layout checks are headless. No physical input or latency claim is made.

[Executed evidence](../evidence/events/prepared-dark-screen-introduction-v1.json)
maps DSI-025..039 to these checks. DSI-040 remains **pending owner approval** of
`scratch/dark-screen-intro-098-review/review.html`. The player deliberately slows
each authored tick to 80 ms and ends on the actual battle-start capture. Review
entry, dark-under-general/Ready, hidden gaps, release tail and the battle switch.
Historical contract flags and prior approvals remain unchanged.

## Reproducing the authored profile

Prepare the descriptor separately from the already validated dark/general/Ready
packages. The IDs below match the retained local prepared inputs:

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build --no-restore -- prepare-dark-screen-introduction --id sample:sequence/dark-screen-introduction --dark-screen-id fft.ps1-us:dark-screen/origin-v1 --general-artwork-id fft.ps1-us:banner/general-0 --ready-artwork-id fft.ps1-us:ready/image-0 --owner-id sample:owner/dark-screen-preview --general-hold 60 --ready-hold 60 --provenance authored --output scratch/dark-intro-example
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --dark-intro-battle --sequence-root scratch/dark-intro-example --sequence-id sample:sequence/dark-screen-introduction --dark-screen-root scratch/dark-screen-094-a-prepared --general-artwork-root scratch/banner-078-a --ready-artwork-root scratch/ready-086-artwork-a
```

Press C to supply a Circle sample. Battle routes stay closed until all workers
terminate and the event exits. Focus loss pauses interactive advancement; old
presses/held buttons are baselined at handoff. Restart then resets only battle.
Use `--check <fresh-scratch-root>` and optional `--circle-ticks <CSV>` only for a
separately planned finite capture. Audit a capture with
`tools/verification/verify_dark_screen_introduction.py --capture ... --dark ...
--general ... --ready ... --battle-reference ... --output ...` using scratch paths.
