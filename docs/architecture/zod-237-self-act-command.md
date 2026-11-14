# Authoritative Accumulate self-ACT handoff — ZOD-237

Source study, 2026-11-04, managed base `e51c1fa`. Read-only fft_decomp HEAD was
rechecked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/self-act-source-v1.json) pins the functions
below; the earlier [actual Accumulate trace](zod-104-accumulate-action.md) and
[AI trace](zod-229-accumulate-ai.md) own the verified retail records and effect.
No C or original program was executed. This study does not activate a host.

The selected ZOD-238/239 children are implemented; their
[current behavior and validation](../behavior/immediate-self-act.md) preserve
the ACT-first and authority boundaries specified below.

## Selection to actual action

`turn.c::ai_command_build` refreshes/selects when undecided or Act was used,
then `ai_simulation_end` restores the attacker block and EXECUTE mode, retaining
RNG. Its no-progress guard precedes reselection. With both choices unused, an
ACT-first nonempty action skips Move even when predicted D1 differs and dispatches
the selected ACT. Move-first instead moves before retaining its ACT; an already
used Act requires fresh post-ACT selection. These entry facts remain explicit
certificates; an arbitrary public plan is not evidence that source dispatch ran.

`controller.c::battle_state_ai_command_update` overwrites the ACT actor with the
actual current unit and prepares in place. Basic Skill uses ordinary targeting.
`targets.c::target_range_set_for_action` returns NoTargeting for Accumulate's
Auto/range0/non-WeaponRange record; `target_tile_select` replaces its target
with the actor's own tile. The AI map-target heuristic initially uses a self-unit
target, but execution normalizes it through this auto-target path. A managed
self command therefore carries actor/ability IDs and no hostile target parameter.

`action_execute_ability` marks Act via `turn_act_taken_set` before preparation.
`action_commit_preparation` clears the immediate-action Charging/Jump/Defending/
Performing state. Those effects, mounts, mimics, pending procs and other broader
status/terrain/reaction work are excluded from the existing immediate profile.
No new restriction/status implementation is selected here.

Formula0x36 writes the unsigned-byte change `Y | 0x80`; Y1 means increase1.
`stat_delta_apply` computes in signed32 from byte base PA, masks seven-bit amount,
clamps base to 1..99 before storage, and recomputes effective PA from base plus
equipment bonus only if base changed. The managed zero-bonus profile uses the
existing shared `AccumulateFormula`: 5→6, 98→99, 99→99. An at-cap use still consumes
Act and resolves successfully, with no PA-changed event.

The record starts at hit1/accuracy100, has one strike and no evade/critical/proc
roll. **Actual resolution adds zero RNG draws in this closed profile.** The
planning coin, hypothetical clocks and lottery remain consumed; the actual self
action must not repeat them or adopt hypothetical HP/PA/CT/positions. Original
frame/VSync interleaving is unresolved; managed display time adds no gameplay RNG.

`turn_move_act_taken_set` preserves the other choice, ending only when both are
unavailable. `turn_end` uses u16 CT arithmetic, refunds20 per unused choice and
caps at60 before byte storage. With Move unused, self ACT leaves the actor active
without a refund or next-clock advance. With Move already used, it ends the turn
without an unused-choice refund. Original control marks the turn choice before
effect playback; the existing managed simulator publishes turn events before
effect events, but must issue any completion receipt only after final PA exists.
`unit_facing_turn_to_cursor` and `unit_facing_turn_to_target` preserve facing on
zero vectors; transient animation-facing save/restore remains presentation work.
The bounded actual simulator preserves facing; Wait/receipt settlement remains
the explicit existing command operation.

## Current managed boundary and chosen changes

`ImmediateAbilityBattleSimulator.Apply` already validates active actor, unused
Act, known effect, explicit learned grant, PA1..99/zero-bonus profile and living
units. It calls ConfirmAct and publishes the actual immutable PA result/events.
It is the actual rules owner, not an AI projection. Keep it unchanged unless a
concrete validator gap is demonstrated. Boosted PA is battle state and survives
later Move/Wait/Advance and later attacks; it does not edit definitions or saves.

`FacingDecisionBattle.Execute` owns one fresh callback, closes its context,
validates proposal ownership and affine RNG forward continuity, privately stages
RNG in both snapshots, then calls a Framework command. `FacingTurnBattle` delegates
actual transitions and issues one revision plus a receipt only after complete
accepted results. Preserve these owners; add no public session/proposal/receipt
constructors and no production AI friend access.

Select two bounded implementation children for this batch:

- **ZOD-238 — Framework immediate self-ACT transaction.** Add a typed self ability
  proposal to `FacingDecisionContext`, store command/profile in its opaque
  proposal, reject wrong actor and invalid/foreign/expired proposals or RNG,
  and delegate through `FacingTurnBattle.UseImmediateAbility` to existing actual
  rules. Success adds one revision, zero actual draws and no automatic facing/
  clock. An already-moved completion receipt binds the final PA, RNG, session,
  lineage and revision; FinishFacing preserves PA. Late command rejection rolls
  back RNG adoption and publishes no events/diagnostic. Revision exhaustion and
  pending facing reject before invoking planning. A zero-draw self ACT can still
  succeed at draw-count MAX if planning/adoption already fits.
- **ZOD-239 — Fresh three-entry ACT-first executor.** Invoke supplied-scenario
  `BasicSkillAiSelection` inside that same Framework callback and retain only
  fresh decision RNG plus typed diagnostics. Require both choices unused and
  existing dispatch-context assertions. Execute selected Attack/Throw Stone
  through existing branches and Accumulate through the new typed self branch.
  Reject Move-first/empty plans atomically. Predicted D1 authorizes no post-ACT
  Move, receipt, token or automatic END. Test the literal Accumulate selection
  at word1/count7: decision word3794901700/count26, actual PA5→6, unchanged final
  RNG, actual origin (1,1), CT15 and unused Move. Include PA99, grant rejection,
  wrong/expired contexts, exact budgets, both teams/identities and real damage
  branch regressions. Public cached plans cannot enter this adapter.

Move-first self continuations need a later child with exact-state/lineage/revision
binding and per-command retry semantics, reusing the existing Move token owner.
Post-ACT three-entry movement must use actual boosted state and freshly traced
list/remaining-choice rules. Derived scenarios, complete turns, broader original
policies, rewards and presentation remain separate. ZOD-106 stays deferred;
Accumulate PA is battle-only and editor/host activation is not selected.
