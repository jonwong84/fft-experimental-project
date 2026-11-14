# Bounded Blue-team pair policy — ZOD-225

Source study, 2026-11-04. Read-only fft_decomp HEAD is clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`, managed base `4d86087`.
The [verified algorithm index](../evidence/ai/algorithm-guide-v1.json) pins the
full files and function bodies cited here. No original/C code was executed.

## What Blue changes

`turn.c::decision_team_targeting_flags_update` counts one eligible ally in the
closed pair, so retreat support remains unavailable and `single_enemy_remaining`
stays zero. NonBlue sets TeamSupportUnavailable on non-enemies; Blue does not.
Preserve the refreshed retained policy bits for Blue instead of forcing the
actor's bit. A pre-existing unavailable flag short-circuits this pass for either
team and remains retained. Neither route draws RNG.

`decision_target_consideration_init` first distinguishes Blue player-controlled
units: they retain their configured auto-battle setting and return before the
ordinary focus calculation. Their policy cannot be inferred from team alone.
For Blue AI-controlled units with no special behavior, the source forces Manual
autobattle, then tests `(hp << 7) / max_hp < 45` using integer arithmetic.
Below that boundary it asks for the nearest healer or crystal. At or above it,
all targets are enabled and it focuses the nearest eligible enemy. The closed
two-living-unit profile then has the same focused opponent as NonBlue.

Admit only the AI-controlled, at-or-above-45/128 branch in the first child.
Require an explicit `BlueAiControlled` source assertion; do not infer control
from unit IDs, currently active actor, mutable content or a cleared bit that the
managed state does not model. Below-boundary targeting, player-controlled
autobattle, extra allies/healers/crystals and configured behavior remain later work.
The existing no-special-behavior certificate still excludes stale focus modes.

`ability_considerations_set` has team-specific inventory admission, but ordinary
sword Attack and learned Throw Stone do not use those menus or consume items.
The three initialized entries (including unusable non-Frog Frog Attack), evasion
quarter-share2, two harmful/no helpful abilities, ranged summary, minimum enemy
reach Move+1, ally reach Move, random-use threshold0 and post-ACT usability clear
therefore remain unchanged. Zero MP/cache facts avoid the separate NonBlue MP
constraint test. Actual command formulas and draw ordering remain team-independent
under the same scoring/opponent/equipment/status certificates.

`scenarios_build` chooses the initial branch from full prepared reach before
scoring. Only NonBlue takes the subsequent `rand()%7` draw and possible halving.
Blue preserves full reach and consumes **no** adjustment draw; diagnostics must
represent the absence rather than inventing raw0/modulo0. Masks, original-origin
insertion, retained priorities, comparison order, D0/D1 and lottery remain shared.
In particular, an unknown range-four actor cast key is still unknown for Blue.

## Ordered implementation children

- **ZOD-226:** admit the certified Blue branch in pair preparation, preserve
  retained policies and store the admitted team in fresh diagnostics. Shared
  restored-state derivation must keep existing Sword/NonBlue defaults unchanged.
  Check policy propagation, literal refresh RNG, threshold boundaries, missing
  control certification, unknown team, action-used clearing and input isolation.
- **ZOD-227:** reuse pair movement kernels for Blue with no reach draw. Keep
  NonBlue's exact streams and nullable adjustment diagnostics. Check in/out-of-
  reach pre/post-ACT scenarios, masks/destinations, null unknown priorities and
  exact budgets where Blue fits and NonBlue needs one additional draw.
- **ZOD-228:** exercise Blue selection, command dispatch and complete headless
  turns through the existing shared owners. Preserve actual effects, exact
  continuations/receipts, per-command recovery and terminal restrictions. Check
  independent literal streams, source guards and both actor identities/replay.
  Do not create another Blue ranking/scoring/execution pipeline.

Preparation and scenario changes must land together before exposing the expanded
selection domain: admitting Blue while still taking NonBlue's adjustment draw
would be an incorrect intermediate runtime. No Blue host activation, UI timing,
new general roster/team model or broad replacement interface is selected.

## Turn-driver source boundary

`controller.c::battle_state_ai_command_update` waits for presentation frames,
calls `ai_command_build`, then dispatches movement, actual ACT or END through
normal game handlers. It does not commit predicted simulation snapshots. Its
runtime display flags on END differ from unit-stat choice flags used by
`clock.c::turn_end` for refunds; managed live choices must retain their meanings.
The ZOD-224 headless composition adds no frame delay, VSync draw or next-turn
clock. Terminal routing is still unsupported, so a lethal published ACT remains
visible even if later movement/facing cannot proceed.

Pinned paths: [turn.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/turn.c),
[abilities.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/abilities.c),
[evaluation.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/evaluation.c),
[controller.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/controller.c).
The source-guide audit passed: 105 core functions, eight external boundaries and
11 direct core RNG sites. Report: ignored `scratch/ai-turn-blue-224-228/source-guide.json`.
