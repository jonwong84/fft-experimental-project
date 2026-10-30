# ZOD-141 — Derive an in-reach ordinary-sword movement scenario

Source/design checkpoint, 2026-10-28. Reference revision:
**11464943bbdbd2a0af022d90cb7f10455cf911df**. Read-only study; no C/original
execution. [Source receipt](../evidence/ai/movement-scenario-contract-v1.json)
extends the selection receipt. Implementation follows in ZOD-143; ZOD-142 stays
deferred for Lion War event extensions.

## Selected branch and inputs

Start at fresh `general_select::scenarios_build`, after original target/ability
considerations and reachable/proximity setup. Keep the existing two living,
positive-Speed, flat ordinary opponents, effect-free scoring and original-slot
TieKey certificates. The selected focused target is the other unit at its current
position. Require named, false-default certificates for focused-opponent context,
ordinary-sword reach summary, and ordinary scenario policy. The latter excludes
stay-near, reflected charging, ranged-ability floor, crystal overrides and special
search modes; fresh comparison context excludes stale/resumed setup. These facts
are absent from the managed snapshot, not inferred from unit/content IDs.

`ability_considerations_set` takes the minimum usable enemy range plus area and
adds original acting-unit Move to its u8 reach. Under the certified ordinary
sword summary, range=1 and area=0, so reach=Move+1. Do not infer that the whole
ability list contains only Attack: the certificate describes its reach summary;
terminal Attack remains a separate selection-tail requirement. Restrict original
Move to 0..124 so the existing movement spread cap and raw Move agree, and the
reach addition cannot wrap. An already-moved actor still has that original Move
for reach, but its reachable mask is only its current tile.

Use `FlatOrdinaryMovement` to derive scenario-0 origins R and target-distance
rows. In this open flat slice reverse propagation clears occupancy; distances
are Manhattan even though ordinary movement normally uses propagation, not the
Teleport formula. Require actor-to-target distance d <= Move+1. Out-of-reach
inputs reject before scoring; their non-player `rand()%7` reach-halving branch
is deliberately outside this milestone.

## Mask, private scores and destination ordering

The in-reach ordinary branch filters R at distance <= Move+d. Triangle inequality
ensures every reachable tile qualifies here, so pre-insertion S=R, nonempty and
including the actor. Retain the actual filter/fallback semantics: an empty filter
copies R. Scenario 2 is R XOR S, empty in this admitted branch; destination 2 copies
D1. No unsupported/unvisited priority cell is read.

Score S exactly once, layer/y/x, with `EvaluateFreshTile`: each simulation starts
from the original snapshot/ticks and consumes its actual one upkeep draw while
carrying the private decision RNG. Never score D0 again. Derive proximity from
original positions/delays, before projected damage: the one enemy contributes
max(10-Manhattan,0) only when its delay <= actor delay. Do not reuse projected-KO
positions or mutate the live RNG/CT/choices.

`destination_select` reads u16 negated priority, shifts it left 16 and adds u8
proximity under signed s32 arithmetic. Use `unchecked` for this packed comparison,
not unsigned ordering. Lower packed value wins; priority-first ties prefer lower
target distance; complete ties select the **last** layer/y/x tile. This differs
from the Attack scan's strict earliest tie retention. All supported move-only
priorities happen to agree, but retain real evaluations and source arithmetic.

- D1: priority-first over S, with no distance restriction.
- D0: because the actor has not acted and this is a focused, in-reach,
  non-reflected branch, priority-first over R at distance <= d-1. No ranged floor
  is admitted. If none qualify, use unrestricted distance-first: lower distance,
  then lower packed score, then last complete tie. At adjacency no free tile has
  distance zero, so this fallback is real, not an error.
- Add actor to the Attack mask only after these stages. Here A=S union actor=S;
  keep that ordering rather than generalizing to out-of-reach masks.

The standalone query publishes immutable distances, proximity/packed comparison
diagnostics, fresh movement evaluations, S/D0/D1 and baseline index. Destination
selection adds no draws/ticks. Any admission/tile failure returns original input
RNG, no partial scenario/scores, and typed reason/index. This transactional policy
is managed error handling, not native frame-resume behavior.

## Acceptance for ZOD-143

Use a small set of source-derived scenarios, not a Cartesian matrix:

1. In-reach boundary and a genuinely out-of-reach rejection; derive S from state,
   score in source order, one draw per cell, original ticks per evaluation.
2. Eligible enemy makes D1 prefer lower proximity; D0 stays within d-1. An
   ineligible enemy gives zero proximity and exposes distance/last-tile ties.
3. Adjacent-target D0 fallback, already-moved singleton, Move=0; keep raw Move
   reach distinct from remaining voluntary movement.
4. Signed packed ordering with positive/negative actual move-only scores;
   complete ties select last, with no assumption that only positive scores exist.
5. Each required fact missing, unknown actor/target, unavailable action, Move>124,
   unsupported geometry/score profile: atomic rejection before RNG. Late draw/tick
   exhaustion likewise publishes no partial result; exact-MAX completion works.
6. Both actor identities, collection order, deterministic replay and explicit
   decision-RNG carry preserve original snapshot/live RNG.

## Ownership for later integration

ZOD-143 is a standalone fresh scenario query. A later source/design checkpoint
will specify an internally owned continuation into composition/selection. It
must consume these private movement scores directly, never accept a public cached
result as authority, never call the old composition to repeat the prefix, and
restore the **whole** query RNG after later scan/lottery failure. Supplied-scenario
APIs remain supported unchanged. Full target/ability search, out-of-reach roll,
Defend, command/facing execution, replacement interfaces and native/editor
activation remain separate.
