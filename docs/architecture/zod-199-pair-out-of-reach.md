# Out-of-reach pair masks and retained score cells — ZOD-199

Investigated 2026-10-29 at clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base `23f326b`.
The [source receipt](../evidence/ai/pair-out-of-reach-contract-v1.json) pins
12 files/24 functions and inherits the pair/preparation/scan contracts.
Nine [isolated descriptions](../fixtures/behavior/ai-pair-out-of-reach-contract-v1.json)
use supplied reachable masks, a uniform absolute movement score, prior actor
priority and one draw per movement example. They do not execute spread, refresh,
clock, trajectories, candidates, managed AI, C or the original game. In particular,
they do not reconstruct prior memory or establish a full decision RNG schedule.

## Source order and written masks

For the focused ordinary pair, minimum enemy reach remains Move+1 despite Throw
Stone's range4. In `scenarios_build`, target distance greater than that reach
takes the out-of-reach branch. `target_distance_tiles_filter(distance-1)` copies
only closer scenario0 tiles into scenario1. If none matches, it copies all of
scenario0 instead. Call the resulting score mask S and original reachable mask M0.

`reachable_tiles_score` visits S in layer/y/x order and writes each absolute score
negated into its unsigned16 grid cell, later read as signed16 by origin ranking.
It does not clear or rewrite the cells outside S. After scoring, a NonBlue team
draws once and halves the minimum reach with integer division when raw%7==0.
Blue skips that draw. D1 uses priority/proximity ordering among S cells within
effective reach, with distance-first nearest fallback when none is in range;
complete ties favor later coordinates. Out-of-reach D0 copies D1. The in-reach
ranged `max(3,distance-1)` D0 chooser is not called here.

Source scenario2 is M0 XOR S **before** the actor tile is added. Finally,
scenario1 becomes S union {actor}. Thus the final scenario1 and scenario2 can
both contain the actor; enforcing disjointness after the OR would change source
behavior. This sequence neither scores the added tile nor draws for adding it.

## Fresh proximity, potentially retained priority

`action_select` resets action/rank defaults and calls `foe_proximity_grid_fill`
before the scenario filter. The latter writes proximity for all M0 tiles,
including the actor, from original enemy eligibility/delays and coordinates.
That actor proximity is fresh even when its priority cell is not written by S.

The traced `action_select`, `ai_workspace_init` and ordinary general/scenario
path do not blanket-clear tile priorities. The initializer resets selected
workspace fields rather than the complete block. An actor outside S therefore
retains its preexisting signed16 priority through this path. A reset best/list
does not prove a reset tile grid, and an initialized rank is not a movement score.

There is also an explicit shared-memory alias. `target/battle.yaml` places the
6600-byte AI storage at 0x8019f3c4. `ai.h` puts tile_priority at offset 0x5f4 for
1152 bytes. `script_event_frame_run` and `menu_event_frame_update` view
`g_ai_workspace` as `event_work_t`; `script.h` puts status_staging at 0x5f0 for
1010 bytes. `unit_status_staging_update` binds that view and its save/clear
functions write staging fields. The ranges overlap. Therefore a retained cell
must not be described unconditionally as a previous AI score: event/status work
can share those bytes too. Map entry and return-from-event paths call the same
partial `ai_workspace_init`.

This proves absence of a current-decision priority write on the traced path,
not a numerical initial value or a complete history of every possible writer.
Loader-level initial contents, actual prior decisions/events and a particular
battle's last writer are not reconstructed here. No original execution was used
to observe those values. A default zero, guessed prior score or extra fresh actor
evaluation would silently replace that unresolved source state and/or add RNG.

## Why Throw Stone can read it

`ai_cast_aim_tiles_build` intersects final scenario1 with useful target range4.
`origin_reachable_evaluate` uses ranked origins for prepared Throw Stone;
`origin_next_take` reads signed priority×65536 plus fresh unsigned8 proximity.
It compares **every** remaining cast-origin key before choosing the lowest,
preferring later coordinates on a complete tie. An out-of-reach actor can still
be within range4 when Move is small. Such an original cast origin can read a
retained cell. Actor-origin selection then substitutes D1 and sets ACT-first;
other origins are Move-first. The sole clear evaluated aim is consumed, including
after NotValuable, so changing the first origin is not a harmless cache cleanup.

Example: actor(1,1), target(4,1), Move1, five ordinary reachable cells. S contains
only (2,1), whose fresh stored priority is 0. Actor proximity is 7; the scored tile's
is 8. Retained actor priority -1 selects the actor/ACT-first; priority +1 selects
(2,1)/Move-first. Unknown prior priority remains unknown, rather than becoming 0.
These are supplied-value witnesses, not claims about a particular retail run.

At original distance4, the actor is still a Throw Stone origin. At distance5 it
is removed by range filtering, so its undescribed priority need not be read by
this pass. Move0 supplies another safe case: the closer-tile filter finds nothing,
falls back to M0 and freshly scores the actor. None of this justifies filling
other unknown cells or broadening current fresh scan admission implicitly.

The examples start after preparation at RNG `(1,7)`. One described movement tile
draws 16838; the NonBlue adjustment then draws 5758/mod7=4 and ends `(2524885223,9)`.
With seed 6 the movement draw is 2726 and the later adjustment 25515/mod7=0 halves
reach 2 to 1, forcing nearest fallback. Blue consumes only the movement draw.
These literal schedules exclude candidate/lottery work and frame/VSync draws.

## Selected child — ZOD-200

Implement **scenario derivation only** after fresh both-unused NonBlue pair
preparation. Own original M0 proximity and filtered movement scoring separately,
then the post-score reach draw and D0=D1. Return scored cells S, final scenario1,
pre-OR scenario2 and explicit fresh-versus-unwritten original-origin provenance.
Retain normal-coordinate and remaining policy assertions. Never rescore the
added actor, substitute zero or accept a public cached result/prior-memory value.

Check range4/5, no-match fallback, masks that overlap after actor insertion,
halving/draw order, literal destinations, exact/late budgets and replay. No action
scan, rank or lottery may read an undescribed cell. A later selection child must
either prove every cast-origin key fresh/not-read, or separately model compatible
cache provenance/history. Current supplied and in-reach APIs keep their fresh-cell
requirements. No general list/status port, blocked paths, live authority,
replacement interface or host/editor activation is part of this child.

## Reproduction and limits

`verify_ai_pair_out_of_reach_contract.py --reference C:/repositories/fft_decomp
--prepared-executable scratch/all-data-extraction/SCUS_942.21 --output <new report>`
checks inherited/pinned source and selected prepared facts, plus these nine
descriptions. Three Python scenarios cover literals/input isolation, missing
priority versus zero, malformed envelopes and tampering. No managed runtime
source/tests changed; last runtime validation remains ZOD-198's clean Release
build and 4,042 passing .NET cases. All **529 Python tests** and the pinned
source/prepared-data audit passed; reports are in ignored
`scratch/ai-pair-out-of-reach-199/` (`python.txt`, `source.json`). This study does
not settle full memory lifecycle or original frame/stream equivalence.
