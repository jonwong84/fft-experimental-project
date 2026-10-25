# Bounded ordinary CT implementation (ZOD-020)

Date: 2026-10-22. The FFT-owned turn cycle implements the selected arithmetic and ordering from [the ZOD-019 trace](ct-turn-cycle.md). The read-only decomp HEAD was rechecked for this implementation: **`611b504d25d4a9b03fab8023d7dbea75fe381d5d`**. Relevant source symbols are `src/battle/turn_order.c::turn_run_clock`, `turn_set_move_act_taken`, `turn_should_end`, `turn_end` and `status_build_upkeep_action`, with byte widths and constants in `include/fft/unit.h` and `include/fft/turn.h`.

This implements a bounded **source-derived** scheduler expectation, not all FFT turns or an executed original-game comparison. The BIOS draw arithmetic has separate executable-reference evidence; its invocation at readiness is supported by the static scheduler trace. No BIN, BIOS, native reference code, renderer, authoring program or filesystem access enters simulation.

## State and domain

`TurnUnitState` is an immutable per-unit value containing runtime ID, effective Speed, CT, explicit tie key, `HasTurn`, `MovementTaken` and `ActionTaken`. Effective Speed is 1..255 and ordinary CT is 0..254. Zero Speed, Quick CT255, negative/duplicate tie keys and default IDs are rejected at construction. Tie keys are supplied compatibility ordering inputs, not derived from stable string IDs or dictionary enumeration.

`TurnCycleState` snapshots a nonempty sequence of units, full explicit `FftRandomState`, a checked unsigned 64-bit clocktick count and one phase:

- `AddCt`: the initial phase; add CT before selecting.
- `PickUnit`: recheck already-ready peers before another tick.
- `UnitTurn`: exactly one unit is active; no scheduler clock advancement is allowed until completion.

Units have unique runtime IDs and tie keys. Non-active phases permit no active unit. An active unit already has CT below 100, and cannot retain both consumed choices because the second marker completes atomically. Finished units retain their Move/Act flags until their next readiness resets them. No fixed original 21-unit table size is imposed on the managed collection.

The model represents only present, living, unmounted, unrestricted units with no status/charge/scenario effects and Brave >= 10, on terrain that adds no turn effects. It contains no status system that could pretend to validate all those conditions. Composition into a real battle must establish the same supported-domain assumptions; unsupported mechanics remain excluded.

## Advancing and consuming confirmed choices

`TurnCycle.Advance` accepts an inactive cycle and returns the next ready unit:

1. In AddCt, increase the explicit tick count and every unit's CT by its positive Speed, with integer addition before `min(254, sum)`. Emit a `TurnClockAdvancedEvent`.
2. Choose the largest CT >= 100; break equal CT by the smallest explicit tie key, independently of input list order.
3. If none is ready, repeat with another AddCt. With positive speeds, a ready unit must occur within 100 ticks. No source UI frame loop or original 1201-switch-iteration guard is copied.
4. Reduce the selected unit's CT with `% 100`, consume exactly one FFT BIOS RNG output for source upkeep, clear its Move/Act flags and activate it. Return UnitTurn state and a `TurnReadyEvent` carrying unit ID, CT before/after selection and the upkeep roll. The updated full RNG word/count lives in state.

The selected healthy units have no visible upkeep result, but the source's `rand()` is unconditional. Only a **new** readiness selection consumes this draw. `Advance` while a turn is active is rejected as `TurnAlreadyActive` without events or draws. This is an explicit managed command distinction: the original RUN interface can re-notify the same active unit, whereas this API names advancement rather than UI notification. A client reads the active-unit snapshot rather than rescheduling it.

`TurnCycle.Apply` accepts a `TurnCommand` identifying a runtime unit and one choice:

- `ConfirmMove`: consume the active unit's unused Move. It represents confirmed movement consumption; it neither checks a destination nor moves a map unit.
- `ConfirmAct`: consume the active unit's unused Act. It is a scheduler marker only; it resolves no targeting, accuracy, damage, statuses or ability costs. The source marks a confirmed ability before its preparation/commit stage, so this must not be described as a damage-success event.
- `Wait`: confirm ending the active unit's turn with its current consumed-choice flags.

The second consumed choice automatically completes the turn, in either Move/Act order. Completion computes `min(60, currentCt + 20 for unused Move + 20 for unused Act)`, clears `HasTurn` and returns to PickUnit. Other unit CT, tick count and RNG state are unchanged. A previously ready peer can therefore act immediately with no extra speed tick. The next readiness clears the selected unit's flags so subsequent turns can use Move/Act again.

Unknown units, inactive units, repeated consumed choices and unsupported command-kind values are rejected before any state/event/RNG change. Calling Wait after automatic completion rejects as inactive; it cannot apply a second refund. This preserves the source completion helper's no-double-refund effect while giving the managed command a useful rejection result instead of returning an ambiguous success code.

## Results, ordering and numeric exhaustion

Both transitions return `TurnCycleResult` with accepted/rejection status, immutable state and an immutable ordered event array. Rejection returns the exact input state instance and zero events. Accepted transitions create a new state without mutating input units, collections, RNG or tick counters.

Event ordering is explicit:

- Advance emits each clocktick event in order, followed by exactly one ready event. A PickUnit peer already over threshold emits only the ready event.
- A single confirmed Move/Act emits one `TurnChoiceConsumedEvent`.
- The second confirmed choice emits choice-consumed followed by `TurnEndedEvent`.
- Wait emits only turn-ended. No hidden automatic Advance occurs after completion.

The fixed BIOS generator wraps its 32-bit word as verified, but the **observational** 64-bit draw and tick counts do not wrap. An exhausted draw count rejects advancement before a draw. If advancement requires a tick beyond `ulong.MaxValue`, it rejects with the original state and no partial tick events, even if local speculative ticks had already been calculated. A ready PickUnit peer can still be selected when the tick count is maximal because no new tick is needed. These rejection/rollback rules are managed snapshot/resource semantics, not a claim that the original game had 64-bit counters.

## Movement composition and ownership

The cycle stays in `Zodiac.Framework`; there is no new generic engine scheduler abstraction. The numeric CT threshold/caps/refunds, slot-derived tie order, choice markers and upkeep draw are FFT rules. It reuses only the existing explicit RNG facilities across the FFT-to-Zodiac boundary.

The [movement composition](../../src/Zodiac.Framework/TurnBattleState.cs) adds `TurnBattleState(BattleState, TurnCycleState)` and `TurnBattleSimulator.Advance`, `.Move`, `.ConfirmAct` and `.Wait`, while leaving the existing movement-only API and fixture-v1 contract unchanged. The constructor requires exactly matching unit IDs, an equal explicit nonnull RNG snapshot, and agreement between battle `HasMoved` and cycle `MovementTaken` for every unit.

`Move` stages the active-unit confirmed choice and the selected existing movement-policy transition as pure local results. It publishes neither unless both accept. This lets validation reject an inactive/repeated mover before doing range work, without spending a choice when the destination fails. Ordered wrapper events are `TurnBattleMovementEvent` first, followed by `TurnBattleClockEvent` for choice-consumed and, if the action had already been used, turn-ended. Advance/ConfirmAct/Wait wrap their scheduler events in order. `ConfirmAct` is explicitly a synthetic flag command with no attack or damage.

After acceptance, synchronization reconstructs the battle snapshot with the cycle's RNG state and each cycle unit's `MovementTaken`, preserving map, positions and immutable movement profiles. A new readiness clears the selected unit's battle `HasMoved`, permitting movement on its subsequent turn. Other completed units retain their flags until selected again. Rejected movement/cycle commands return the original composed state instance, no events, the appropriate turn or movement rejection, and unchanged RNG. No failed local result is published. No automatic next-unit selection occurs after Wait; the caller explicitly advances.

No movement fixture v1 record is silently upgraded to a turn fixture: its current explicit-null RNG/command schema remains separate. The new turn replay below is an executable C# scenario with its own explicit state/event assertions.

## Regression evidence

[TurnCycleTests](../../tests/Zodiac.Framework.Tests/TurnCycleTests.cs) covers source-derived tick, threshold, saturation, modulo and completion boundaries, including:

- CT 89 / Speed 10 needing two ticks, CT 90/91 reaching 100/101 in one, and CT 254 + Speed 255 saturating 254 rather than wrapping as a byte.
- PickUnit CT 199→99, CT 200→0 and CT 254→54; unequal CT taking priority over tie key and equal CT using the explicit key despite reversed input order/string identities.
- Refund boundaries around 59/60, both-used high remainder clamping to 60, both choice orders auto-ending, preserved finished flags and rejected repeated completion.
- Inactive/unknown/repeated/unsupported commands, active Advance, empty/invalid domain, duplicate identity/tie key, constructor collection immutability and atomic counter exhaustion.

The full two-unit replay runs the [ZOD-019 sequence](ct-turn-cycle.md): A ready at tick 10, B ready during that same tick, A at 16, B at 18 and A at 26. It checks all 12 intermediate CT pairs, repeats from the identical input and compares every unit field, phase, active ID, ticks, RNG state and ordered event sequence. Wait-only, Move+Wait, Move+Act and Act+Wait all occur. Tick events total 26; new-readiness events total 5.

The replay's five upkeep outputs are the first five committed **seed 1 executable-reference BIOS vectors**: 16838, 5758, 10113, 17515, 31051; final full RNG state is 4182499122 with draw count 5. Expected RNG values come from [the committed BIOS vector metadata](../fixtures/rng/scph5501-rng-vectors.json), not a second copy of the generator formula inside the test. Turn ordering/CT expectations remain source-derived, not independently executed original turn behavior.

[TurnBattleSimulatorTests](../../tests/Zodiac.Framework.Tests/TurnBattleSimulatorTests.cs) separately covers the movement wrapper's active-unit gating, same-tick peer selection, reactivated movement availability, action-first/move-second completion, failed-move rollback, constructor consistency and deterministic composed replay.

Coordinator validation on 2026-10-22: Windows locked restore, Release build with zero warnings/errors and all 464 solution tests passed. This implementation introduces no status, charge, zero-speed fallback, AT List prediction, combat effect, UI timing, capture or imported media support. Source trace uncertainties and remaining original-game comparison needs remain those documented in ZOD-019.
