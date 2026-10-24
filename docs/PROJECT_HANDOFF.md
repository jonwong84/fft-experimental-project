# Zodiac / FFT Project Handoff

**Latest checkpoint (2026-10-23):** ZOD-023's bounded headless battle and ZOD-025's synthetic
Windows host are implemented; ZOD-027's [Chocobo idle-pose trace](architecture/zod-027-chocobo-idle-pose.md)
and ZOD-028's [static pose preparation/loading](development/prepared-sprite-pose.md) are complete.
ZOD-029's [MAP003 source map](architecture/zod-029-map003-source-map.md) selects a frozen
day/dry scene and exact four-cell geometry/terrain checkpoint. ZOD-030's
[map preparation/loading](development/prepared-map-slice.md) prepares that scene repeatably
as geometry, indexed textures/palettes, frozen presentation metadata and both native terrain layers.
ZOD-031's [fixed prepared scene](development/prepared-scene.md) renders that map and the held
Chocobo with an authored camera, neutral texture lighting and modern depth/cutout policy.
ZOD-032's [playable two-unit battle](development/playable-battle.md) adds mouse/keyboard
selection, legal highlights, move/attack/wait/advance, status and restart for synthetic M1.
Start with the current [backlog](BACKLOG.md) and
[Windows host instructions](development/windows-synthetic-host.md); the original handoff sections
below are historical. Locked restore, zero-warning Release build and all **868 tests** passed for ZOD-032.
The selected six-piece pose and 9x13 map are prepared under ignored scratch and loaded by Client without
original-format parsing; native terrain heights remain intact. The original synthetic view remains available.
Two finite prepared-scene GPU runs produce identical images and unchanged input hashes.
Two programmed Windows battle runs also match the original headless result and submitted-command
replay, including after a resize. ZOD-034 now implements independent Windows CI,
TRX/JUnit reports and a self-contained folder check with separate locked publish
graphs and dependency notices. Local publishing, headless launch and the published
GPU check passed; remote CI and clean-machine interactive validation remain pending.
See the [launch guide](development/windows-launch-validation.md). ZOD-035 completes
the [bounded save/load proof](development/two-unit-battle-saves.md): versioned DTOs,
selected content bindings, active-turn/RNG restoration, strict validation, synthetic
migration and safe file replacement. All thirteen checkpoints continue with identical
state/events. Latest locked restore, zero-warning Release build and all **954 tests**
passed. ZOD-040's [script investigation](architecture/zod-040-event-script-investigation.md)
now maps script families, one source-derived synthetic event and pinned tool
capabilities/limits. ZOD-045's [coverage inventory](coverage/README.md) records 58
planning entries across nine domains, with scoped evidence/checks and open gaps.
The source/link/view checker passed against the pinned read-only reference; these
documentation tasks do not rerun the 954-test suite or implement script execution.
ZOD-064 now implements [synthetic event preparation/loading](development/prepared-scenario-event.md):
raw/processed framing, retained inspection bytes, closed capability/CFG validation
and strict prepared JSON/manifest loading. No event execution or retail import.
Locked restore, zero-warning Release build and all **1,012 tests** pass (58 new).
ZOD-067's [campaign save trace](architecture/zod-067-campaign-save-state.md) maps
field lifetimes, entry paths and the unsaved BIOS/event RNG lifecycles. Retail
checkpoint enumeration and original observations remain pending. ZOD-065 now
[executes the bounded event](behavior/conditional-event-execution.md) through a
Core-owned immutable program/state and pure Step/Run transitions, with Content
projection, exact traces/replay and atomic arithmetic/budget rejection. Domain:
variables 0..3 with values 0..65,535; no EventEnd, scene lifecycle or save integration.
Latest locked restore, zero-warning Release build and all **1,057 tests** passed
(45 new). ZOD-066's [BattleStart trace](architecture/zod-066-battle-start-event.md)
now pins verified original event 401, all 13 opcode kinds, fallback selection and
EventEnd continuation to normal battle. Inspection is complete; original playback
requires concurrent cameras, mutable operands, banners, task barriers and lifecycle
support. No original observation/event-entry state was identified. ZOD-071 now
implements [explicit-tick frame waits](behavior/timed-event-waits.md) in a separate
non-tutorial speed-one profile: signed counts 0..32,767, immutable pending state,
RunUntilBlocked/AdvanceTicks, exact resume boundaries, batch replay and atomic
arithmetic/instruction/tick rejection. Zero and sequential waits, maximum counts,
profile isolation and prepared-file loading are covered. Latest locked restore,
zero-warning Release build and all **1,105 tests** passed (48 new). Conditional
behavior, battle save schemas, project references and package locks remain intact.
ZOD-072 now [defines the scene task/barrier contract](architecture/zod-072-event-task-contract.md):
scene-wide live polling, mandatory first yield, BATTLE/WORLD scan differences,
stable scene/task identities, revisions, dark-screen phases and atomic explicit
updates. Its original JSON remains specification-only. ZOD-073 now
[implements the pure scene ledger/barrier](behavior/scene-task-barriers.md):
stable scene/task IDs, revisions, terminal tombstones, explicit ordered updates,
live polling and atomic rejection. All 17 cases have full-state/trace/replay
regressions, with 40 additional boundary cases. Locked restore, zero-warning
Release build and all **1,162 tests** passed (57 new Core cases). ZOD-074 now
[integrates synthetic E5 barriers](behavior/scene-event-barriers.md) into a separate
BATTLE profile with conditional operations/F1, shared explicit ticks, exact
resumption, atomic ledger/VM rollback and retained scene tasks across exit/restart.
The authored seven-tick fixture has full combined snapshot/trace/replay checks;
old profiles still reject E5. Locked restore, zero-warning Release build and all
**1,230 tests** passed (68 new: Core 27, Content 23, CLI 18). ZOD-075 now
[selects condition 8's post-readiness logical banner](architecture/zod-075-general-banner-effect.md):
20/H/10 reveal/hold/hide iterations, an extra exit yield, Circle sampling only in
reveal/hold, immutable managed-effect authority and atomic scene composition.
Its source JSON remains a historical specification. ZOD-076 now
[implements the pure logical banner driver/adapter](behavior/general-banner-effect.md),
including all 24 cases and 47 additional ownership/resource/lifecycle checks.
The driver owns one retained task, generates terminal facts, shares one atomic
tick with VM/ledger, preserves effect progress across interpreter exit/restart,
and distinguishes original Circle completion from native owner cancellation.
Old profiles/APIs still reject opcode 78; startup/loading and graphics remain open.
Locked restore, zero-warning Release build and all **1,302 tests** passed (72 new:
Core 71, CLI 1). ZOD-077 now [traces banner-0 assets and readiness](architecture/zod-077-general-banner-assets.md):
verified image/overlay ranges, palette bank 4, six shaded pieces, UV wrap, native
screen origin and order-dependent add/subtract. Selected samples avoid the native
loader's undefined upload suffix. Defines immutable prepared data and a preloaded
host boundary; includes 20 authored, unexecuted presentation expectations. This
documentation milestone did not rerun the .NET suite. Next event step: ZOD-078's
prepared conversion/loading and RGB8 CPU preview; no original loading duration,
opcode-78 admission or original visual/timing equivalence is established.
ZOD-078 now implements the [prepared general-banner preview](development/prepared-general-banner.md):
verified local input/range decoding, staged fresh-root preparation, strict immutable
Content loading and accepted-snapshot CPU composition with a preloaded Windows host.
All 20 authored expectations now have regressions. Locked restore, zero-warning
Release build and all **1,388 tests** pass (86 new: AssetTool 15, Content 38,
Client 26, CLI 7). Two preparations match all five files; two finite Windows
checks complete at tick 91 with matching 15 PNGs/report and exact CPU/GPU readback.
The second run uses a separate working directory with prepared files only and no
original input/reference paths. Core/Engine, event profiles/opcode rejection,
save schemas and dependency/package graphs remain intact. Outputs remain ignored
under scratch. No automatic commit. Original parity/precision/latency/comparison,
opcode-78 and complete BattleStart remain open; select the next source-traced
scene capability separately after this preview is reviewed.
ZOD-068's synthetic world checkpoint service remains
independent, and ZOD-069's UI follows service/navigation. Original captures (ZOD-026), comparison
checkpoints (ZOD-033), and remote CI/clean-machine evidence (ZOD-034) remain open.
MAP003's static scene does not broaden M1
combat's height-10 domain. Gameplay is limited to the synthetic local two-unit battle;
source-derived checks do not establish original-rendering equivalence. Physical input timing
remains a manual follow-up. Original media/captures remain ignored.

ZOD-026 now completes a bounded [original-instruction sprite oracle](development/sprite-executable-reference.md):
the original Chocobo attack decoder/helper execute from verified BATTLE.BIN bytes
under existing Unicorn 2.1.4, matching the 25,600-byte managed fixture digest.
Two captures/reports match; wrong-hash and existing-output gates pass; managed CLI
verification passes. No original game or GPU render ran. Historical fixture
provenance remains intact; new executable-reference metadata is separate. No .NET
code changed or full-suite rerun; the last full validation remains 1,388 tests.
Matched original rendered captures/comparison checkpoints (ZOD-033) remain open,
along with remote CI/clean-machine evidence (ZOD-034). No automatic commit.

**2026-10-24 update:** ZOD-033's [synthetic visual comparison lane](development/visual-comparison-checkpoints.md)
now has strict headless PNG/capture/plan validation, separate tick/state/view/
environment failures, regional RGBA diagnostics, explicit threshold rationale and
a synthetic Windows receipt adapter. All 22 Python tests pass. Two fresh synthetic
120-frame Windows captures and two reports match exactly with zero tolerances;
snapshot unchanged, logical tick 0/RNG 1/0. The adapter fingerprints an explicit
bounded turn projection; no broader simulation equivalence is implied. No managed
source/package/save/event changes or full .NET rerun; last full suite remains
1,388 tests. RetroArch has since been located and boot-checked; see the setup below.
ZOD-033 remains partial until matched original captures and reviewed original thresholds/
human inspection exist. No emulator installed, no original fidelity claim or commit.

The [private RetroArch reference setup](development/retroarch-reference-capture.md)
now uses the existing RetroArch 1.22.2/Beetle PSX software installation with verified
BIN/CUE/BIOS, native software settings and scratch-only configurations/output.
Two finite, input-disconnected 1,200-frame boots reach the introductory game credit
with identical 350x240 raw PNGs. Normal emulator configuration hashes are unchanged;
selected software options are retained. All 30 Python tests pass (8 new); managed
code and the last 1,388-test full validation remain unchanged. The local interactive
launcher is `scratch/retroarch-reference/launch.ps1`. BSV recording exists but replay
playback remains unverified. The owner subsequently captured the target banner and
a state whose thumbnail matches the raw screenshot. Frozen copies are under
`scratch/banner-033-original-a`. Two four-frame restores produce identical banner
scene images; one-frame startup attempts produced black images. See the
[observation metadata](evidence/visual/original-banner-state-v1.json). Exact timing,
event-path identity and the original/managed comparison remain open; ZOD-033 stays partial.

**First original/managed banner diagnostic (2026-10-24):** The
[observed comparison guide](development/observed-general-banner-comparison.md)
records a separate strict contract, original 280x240 buffer with measured 12-column
black borders, and managed tick-20 full-visible artwork anchor. Two local reports
and review pages match exactly. Apparent text placement is consistent on visual
inspection. Of 1,577 managed-background-invariant samples, 1,460 differ; the
original is equal or darker, by up to 16 RGB levels. Background-sensitive samples
remain separate. No original tick/simulation equivalence, automatic cause or
fidelity verdict is claimed; no tolerance is widened or runtime changed. All 52
Python tests pass (22 new); the .NET suite was not rerun (last result: 1,388).
Trace native color precision before changing rendering, and obtain pre-banner
anchors for reveal/hide timing. ZOD-033 remains partial. No automatic commit.

**Color precision follow-up (2026-10-24):** The
[banner study](architecture/general-banner-native-color-study.md) reads the frozen
state's dtd=0 and displayed VRAM, and matches all 1,577 earlier glyph samples
exactly with source-informed five-bit arithmetic, a quantized color gradient and
zero-padded emulator scanout. Two reports match; 66 Python tests pass (14 new).
No runtime renderer changed or .NET suite reran (last: 1,388). Pinned emulator
source is study evidence, not the identified installed DLL build; the exact
banner-time draw-mode writer remains untraced. Next select a bounded precision
profile with explicit draw state, and obtain pre-banner timing anchors.
ZOD-033 remains partial; no automatic commit.

**Five-bit implementation (2026-10-24):** The
[bounded banner profile](development/five-bit-general-banner.md) now accepts explicit
per-draw dithering/framebuffer phase and is available with
`--precision five-bit --dither off|on`; the existing RGB8 path remains default.
Frozen RAM ordering-table reconstruction identifies the dark-screen worker's
`0xE1000006` before all six banner draws. Two native Windows runs and actual
original comparisons match 1,577 foreground samples exactly; all 16 legacy RGB8
capture files also repeat unchanged. Dither-on is a retained negative comparison.
Fresh locked restore, zero-warning Release build and 1,418 .NET tests (30 new),
plus 81 Python tests (15 new), pass. No logical scene/Core/Engine or asset-schema
change; ZOD-033 remains partial for timing, blending/coverage and wider integration.
Next obtain pre-banner timing anchors and original background-sensitive evidence.
No automatic commit.

**Later owner notes (2026-10-24):** [Presentation decisions](development/later-presentation-decisions.md)
and deferred ZOD-079 retain requested original CRT/4:3 aspect correction, an
optional CRT shader, hardware/software rendering and resolution discussion,
an in-game Settings menu, and accurate music/sound effects (ZOD-043).
No shader/backend/stretch factor is selected; revisit much later without changing
the current banner investigation or native-pixel diagnostic captures.

**Original continuation (2026-10-24):** The
[saved-task continuation](development/original-banner-continuation.md) identifies
the existing full-visible original state as reveal iteration 18. Two 17-checkpoint
no-input restore sets repeat screenshots, exit-state files and decoded task/quad
observations through late reveal, sampled hold boundaries, every hide iteration
and natural task exit. Eighteen metadata-only managed replay cases match the
recorded phases/counters/crops; the task is still live at checkpoint 90 and clears
at 91. A later reused slot is rejected. Locked restore, zero-warning Release build,
1,436 .NET tests (18 new Client) and 92 Python tests (11 new) pass. The owner
accepted the continuation gallery's visual appearance on 2026-10-24. No runtime,
package, asset/save-schema or opcode-admission change. Full reveal/loading,
physical Circle input, event identity, screenshot/task latency and background
blending/coverage remain open; ZOD-033 stays partial. Original media remain ignored;
no automatic commit. Next obtain an earlier state for reveal or select bounded
background-sensitive comparisons; ZOD-079 remains deferred.

**Controlled background blending (2026-10-24):** The
[full-visible blend check](development/general-banner-background-blending.md)
now makes only the selected palette transparent in a scratch state copy to recover
a matched background. All 28 non-GPU subsystems, every GPU register and all 58,312
outside-footprint pixels remain unchanged. Actual C# `ComposeOver` output matches
all 3,128 affected original pixels exactly, including the 1,551 previously excluded
background-sensitive samples. Two original/probe/C# runs repeat; the default
checker retains all 16 historical GPU capture files byte-for-byte. The pure
compositor gains explicit opaque RGB5 background input; a standalone locked CPU
evidence host stays outside the main solution. Locked restores, zero-warning
Release builds, 1,450 .NET tests (14 new Client) and 104 Python tests (12 new) pass.
The owner confirmed the original and C# views look identical on 2026-10-24.
This altered-state control is labeled
separately from unmodified original evidence and does not validate managed map
rendering. Partial reveal/hide pixels and framebuffer correspondence, early
reveal/loading/input, event identity and full scene integration remain open.
ZOD-033 stays partial; ZOD-079 remains deferred. Original artifacts stay ignored;
no automatic commit.

**Displayed hide comparison (2026-10-24):** The
[hide-frame check](development/displayed-banner-hide.md) binds pre-submission
VSync/main-stack state, pending display/draw environments and two-buffer source
order to earlier hash-bound submissions. All five PNG crops match displayed VRAM;
actual C# output matches 6,917 affected samples across four visible hide anchors
and preserves the fully hidden background. Controlled probes retain all 28
non-GPU subsystem payloads/GPU registers, and two runs repeat. No pixel fitting,
universal lag or managed map-rendering claim. Locked restores, zero-warning builds,
1,450 .NET and 116 Python tests (12 new) pass. Runtime, schemas and dependency
graphs stay unchanged; only the developer host gains a bounded sequence mode.
The owner approved the hide-frame gallery on 2026-10-24. Early reveal/loading, physical Circle
input, event reachability and broader rendering remain open. ZOD-033 stays partial;
ZOD-079 remains deferred. No automatic commit.

**Earlier original anchor received (2026-10-24):** The owner's new private state
is frozen separately under `scratch/banner-pre-anchor-a`; the old late-reveal
anchor is preserved. [Intake evidence](evidence/visual/original-pre-banner-anchor-v1.json)
records ten finite no-input probes, including accepted reveal counter 11, hold
counters 11/36 and hide counter 1. Two budget-400 restores repeat PNG and exit-state
bytes. All protected inputs/base configurations and the owner's slot remain
unchanged. Entry overlay bytes and the budget-340 packet set fail existing guards;
these remain unclassified. Budget 600 reaches READY with different task parameters.
Next trace initial reveal/readiness from this earlier anchor, retaining explicit
unsupported states. Loading duration, physical Circle behavior, event identity and
new pixel comparisons remain open. No runtime changes or full-suite rerun.

**Early reveal/readiness diagnostic (2026-10-24):** The
[source-bound investigation](development/early-banner-reveal.md) identifies all
four loader yields and the first pre-reveal yield from nine original snapshots.
Existing byte residency predates the loader; it is not used as readiness proof.
The old budget-340 guard failure is explained by the first triplet's three
degenerate submissions. Explicit early-reveal checks bind exact source counters,
triplet admission, XY/UV and both pre-submission VSync call sites. Sixteen new
metadata-only managed replay cases match observed counters/crops. Nine of eleven
displayed canvases match actual C# composition; reveal 4 differs at 21 pixels and
reveal 13 at 14 pixels, blue only, original minus C# +8. All isolation, scanout,
geometry, footprint and C#/Python checks pass. Two runs repeat 107 selected
artifacts and the strict differences; no tolerance/runtime renderer change.
Locked restores, zero-warning builds, 1,466 .NET and 130 Python tests pass, plus
five actual-host rejection checks. The owner visually approved
`scratch/banner-reveal-d/review.html` on 2026-10-24; the two measured pixel
differences remain open. Next trace the two 24-row color differences before a bounded renderer
correction. Universal load duration, Circle input and event identity remain open;
ZOD-033 stays partial. No automatic commit.

**Triangle color correction (2026-10-24):** The
[bounded source trace](development/banner-triangle-colors.md) identifies different
Q12 color seeds for the two rectangle triangles. Client now selects the seed
per sample using the source-derived shared diagonal; the Python model follows
the same bounded reduction. Actual C# composition matches all eleven archived
original reveal frames exactly, changing only the prior 21/14 blue-channel pixels.
Two runs repeat 57 selected artifacts. Full-visible/five-hide RGBA and receipts
remain byte-identical, as do fifteen old five-bit Windows PNGs and all sixteen
RGB8 files; the five-bit receipt policy text changes explicitly. Locked restores,
zero-warning builds, 1,486 .NET and 135 Python tests pass. The new CPU verifier
reuses hash-bound original/control states and launches no emulator; the unchanged
GPU schedule excludes the newly corrected 24-row frames. The owner visually
approved `scratch/banner-triangle-colors-a/review.html` on 2026-10-24. Historical
diagnostic evidence/approval remain intact. Next select a bounded original Circle-input
validation method; event identity and wider rendering/integration remain open.
No automatic commit.

**Physical reveal Circle observation (2026-10-24):** The
[paired original witness](development/original-banner-circle-input.md) verifies
the owner's USB-controller Circle press from neutral reveal count 5. Saved input,
retained loop count, source frame, active task and empty queued banner list
distinguish the shared skip/natural exit address. The task takes one extra active
yield then clears on the next source update, retaining 5/0/0 progress. Two
no-input continuations repeat state/PNG bytes; two inspection reports repeat.
Three metadata-only C# cases match the existing managed driver. Locked restore,
zero-warning build, 1,489 .NET and 143 Python tests pass. No runtime changes or
automatic commit. A separate ignored state-review launcher omits recording so
older states load; Quick Menu saves succeeded where F2 notification/file updates
were uncertain. Future helper sessions enable the notification font; the current
session still uses its frozen earlier configuration. The next physical case is
hold skipping: a repeated neutral Holding count-1 state is installed in private
slot 1, preserving slot 0. Have the owner load slot 1 and save a neutral checkpoint,
then freeze/inspect the actual starting phase before requesting Circle. Hold/hide
input, BSV playback, USB/display latency, original event 401 identity and broader
rendering/integration remain open; ZOD-033 remains partial.

**Physical hold Circle follow-up (2026-10-24):** The
[paired hold witness](development/original-banner-circle-input.md#physical-hold-result)
now verifies the owner's slot-1 press from neutral Holding count 1. Source frames
9000/9001/9002 map to logical ticks 21/22/23, retaining 20/1/0 progress through the
extra active exit yield and task clear. Two dedicated three-frontend-frame
restores repeat original state/PNG bytes; both inspector reports match. The first
four-frame sample reached a new condition-0 task at frame 9003; the existing
clear guard correctly rejects that slot reuse. Private slots and protected input/
configuration hashes remain unchanged by the finite restores. Combined validation
now passes six metadata-only C# checkpoints and ten authored Python witness
checks: zero-warning Release build, 1,492 .NET and 145 Python tests. Runtime code
is unchanged; nothing is committed automatically. Physical hide input and BSV
playback remain open alongside USB/display latency, event 401 identity and wider
integration/rendering. Select the hide-phase or input-movie case separately.

**Hide-input preparation (2026-10-24):** The owner selected physical hide input
next. Two hash-bound archived natural-continuation states/PNGs match exactly;
fresh source/input/task inspection verifies neutral Hiding count 1, logical tick
81 / progress 20/60/1 / source frame 6435. A copy and matching thumbnail are now
installed in unused private **slot 2**, preserving slots 0/1 and protected inputs/
configuration hashes. Preparation evidence is
`scratch/banner-circle-hide-anchor-a/report.json`; no emulator or test suite ran.
The next user step is to load slot 2 and save a neutral checkpoint through Quick
Menu with the game paused. Freeze/inspect that actual baseline before requesting
Circle; physical hide behavior remains unverified. Last full validation is still
1,492 .NET and 145 Python tests. See the
[operator notes](development/original-banner-circle-input.md#prepared-hide-input-checkpoint).

**Physical hide Circle result (2026-10-24):** The
[paired hide witness](development/original-banner-circle-input.md#physical-hide-result)
now verifies the owner's slot-2 press. The frozen neutral baseline at source
frame 6435 and Circle-only after-state at 6436 show the same active task/stack,
hide count 1 -> 2, buffer alternation and six exact 8-row crops. Logical ticks
81/82 retain normal 20/60/1 -> 20/60/2 progress with no exit cause. Two inspector
reports match; two additional metadata-only managed cases match the unchanged
driver. Zero-warning Release build, all 1,494 .NET and 152 Python tests pass.
No emulator ran for this pair inspection; no runtime behavior changed. Original
states/media stay ignored and nothing is committed automatically. Each phase now
has one bounded physical press observation. Review/commit the evidence before
selecting input-movie playback or exact original event-entry identification as
the next gate. Later natural completion after the hide press, boundary timings/
repeats, USB/display latency, event 401 identity and broader integration remain
open; ZOD-033 remains partial.

**Owner formula direction (2026-10-24):** Keep weapon and ability algorithms
readable/editable as ordinary C# code. The
[formula notes](development/editable-combat-formulas.md) distinguish current bounded
sword execution/learning-only ability metadata from future formula families.
Before broader combat work, consider small typed arithmetic methods, explicit
RNG/modifier sequencing, shared validated data and original/custom profile
isolation. Source edits and rebuilding are sufficient initially; no runtime
compiler, DSL or arbitrary plugin system is selected. Recorded under ZOD-038/048;
no combat refactor is made during the banner milestone.

**Prepared:** 2026-10-20
**Workspace:** `C:\repositories\fft-experimental-project`  
**Branch:** `feature/initial-development`  
**Last committed project change observed at original handoff creation:** `48b4772 Add battle simulation and movement model foundations`
**Engine name:** Zodiac  
**First content set / compatibility target:** North American PlayStation release of Final Fantasy Tactics, `SCUS-94221`

This document is a restart point for the owner or another coding agent. It distinguishes completed work, local-only prerequisites, current uncommitted work, and future intentions. The goal is not to imply that the project is already a game engine capable of playing FFT: it is an early .NET foundation with useful asset-pipeline proofs and small simulation models. Amendments dated 2026-10-20 extend the architecture to make Zodiac reusable and separate it from the FFT game layer.

## 1. Mission and confirmed direction

Build **Zodiac**, a native, managed C#/.NET game engine and Windows application. Final Fantasy Tactics is the first game/content set to run on it. The original C decompilation is a research and validation reference only; the finished program must not link, invoke, or depend on it at runtime.

The user confirmed this is a personal project with no intent to distribute. A locally owned original BIN is available for local asset import. Keep it and all extracted/decoded game data under ignored `/scratch/`; never commit or upload those files. This personal scope does not establish the upstream decompilation's license or settle all local-use questions.

Priorities:

1. Preserve the PS1 baseline behavior and campaign as accurately as practical. Record confidence, deviations, and observable quirks instead of silently fixing them.
2. Keep the simulation deterministic, headless, and independent from display, audio, input devices, filesystem access, and wall-clock time.
3. Separate game/content definitions from mutable runtime state and save data.
4. Support additional jobs, abilities, items, characters, monsters, maps, encounters, campaigns, and eventually mechanics without copying original fixed table capacities as accidental constraints.
5. Provide authoring tools so the user can inspect and edit game data, rather than requiring hex editing. C# should be a first-class way to define typed data (for example `JobDefinition` with named properties), with a path to visual editors for content and assets.
6. Treat The Lion War / War of the Lions integration as a later explicit profile/content set after its exact variant and feature scope are chosen.

## 2. Current working-tree state

At original handoff creation, `git status --short` showed the movement-policy/documentation work below. That work was subsequently committed and pushed as `ed72bf7 Add terrain movement proof and project handoff`. The current architecture clarification is a new, separate documentation change; check `git status --short` for its exact files before continuing.

```text
 M PROJECT_PLAN.md
 M README.md
 M docs/architecture/overview.md
 M docs/behavior/movement-inputs.md
?? src/Fft.Core/TerrainAwareMovementRange.cs
?? tests/Fft.Core.Tests/TerrainAwareMovementRangeTests.cs
```

The committed movement slice:

- Adds `TerrainAwareMovementRange`, a deterministic, same-layer, cardinal movement-cost propagation policy using a priority queue.
- Charges the cost of the tile being entered; accepts a tile when minimum entered-tile cost is no greater than effective `Move`.
- Does not propagate into tiles flagged Blocked or surfaces whose effective cost is blocked.
- Filters Cannot Stop and Untargetable from final destinations, while allowing propagation through them in this deliberately limited model.
- Throws when a traversed surface has no cost in the supplied effective movement profile; it does not silently invent a cost.
- Does **not** modify or replace the `BattleSimulator`'s prototype `FlatGridMovementRange` behavior.
- Updates the movement source note and plan, and records Zodiac as the engine name in README and architecture documentation. Assembly and namespace names remain `Fft.*`.

The terrain policy is an algorithmic simplification informed by `move_range.c`; it is not a line-by-line port and is not a claim of full FFT pathfinding equivalence. Source trace details and exclusions are in [`behavior/movement-inputs.md`](behavior/movement-inputs.md).

### Last verification

The most recent complete solution run succeeded:

```text
dotnet test Fft.sln -c Release --no-restore -m:1
Core: 52 passed; AssetTool: 66 passed; AssetTool.Cli: 10 passed; Content: 6 passed
Total: 134 passed, 0 failed, 0 skipped
```

`git diff --check` completed without whitespace errors. In this Codex environment, testhost needs normal local process communication and temporary files were directed to ignored `scratch/test-temp`; a normal local Windows terminal may not need those workarounds. The full test command also builds the projects. Last explicit `dotnet build` before this new policy was successful; run both build and tests again before merging the new handoff/documentation changes.

There are no test fixtures containing the BIN or extracted images. Game-derived data remains local under ignored `/scratch/`.

## 3. Repository and toolchain snapshot

### Projects present

- `src/Fft.Client`: .NET 10 console composition/bootstrap. It can read one prepared asset through `Fft.Content`; it is not yet an interactive game.
- `src/Fft.Core`: stable IDs, map/tile/unit/battle state models, movement inputs, the typed battle transition proof, prototype flat movement range, and the just-added standalone terrain-cost movement policy.
- `src/Fft.Content`: versioned manifest-based loose-file reader by logical asset ID, with size/hash verification.
- `src/Fft.AssetTool`: managed raw-sector reader, ISO/PVD inventory, ordinary-file extraction, manifest generation, and bounded unit `.SPR` parser/attack-image decoder.
- `src/Fft.AssetTool.Cli`: command host for inventory, comparison/extraction, and sprite verification/preview.
- Tests: `tests/Fft.Core.Tests`, `tests/Fft.AssetTool.Tests`, `tests/Fft.AssetTool.Cli.Tests`, `tests/Fft.Content.Tests`.
- Solution: `Fft.sln`, targets `net10.0`. Require .NET 10 SDK.

There is not yet a graphics framework, real-time game loop, Windows rendering backend, input/audio adapter, campaign system, save system, data editor, extension loader, or full FFT rules implementation.

### CI

`.circleci/config.yml` has a CircleCI 2.1 job using `mcr.microsoft.com/dotnet/sdk:10.0`. It restores, builds Release, tests, and runs SonarQube Cloud analysis. PR builds query GitHub for the actual target branch. The job expects Sonar credentials from the configured `global-v1` context and a GitHub token for PR base lookup. The CI container is Linux; it currently verifies the platform-neutral .NET projects, not a Windows graphics/application package. Publishing is not configured.

### Local reference and game data

- Decomp/reference checkout: `C:\repositories\fft_decomp`; read-only.
- Recorded source snapshot used for current behavior notes: `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. It is a snapshot, not a claim about current upstream. Before a new source-specific study, verify its exact current local HEAD and record that SHA. The upstream changes frequently.
- The user-provided `scratch/fft-clean-base-game.bin` is documented as matching the `SCUS-94221` target profile (541,315,152 bytes; SHA-256 `29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9`). CUE is local alongside it. Do not copy it to the decomp repo or stage it.
- `/scratch/` is in `.gitignore`.
- The decomp inventory has 2,464 ISO entries. Managed inventory matches paths and LBAs. The 2,457 ordinary `data` entries were extracted locally and hash-checked against the reference inventory (162,790,593 bytes). Seven mixed XA/STR entries remain unsupported.
- One ordinary file (`MENU/TUTO7.SCR`) was independently extracted from BIN and matched reference SHA-256.
- `BATTLE/CYOKO.SPR` completed a local extract → manifest → hash-verified `Fft.Content` load → parse → bounded attack-image decode flow. A source-derived golden hash and 16-palette diagnostic PNG path exist; the preview is not an emulator screenshot or final GPU result.
- BIN extraction is an offline preparation workflow only. Runtime should load prepared files from disk and not access the BIN.

Relevant detailed notes: [`PROJECT_PLAN.md`](../PROJECT_PLAN.md), [`asset-inventory-poc.md`](asset-inventory-poc.md), [`sprite-attack-image-decoder-poc.md`](sprite-attack-image-decoder-poc.md), [`architecture/overview.md`](architecture/overview.md), [`battle-transition-poc.md`](battle-transition-poc.md), and [`behavior/`](behavior/).

## 4. Architecture direction

The current architecture note is [`architecture/overview.md`](architecture/overview.md); the ADRs record significant boundaries. The user has named the reusable engine **Zodiac** and clarified that the project has two parts: the Zodiac engine and an FFT game/content implementation that runs on it. Proposed ADR 0004 records this one-way boundary. Current project names are still FFT-specific and the code has not yet been split; do not rename all projects/namespaces in a broad refactor. Decide and prove the product/API split incrementally.

Target dependency shape:

```text
Zodiac Windows client / composition root
    ├── Zodiac application/use cases (only when concrete needs arise)
    ├── Zodiac engine core: deterministic simulation and generic runtime contracts
    ├── FFT content/rules profile: FFT-specific jobs, formulas, actions, maps, scripts
    ├── platform adapters: rendering, audio, input, file/save services
    └── authoring/import tools: data editor, asset converters, validators
```

The intended dependency is one-way: the FFT game/content layer depends on Zodiac engine contracts; Zodiac cannot depend on FFT. The current `Fft.*` solution does not satisfy this clean split yet. [`adr/0004-zodiac-engine-and-fft-game-boundary.md`](adr/0004-zodiac-engine-and-fft-game-boundary.md) records the new proposed decision.

Important boundary decisions:

- Keep deterministic simulation headless and deterministic; commands in, state/events out.
- Definitions (jobs, abilities, formulas, items, text, maps, animations, encounter/campaign data) are immutable validated content. Battle/campaign unit values and save progress are mutable runtime state, with explicit versioned persistence later.
- Use stable namespaced IDs and content-sized collections. Preserve original fixed widths only at import/compatibility boundaries where evidence requires them.
- Baseline FFT-compatible profile stays distinct from extensions and Lion War content.
- Load prepared content via manifest/logical IDs. Do not leak sector/LBA handling into game runtime.
- Avoid arbitrary code execution or plugin buses until typed declarative content and real extension needs are understood.
- `Fft.Core` currently contains FFT-flavored values. The intended engine/content split is a design goal, not yet an implemented separation; decide what is genuinely reusable before renaming or relocating it.

## 5. New requirement: content authoring and editing tools

The user wants a way to manage/edit the full body of game data: flags, battle formulas, text, sprites, animations, and other records. They would like to author some data in C# (example: a `Job`/`JobDefinition` class with typed properties), not be confined to a GUI or hex editor. This is now a core product requirement, not merely a possible convenience.

### Proposed approach (not yet implemented or fully decided)

1. **Typed C# domain definitions.** Define immutable classes/records such as `JobDefinition`, `AbilityDefinition`, `ItemDefinition`, `StatusDefinition`, `BattleFormulaDefinition`, `MapDefinition`, `EncounterDefinition`, `AnimationDefinition`, `TextEntry`, and campaign/script references. Give every record a stable ID, explicit fields, validation, and references by ID. Avoid anonymous integer indexes as the public content API.
2. **Editable package representation.** Serialize definitions as versioned human-reviewable data (JSON or YAML is not chosen). The C# types are the validated object model, regardless of the chosen text format. Support diffs, schema versions, migration, validation diagnostics, and reproducible package builds.
3. **C# authoring API.** Allow trusted local authoring projects to construct definitions in C# and run validators/package builders. Do not load arbitrary third-party C# into the game process at first. A project can compile C# authoring code at build time to produce validated package data.
4. **Visual editor.** Build a Windows editor over the same schemas/validation APIs after the first useful content model exists. Start with inspect/search/edit/validate/preview for one well-defined category; do not create a giant editor before the content schemas and engine contracts stabilize. Data and asset preview tools can be separate tabs or focused editors.
5. **Simulation formulas.** Separate original-compatible formulas (implemented and regression-tested in managed C#) from authored parameters and custom mechanics. For extensible formula behavior, first consider typed operations/expressions or a constrained DSL that the C# engine evaluates. Arbitrary DLL/script execution is deferred until trust, determinism, versioning, and sandbox requirements are designed.
6. **Text, sprites, animation.** Use localized text records with stable keys and font/layout preview. Treat image/sprite import and animation/event timeline editing as asset-editor concerns over prepared files and decoded metadata. An asset preview must distinguish raw decoded bytes from faithful PS1 presentation (CLUT selection, STP/blending, ordering, frame transforms, timing).
7. **Flags.** Replace unexplained bitmasks in authoring surfaces with named typed flags and descriptions, but retain a lossless import/export mapping to original packed fields where needed. Tests must verify round-trips and reject unknown/invalid combinations explicitly.

### Existing community tools to investigate

Initial web research on 2026-10-20 found:

- [Glain/FFTPatcher](https://github.com/Glain/FFTPatcher) is a continuation of the FFTPatcher suite and is GPL-3.0. Its repository lists `FFTacText`, `EntryEdit`, `ShishiSpriteEditor`, and patcher components. Its README describes editors for abilities, items, jobs, job levels, skill sets, monster skills, status effects, battle/event (ENTD) data, move-find items, and store inventory. It is a strong reference for editor workflows and data categories. Its GPL license means do not copy code into this project casually; inspect licensing before adapting code. Investigate which subtool handles which script format before relying on it.
- [Nax/TacticsCrafter](https://github.com/Nax/TacticsCrafter) describes itself as both an API and patch-builder application for the PSP game *Final Fantasy Tactics: The War of the Lions*. The repository identifies an MIT license and a C++/Qt build. It may inform typed APIs/mod workflow, but targets a different release and is not a direct PS1 data-format dependency.

These findings are preliminary. Inspect exact capabilities, supported editions, source/asset licensing, maintenance/build status, and format compatibility before deciding whether Zodiac imports, interoperates, or only borrows workflow ideas. The user mentioned community script-editing tools; identifying the exact relevant editor and script encoding is a concrete research task. Avoid inventing script compatibility from tool names.

## 6. Testing and behavioral-equivalence strategy

No single oracle is sufficient. Use a ladder of evidence:

1. **Build/CI:** reproducible .NET 10 Release build and all tests; later add Windows-targeted build/package checks when UI or platform dependencies appear. Keep Linux CI only where it reflects supported platform-neutral layers.
2. **Unit tests:** one deterministic rule per test group, normal/boundary/invalid cases; exact integer widths, signedness, truncation, rounding, order of updates, and RNG draw counts where relevant.
3. **Source-traced behavior fixtures:** each fixture records decomp URL/path, exact commit SHA, relevant callers/data tables, test input, expected observable output, and confidence. Do not cite moving line numbers alone.
4. **Differential harness:** wherever a reference executable or trustworthy extracted behavior vector exists, run the same structured scenario against C# and compare state transitions/logs. Static C code is research material, not a callable runtime oracle. Decomp byte-match checks prove rebuilt C machine code, not C# rule equivalence.
5. **Scenario/regression tests:** battle snapshots covering turn, move, action, status, reaction, victory, loot, campaign outcomes; replay seeds and command sequences deterministically. Add cases for known quirks, not just idealized rules.
6. **Asset tests:** synthetic malformed/edge-case files, source hash/size checks, golden decoded-byte hashes, manifest integrity, and parser fuzz/bounds cases as formats expand. Keep copyrighted data out of fixtures.
7. **Visual/audio validation:** render deterministic scenes at fixed simulation ticks and compare against documented captures where the original/emulator can be run. Start with geometry/pixel regions and tolerances; supplement automated image metrics with human review because scaling, color, blending, timing, and filtering can make strict whole-frame hashes brittle. Track simulation correctness separately from presentation correctness.
8. **Manual checkpoints:** emulator/original-game recordings or screenshots for features not independently executable from source; note game version, scenario, setup, and capture conditions.

Reference `make check` is valuable for proving C-source byte matches; its Go tooling tests do not constitute battle-rule tests. A source-derived C# vector should be marked as such until checked against an executing original/emulator.

## 7. Work completed, in sequence

- Inspected the `adamrt/fft_decomp` reference and established that the application repository started nearly empty. Documented source organization, platform constraints, modules, data/media omissions, and source/license questions.
- Added a Windows-first .NET 10 solution, console client, Core library, xUnit test projects, CircleCI build/test/Sonar workflow, README and project plan.
- Added `TilePosition`, immutable map/tile structures, movement-relevant terrain fields, typed movement flags, stable surface IDs, slope packing, and effective `UnitMovementProfile`; profile owns a defensive/read-only surface-cost map and has structural equality.
- Added `BattleState`, unit IDs/state, move command, transition/event types, and a deliberately simplified `BattleSimulator` with immutable successful transitions and rejection results. Tests cover immutability, invalid commands/occupancy/bounds, events, and determinism. This is not an FFT-faithful movement/turn simulator.
- Added `FlatGridMovementRange` as a narrow cardinal flat-grid prototype.
- Added source notes for map boundary and movement-input fields. Added the latest standalone terrain-cost propagation proof (uncommitted at handoff).
- Added managed `Fft.AssetTool`/CLI components in milestones: raw Mode 2 sector reading; PVD/ISO inventory; image profile/hash/CUE checks; reference-inventory comparison; bounded safe extraction; path collision validation; manifest writing; narrow `.SPR` fixed-prefix/attack-image parsing/decoding.
- Added `Fft.Content` versioned manifest and logical-ID loose-file loading with size/hash validation. Client can load a prepared file; no BIN runtime access.
- Extracted all 2,457 ordinary data entries locally and verified hashes. Mixed XA/STR entries are deferred.
- Used `BATTLE/CYOKO.SPR` for a managed extraction/load/parse/decode proof and output-hash fixture, plus all-palette diagnostic PNG preview. Visual result is explicitly approximate; no native/emulator render equivalence is established.
- Added architecture overview and ADRs for headless determinism, compatible baseline vs extension profiles, and offline import/loose runtime assets. ADRs 0001 and 0002 are proposed; ADR 0003 is accepted/implemented.

## 8. Roadmap and measurable acceptance criteria

The detailed roadmap lives in `PROJECT_PLAN.md`; the sequence below emphasizes next work and missing engine/editor foundations. Do not estimate a full completion timeline from current evidence.

### Immediate: preserve and review the current slice

- Review the uncommitted `TerrainAwareMovementRange` and docs as one bounded change; the full test suite passed (134 tests) before preparing this handoff.
- Ensure the project handoff itself and current README/plan changes are included in the next deliberate commit or PR. Do not stage `/scratch/`.
- Re-run clean Release build/tests and review the final `git status` and diff. Keep PR scope reviewable.

### Next simulation slice: movement geometry

- Trace one constrained height/slope/jump case through callers, source fields, arithmetic units, and direction ordering at a freshly recorded decomp SHA.
- Add a synthetic source-mapped map/unit fixture and exact expected reachable set; include boundary and negative cases.
- Add only that rule to the movement policy (or a separate focused service); keep unsupported movement types explicit.
- Acceptance: deterministic tests match the documented fixture; each field used has source provenance; out-of-map/invalid profile behavior is tested; unsupported cases are documented; existing 134 tests remain green.
- Do not integrate terrain policy into `BattleSimulator` until the command API, occupancy/path reconstruction, and policy selection are deliberate.

### Simulation foundations

- Resolve effective unit movement inputs from job, equipment, statuses, weather, and abilities, with tables/precedence traced. Model source-width conversion at import boundaries.
- Choose a first vertical slice such as one battle setup plus turn advancement and a basic physical attack; source-trace CT/turn order, action legality, damage, RNG, update ordering, and victory checks before implementing.
- Define deterministic RNG state and recorded input/command/replay format before RNG-dependent combat grows.
- Acceptance: headless scenario fixture with deterministic replay, observable expected transitions/events, and no client/platform/asset I/O dependency.

### Content model and authoring

- Inventory source records/tool formats for jobs, abilities/formulas, flags, equipment/items, text, sprites/SHP/SEQ/animation events, maps/terrain, encounter/ENTD, AI and campaign scripts.
- Create one typed C# definition model end-to-end (recommend a Job definition plus linked action/ability IDs), validator, schema version, serialization/package round-trip, and CLI diagnostic output. Separate original imported definitions from authored extensions and runtime state.
- Decide serialized format (JSON/YAML/other), extension conflict/override rules, stable ID namespaces, package versioning, localization model, provenance, and whether tool input can be C# source, declarative files, or both. C# authoring should not imply runtime arbitrary-code execution.
- Prototype editor UX only after schema stabilizes: browse/search, inspect/edit, reference navigation, validation errors, and preview. Make editor call shared validators, never duplicate runtime rule semantics.
- Investigate FFTPatcher script/text/sprite tooling and exact script format; inspect TacticsCrafter only for ideas applicable across versions. Record licenses before copying/adapting anything.
- Acceptance: user-authored package with at least one new job and linked ability loads in a test without editing original content or rebuilding engine; invalid references get clear diagnostics; package round-trips; baseline definitions cannot be silently overwritten.

### Assets and presentation

- Confirm the `.SPR` source-derived output hash against an executing original routine/emulator when practical; parse SHP/SEQ metadata and trace frame/palette selection.
- Continue one format at a time: battle map/model, font/text, effects, sound/music, movies only when required by the chosen campaign slice. Keep raw extraction and decode separated.
- Select a Windows graphics/audio/input stack only when a minimal interactive battle view is scoped. Rendering has its own reference image/manual checks and must remain separate from deterministic state updates.
- Acceptance: show a small authored/extracted battle scene at fixed ticks using disk-loaded assets; record manual and automated visual checks; CI can build the Windows client.

### Persistence, campaigns, extensibility, Lion War

- Add explicit versioned save DTOs/migrations after campaign state exists.
- Support content packages and declarative mechanics before untrusted executable plugins; define resource limits, trust, versioning, and determinism before scripts/extensions execute.
- Pin the exact Lion War/TLWotL variant/revision and build a feature-by-feature inventory. Distinguish PSP-origin WotL data from adaptations. Keep original PS1 profile as regression baseline.
- Acceptance for campaign end state: complete original campaign and save/load path, broad content/accounting coverage, source-backed critical rules, manual presentation checkpoints. Acceptance for Lion War: explicitly selected feature list and scenario checks; it remains separate from baseline.

## 9. Risks and open decisions

| Risk / unknown | Why it matters | Next evidence/action |
|---|---|---|
| Decomp is continuously changing and local checkout can lag | Findings can be based on stale code | Record local HEAD for each new source trace; never equate local pin with upstream main |
| Static source is not executable behavioral oracle | C# could reproduce source transcription errors or miss runtime interactions | Find emulator/original-run capture workflow and build scenario vectors |
| Current movement is intentionally narrow | Real FFT movement includes elevation, slope, jump, water, units, mounts, statuses and special modes | Grow one source-traced fixture/rule at a time |
| No complete content schema/editor exists | Adding content manually becomes error-prone and difficult for third parties | Start with typed Job + linked Ability package and shared validation |
| “Battle formulas” can imply arbitrary code | Unconstrained extensions threaten deterministic execution and compatibility | Separate baseline C# rule implementations from declarative parameters/typed effects; postpone arbitrary plugins |
| Asset formats are only partially decoded | Extracted files are not necessarily render-ready | Inventory formats; parse and validate formats one by one |
| Sprite preview is not faithful rendering | STP blending, layer order, animation, timing, camera and scaling affect appearance | Capture fixed-tick reference scenes and compare with tolerances/human review |
| Windows app framework is undecided | UI/rendering choices affect deployment and authoring workflows | Build a headless simulation slice first; choose stack with an actual battle-view prototype |
| Existing CI is Linux and Sonar credentials are environment-dependent | It cannot catch all Windows client packaging issues | Add Windows runner/build once Windows-only packages are chosen; keep secrets in CircleCI contexts |
| Community tooling has edition and license differences | Directly reusing a PSX/PSP tool may be incompatible or impose license obligations | Inspect FFTPatcher license and target formats; use as reference until a deliberate reuse decision |
| Exact Lion War variant is unspecified | Content scope and compatibility behavior differ by release | Select and pin variant/revision before integration planning |
| Project/decomp licenses and local media obligations | Personal/no-distribution intent is not a formal source license | Review upstream and mod licenses before code/data reuse or distribution |

## 10. Starting instructions for whoever continues

**Planning update (2026-10-22):** Use [BACKLOG.md](BACKLOG.md) for current work order and ticket acceptance criteria. Its first task audits current type ownership before any engine extraction; the next movement implementation remains gated on source tracing. The agreed naming convention is recorded in [the architecture overview](architecture/overview.md). Earlier working-tree and test-run snapshots in this handoff are historical; inspect the current checkout before continuing.

1. Read `AGENTS.md`, this handoff, `PROJECT_PLAN.md`, architecture overview, and relevant ADR/behavior note.
2. Check `git status --short` first. At this handoff, current work is uncommitted; preserve it and do not reset or rebase without user direction.
3. Keep `C:\repositories\fft_decomp` read-only. Verify and record its current HEAD before new source analysis. Do not rely on stale line numbers without checking the pinned commit.
4. Keep BIN, extracted files, decoded media, inventories, and previews inside ignored `scratch/`.
5. Run `dotnet build Fft.sln -c Release` and `dotnet test Fft.sln -c Release`; the expected test total is currently 134. Review diffs and run `git diff --check`.
6. Keep the next task narrow and source-traced. The next recommended simulation task is one height/slope/jump rule after reviewing the current cost-policy fixture. The next authoring-design task is a typed `JobDefinition` + ability reference + package validation spike, not a full editor.
7. Do not claim the work is a complete engine/game or that extracted assets are legally redistributable. State what is verified, source-derived, simulated, and still unknown.

## 11. Helpful commands

```powershell
dotnet build Fft.sln -c Release
dotnet test Fft.sln -c Release
dotnet run --project src/Fft.Client/Fft.Client.csproj -c Release --no-build
git status --short
git diff --check
```

For prepared Chocobo fixture verification (requires the ignored local prepared directory):

```powershell
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- verify-sprite --content-root scratch/prepared-chocobo-content
```

