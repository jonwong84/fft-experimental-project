# Complete three-entry turn ownership — ZOD-247

Source/design study, 2026-11-04, managed base `68b108a`. Read-only fft_decomp
HEAD was freshly checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/three-entry-turn-source-v1.json) pins the
selected command, continuation, clock and PA functions. The
[post-ACT trace](zod-243-three-entry-post-act.md) and
[complete pair adapter](../behavior/attack-throw-stone-ai-turn-execution.md)
remain the detailed owners of their established boundaries. No C/original
program or presentation host was executed.

The selected ZOD-248 child is implemented in the
[complete-turn behavior contract](../behavior/basic-skill-ai-turn-execution.md).

## Source flow

`turn.c::ai_command_build` saves Move/Act flags after dispatch. A completed
Move changes the movement flag while Act remains unused, so the next call
retains the selected action. After actual ACT changes its flag, a new simulation,
refresh, ability preparation and movement selection are required. Unchanged
saved choices instead trigger the earlier no-progress END guard. The managed
orchestrator must not rerun that guard against a just-created fresh decision.

`acting_unit_init` reads current position, Move and turn delay on re-entry.
The cleared post-ACT list and actual PA/HP/displacement feed fresh preparation.
`scenarios_build` compares source target distance with the prepared target reach
before the out-of-reach NonBlue modulo7 draw; Blue skips that draw. Thus one
owned post-ACT preparation can derive reach via the existing pure geometry and
continue exactly one existing in/out-of-reach branch. Repeated preparation would
add a second hypothetical refresh draw and change subsequent selection.

`controller.c::battle_state_ai_command_update` delegates actual Move/ACT/END.
`battle_state_after_command_update` waits for display completion, then
`battle_state_continue_turn_update` chooses remaining action menus or turn-end
Wait/facing from `clock.c::turn_is_over`. Its camera/display gates are outside
deterministic headless simulation. Actual choice changes, `turn_end` refunds/cap
and explicit facing settlement remain the existing managed rules' responsibility.
The driver stops when this actor is settled, before next-turn clock advancement.

Accumulate's actual PA change is already final before post-ACT preparation.
Formula0x36 and stat application retain PA99 cap and zero actual draws. The
driver must neither repeat that ACT on retry nor install its old predicted D1.

## Comparison with the pair driver

`AttackThrowStoneAiTurnExecution` composes fresh commands, the opaque retained
Move token and the actual pending facing receipt. It publishes every accepted
step and stops immediately on rejection. Its recovery cursor freezes inputs,
the exact partial session, next stage and any existing Framework token. Resume
returns only newly published events/decisions and cannot bind an equivalent
replay or foreign session. These are orchestration semantics, not additional
Framework authority or a persisted save checkpoint.

The three-entry fresh command and post-ACT command APIs already implement the
corresponding real transitions. Keep them as owners. Add automatic post-ACT
selection/execution by extending their private cores with derived reach; preserve
the public explicit-branch APIs, result types and streams. Do not invent a pair
list certificate or introduce a second general dispatcher.

## Selected child — ZOD-248

Complete supplied-scenario three-entry headless turns with these boundaries:

- One immutable turn configuration owns the existing pre-ACT profile, selection
  assertions and post-ACT scenario assertions. Derive post-ACT configuration
  from that same preparation owner; do not duplicate mutable grants/profiles.
- Start with `BasicSkillAiCommandExecution`. Move-first follows its retained
  Attack/Stone/Wait token and any actual completion receipt without fresh search.
  ACT-first follows the actual result with one automatically routed post-ACT
  preparation/selection/Move-or-END transaction. Same-tile empty END stops directly.
- A finite stage loop consumes a choice, ends the turn or settles facing on
  each success. Stop on rejection without a speculative retry or whole-turn
  rollback. Ordered output includes only published events and successful fresh
  decision diagnostics; settlement adds no extra clock/refund/RNG.
- An opaque recovery cursor binds the exact returned partial session, frozen
  inputs, next stage and existing token. Resume repeats only the failed stage.
  Foreign/replay states reject with no effects and no new recovery cursor.
- Preserve ACT-first terminal rejection after actual lethal damage and
  Move-first NoLivingEnemy facing rejection after the actual retained ACT,
  including its pending receipt. No living target, final facing or battle
  outcome is invented. A late post-ACT settlement rejection keeps the earlier
  PA boost/damage and its RNG checkpoint. Unchanged exhausted budgets stay failed.
- Check literal complete self/Attack/Stone/empty streams, both admitted teams,
  identities/order, exact budgets, failed-first/retained/post-ACT/facing stages,
  recovery event isolation and exact binding. Existing pair and explicit APIs
  remain unchanged. No new Framework implementation, production friend access,
  session/receipt constructors or frozen self-Move token is needed.

Pre-ACT S/D0/D1 known-score provenance remains supplied. Broader original
policies/rosters/maps, terminal encounter routing, host/editor activation,
replacement contracts, battle saves and original frame/VSync RNG remain separate.
ZOD-106 stays deferred; PA is battle-only. Preserved definition drafts including
AbilityToggles remain untouched.
