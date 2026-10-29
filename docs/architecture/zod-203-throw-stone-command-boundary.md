# Authoritative Throw Stone command handoff — ZOD-203

Investigated 2026-10-29 at clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, managed base `4c42756`.
The [receipt](../evidence/ai/throw-stone-command-contract-v1.json) pins ten C files /
21 functions, inherits pair/preparation/scan evidence, and records fourteen managed
study-file snapshots. Managed hashes describe this study, not current-code tests.
Eight [supplied checkpoint descriptions](../fixtures/behavior/ai-throw-stone-command-contract-v1.json)
cover closed actual-resolution arithmetic and proposed atomic draw exhaustion.
They execute neither C nor C# commands, fresh AI, planning transactions or receipts.

## Source selection and dispatch

`ai_command_build` checks its no-progress guard before reselection: an already
decided action with unchanged saved Move/ACT flags returns END. Otherwise an
undecided action or ACT already used enters simulation, refresh/preparation,
reachability and selection. `ai_simulation_end` restores the saved attacker block
and returns `g_action_state` to EXECUTE; it does not rewind the shared RNG.

For a fresh selection, unused Move plus (ACT used or Move-first) issues MOVE when
the destination differs. Otherwise ACT used or no selected skillset issues END;
the remaining branch copies the selected action into ACT. Saved choices and
command history update on normal dispatch. Move-first therefore retains its
selected action across ordinary Move; ACT-first requires fresh post-ACT selection
on re-entry. The pre-ACT predicted D1 is not authority to move after a real hit.

`battle_state_ai_command_update` prepares a ready ACT with
`action_prepare_in_place`, dispatches Move through actual movement, and routes
ordinary Basic Skill through target selection and the common action path. It
also contains original presentation waits/VSync behavior. Those frame effects
are not a reason to add managed rendering/pauses to gameplay RNG. Whole-frame
retail stream equivalence remains unresolved.

## Prediction differs from execution

`formula_damage_random_y_times_pa` routes formula0x37 through physical evade,
effective PA/modifiers and `formula_apply_damage_and_knockback`. For the current
ordinary, neutral, zero-equipment-evade profile:

- Evasion calls still consume accessory, right-hand, left-hand and class rolls.
  `evade_source_roll` does not turn an AI evade into an actual miss. AI projection
  carries calculated accuracy separately and uses midpoint0x4000 for damage,
  giving 2×PA when accuracy is nonzero. Its hypothetical clock adds its own draws.
- Actual execution checks the same four sources; front class evasion can miss.
  A miss consumes ACT and four resolution draws, then skips damage/push draws.
- A hit takes a fifth draw for `(floor(raw×2/32768)+1)×PA`, a sixth for
  `floor(rawAttacker×attackerBrave/32768)`, and an independent seventh through
  `random_roll_pass_fail` for the target's Brave-scaled comparison. Equality fails
  the push test. There is no weapon critical/proc draw in this formula.
- Seven hit draws remain consumed even when lethal damage cancels the push or
  an off-map destination prevents displacement. The flat push is one tile along
  the dominant axis, Y on an exact tie. Preserve the victim's voluntary choices,
  CT and facing. Broader obstacles/falls/interception remain outside this profile.

These are resolution costs, not whole-turn schedules. Planning draws must already
be retained; command application adds no hypothetical upkeep/clock advance.
Original ACT marking ends the turn only when no choice remains. The managed
`TargetedAbilityBattleSimulator.Apply` already implements the bounded real action,
including grants/range validation and settled damage/displacement, independently
of the AI projection. [Original action trace](zod-108-throw-stone-action.md).

## Existing boundary and chosen design

`FacingDecisionBattle.Execute` invokes one fresh synchronous planner, closes its
context, verifies proposal ownership and RNG forward continuity, stages RNG into
both battle/turn snapshots, then applies a Framework-owned command. It accepts
Attack/Wait/turn-ending Move today. `FacingMoveDecisionBattle` additionally owns
confirmed Move with a frozen Attack/Wait continuation bound to the exact returned
state/battle, actor, revision and lineage. Neither boundary currently accepts a
targeted ability. `FacingTurnBattle` also lacks a targeted-ability delegate.

Extend these existing typed boundaries incrementally. Keep Framework independent
of AI; add no production friend access or public constructors for sessions,
proposals or receipts. A diagnostic plan is never executable authority. Shared
ordinary geometry does not turn Throw Stone into a sword action: its command and
immutable `TargetedAbilityBattleProfile` retain distinct ability/grant admission.

The immediate ACT path should work as follows:

1. Framework checks active actor, pending facing and revision before invoking the
   planner. The planner can issue a context-bound `PrepareTargetedAbility`
   proposal with `UseTargetedAbilityCommand`, immutable profile, progressed RNG
   and diagnostic. Closed/foreign/cached proposals and actor mismatch reject.
2. Validate forward RNG continuity with the existing affine check, not a replay
   loop or a claim that diagnostics prove policy correctness. Preserve original
   positions/HP/CT/ticks/choices until actual application; only decision RNG is
   adopted. A hypothetical refresh's units must never become the live snapshot.
3. Delegate through a new `FacingTurnBattle.UseTargetedAbility` entry to existing
   `TargetedAbilityBattleSimulator.Apply`. Recheck current command/grants/range;
   never copy predicted damage, status or displacement into the result.
4. One successful ACT adds one revision. Adoption adds none. If Move was unused,
   ACT leaves the actor active with unused Move and no completion receipt. If
   Move was already used, mint a receipt only after the complete actual result,
   binding final HP/KO/knockback, RNG, lineage and revision. Existing explicit
   `FinishFacing` adds its own revision and no extra refund/draw/tick.
5. Reject invalid commands or late RNG exhaustion atomically: identical incoming
   session, no events or success diagnostics, and neither snapshot adopts RNG.
   Check actual draw costs, not a blanket reservation of seven draws. A miss can
   succeed with exactly four left. Revision MAX rejects before invoking planning;
   a command at MAX-1 may succeed while later explicit facing at MAX rejects.

For **Move-first**, a later child should extend the existing Move proposal/token
with a frozen typed targeted command/profile. Adopt fresh selection RNG once,
publish actual confirmed path/final heading, then use current post-Move RNG when
continuing. No second selection, damage preview, replacement payload or RNG
adoption is accepted by continuation. Validate the exact returned session/token.
Follow-up failure preserves the already-published Move and permits deterministic
retry; it does not rewind the earlier successful command. Only completed actual
ACT can issue a final facing receipt. This is per-command atomicity.

For **ACT-first**, later AI orchestration owns fresh in-reach selection and actual
ACT, then re-evaluates unused movement from the real post-ACT state. Real miss,
Critical, push or KO can invalidate prior focus, paths or projected D1. The current
Attack-only post-ACT list certificate cannot truthfully stand in for a pair list.
Select a separately traced pair/general post-ACT child. Unsupported terminal/KO
policy must remain explicit; do not invent a living enemy for deferred facing.

MoveOnly retains existing confirmed-Move/Wait handling. Unknown cast scores within
range4 still reject before authority; no zero guess, actor rescore, caller-supplied
prior memory or cached selection is introduced by any execution adapter.

## Selected child — ZOD-204

Implement the **Framework-only targeted-ability ACT transaction** in
`FacingDecision` / `FacingDecisionBattle` and the `FacingTurnBattle` delegate.
Reuse existing immutable command/profile, ordinary session and receipt lifecycle;
do not generalize to arbitrary actions or change existing Attack/Wait/Move APIs.

Use a few meaningful Framework scenarios for live seven-draw hit/push versus
four-draw miss, current PA, KO and blocked push draw retention, both-unused versus
already-moved completion, post-resolution receipt binding/facing, expired/foreign
proposals, wrong actor, invalid RNG/ability/grant/range, exact/short actual budgets,
late rollback and revision boundaries. Prove both RNG copies stay synchronized
and original inputs remain immutable. Do not add AI adapters, Move continuation,
post-ACT pair reselection, generic interfaces or host/editor activation in ZOD-204.
Those follow only after this reusable authority primitive is validated.

## Literal witnesses and validation

The ZOD-202 Move1/distance5 normal pair ends selection at `(3210001534,16)`.
Supplying that checkpoint to actual Throw Stone from (2,1) toward (6,1), PA5 and
Brave70/70 gives evade raws 4086/2749/12767/9084, damage raw 12060 → **5** (AI
projected 10), then Brave raws 32225/17543 → thresholds 68/37 and a push to (7,1).
Final RNG is `(3297241969,23)`; actual command application adds seven draws.
This is a supplied-checkpoint description, not a completed AI execution adapter.

Supplied front evade100 instead misses after four draws at `(595337866,20)`;
that changed-stat witness does not claim the normal pair would select it. Lethal
HP5 and off-map push retain all seven draws with no displacement. The diagonal
witness pushes along Y; unequal Brave shows the independent scaled comparison.
An exact seven-draw budget fits; one short returns no partial result under the
proposed managed transaction policy. No receipt/proposal behavior is simulated
by these arithmetic witnesses; those acceptance scenarios await ZOD-204.

`verify_ai_throw_stone_command_contract.py --reference C:/repositories/fft_decomp
--prepared-executable scratch/all-data-extraction/SCUS_942.21 --output <new report>`
checks pinned/inherited source and prepared facts plus eight authored descriptions.
Three focused scenarios, all **532 Python tests** and the pinned source/prepared
audit pass. Runtime source/tests are unchanged; last
runtime validation remains ZOD-202's clean Release build and 4,054 passing .NET
cases. Source/full Python reports are in ignored `scratch/ai-throw-stone-command-203/`.
No original/C execution or UI review; no full decision/retail-frame equivalence claim.
