# Bounded conditional-event execution (ZOD-065)

Date: 2026-10-23. Target: North American PS1 `SCUS-94221`.
Read-only reference HEAD, verified for this milestone:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

The [ZOD-040 authored conditional event](../architecture/zod-040-event-script-investigation.md#chosen-event-synthetic-conditional-outcome)
now executes through pure FFT-owned transitions. Input variable 2 selects an output
in variable 3: input 1 yields 1, every other admitted input yields 2. Dirty initial
values in variables 0, 1 and 3 are cleared by the actual instructions. Both paths
execute ten operations and exit at source offset 45; neither executes DB.

This is source-derived behavior and synthetic managed regression evidence. No
original event, C interpreter, emulator or independent semantic oracle was run.
It does not establish retail event, campaign or complete arithmetic compatibility.

## Ownership and admission

[PreparedScenarioEvent.ToExecutionDefinition](../../src/Zodiac.Content/Events/PreparedScenarioEvent.cs)
projects ZOD-064's validated [prepared representation](../development/prepared-scenario-event.md)
into [ConditionalEventProgram](../../src/Zodiac.Framework/Events/ConditionalEventProgram.cs).
Content retains raw framing/encoded sizes, labels, source hash, edition, scene,
dialect/profile and JSON provenance. Core receives only the stable event ID,
ordered diagnostic source offsets and normalized semantic operations. Content
resolves the first legal label and projects the **after-marker landing**. Core
does not scan bytes or labels; DB is omitted and target/exit labels are discarded.
Battle/World metadata remains on the prepared object; this proof's operations
have no scene-specific behavior or scene setup/completion authority.

Core independently validates the normalized contract: known operations/operand
shapes, variable IDs 0..3, strictly increasing nonnegative offsets, forward targets
on admitted operations, and explicit exit on every reachable path, checking both
conditional edges. Offsets are source identities, not array indices or encoded
widths; normalized sequencing uses operation order. Unsupported or malformed
definitions fail before a state can be started. Validation covers unreachable
operations too; unreachable supported operations need not run.

`Zodiac.Framework.Events` owns the immutable definition, snapshot, variables, transition
results and traces. Content still references Core; Core has no Content reference.
Zodiac gains no FFT scripting responsibility. No project/package changes, save
schema changes, application command or UI integration are required here.

## Source semantics and explicit proof limits

The pinned
[battle dispatcher](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c)
and [world dispatcher](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/event/interpreter.c)
call the shared
[control helpers](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/control.c).
Arithmetic operands are decoded as unsigned 16-bit fields, while variables 0..3
are signed 32-bit words. `script_variable_command_run` reads the operand before
writing the destination, so an aliased add reads the old value. BE writes zero;
B0/B1 add an immediate/source value; A2 compares words 0 and 1 and replaces word
0 with integer 0 or 1. F2 and sequential D2 do not write variables.

D0 branches only when word 0 is zero and may target the first matching D2 or D4;
D1 always branches and only targets D2. Both continue after the marker, without an
extra sequential increment. Sequential D4 exits the thread. These rules are traced
in `script_jump_target_find` and both dispatchers. Core's `Target` is sequential
D2 and `Exit` is sequential D4. DB/E3 EventEnd lifecycle remains unsupported.

The proof has these explicit restrictions:

- Exactly four variables, with every initial value and every written value in
  **0..65,535**. This is a selected proof domain, not the original variable width.
  Admitted addition/equality is exact within signed 32-bit arithmetic. B0 operands
  retain their unsigned 16-bit range. A sum above 65,535 rejects instead of wrapping,
  clamping, widening the proof or claiming original overflow behavior.
- Progress suppression is off. No packed fields, event/BIOS RNG, weather/text or
  party side effects. Source accessor/veto behavior outside this subset remains
  excluded; see [variable access](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/variables.c).
- At most 8,192 normalized operations and a caller budget in 0..8,192. These are
  proof resource limits, not original table capacities. The forward-only graph
  cannot loop, but callers can still set an insufficient budget.
- No waits, concurrent tasks, backward jumps, dialogue, timing, RNG draws,
  unit manipulation, EventEnd, scene transitions or campaign completion. All
  original-byte capability gates remain in force before projection.

Do not infer signed/overflow semantics, original scene initialization or general
script compatibility from the admitted arithmetic range. Later bug corrections
(ZOD-070) remain separate selected FFT profile work.

## State, traces and failure behavior

`program.Start(new ConditionalEventVariables(v0, v1, v2, v3))` validates all initial
values and creates a snapshot at the first operation. The state has a program
reference, instruction offset, four values and `Exited`; callers cannot forge an
instruction position or mutate a snapshot. Instructions and caller collections are
copied into immutable definitions. A fresh Start is an explicit restart; there is
no persisted event checkpoint or extension of ZOD-035's battle save format.

[ConditionalEventExecutor](../../src/Zodiac.Framework/Events/ConditionalEventExecutor.cs)
offers `Step(state)` and `Run(state, instructionBudget)`. Step commits exactly one
operation. Run commits only if explicit thread exit is reached within the budget;
the Exit operation itself consumes one instruction. Starting from an existing
instruction-boundary snapshot is supported. Neither function reads files, time,
input, rendering, randomness, the BIN or reference code.

Each successful operation emits one ordered immutable trace: source offset,
operation, variables before/after, next offset and exit status. Even no-op/target,
branch and exit operations appear in the trace. On exit the final offset remains
the Exit operation's source offset (45 for the selected fixture), with `Exited`
true; it is not a fabricated next EventEnd position. Repeated Step/Run on an
exited state rejects with `AlreadyExited`.

Arithmetic, budget and invalid-budget rejections return the **exact input state
object**, an empty committed trace, a typed reason, diagnostic and failure offset.
Run discards any candidate steps on failure, including earlier variable writes.
Callers using successful individual Steps intentionally publish those boundaries;
a later failed Step preserves its own input boundary. No failure is reported as
normal thread completion. Definition/initial-domain admission errors throw
argument exceptions before any running snapshot is returned.

## Regression evidence and verification

[Core regressions](../../tests/Zodiac.Framework.Tests/ConditionalEventExecutorTests.cs)
independently author the full expected per-instruction before/after/next-offset
traces for inputs 0, 1, 2 and 65,535 with initial values `(7,8,input,9)`. They check
Step/Run agreement, unchanged inputs, exact replay, continuation, equality bounds,
aliased addition, zero/NOP/target/exit, admission failures, arithmetic rollback,
zero/short/exact/max/invalid budgets, and post-exit rejection. Expectations are not
generated by the executor or by inspecting its definition.

[Content integration regressions](../../tests/Zodiac.Content.Tests/PreparedScenarioExecutionTests.cs)
load the independently authored committed JSON through a hash-verified temporary
manifest, project and execute it without a raw block, then remove the files and
replay. Projection leaves the prepared serialization unchanged. Separate synthetic
programs demonstrate D0 landing after D4 versus sequential exit, and D1 ignoring
D4 labels and skipping the matching D2 marker. No original asset is needed.

Run from the repository root:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

Targeted verification passed **39 Core + 6 Content cases** for this milestone.
Full locked restore passed, the Release build reported zero warnings/errors,
and all **1,057 tests** passed: Core 332, Content 336, AssetTool 164, CLI 103,
Engine 24 and Client 98. This adds 45 cases to the previous 1,012-test baseline.

ZOD-066 now [traces original common BattleStart](../architecture/zod-066-battle-start-event.md),
including activation, every opcode and completion prerequisites. It selects
ZOD-071's [separate timed profile](timed-event-waits.md) now implements that bounded
frame wait; this synchronous profile remains unchanged. Original playback still
requires camera/task/banner/EventEnd capabilities.
No silent instruction skipping or bulk interpreter port is justified by this proof.
ZOD-068's synthetic campaign save service remains a separate deferred lane.
