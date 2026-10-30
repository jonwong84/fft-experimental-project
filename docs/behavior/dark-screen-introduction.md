# Pure combined DarkScreen introduction (ZOD-097)

`DarkScreenIntroductionProgram` now composes the existing pure DarkScreen,
general-banner and Ready workers through one clock, event consumer and retained
task ledger. It publishes battle availability only at accepted synthetic event
exit with all three tasks terminal. This is authored logical behavior; prepared
loading, combined rendering, device/input routing and the actual battle-window
bridge remain ZOD-098.

Date: 2026-10-25. Managed base: `d2f7903`, clean on entry. Freshly verified clean
read-only source revision: `11464943bbdbd2a0af022d90cb7f10455cf911df`. Forty
source files and sixteen existing managed components retain the identities in
the [ZOD-096 source evidence](../evidence/events/dark-screen-intro-source-v1.json).
The [selected contract](../architecture/zod-096-combined-dark-screen-introduction.md)
remains historical. Separate [executed evidence](../evidence/events/dark-screen-intro-worker-v1.json)
maps DSI-001..024 to passing tests without changing its original unexecuted flags.

## API and scope

The implementation is in
[DarkScreenIntroduction.cs](../../src/Zodiac.Framework/Events/DarkScreenIntroduction.cs).
Construct the program with a validated `StartDarkScreen`, condition-8 general
definition, condition-0 Ready definition, unsigned-byte display holds and one
shared owner. Program, definition, artwork and owner IDs must be distinct and
bounded. Original table preparation remains outside simulation; tests use an
authored 288-cell grid with delays 0/1/41, not copied original assets.

`program.Start(sceneRun, consumer, binding)` creates an unadmitted candidate.
The binding identifies all three ready logical definitions/resources; it makes
no claim about file or GPU readiness. `RunUntilBlocked` validates the complete
binding before admitting DarkScreen and entering the first pending barrier at
tick zero. A mismatched/missing binding consumes no task identity or tick.
`AdvanceTick` takes one explicit nullable-Boolean Circle sample and shared
instruction/update budgets; null rejects even when no worker would read it.

The public program has nine fixed operations and exposes no mutable instruction
list, external task-update batch, cancellation, restart or arbitrary release API.
Only its event phase releases DarkScreen after Ready's barrier. Referenced
standalone preview holds do not exist in this pure program. The previous public
programs and executors, including accepted no-match standalone releases, are
unchanged. No new engine abstraction or helper refactor was needed.

## Atomic ordering and retained state

Each tick first validates the accepted snapshot, then advances/polls the event
using the preceding ledger facts. It pumps event operations until another
suspension or exit, resumes only workers that existed before the call, and
publishes worker facts after that parent poll. DarkScreen resumes before the
active display; Ready's dispatcher resumes before its child. A new barrier
always begins pending and is never repolled in the same command.

DarkScreen uses the candidate event's same-tick release signal. That produces
one final flash and resets delays without stepping the release body immediately.
General's birth consumes no Circle sample or worker resume; Ready retains its
initialization draw without a second birth resume or input sample. Unread Circle
samples are not stored or forwarded. Trace entries mark Circle only where the
worker actually reads it, preserving ignored-sample state/trace equality.

Stable task sequences are DarkScreen 1, general 2 and Ready dispatcher 3.
Ready's child is a derived identity, not a fourth ledger task. DarkScreen retains
revisions 0/1/2/3 through Entering/Holding/Leaving/Completed; displays complete
from revision 0 to 1. At most two ledger tasks are live together. Every terminal
record remains a tombstone; completed workers are retained without further steps.

Binding validation checks program and inner-VM identity, fixed continuation/
barrier stage, scene/owner/resource identities, worker clocks, selected lifecycle
coherence, task phases/revisions and sequence counters. States have no public
constructor or setters; this API does not add an untrusted runtime-save loader.

Instruction budgets are 0..8192 and ledger-update budgets 0..64, shared across
the whole command. Any command rejection returns the exact input object, an
empty trace, zero accepted counts and no availability publication. A late
completion failure restores already advanced dark/display candidates as well
as the event poll, ledger, input progress and clock. Checked clock overflow is
reported through the existing task-ledger rejection. Retrying with adequate
budgets matches a clean transaction.

## Executed logical checkpoints

For the selected maximum delay 41 and natural 60/60 holds, tests establish:

- Tick 0: DarkScreen birth, no draw, pending transition barrier.
- Tick 54: all entry counters finished, transition task still live.
- Tick 55: same task becomes Holding, retaining the extra tile pass.
- Tick 56: parent admits general; dark emits its first flash.
- Ticks 147/148: general clears; parent subsequently admits Ready at scale 48.
- Tick 208: Ready dispatcher releases its child, which draws fade scale 128.
- Ticks 216/217: child then dispatcher clear; battle is still unavailable.
- Tick 218: parent releases dark; it emits its final flash.
- Tick 219: first release tile body.
- Ticks 273/274: extra release body, then task clear; parent still waits.
- Tick 275: parent observes completion, synthetic exit and one availability fact.

Natural holds 0/0 and 255/255 exit at 155 and 665. The specified samples at
1/55/56/57/59/60 skip general and Ready while discarding birth samples, producing
exit at 127. General hide ignores Circle. Independent closed-form checks cover
all cells/draws, display counters/scales, barrier stages and availability across
natural hold variants; authored maximum delays 0/1/7/41 also preserve variable
worker duration rather than a hardcoded 55-tick timer.

These are executed managed logical transactions, not measurements of original
loading, animation scanout, physical input or wall-clock duration. Original
event 401's camera gap and EventSpeed/EventEnd are still excluded.

## Availability and validation

`State.BattleAvailable` requires accepted event exit, all three workers terminal
and no live ledger tasks. `Result.BattleBecameAvailable` and its trace fact occur
once along an accepted execution. Child clear, dispatcher clear, zero-area dark
draw and even all-terminal tasks at tick 274 do not open the gate. Post-exit
commands reject without another tick or publication. The pure aggregate has no
battle command, RNG, CT, renderer, device, filesystem or time dependency.

All **80 new Framework checks** pass: 24 historical contract cases and 56
additional boundaries. They include natural/zero/maximum/skipped timelines,
independent per-tick cell/display equations, same-tick birth/input rules, trace
order, source-phase gaps, constructor/binding/identity negatives, cumulative
budgets, late rollback/retry, checked overflow, deterministic complete state/
trace replay, terminal gate and standalone no-match preservation.

Locked solution restore and a zero-warning/error Release build pass. All
**2,111 .NET tests** pass: Engine 24, Framework 805, Content 546, AssetTool 202,
CLI 202 and Client 332. All **269 Python tests** pass. Existing source workers,
Content/client/engine code, historical contracts and original inputs are unchanged.
There are no new packages, native captures, emulator runs or GPU checks.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p "test_*.py"
```

DSI-025..040 remain unexecuted ZOD-098 work: the closed prepared descriptor,
three-resource readiness, DarkScreen-before-display composition, Windows
CPU/GPU/model checks, actual battle/input bridge and separate owner gallery
approval. No new visual review is required for this pure milestone. Full
original startup/event playback, missing original DarkScreen endpoints, map,
hardware, physical timing, CRT and audio remain separate.
