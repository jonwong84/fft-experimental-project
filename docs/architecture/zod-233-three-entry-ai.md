# Shared Attack / Accumulate / Throw Stone search — ZOD-233

Source study, 2026-11-04, managed base `4aa1314`. Read-only fft_decomp HEAD
rechecked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/three-entry-source-v1.json) identifies the
file/function fingerprints and independently checkable supplied stream below.
The [complete algorithm-guide audit](original-ai-algorithm.md) verifies these
functions at that revision. No original or C program was executed.

The selected children ZOD-234–236 are now implemented; their
[current behavior and validation](../behavior/basic-skill-ai-selection.md) retain
the supplied-scenario and authority boundaries below.

## Source ownership and order

`turn.c::general_select` builds movement scenarios, seeds the scenario-zero
movement baseline, considers keeping a charge, then selects active targets and
calls `ability_list_evaluate`, followed by one ranked lottery. The ordinary
closed human lists are Attack, learned Accumulate, learned Throw Stone, non-Frog
Frog Attack and terminator. Preparation clears Frog Attack usability; it remains
in initialization denominators. Each usable entry is loaded and evaluated in
source order. No new preparation, refresh or movement-prefix evaluation occurs
between entries. The existing three-action preparation already derives both
admitted teams without asserting the old Attack/Stone-only list certificate.

`evaluation.c::ai_ability_outcome_evaluate` copies the active mask into a fresh
targetable mask per entry. Accumulate takes the map-target branch: set unit target
to actor, use scenario-one destination D1, filter usefulness once, evaluate
ACT-first and then Move-first only when D1 differs, insert the best rank, return.
It does not use the damage action's hostile target ID as its effect target or
repeat the classifier for each origin. See the [Accumulate trace](zod-229-accumulate-ai.md).

`ai_action_simulate_and_score` restores selected baseline fields after each
candidate; its RNG is not restored. Thus every entry and origin starts from
actual HP, PA and choices, while draws from losing candidates, misses and
NotValuable evaluations remain consumed. Attack draws precede the Accumulate
coin; any self candidate clocks precede Throw Stone. An odd coin still advances
the stream and does not prevent the later Throw Stone entry. Accumulate PA99
still receives +3/hit100 when useful. It never boosts the hypothetical Throw
Stone candidate that follows. Do not concatenate independently refreshed public
queries or feed diagnostic results back as trusted input.

## Ranks, arithmetic and lottery

`ai_best_rank_insert` first requires a strict `rank_compare` improvement over
the move baseline: marker, signed16 priority, unsigned-byte hit percent. Proximity
does not pass that gate. It then compares against the last original ranked slot,
uses lower foe proximity on a rank tie, shifts entries and resets the working
best. Only the ranked array and decision stream cross entry boundaries. A
complete tie preserves the earlier entry; the three entries must therefore be
inserted in Attack → Accumulate → Throw Stone order. Eight ranked slots are
observed compatibility behavior, not a general managed catalog capacity.

Extract the current pair's insertion operation as an internal shared owner.
Retain its fresh-null representation of original reset slots in the admitted
range. Reuse the existing signed score narrowing, movement key/slot ordering,
source formula projections and clock. No new scoring formula is required.

`ranked_select` consumes rand()%100 per visited slot, including zero-hit slots.
First pass wins; after eight failures any nonzero hit selects slot0, otherwise
fallback uses D0 with Move-first and no ACT when Defend is absent. Hit100 on
Accumulate is a threshold in this sequential lottery, not a normalized weight.
Facing remains deferred ChooseAtWait. Typed plans are diagnostics, with no
Framework receipt, command authority or live RNG adoption.

## Supplied scenario boundary

First compose against the existing immutable `ThrowStoneAiScanScenario`:
explicit scored origins S, action scenario origins, D0 and D1. Validate unique
reachable masks, subset rules and known score-cell provenance before scoring.
Evaluate S once in source layer/y/x order and reuse the resulting baseline and
proximity cache for all entries. Do not invent zero for an inserted actor tile
whose original score cell is unknown. This initial child requires D0 and D1 in
S, matching the existing explicit known-score admission. It does not derive
three-entry in/out-of-reach scenarios or consume a new team reach-adjustment draw.

Accumulate uses D1 directly, independently of the damage scenario mask. An empty
damage-origin mask must not suppress an otherwise useful self entry. Both active
units follow the certified ordinary general-search mask; the fresh prepared
wrapper must reject inactive-opponent scan assertions instead of manufacturing
them. Its explicit general-entry assertion excludes earlier policy/charge work.
No caller-provided score, prefix, preparation, usefulness result or ranked list
can enter the owning composition. Failures return original whole-query RNG and
no partial output, retaining the existing managed rollback contract.

## Independently checkable stream

Take two living maxHP50 units: actor(1,1), opponent(2,1), PA5, sword power2,
class evade97 facing West, CT15/95, Speed5, ticks20; source slots actor before
opponent. S and damage mask contain (0,1),(1,1), D0=(1,1), D1=(0,1).
Start RNG word1/count7. The source LCG is unsigned32 `w*1103515245+12345`,
raw `(w>>16)&32767`.

- Draw1 refresh; draws2–3 score S in x order.
- Draws4–7 Attack evasion sources; draw8 Attack's next living-unit upkeep.
- Draw9=16212, even, makes Accumulate useful. Draws10–11 are its two independent
  clocks; each projects PA5→6 from the original state.
- Draws12–15 Throw Stone evasion; draw16 its clock. Its damage is 10 from PA5.
- Absolute ranks are Attack(26,3), Accumulate(3,100), Stone(26,3); all destinations
  have the same proximity. Final order is Attack, Stone, Accumulate.
- Lottery raws25089,21183,25137 give 89≥3, 83≥3, 37<100: select Accumulate at
  slot2. Final RNG word3794901700/count26, with unchanged live PA/HP/choices.

If the damage mask is empty, Attack draws nothing; with the same two S scores,
the self coin instead consumes draw4=17515 (odd). With no eligible damage
candidates, eight empty lottery slots then take draws5–12, ending at
word836760821/count19 and MoveOnly at D0. These descriptions fix phase ordering;
they are not original execution observations or certificates for derived scenarios.

## Selected implementation children

- **ZOD-234:** extract shared private rank insertion and an Accumulate entry
  evaluator that takes fresh owned state/profiles/D1/RNG, without refreshing or
  scoring movement. Route old pair/map-entry APIs through those same owners;
  preserve existing result shapes, streams and guards.
- **ZOD-235:** fresh closed-list preparation, one supplied known-score movement
  prefix, Attack scan, Accumulate entry, Throw Stone scan and ordered insertion.
  Return typed hypothetical ranks/diagnostics; preserve both teams and old APIs.
  Cover literal streams, cross-entry state isolation, empty damage masks, score
  ties, distinct IDs, unsupported scenario provenance and late-stage rollback.
- **ZOD-236:** one terminal source lottery over freshly composed ranks and a
  typed Attack/Accumulate/ThrowStone/MoveOnly plan. Require terminal/default/
  no-Defend assertions, preserve deferred facing, and cover first/later success,
  empty fallback, exact/insufficient budgets, replay and both teams/identities.

Real self-command dispatch/continuations, derived scenario entry integration,
general rosters/lists, host/editor activation and battle saving remain outside
these children. ZOD-106 remains deferred; boosted PA remains battle-only.
