# ZOD-154 — Decision RNG ownership at ordinary AI execution

Source/design, 2026-10-28. Managed base
**b3894f0e5cfe150a1bf0d4bab6118074f7b8e3b5**; read-only clean fft_decomp
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[receipt](../evidence/ai/execution-rng-contract-v1.json) extends the selection and
Wait receipts. No production execution API, native activation or original/C
execution is added. ZOD-155 is the bounded implementation child below.

## Source stream and restoration

`ai_command_build` enters `ai_simulation_begin`, performs refresh, movement and
selection, marks selected.rank.decided=0xff, then calls `ai_simulation_end`
before dispatching one command. Begin/end change action mode and save/restore
the Mime-attacker block. Neither saves or reseeds RNG.

`ai_action_simulate_and_score` saves selected unit fields, simulates a candidate
and turn-order events, restores between-turn globals and unit fields, then
returns its score. `simulation_unit_state_save/restore` cover the 64-byte compact
unit snapshot, action records and team Golem; no RNG word is included. Discarding
a candidate's HP, position or CT changes therefore retains its random draws.
This includes unsuccessful candidates, ordinary readiness upkeep and lottery
slots whose hit percentage is zero. The winning score is not a combat result.

For the closed sword profile, each projected Attack draws four evades even when
evade is zero: `evade_source_roll` calls `random_roll_pass_fail` before checking
action mode. The latter directly calls BIOS rand. In contrast,
`formula_get_random` returns midpoint0x4000 in AI mode, so critical/bonus draws
are absent; the conditional proc also short-circuits. Execution uses the retained
stream for fresh evade, critical/bonus and proc rolls. A miss stops the later
rolls. Do not apply projected damage or restart RNG at the winning candidate.

These behaviors use source u8 choice/decided fields, signed32 rand return and
scaling/modulo arithmetic. The verified BIOS state/update is UInt32; the managed
UInt64 draw counter is diagnostic, not a source field. Preserve the existing
[BIOS evidence](../behavior/bios-rng-verification.md) and checked counter policy.
No new RNG algorithm or seed is selected.

## Entry point and presentation limits

The existing derived selector starts at the certified **general-selection
scenario prefix**, not the start of `ai_command_build`. Earlier
`decision_flags_refresh` itself calls the action/turn simulation; other setup,
ability policies and special selection may also draw. There is no evidence that
this prefix costs zero draws in ordinary play. The new boundary must explicitly
require **GeneralSelectionEntryContext**, default false: refreshed/unrepresented
facts and all prior logical RNG work are already reflected in the input session.
This certifies the selected scope, not independent proof of a retail stream
position. Earlier prefix computation and full command-build equivalence remain
open. Do not subtract, reset or invent a fixed prefix cost.

`rand` is a BIOS veneer shared by these callers. `boot_startup_run` installs
`system_vsync_callback`; that callback calls rand once per VSync. The controller
also waits before command building, and search can yield across frames. Original
animation/effect consumers add other possible interleavings. Equal initial seed
and equivalent decisions therefore do not establish whole-game stream equality.

For this managed slice, decision and combat draws share one explicit gameplay
stream. Rendering, wall time and presentation pauses add **no** gameplay draws.
This is a deliberate isolation boundary, with exact frame-interleaved retail
RNG still unresolved. Do not implement the callback by drawing per managed
rendered frame. A future fidelity mode would need traced logical scheduling or
recorded external draws/incoming state; it remains separately scoped. Pure-query
previews continue to return hypothetical RNG without changing the live stream.

## Source command lifecycle

Before a new decision, `ai_command_build` ends the turn if its previously decided
command changed neither observed Move nor ACT flag. Otherwise it decides afresh
when undecided **or ACT is now taken**. An un-interrupted Move before ACT can reuse
the selected action. An ACT before Move can trigger a new search from actual
settled HP/positions/RNG; the earlier predicted destination is not authority.
Interrupted movement can reset selection too. Do not implement every plan as an
unconditional pair of commands or repeat its decision draws after an ordinary
Move. General continuation/interruption handling needs its own child.

After selection, an unused Move is issued only when the source order permits it
and destination differs from the current tile. Otherwise an already-used ACT or
zero skillset emits EndTurn; a real skillset emits ACT. The controller executes
that command, then ContinueTurn routes an ended unit to Wait facing. Ordinary
EndTurn changes runtime flags but uses the actual stat choice flags for refunds.
Reuse [ZOD-153](../behavior/sword-ai-wait-settlement.md): active Wait ends/refunds
once; post-Attack receipt completion only publishes facing.

## Smallest implementation: ZOD-155

Add one opt-in **already-moved, unused-ACT** command transaction on a real
`FacingTurnState`. Take current state, bounded weapon, existing semantic
candidate/score/comparison/scenario/derived/selection facts, plus false-default
GeneralSelectionEntryContext and the ordinary Wait/end facts. Accept no external
decision RNG, selected plan, projected result, cache, receipt to mint or raw
snapshot to attach. Keep the existing pure APIs unchanged.

1. Validate active actor, no pending facing, revision room, the already-moved/
   unused-ACT restriction and all source context. All existing geometry/weapon/
   first-terminal-Attack/no-Defend limits still apply. Use the session's current
   RNG, with both snapshot copies agreeing, as the only decision entry state.
2. Privately run the existing in-reach derived selection **once**. Already-moved
   geometry has only the actor as an origin; selected destination must remain
   that tile. No movement command or trusted continuation token is needed.
3. Adopt only that invocation's final RNG into both Battle and Turns snapshots
   inside the private transaction. Preserve every other field, profile, lineage
   and revision. Do not publish an RNG-only session or add a public RNG setter.
4. For selected Attack, construct a fresh actual Attack command and delegate to
   `FacingTurnBattle.Attack`. Read its real hit/damage/KO/knockback and RNG. Both
   choices now being spent, return its fully settled pending completion receipt.
   Do not rescore, copy projected HP, call Advance, or perform another decision.
5. For MoveOnly fallback, destination is unchanged and no ACT payload is active:
   call `SwordAiWaitSettlement.Wait` with the privately adopted state. It applies
   the unused ACT refund and facing once, adding zero draws/ticks.

Successful publication contains the fresh decision diagnostics, actual command,
existing transition/events and final session. Exactly one existing command
increments revision, whether Attack or Wait. Decision/adoption adds no revision
or clock event. An Attack receipt binds the final post-decision/post-combat
snapshot and retains the original lineage. Finish it separately through ZOD-153;
that operation increments revision once with no RNG draw or second refund.

A lethal Attack is a valid committed command. Its receipt remains pending, but
the current two-living-opponent AI facing query cannot finish it. Report that
existing limit; do not undo a successfully published Attack or invent a no-enemy
facing. A production host must handle this separately before activation.

Admission, selection or actual-command failure publishes the identical original
session, empty events, no decision/command diagnostic or completion receipt, and
original RNG. Preserve typed stage/nested reasons **without exposing a private
transition's advanced-RNG state**. This whole-command rollback is managed
transaction policy, not original C rewind. An already-published command is never
rolled back by later facing failure. Retrying an old immutable input is valid;
reusing its receipt on the returned/newer session follows existing guards.

Check actual draws, not a maximum reservation. If selection fits but actual
Attack exhausts RNG, reject the entire transaction. Wait can finish exactly at
MAX when no draws remain. Revision exhaustion rejects before decision work.

## Evidence and acceptance

The [tapes](../fixtures/behavior/ai-execution-rng-contract-v1.json) supply known
movement/projection draw costs from prior managed cases. They do not run fresh
selection, C# commands, original C or the frame loop. For the already-moved
PA5/power5/HP50 case at general entry word1/count7:

- One movement evaluation plus a five-draw Attack candidate reaches
  word2516284547/count13. Hit11 takes lottery raw23010%100=10 and ends at
  word3655513600/count14. Actual front evade89 then rolls
  7419,16212,4086,2749, **misses**, leaves HP50 and ends word180171308/count18.
  Projected normal damage25/HP25 must not become the actual result.
- Hit10 fails that equality test, visits eight lottery slots and selects the
  same Attack at word2937909499/count21. Its actual miss ends
  word3535774167/count25. Do not replay scoring or skip seven empty-slot draws.
- An absent Attack with one movement draw takes eight lottery draws to MoveOnly
  at word3210001534/count16. Wait preserves that RNG, refunds only the unused
  ACT (ordinary CT15->35), and has no pending completion.

ZOD-155 must exercise these through real sessions and fresh managed selection,
including both RNG copies, preserved decision-time HP and actual miss events.
Verify exact-MAX success and one-draw-short late-command rollback, missing context/
wrong choice phase, receipt revision/binding, both actor identities/order/replay.
Reuse existing selection and facing tests; do not duplicate their full guard
matrix. New managed cases remain **unexecuted proposals** until that ticket.

The word1/count7 entry can be reached without injecting an active snapshot:
Begin on the prior 5x3 flat setup at word3945894376/count6, both Speed5,
actor CT110 at (3,1), target CT90 at (1,1), Move1 and both facing East.
Advance once reaches word1/count7 and actor CT15/target CT95; a real Move to
(2,1) costs zero draws. The source-context certificates still apply. This is a
synthetic general-selection entry, not evidence of a retail refresh prefix.

Audit reproduction:

```powershell
python tools/verification/verify_ai_execution_contract.py --reference C:/repositories/fft_decomp --output <new scratch report>
python -m unittest discover -s tools/verification -p 'test_ai_execution_contract.py'
```

The clean pinned audit passed: six additional files, 16 functions and three
supplied-prefix tapes, with inherited receipts also verified. Report:
`scratch/ai-execution-rng-154/source.json`. All **500 Python tests** passed;
log: `scratch/ai-execution-rng-154/python.txt`. No .NET code changed, so the last
full .NET validation remains ZOD-153's 3,821 cases and was not rerun here.

Full dispatch, Move-first continuation, post-ACT reselection, refresh-prefix
porting, out-of-reach execution, general abilities/maps, frame fidelity,
replacement interfaces, host/editor activation and saveable AI tokens remain
outside this child. No new general architecture is required to validate it.
