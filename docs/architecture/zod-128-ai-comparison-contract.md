# ZOD-128 — Supplied sword candidate comparison

Source/design checkpoint, 2026-10-27. Read-only reference freshly checked clean at
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[evidence](../evidence/ai/comparison-contract-v1.json) pins whole files and function
bodies; symbols below refer to that revision, not upstream main. This milestone
adds offline descriptions and tests, **no production comparator**. Existing
ZOD-125 projection, ZOD-127 scoring and historical evidence remain unchanged.

## Caller and comparison order

`src/battle/ai/turn.c::ai_command_build` initializes the actor, refreshes decision
flags, builds reachable tiles, initializes targeting, then enters `action_select`.
On fresh entry, `action_select` resets action ranks and fills foe proximity once
before the status/retreat/autobattle/general selection passes. On resume it jumps
back to the suspended pass without rebuilding that cache.

`evaluation.c::ai_action_evaluate` simulates and scores the current candidate.
Only a nonzero completed score result calls `ai_rank_is_higher(current, best)`;
only true copies the candidate over the incumbent. Thus an exact tie keeps the
earlier incumbent. This pair contract neither invents an enumeration order nor
claims that the two supplied candidates are the complete original search.

`turn.c::rank_compare` compares these fields in this order:

1. `decided`: unsigned byte; greater wins.
2. `priority`: signed 16-bit; greater wins.
3. `base_hit_percent`: unsigned byte; greater wins.

The `include/fft/ai.h::ai_action_rank_t` memory layout is decided, hit, priority
(four bytes); comparison order differs from field order. Source rank enum values
are Below=0, Equal=1, Above=2. Offline `rankOrder` uses −1/0/+1 as a diagnostic
adapter, not those source enum ordinals.

`ai_rank_is_higher` returns true immediately for Above, false for Below, and on
Equal reads the two action **destination** coordinates from `tile_foe_proximity`.
Strictly lower proximity wins; equal proximity returns false. There is no random
tie break. Priority outranks accuracy even when negative; neither damage delta
nor priority multiplied by hit chance is a comparison key.

Every scored candidate has decided=0 (`evaluation.c::rank_current_decision_clear`).
The later selected action gets 255 only in `ai_command_build`, after selection.
The proposed managed pair accepts no caller marker and derives 0 for both fresh
scores. `ai_action_rank_reset` uses priority −32767/hit 0, but that reset record
is not an absent managed score and is not a third candidate in this contract.
`ai_best_rank_insert` separately compares against a movement baseline, inserts
through a ranked list, and `ranked_select` later consumes lottery draws. Those
callers remain outside the proposed pair query.

## What the proximity cache actually contains

`foe_proximity_grid_fill` rebuilds a 32-slot targetability scratch list. A unit
contributes only when both `target_is_out_of_play` and
`target_is_excluded_from_imminent_enemies` return zero. The first excludes cached
untargetable/dead-without-Reraise flags and current Petrify. The second requires
the cached enemy flag, estimated target turn delay **no greater than** the cached
actor delay, and no Stop, Don't Act, Sleep, Chicken or Charm active through that
actor delay. `ai_status_is_active_through_delay` treats equality of status duration
and delay as active. These are predicted-duration checks, not simple current-bit
tests. Self is non-enemy; actor current-team versus opponent initial-team and
fresh targetability classification retain the ZOD-126 certification boundary.

`acting_unit_init` stores `ai_unit_turn_delay_calculate(actor)` in
`acting_unit_remaining_clockticks`, an **unsigned byte**, although the helper
returns signed 32-bit. For living ordinary None/Critical units with Speed 1..255:

`delay = ceil(max(100 − CT, 0) / Speed)`

No Stop/Sleep timers or inert/dead branches apply in this admitted subset. The
active actor's CT has already been reduced modulo 100 by
`src/battle/turn/clock.c::turn_clock_run` before setting has_turn; do not assume its delay
is zero merely because it has the current turn. For CT=15/Speed=5 it is 17. A
target at CT=95/Speed=5 has delay 1 and contributes; at CT=0/Speed=1 it has delay
100 and does not. Equal delays contribute. Admitted delays lie in 0..100, so
the actor's byte assignment cannot truncate them. The general helper's inert
0x7fffffff and status-dependent byte narrowing remain outside this profile.

For each layer, y, then x within terrain bounds, the builder visits only bits set
in **scenario 0** reachable rows (u16, high bit means x=0). At a visited cell it
resets the unsigned byte to zero, then visits eligible unit slots in original
order and adds:

`max(10 − (abs(enemyX − x) + abs(enemyY − y)), 0)`

The actual branch is distance <11; distance=10 adds zero. Height and the enemy's
layer are ignored; equivalent cells on two layers receive the same contribution.
The cache has two layers of 18 rows by 16 columns. Addition narrows to byte at
each contribution; a general roster can wrap. The two-opponent profile has at
most one eligible enemy, so each freshly built value is 0..10 and cannot wrap.
The isolated multi-foe/wrap/layer vectors describe source kernels, not expanded
admission to the proposed managed wrapper.

Unmarked cells are **not cleared**: their old bytes survive. Consequently missing
membership is never equivalent to proximity zero. Nor is raw Manhattan distance
the cache value: lower cache values usually favor being farther away, become
equal beyond the cutoff, and are all zero if no foe is eligible. This cache is
separate from `tile_target_distance`, movement propagation, and signed tile
priority packing used by other selection functions.

## Closed managed follow-up

Proposed additive entry, without changing older APIs:

`SwordAiCandidateComparison.Evaluate(originalState, incumbent, challenger, weapon,
candidateProfile, scoreProfile, comparisonProfile, decisionRng)`

Both supplied candidates must refer to the **same actor and same opponent** from
one original snapshot, the same nullable weapon and the same candidate/score
profiles. Orders and final destinations may differ. Both current live units must
initially be living, with the existing canonical clock, ordinary sword, human,
unchanged equipment, stable teams and zero MP/current+baseline Golem certificates.
Both actor identities are supported in separate calls; collection order must not
change original TieKey semantics. No public score/result/cache record is an input.

Add an immutable `SwordAiComparisonProfile` binding actor ID, target ID and an
explicit `FreshDecisionBaseline` boolean. Missing/default/false certification or
mismatched identities rejects before scoring. True certifies that initial decision
flags, relation/targetability and cached actor delay correspond to this exact
original settled state at fresh action selection, after refresh/reachability and
before any candidate simulation. Unrepresented original cache provenance cannot
be inferred from IDs. The existing movement/candidate profiles already certify
no mounts, traps/pickups/control/timed/formation or other omitted effects; do not
silently broaden those certificates. General stale/resumed/imported workspace
caches are not accepted. Required null arguments remain programmer errors.

Use unchanged `FlatOrdinaryMovement.Evaluate` on the original state to obtain
scenario-0-equivalent `AiOrigins`. Its certified open, dry, cost-1, lower-layer
map has dimensions 1..16 and no known traps. The source clears other living-unit
destinations, includes the actor's own tile, and restricts a moved actor to stay;
the existing query supplies these semantics. Validate both destination memberships
before reading/building cache values or evaluating either score. Reject unsupported
geometry/movement and invalid/unwritten destinations with typed diagnostics. Do
not require the prototype's generic path heading or invent a fresh movement port.

Build private values for the two validated destinations using the **original**
target position, original None/Critical status, CT/Speed and stable opponent
classification. No public cache or arbitrary mask is trusted. Keep diagnostic
actor/target delays, eligible enemy ID (or none), destination/value pairs and
comparison reason. Evaluate this cache only once, before either projection. A
lethal candidate must still compare using the initially living foe and this
baseline cache; neither its projected Dead status, next-ready identity nor
new CT/choice flags recompute the cache. ActThenMove uses its final destination
for proximity even though its action was evaluated from the old actor origin.

Invoke unchanged `SwordAiCandidateScoring.Evaluate` once for the incumbent and
then once for the challenger, both on the original state and profiles. Pass the
first **returned decision RNG** into the second query. This avoids trusting
constructible public records as proof of origin or re-projecting an outcome to
validate it. The wrapper's returned comparison remains hypothetical data, with
no command receipt, live turn, or authoritative RNG commit.

## Outcomes, RNG and failure policy

Return an immutable typed result with Compared, NoScoredCandidate or Rejected,
retained candidate index/identity (if any), successful fresh score diagnostics,
cache diagnostics, comparison reason and returned decision RNG. Preserve nested
typed score/projection rejection reason and failed candidate index on failure;
do not publish a partially accepted pair of evaluations.

- Two Scored results compare challenger against incumbent with both markers 0.
  Exact priority/hit/proximity ties retain index 0. Priority, hit, proximity and
  equality should have distinguishable diagnostic reasons.
- Only one Scored result retains that candidate. This includes legitimate zero
  or negative priority: an absent score never becomes priority 0.
- Two NotValuable results return NoScoredCandidate with no retained candidate.
  Both successful evaluations' formula draws are still carried.
- A preflight/profile/membership failure rejects before score calls or draws.
  A rejected first score skips the second. A rejected second score rejects the
  whole pair, returns the **original pair RNG** and no successful evaluation/cache
  payload, while preserving failed index and typed reason.

That last rollback is an explicit **managed transaction proposal for unsupported
or exhausted queries**, not a claim that original C search rewinds its RNG. The
original does not have our draw-count exhaustion/unsupported-profile result type.
Within an admitted successful pair, neither losing a comparison nor NotValuable
refunds any evaluation draws. Both evaluations use the same original spatial/HP
state, never the incumbent's hypothetical post-state. The explicit supplied
incumbent-then-challenger order defines RNG ownership for this local query only;
it does not assert the full original enumeration or choose a real command.

Cache creation and comparison themselves consume **zero draws**, zero elapsed
ticks and no Move/Act choices. Full evaluation still consumes the scoring calls'
formula/upkeep draws. A pair completing its second supported score exactly at
ulong.MAX draw count succeeds. A late second overflow rolls back the pair even
if the incumbent score succeeded privately. Inputs and independent branches are
immutable; replay with identical original inputs and decision stream must match.

## Evidence and validation

The [60 classified vectors](../fixtures/behavior/ai-comparison-contract-v1.json)
comprise 38 isolated source kernels (rank, ordinary delay, eligibility and masked
proximity) and 22 normalized managed pair proposals. The latter adapt the existing
ZOD-126 scoring descriptions, including supplied normalized damage/accuracy and
movement membership facts; **these dictionaries are not the future C# input
schema, an original-game observation, or executed managed comparisons**. Production
must derive formula results and membership through the existing managed queries.
The hit-variation adapters use MoveThenAct from the target's front versus stay
from its back; they need a 3x2 map, sufficient Move and matching class evade in
future implementation tests. Same-weapon pair vectors share normal damage.

Twenty new Python tests check literal widths/order/ties, exhaustive strict
comparison permutations, ZOD-116 rank reconciliation, ceil/byte delay, all
eligibility exclusions, distance cutoff/layer/retained-cell/byte-wrap kernels,
fresh calls and forward RNG, both orders, lethal baseline cache, NotValuable,
early and late atomic failure, exact MAX budget, replay and unchanged inputs.
ACM-001..014 explicitly remain **unexecuted production cases**. Identified actor
role changes and reversed input collections are covered by normalized descriptions;
actual managed identity/collection, geometry and formula materialization remain
required checks. These descriptions are not evidence of executed production cases.

Reproduce the offline audit with:

```powershell
python tools/verification/verify_ai_comparison_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --output scratch/<new-report>.json
python -m unittest discover -s tools/verification -p 'test_*.py'
```

The audit checks fixture identity, the pinned clean reference and function hashes,
110 preserved historical artifacts and 31 managed source/test baselines. The old
ZOD-126 source/data audit independently retains its two prepared metadata checks;
no additional original bytes are required by this comparison contract. Offline
tests do not need original media or a reference checkout. Final suite/audit
results and log identities are recorded in evidence. No C# changed, so the last
implementation validation remains ZOD-127's **3,957 .NET / 432 Python tests**.
The final Python suite passes **452 tests**, including the 20 new tests above.

**Next: ZOD-129**, implement only this supplied-pair comparison wrapper and execute
ACM-001..014. General enumeration, movement baseline/rank-list insertion, lottery,
roster/policy, replaceable AI interface, decision runner and authoritative RNG
publication remain future work. Editor UI and ZOD-106 remain deferred.
