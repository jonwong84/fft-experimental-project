# ZOD-297: roster Throw Stone projection and ranked search

Reference: clean read-only `C:/repositories/fft_decomp` at
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed baseline `d6e72e1`.
The [source receipt](../evidence/ai/roster-stone-source-v1.json) pins files and
function bodies. No original or C execution is claimed.

## Effect and candidate boundary

The selected prepared ability148 facts remain those independently checked by
[ZOD-185](zod-185-ai-ability-iteration.md): immediate range4, formula0x37/Y2,
enemy/HP/physical/direct/evadeable, no follow/reflect/random/map-target pass.
An AI occurrence does not establish an executable effect or learned grant.
Require the existing typed `BoundedThrowStoneAbility`, explicit actor grant,
zero equipment PA bonus, exact ground and complete roster grant membership.
Keep the existing PA1..99 domain for every unit; do not broaden the command
simulator's two-unit admission to make an AI query work.

`formula_damage_random_y_times_pa` uses the non-execution midpoint16384 without
a draw, producing `2 * PA` (PA1/5/99 gives2/10/198). Four physical-evasion calls
consume real draws even at zero accuracy. Front includes either facing on an
exact diagonal. Positive accuracy projects full normal damage separately from
the percentage; zero accuracy is NotValuable before the clock. AI never runs
the execution-only knockback block, Brave rolls or damage draw.

Use the actor's original tile for ACT-first and the supplied destination for
Move-first. Validate the destination against the initial living whole-roster
movement result before a hypothetical KO. Change only the named target's HP;
restore every peer, PA, facing, choices and original position before the next
candidate, while retaining supported decision RNG. Reuse the existing explicit
target `AiCandidateState`, whole-roster `AiSimulationClock` and source-order
`AiCandidateScoring`. End CT is min60(currentCT +20 only when no Move was taken).
KO sets counter3; the effect-free clock retains the existing repeated corpse
upkeep limitation. Score all stable teams relative to the actor, not one foe.

## Direct trajectory is an independent prerequisite

`projectile_direct_check_between_units` calls `obstacle_units_list` excluding
only source and selected target, absent units, Jump and mounts carrying riders.
It does not filter by team or by AI usefulness. The initially living bounded
roster therefore contributes every other occupant, including allies and other
enemy teams. A defeated hypothetical target from an earlier candidate must not
remove an obstacle from a later candidate.

Tile centers use world X/Z =28*coordinate+14 and terrain height steps12.
Endpoints are raised by integer `spriteHeight*2/3`; a zero target raise becomes12.
The sprite-height reader returns0 for absent/dead/crystal/treasure; ordinary
living heights come from sprite-sheet graphic metadata, which current battle
state does not contain. Terrain columns are checked before unit boxes. Unit
boxes use strict bounds X/Z center-8 < point < center+8 and
baseY-height < pointY < baseY, and reject zero-height boxes.

`direct_path_trace` obtains distance through external `SquareRoot12`, advances
Q12 distance by0x2000, and visits floor(distance/0x2000) samples. Interpolation
uses `(current<<8)/(total>>4)` then signed arithmetic shifts of delta products.
There is no RNG. This is not a panel Bresenham test or continuous line/box test.
The local checkout declares `SquareRoot12` without its implementation; exact
SDK rounding/table behavior is an unresolved dependency for general samples.
Do not substitute floating-point square root or infer clearance from range.

The first child may retain an explicit **clear direct trajectory certificate
for the one supplied candidate**, covering its order-specific origin, terrain,
actual sprite heights and every other occupant. A false certificate rejects
without draws. It must not claim to compute obstruction or accept a blocked
candidate as merely NotValuable. This preserves the old pair projection's
explicit clear-geometry boundary without inventing new sprite metadata.

## Ranked origins and consumed aims

`ai_ability_outcome_evaluate` classifies every active target; useful enemies
contribute range4 cast masks intersected with scenario1 and distinct aim panels.
Instant CT does not remove an enemy through the charge-deadline check. Resolve
every initial ranked origin's signed16 tile priority from fresh writes or
retained knowledge; missing keys are a query failure, never zero. Compare
priority*65536 + byte proximity with `>=` in map order, so the last map tile wins
a complete origin-key tie. This origin selection differs from candidate rank,
whose complete priority/hit/proximity ties keep the first candidate.

Remove the selected origin and decrement origin count before evaluating it.
At actor origin use ACT-first with D1 as the final destination; otherwise use
Move-first to that origin. Iterate remaining aim panels in layer/Y/X order,
intersecting actual range from this origin. An ally or enemy standing between
the actor and an aim remains a collision obstacle despite not being an aim.

A failed direct trajectory takes `continue` **before** setting consume/evaluating:
zero formula/clock draws and the aim remains for a later origin. A clear aim
runs the candidate and is consumed even at zero accuracy or a losing score.
Do not retry that target from another origin or run a clock for zero accuracy.
Stop when either remaining aim count or remaining origin count is zero. Never
drop initially blocked aims, consume an out-of-range aim, or repeat the fresh
refresh/general movement prefix for each target. Per-candidate state restores;
decision RNG chains; any unsupported failure rolls back the entire owning query.

The [independent tapes](../fixtures/behavior/roster-stone-contract-v1.json) cover
midpoint/evasion, peer scoring/clock/KO, ranked tie order, aim-map order, blocked
retry and zero-accuracy consumption. Search tapes supply explicit clearance
facts; they verify source control flow, not projectile sampling correctness.

## Selected children

- **ZOD-298:** implement only the supplied roster candidate under the explicit
  clear-trajectory certificate. Share the current Stone formula and roster
  state/clock/scoring owners; preserve public pair command/profile guards.
- **ZOD-299:** resolve bounded direct-trajectory admission and missing sprite/SDK
  inputs before a computed blocked-path roster search. Record the available
  exact domain or an explicit remaining evidence dependency. Do not manufacture
  a default height or approximate SDK sampling.
- Select the fresh isolated ranked Stone entry after that boundary is concrete;
  then full-list composition/ranking/lottery and roster command/turn integration.

Other abilities, statuses, policies, maps, complete corpse lifecycle, automatic
facing, terminal/frame RNG, host/editor, owner drafts, replacement interfaces and
ZOD-106 stay separate/deferred.
