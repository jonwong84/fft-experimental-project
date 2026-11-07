# Captured banner event identity — event 4

Date: 2026-10-24. Edition: `SCUS-94221`. Managed base:
`07431935a64890db4524ae0be223a9c688392d21`. Read-only reference HEAD, freshly
verified: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

The earliest frozen pre-banner state and seven selected later states identify
**event 4 (`0x004`)**. The target condition-8/hold-60 banner belongs to its
`DisplayConditions` command at block offset **804**, followed by the task-56 wait
at **807**. These captures do not establish common BattleStart event 401's
fallback activation. The earliest state is already inside event 4; its original
event-entry and scenario-selection steps remain unobserved.

The [metadata evidence](../evidence/visual/original-banner-event-identity-v1.json)
binds the existing states, archive, executable ranges, plan, tools and repeated
reports. No new capture or original instruction execution was required. The
existing banner appearance/input evidence remains valid for its stated effect;
historical documents retain their earlier identity gap rather than being rewritten.

## Independent identity checks within each state

The resident interpreter, event loader, loaded-event helper, variable access,
frame-wait helper, DisplayConditions wrapper and scheduler save-context prefix
match ranges from the whole-file hash-verified original `BATTLE.BIN`:
`7af579c7bcfd46f21f8d6886acb6aae156474dace667553e76079be7c7d6db94`.
The reference's `target/battle.yaml` maps it directly at `0x80067000`.
The 244 original instruction lengths match the existing managed structural
metadata hash `5f50d72e0e4d2a62215be25bc01f9123c0727a2abdcb9a4ff038511c569d915f`.

All eight states agree on:

- `g_script_variables` points to `0x8005771c`. Its 32-bit current-event variable
  `0x27` is **4**. The separate loader cache at `0x801696fc` is also **4**.
- `g_script_event_block` points to `0x8004a6bc`. Its first word is the processed
  NOP marker. Every remaining byte of the 8,192-byte block, including text and
  padding, exactly matches raw archive block 4. An exhaustive payload comparison
  against the 500 physical archive slots gives **one match: 4**. This does not
  assert that all slots are valid or reachable events.
- The raw header gives text offset **823**; event text section 0 points to
  `0x8004a9f3`, exactly block base + 823. The raw code structurally walks to the
  DB sentinel at offset **822**, with **136 records** starting at offset 4.
- Slot 1 is running task **68**, the BATTLE event interpreter. Its saved opcode,
  instruction offset, known yield site, bounded stack and caller chain agree.

The archive is the previously verified 4,096,000-byte `EVENT/TEST.EVT`, SHA-256
`107b109254ccbb150aca7baf336b632837b22e340677e2b9e746ac61b3de4405`.
Raw block 4 occupies archive offset **32,768**, SHA-256
`528875cb3cde1eb84c709f8b2de9afb41478041710b134a0ede075ae34d1bef7`.
The loaded block differs only in bytes 0..3, as required by `script_event_load`.
No fuzzy matching, ignored mutable operand ranges or text decoding is used.

## Saved interpreter position and banner attribution

The original interpreter uses register **$fp** for `instruction_offset`. It is
saved in thread context offset `0x40`; it must not be treated as a stack address.
Original instructions at `0x80143d18` add $fp to the event block for fetch, and
`0x80145f30..38` advance it by opcode length + 1. The scheduler stores $fp at
context + `0x40`. Other saved registers hold eager operands and loop scratch;
they are not alternative instruction pointers.

The earliest owner state, source frame **8769**, is inside `Wait(25)` at block
offset **731**. It is suspended at `thread_frames_wait` return `0x8014c87c`, on
yield **24 of 25**. The original helper increments its loop ordinal in the
yield call's delay slot, so this value is the active yield ordinal, not a count
of completed resumes. Its 40-byte stack frame stores the interpreter return
`0x80145a34` at SP + `0x20`; unwinding it reaches the event-thread caller
`0x80143b98`. The overwritten REQUIRE overlay in this early state is therefore
not grounds to reject the independently verified resident interpreter.

At the retained 300-frontend-frame continuation, source frame **8959**, the
interpreter waits at command **801** for task **54**. At the allocation yield,
source frame **8965**, it has reached command **807**, waiting for task **56**.
The first pre-reveal state at frame **8979**, reveal count-5 state at **8984**,
hold count-1 state at **9000**, and separate hide-input pair at **6435/6436**
remain at that same wait. Those frame counters belong to separate capture
lineages; they are not one chronological sequence or frontend-to-source scale.

The event's local command sequence is:

1. Offset 801: wait for task 54.
2. Offset 804: DisplayConditions, condition **8**, hold **60**.
3. Offset 807: wait for task **56**.
4. Offset 810: DisplayConditions, condition **0**, hold **60**.
5. Offset 813: wait for task **56**.

This explains why later samples can reuse the original banner slot for a
different condition. At the six selected states suspended at offset 807, slot 3
is running task 56 with parameters 8/60, matching the immediately preceding
command. The original opcode-78 handler starts `script_conditions_display` in
the first idle slot and transfers those two operands into the thread parameters;
the wrapper enters REQUIRE's dispatcher. This is source-bound saved-state
attribution, not an instruction-by-instruction execution recording.

## Scenario path and remaining gap

All selected states have `g_scenario_event_active == 1`, battle-not-started **1**,
and debug-battle-style **0**. The latter two IDs, `0x1fd` and `0x1fe`, are packed
bits: their word is `variables[0x80 + (id - 0x80) / 32]`, with bit `id & 31`.
Reading them as `variables[id]` would inspect unrelated storage. An authored
regression deliberately places a misleading value at that incorrect uniform-word
location.

Pinned `src/lib/menu/command_prompts.c` gives active scenario conditions precedence
over the common event-401 fallback. These observed gates and event identity are
consistent with the scenario path. They do not show the earlier condition scan,
the caller that selected event 4 or a complete event-entry transition. No encounter
name or universal statement about all battle starts is inferred.

The [event-401 source analysis](../architecture/zod-066-battle-start-event.md)
continues to describe a distinct conditional fallback. Its original activation
remains unobserved. Original opcode-78 admission, complete event playback, camera
workers, mutable operands and EventEnd lifecycle remain separate runtime work.

## Reproduce and validation

```powershell
python tools/verification/inspect_banner_event.py --plan scratch/banner-event-identity-plan/plan.json --output scratch/banner-event-identity-new
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_baseline_coverage.py --reference-root C:/repositories/fft_decomp
```

Use a fresh output directory. The retained `banner-event-identity-a` and `-b`
reports match byte-for-byte. The verifier reads only frozen local files; original
states/code/archive bytes remain ignored. Eleven authored Python tests cover
current/cache/payload agreement, missing or ambiguous matches, processed marker,
pointer/text relocation, command boundaries, opcode/task/wait/stack/caller guards,
adjacent worker arguments, packed flags, malformed raw framing, frame-wait ordinal
boundaries and fingerprint/output rejection. All **163 Python tests** pass.

No managed code, schema, package graph or runtime behavior changed. The last
full .NET validation remains **1,494 passing tests** from the Circle milestone;
the .NET suite was not rerun for this offline analysis. No automatic commit.

The next implementation decision is a separately traced, bounded opcode-78 scene
admission using the existing preloaded banner driver/readiness contract. Keep
original loading duration and complete event 4/401 playback outside that admission
until their additional capabilities are independently implemented. A verified
event-401 entry capture remains useful evidence, but is not a prerequisite for
preserving the effect results now attributed to event 4.
