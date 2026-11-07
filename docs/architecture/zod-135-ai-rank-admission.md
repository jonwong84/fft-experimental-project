# ZOD-135 — Movement baseline and ranked-list admission

Source/design checkpoint, 2026-10-27. Read-only reference HEAD:
**11464943bbdbd2a0af022d90cb7f10455cf911df**, clean at inspection.
[Source receipt](../evidence/ai/rank-admission-contract-v1.json) pins the inspected
files/functions. Symbols below refer to that revision. This completes the source
investigation; it adds no production API, command authority or active AI.

## Source lifecycle and baseline provenance

`turn.c::action_select` resets eight ranked entries, current, best, move-baseline
and selected records only on fresh entry. Resume jumps back into the suspended
branch. `ai_action_rank_reset` sets priority=-32767, decided=0, hit=0,
wait-facing=ChooseAtWait (5), reflected=0. Coordinates and command payload survive;
this is not a zero-filled action. Successful state scoring clears current.decided;
only later command construction marks the selected record 255.

`general_select` runs `scenarios_build`, explicitly sets scenario **0** and calls
`rank_baseline_seed`, then uses scenario **1** for charging/ability evaluation.
The baseline is not necessarily the scan's chosen destination or its best score.
`ability_list_evaluate` loads entries without resetting best on every entry;
`ai_action_evaluate` replaces best only for a completed valuable evaluation that
strictly outranks it. Earlier stages may share best, while self/reflected/map/
final target stages call insertion, which resets best. Early returns can skip
insertion. Thus ZOD-131's fresh-best certificate is material; neither an arbitrary
source entry nor a resume can be treated as a new empty pass.

`scenarios_build` filters scenario 1, then `reachable_tiles_score` walks its mask
in layer/y/x order. Each visited tile sets MoveThenAct, skillset=0, updates pickup
metadata, and calls `ai_action_simulate_and_score` directly. It writes the negated
signed priority as a **u16 pattern** into the s16 tile-priority grid, and restores
actor placement after completion. It does not accumulate an ability best.

Destination selection also consults target distance and proximity; it can prefer
the last tile on equal destination keys. Scenario 0 may copy scenario 1's chosen
tile or perform a different destination search. Filtering does not initialize
every grid cell: do not infer that every reachable cell has a fresh score merely
from scenario-0 membership. The full lifecycle of unvisited/stale cells is outside
the next implementation. Require explicit fresh-cell provenance for its supplied
baseline destination. Do not replace an unproven cell with a fresh guessed value.

`rank_baseline_seed` reads only the selected scenario-0 tile's stored **u16**,
negates it, narrows to s16, and sets decided=0/hit=100. No simulation or RNG occurs
in seeding itself. For score `p`, storage is `(-p) & 65535`, and seeding recovers
`signed16(-stored)`, including -32768. Baseline coordinates/payload are not copied;
the admission gate never consults them. The grid's 16×18 strides per layer are
source layout, not proposed managed table limits.

## Movement-only simulation and RNG

`evaluation.c::ai_action_simulate_and_score` starts priority=0/hit=255, performs
movement/pickups, skips ACT for skillset=0, then sets hit=100. A coordinate change
marks movement taken; equal canonical coordinates preserve the original movement
flag. `turn/clock.c::turn_end` refunds 20 CT for each unused Move and ACT, capped
at 60. In the admitted action-available subset:

`endCt = min(60, originalCt + 20 + (alreadyMoved || destinationChanged ? 0 : 20))`.

ACT remains unused. Reusing a sword projection with zero damage is incorrect:
that projection consumes ACT and four formula draws. Both simulations advance
the turn clock before scoring, even when no unit is charging. In the existing
two-living-opponent, effect-free, positive-speed certificate, the first living
ready event ends advancement; it consumes **one upkeep draw**, modulo-reduces CT,
and uses highest CT then original slot order. No formula draws occur. Movement
can change readiness through the refund even though HP/status do not change.

The score is the absolute allied-minus-enemy HP/status/equipment total, not an
improvement from zero. Under the existing unchanged-equipment/no-MP/no-Golem
certificate, movement-only HP and status remain unchanged; equipment terms cancel
after scoring each unit, and final priority is within -160..160. Source additions
narrow to s16 after each unit. A Critical actor at 10/100 HP against a healthy
100/100 opponent scores `(12-32+357)-(128+357) = -148`.

Original scoring restores unit data, not the RNG stream. Scoring all scenario
tiles precedes ability scans and retains all those draws. The scenario policy
may additionally draw to halve non-player reach; the later lottery also draws.
An isolated supplied-destination query must not claim to reproduce that complete
decision stream. Its caller supplies the stream at that hypothetical evaluation.
The next helper will not run the original VSync suspension machinery.

## Strict admission and list insertion

`rank_compare` compares decided:u8, priority:s16, then hit:u8, all descending.
`ai_rank_is_higher` additionally prefers strictly lower cached foe proximity at
the action's **final coordinates** on a rank tie. Exact ties preserve incumbents.
Original `AI_RANKED_ACTION_COUNT=8` is an observed lottery/list capacity, distinct
from roster limits; it is appropriate for this compatibility algorithm only.

`ai_best_rank_insert` applies these steps in order:

1. Require best to beat move-baseline by **rank_compare alone**. Proximity cannot
   rescue equal priority/hit. With ordinary decided=0 and hit<=100, priority must
   strictly exceed the baseline, even if the baseline is negative.
2. Require best to strictly outrank slot 7 using rank plus proximity. A complete
   tail tie rejects before searching for an insertion position.
3. Find the first strictly outranked slot, shift full 32-byte action records right,
   drop the old tail, and copy best into the gap. Existing equal entries stay
   ahead. There is **no identity/destination/payload deduplication**.
4. Reset best unconditionally, including a failed admission/tail test. Retain the
   remainder of its payload as specified above. Add no RNG draws.

For the closed managed score envelope, every real score beats a reset sentinel;
representing empty slots/best explicitly is safe without stale-coordinate reads.
This equivalence is not established for arbitrary signed-minimum source scores.
Absent best is not score zero. A zero candidate can beat baseline -1; a negative
candidate -10 can beat baseline -11. No scored candidate still means no insertion.

`ranked_select` is separate: every visited slot draws, including hit=0; it may use
the first entry if all rolls fail but any hit is nonzero. Insertion neither runs
this lottery nor selects a command. General ability/scenario search, inherited
best state, special AI policies and authoritative RNG remain separate work.

## Smallest implementation follow-up: ZOD-136

Implement **one supplied movement-only baseline projection and score**, before
composing scans with a private ranked list. Reuse the closed two-opponent state,
movement/score certificates and original actor/target IDs. A separate baseline
certificate must default its unrepresented assertions to false: supplied tile is
the original scenario-0 baseline destination, its source score cell was freshly
written in this context, normal scoring is enabled, and coordinates are canonical.
Check destination against unchanged original AiOrigins and existing admission;
do not require it to equal the Attack scan's scenario-1 destination.

Evaluate once from original inputs and explicit decision RNG. Use no public
cached score/projection/scan result as trusted input. Return immutable hypothetical
units, unchanged HP/status, turn-end CT, first-ready diagnostics, fresh absolute
priority and hit=100. Reject unsupported/exhausted queries atomically with the
original RNG and no partial payload. Successful queries retain the one upkeep
draw. Keep live state/RNG unchanged. Use named booleans for new semantic facts;
do not expand packed flag APIs as part of this slice.

Focused acceptance cases, **not yet executed in C#**:

- Move versus stay/already-moved refunds, 60 cap, changed first-ready unit and
  original slot ties; original facing/HP/status preserved.
- Zero and negative absolute baselines, both actor identities and stable score
  ordering; score/double-negation round trip within the admitted envelope.
- Exactly one upkeep draw and no formula draws; exact-MAX completion, exhausted
  RNG/ticks, and an already-ready opponent needing no tick addition.
- False certificates, wrong/occupied/layer/out-of-bounds destination, terminal or
  unavailable original action reject; unknown facts never mean empty success.
- Replay/input immutability and full rollback, without broad permutation matrices.

After that helper is validated, select a bounded composition that owns fresh
baseline evaluation, scans, private best/cache and eight slots. Preserve original
ordering and every successful evaluation draw. Do not expose a general
caller-populated ranked-action API merely to exercise list capacity.

## Verification

Eight focused [Python tests](../../tools/verification/test_ai_rank_admission_contract.py)
exercise source-derived rank/CT literals and reuse existing offline score/clock
kernels. They execute neither C# nor original C, and do not establish end-to-end
AI equivalence. No prepared original assets are needed. Source hashes are an
explicit receipt, not routine current-code freezes. Reproduce with:

```powershell
python -m unittest discover -s tools/verification -p 'test_ai_rank_admission_contract.py'
python tools/verification/verify_ai_rank_admission_contract.py --reference C:/repositories/fft_decomp --output scratch/ai-rank-135-source-audit.json
```
