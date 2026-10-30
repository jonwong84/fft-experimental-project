# ZOD-084 — Ordered Ready dispatcher and child

**Presentation follow-up (2026-10-25):** ZOD-085's
[selected Ready policy](../architecture/zod-085-ready-rendering.md) is complete.
ZOD-086 can now implement its separate prepared artwork/authored host. The
remaining-gates section below records the original ZOD-084 checkpoint.

Date: 2026-10-25. Edition: `SCUS-94221`. Managed starting revision:
`4189d79a38bad257d2f76d45e92388ce2c359b19`. Read-only reference HEAD verified:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

ZOD-084 implements the pure condition-0 Ready aggregate selected by the
[ZOD-083 source study](../architecture/zod-083-ready-display.md). The existing
general-banner APIs, behavior and profiles are unchanged. This milestone contains
no Content, preparation, host or graphics implementation.

## Public boundary and ownership

`ReadySceneProgram` validates normalized conditional operations, frame waits,
selected task barriers and at most one syntactic `DisplayReady`. Validation includes
unreachable records. Ready requires condition 0, unsigned-byte hold 0..255 and
explicit logical definition, artwork and owner IDs. Forward branches can bypass
admission; a program without a display can consume an existing worker's lifetime.

The existing `PreloadedBannerBinding` serves as a trusted logical readiness
assertion containing definition/artwork/owner IDs. A missing or mismatched binding
rejects execution before allocation. Files, palettes, textures and readiness
callbacks never enter Framework state; composition must establish actual resource
readiness before starting a scene in the future ZOD-086 host.

`ReadySceneExecutor.RunUntilBlocked`, `AdvanceTick` and `StartNextEvent` return an
immutable accepted state/trace or the exact original state with empty trace and
zero accepted counters. No public phase-step or callback API exists. Programs and
snapshots are immutable; the driver alone advances a retained aggregate.

One ledger task of family `ConditionDisplay` (class 56) represents the dispatcher.
`ReadyChildId` derives from that identity and is not a second named ledger task.
The child has its own phase and terminal state within `ReadyDisplayState`; the
named task remains Running/revision 0 after child completion. Dispatcher exit
publishes Completed/revision 1. All identity, clock, aggregate and terminal records
survive event-consumer replacement. Even a terminal aggregate prevents a second
Ready admission in that scene. Replacement requires an exited VM and a different
explicit consumer ID.

## Ordered transaction and timing

An initial run pumps instructions until a wait or exit. Admission consumes one
instruction and one task Start, initializes the dispatcher and child to their
first yields, and consumes no tick or Circle sample. H>0 submits logical scale 48
at birth; H=0 releases before child initialization and submits scale 128.

Every explicit tick validates inputs, polls/resumes the event using prior task
facts, pumps any resumed instructions, applies supplied facts in order, resumes the
existing dispatcher, then resumes its child. Generated terminal facts publish
after supplied facts. A newborn already initialized during that call receives no
additional resume; its Circle sample is discarded. An exited event still permits
worker progress. A post-poll completion cannot release the event retroactively;
a post-poll Start cannot re-block an event that already resumed.

The dispatcher samples Circle once per positive-hold wait resume. Fade-in proceeds
concurrently: scales 48,56,64,72,80,88,96,104,112,120 precede full scale 128 at tick
10. Natural release is R=H; first sampled Circle in 1..H releases earlier and
records `CircleSkipped`, including on the last sample. H=0 takes no wait samples.
Circle after release does not affect the child. Release resets the child to scale
128, including the observable jump from an interrupted fade-in.

The child submits seven fade-out draws: 128,116,104,92,80,68,56. At R+7 it resumes
into the extra active yield and submits no draw (`DrawScale = null`); R+8 clears
the child. The earlier dispatcher poll still sees the child active that tick.
At R+9 the dispatcher clears and publishes its task completion; the earlier event
poll resumes at R+10. For natural holds 0/1/60/255, child clear is 8/9/68/263,
dispatcher clear 9/10/69/264, and waiting event exit 10/11/70/265. Initialization
after an F1 wait shifts these times by its birth tick.

`ReadyCancellation` is explicit scene-owner policy, separately validated against
the managed identity/revision. Acknowledged cancellation cancels the live aggregate
post-VM, publishes its named cancellation, and releases a waiting event on the next
tick. An already completed child remains completed. This does not implement the
original child's parameter-3 value-1 path or reinterpret Circle as cancellation.

Trace facts distinguish admission, dispatcher/child initialization, input/release,
child polls, child draws/clear and dispatcher clear. Event/ledger facts retain the
shared primitive traces. Terminal observations retain the same aggregate instance
without advancing its last-processed tick or accepted-effect counter.

## Atomicity and regression evidence

The executor reuses internal one-instruction/tick primitives from
`SceneEventExecutor`, post-VM ledger validation and existing immutable task helpers.
It does not change the older executors' public ordering. Instruction budget is
0..8192; generated starts, supplied facts and generated Complete/Cancel share a
0..64 update budget. The live/total task limits remain 64/256. Later completion
cannot free capacity for an earlier admission. Managed IDs are protected from
external Phase/Complete/Cancel, including newborn predicted identities and retained
tombstones. Late arithmetic, counter, capacity, authority or budget failures roll
back all tentative event/child/dispatcher/task/clock/trace changes.

[ReadySceneContractTests](../../tests/Zodiac.Framework.Tests/ReadySceneContractTests.cs)
loads and executes all 28 Core cases, RDY-001..028, from the unchanged
[preimplementation contract](../evidence/events/ready-display-contract-v1.json).
Its original `specificationOnly` / `executed: false` flags remain historical.
This note records subsequent managed execution. Another 25 boundary cases cover
unsupported/unreachable definitions, branch skips, invalid envelopes and command
states, cumulative/zero budgets, newborn authority, late admission/release failures,
capacity ordering, checked clock/identity/revision overflow, cancellation and
corrupted bindings. Full replay compares accepted states and complete traces with
zero, one and repeated snapshot reads, and verifies retained snapshots never mutate.

Validation on 2026-10-25:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p 'test_*.py'
```

Locked restore succeeds; Release build has zero warnings/errors. All **1,682 .NET
tests** pass (Framework 621; Engine 24, Content 450, AssetTool 179, CLI 166, Client
242), together with **163 Python tests**. The coverage inventory/view, pinned
reference checks, links and whitespace checks pass.

## Remaining gates

The selected schedule is event → dispatcher → child; arbitrary native slots and
shared-pool concurrency are unsupported. This proves source-derived managed
behavior, not measured original duration, startup loading or visual equivalence.
No emulator was run and no new original capture is claimed. Draw scales are logical
submissions, with no renderer or renderer completion acknowledgment.

RDY-031 remains ZOD-085's separate artwork/rendering/readiness selection: image 0,
palette 2, six glyph quads, full-corner color scaling and average/STP blending.
RDY-029/030/032 remain ZOD-086's separate authored conversion, preparation and
prepared-files-only host proof. Old general-banner rendering policies and profiles
must remain closed. Condition 1, WORLD/tutorial/speed changes, retail events 4/401,
combined general→Ready playback, EventEnd and full BattleStart remain excluded.
