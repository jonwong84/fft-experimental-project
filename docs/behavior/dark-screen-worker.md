# Pure dark-screen worker and ordered task proof

ZOD-093 implements one post-readiness origin-style DarkScreen worker in
`Zodiac.Framework.Events`. It uses the existing scene ledger and conditional,
frame-wait and barrier executor inside a separate typed authored profile.
General-banner, Ready, introduction/battle and original bytecode profiles are
unchanged. No graphical host admits this worker yet.

Date: 2026-10-25. Managed base: `c064161`. Read-only reference HEAD was freshly
checked as `11464943bbdbd2a0af022d90cb7f10455cf911df` before implementation.
The [ZOD-092 trace](../architecture/zod-092-dark-screen-transition.md) binds its
callers, parameters, arithmetic, yields, material and original holding evidence.
This implementation executes no original instructions and reads no original
tables, files or saved states at runtime. The separate
[validation metadata](../evidence/events/dark-screen-worker-v1.json) records this
milestone's checks; the historical source contract remains unchanged.

## Definitions and authority

`DarkScreenDefinition` defensively copies 288 staggered, row-major tile
coordinates and supplied initial delays. It validates the compatibility layout,
delay envelope 0..41, bounded namespaced ID and exact selected parameters:
reserved 0, origin mode 1, divisor 12, angle step 64, radius step 4, image 0.
Other parameters fail before any scene/task admission. The original zero-divisor
trap is documented in the trace; rejection is this proof's unsupported-input
policy, not a claim that the original clamps or recovers.

The definition takes **prevalidated delay data**. It does not derive delays from
exact sqrt, ideal trigonometry or an original table. The authored test grid uses
synthetic delays 0/1/41; smaller maxima exercise phase boundaries. Original-table
conversion and verification of every prepared delay/rotation sample are ZOD-094.
The original approximation's six differing delays remain a preparation gate.

`ReadyDarkScreenBinding` attests only the ready logical definition and owner IDs.
Missing/mismatched bindings reject before admission. It does not attest loaded
BONUS/general/Ready artwork or a prepared package. File/resource readiness must
be added by the later Content/host milestone before a real scene starts.

`DarkScreenProgram` admits typed Start/Release and existing scene instructions;
it accepts one syntactic admission, multiple releases and forward control flow.
An unreachable admission may be skipped. It imports no original script and
does not widen any legacy profile. `StartScene` preserves the supplied ledger's
run identity, sequence, tombstones and clock. Another live DarkScreen task is
unsupported; completed history is retained. The worker can coexist with other
task families in this pure ledger, but no banner/Ready composition is introduced.

The state exposes immutable tile counters and an accepted `DarkScreenDraw`:
None, row-major Tiles or Flash. Tile draws retain their pre-update angle/radius,
including zero-area submissions, and refer to the definition by index. The
selected RGB is 48/40/16. These are logical submissions, not rasterized pixels,
native packet-bank emulation or a GPU completion signal. A renderer must later
respect reversed native execution order and the selected flat RGB5 material.

## Ordered transactions and timing

`RunUntilBlocked` runs the initial Ready event to a wait/exit. A newborn worker
remains at its first yield with no draw or step. `AdvanceTick` advances exactly
one shared ledger tick and performs these ordered candidate operations:

1. Poll/resume the parent using only facts published by the preceding transaction.
2. Run the event until its next wait/exit, including admission or a matched release.
3. Apply ordered unmanaged facts using the remaining update budget.
4. Advance an existing worker once and publish any resulting phase/completion fact.

Nothing is published until all four steps succeed. Instruction exhaustion,
arithmetic overflow/domain errors, ledger limits, invalid facts and late update
budget failures return the exact input object, empty trace and zero accepted
counters. This includes rollback after a candidate admission, release, Holding
publication or completion. Retries retain identity and publish transitions once.
Predicted newborn identities, stale managed facts, external cancellation and
external DarkScreen starts are rejected. Other families still use the existing
ledger's ordered validation and shared capacity/budget rules.

For a selected maximum delay of 41, the worker preserves the source sequence:

- Birth tick 0: task 54, no draw. Entry waiting cells decrement without a draw;
  started cells draw before adding 64/4 and clamping to 512/48.
- Tick 54: strict overshoot has first latched every cell finished. Task 54 lives.
- Tick 55: the extra full tile pass changes the same task to Holding, class 55,
  revision 1. This accepted snapshot still contains tiles.
- Tick 56: the parent observes no class 54; the first flash is submitted.
- Holding continues indefinitely. There is no Circle/input parameter in this
  API, and presentation cannot advance or release the worker.
- A release while Entering, before admission, while Leaving or after completion
  is an accepted no-match no-op. It never queues a future release or restarts work.
- A matched ordered release at R changes the same task to Leaving/class 54,
  revision 2. The worker emits one final flash, restores initial delays and reaches
  its release first yield. Tile stepping begins at R+1. Waiting cells still draw
  their full stored shapes while their delays decrement.
- R+54 first latches completion; R+55 emits the extra zero-size tile pass and
  reaches the exit wait with class 54 active. R+56 clears the task, revision 3,
  retains its completed tombstone and submits no draw. Parent observation is R+57.

For R=100 these final checkpoints are 154/155/156/157. For a synthetic maximum D,
either body takes D+14 resumes. All times are explicit post-readiness scheduler
transactions derived from source, not observed original display durations.
An exited interpreter stays exited while its scene-owned worker continues;
terminal observation advances only the scene clock and never revives the worker.

## Validation and remaining gates

Locked restore and the Release solution build pass with **zero warnings/errors**.
All **1,967 .NET tests** pass: Engine 24, Framework 725, Content 516, AssetTool 192,
CLI 196, Client 314. The **58 new managed tests** execute DSC-002..021/023/024 at
the logical layer, and the logical portions of DSC-001/022. They cover births,
all phase boundaries, stable IDs/revisions, no-match release, multiple releases,
full rollback, ownership, late external failures, budget/capacity/clock boundaries,
retained history and exact full-state/trace replay with repeated/omitted reads.
An independent closed-form check covers every authored cell's counters and
pre-update draws through both phases. All **229 Python tests** pass.

DSC-001's actual prepared/resource readiness and DSC-022's real GPU/resource
recreation remain open. DSC-025..029 remain unexecuted: original prepared math,
flat-quad rasterization/material/coverage, prepared-only loading and original
entry/release presentation evidence. DSC-030 has the full managed regression
suites, but this milestone does not rerun native capture/output comparisons.
Historical expectation flags remain `executed: false`; the new validation
metadata distinguishes executed logical checks from these pending gates.

No original capture, emulator execution, image/pixel comparison, native UI review,
audio/CRT work, combined startup/EventEnd or physical input timing is claimed.
No native/presentation outputs need approval for this headless logic milestone.
**Next: ZOD-094**, verified offline definition/rotation preparation, strict
separate prepared loading and a bounded flat-quad preview, with its own finite
native checks and gallery review. Integrating a held DarkScreen with the
existing banner/Ready scene remains subsequent work.

Reproduce from the repository root:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p "test_*.py"
```
