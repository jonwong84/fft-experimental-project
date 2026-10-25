# ZOD-081 — Ordered event/banner transactions

Date: 2026-10-25. Edition: `SCUS-94221`. Managed starting revision:
`3d69dc99c921b5ef622854560007b7bb72d2f8ab`. Read-only reference HEAD verified:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

ZOD-081 implements the typed, pure `Zodiac.Framework` transaction selected by
[ZOD-080](../architecture/zod-080-banner-opcode-admission.md). Its source trace,
operand widths, scheduler relation and retained original observations remain in
the [source evidence](../evidence/events/banner-opcode-admission-v1.json).
No original bytecode, resource loading or presentation code executes here.

## Public boundary

`OrderedBannerProgram` validates normalized conditional operations, frame waits,
selected task barriers and at most one syntactic `DisplayGeneralBanner`, including
unreachable instructions. Admission requires condition 8, hold 0..255 and explicit
definition, artwork and owner IDs. Programs may bypass admission through a forward
branch or contain no display instruction, allowing a subsequent consumer to wait
on an existing scene worker.

`PreloadedBannerBinding` is the composition root's trusted logical assertion that
the selected definition/artwork/owner is ready. It contains no files, textures or
resource callbacks. `OrderedBannerProgram.Start` creates a fresh consumer over a
supplied scene ledger. A missing or mismatched binding causes executor rejection
before task/clock/identity changes; Core cannot establish disk/GPU readiness.

`OrderedBannerExecutor.RunUntilBlocked`, `AdvanceTick` and `StartNextEvent` publish
an immutable `OrderedBannerResult` or return the exact input state, empty trace and
zero accepted counters. All scene/task/effect identities and terminal records
survive consumer replacement. Reaching a second display in the same scene fails,
even after the first banner has completed. Consumer replacement requires an exited
VM and a different explicit consumer ID.

## Transaction order and shared implementation

An initial run pumps the VM in instruction order. Display allocates one managed
condition task and initializes the existing `GeneralBannerState` at its first
reveal yield, then continues asynchronously. It consumes one instruction and one
update, no tick or Circle sample. E5 always enters Pending without polling.

An explicit tick:

1. Validates the logical binding, explicit Circle sample (including false), typed
   update envelope, cancellation and resource budgets.
2. Increments the checked clock and polls/resumes the prior suspension using only
   task facts published by the preceding transaction.
3. Pumps the resumed VM, possibly admitting the banner or entering a new wait.
4. Applies unmanaged facts in supplied order, then advances a previously existing
   effect and appends its generated terminal fact if needed.
5. Publishes the complete candidate and ordered trace together.

A newborn effect does not advance on its birth tick; that tick's Circle sample
is discarded rather than deferred. An exited VM can still tick its scene worker.
Only the driver may update the managed identity, including a terminal tombstone
or an identity newly allocated earlier in the same call. Owner cancellation is
validated separately and clears the worker in the post-VM phase; it is distinct
from the original Circle skip's extra active yield.

The existing `SceneEventExecutor` shares internal one-instruction and
tick/resumption primitives. `SceneTaskLedger` shares validation and an internal
post-VM update phase that never polls. No public half-tick or admission callback
exists. The old public scene executor still applies updates before polling;
`BannerSceneExecutor` keeps its effect-first behavior and trace contract.

Instruction work shares one 0..8192 envelope per call. Generated Start, supplied
facts and generated Complete/Cancel share one 0..64 update envelope. A later
completion cannot free capacity for an earlier admission. Budget, arithmetic,
identity, revision or clock failure in any phase discards all candidate work.

## Regressions and validation

[OrderedBannerContractTests](../../tests/Zodiac.Framework.Tests/OrderedBannerContractTests.cs)
loads the unchanged [preimplementation expectations](../evidence/events/banner-opcode-admission-contract-v1.json)
and executes every Core case, `BOP-001` through `BOP-027`. Their original
`specificationOnly` / `executed: false` flags describe that historical artifact;
this implementation note records their subsequent execution. `BOP-028` remains
the separate Content/host gate for ZOD-082.

The 27 contract cases prove:

- Natural hold 0/60/255 clears at tick 31/91/286; waiting parents resume at
  32/92/287. The effect lifetime itself is unchanged.
- First-reveal skip clears at 2 and resumes at 3; the selected hold skip clears
  at 23 and resumes at 24. Hide ignores Circle, including zero-hold transition.
- Birth after F1 stays at zero progress; sequential empty barriers require
  separate ticks. All matching owners delay a class barrier.
- Post-poll completion cannot release the parent retroactively, and post-poll
  Start cannot re-block a released parent. Traces retain that phase order.
- Tentative allocation, late arithmetic/budget/capacity failure, missing binding,
  managed-identity forgery, consumer restart, terminal retention, cancellation and
  checked counter exhaustion preserve the selected atomicity/lifetime rules.
- Repeated command histories produce identical complete traces and final
  VM/effect/task/identity state, with omitted or repeated immutable snapshot reads.

Another 33 boundary cases cover skipped admission, resumption rollback, cumulative
instruction/update budgets, newborn identity protection, post-release failure,
tombstone capacity, revision overflow, invalid envelopes, command state, strict
typed operands and cancellation failures.

Validation on 2026-10-25:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p 'test_*.py'
```

Locked restore succeeds. Release build has zero warnings/errors. All **1,554 .NET
tests** pass (Framework 568, including 60 new; other suites unchanged), as do all
**163 Python tests**. Original/reference files and historical evidence remain
unchanged. No emulator run or visual comparison was needed for this pure milestone.

## Next gate

ZOD-082 adds the explicit authored preloaded profile, unsigned byte conversion,
strict Content projection and prepared-files-only host composition. Existing
profiles still reject opcode 78. Original events 4/401, original loading duration,
Ready/other conditions, EventEnd, full BattleStart and arbitrary native scheduling
remain unsupported. No automatic commit.
