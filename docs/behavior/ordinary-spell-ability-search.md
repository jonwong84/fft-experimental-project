# One ordinary spell's caller passes — ZOD-441

`DefaultAbilityAiAbilitySearchQuery.Evaluate` now composes one owned offensive
spell's ordinary caller passes for one fresh Main/Complement movement scenario.
Movement is derived once. Remaining aims reset per pass, while best action and
RNG continue across passes. The final movement-baseline gate is diagnostic;
ranked-list insertion, lottery and commands retain their separate boundaries.

Managed base: `2f2293d8223b8affacaf1392837e6aec1a461a05`. The clean read-only
reference is `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[receipt](../evidence/ai/spell-ability-search-implementation-v1.json) pins the
source routines, managed files, independent literals and validation. No C or
original executable was run. Read the [origin study](ordinary-spell-movement-origins.md)
and [one-pass search](ordinary-spell-pass-search.md) for the inherited masks,
geometry, origin ordering, private simulation and admission limits.

## Code entry and routine ownership

Start at
[`DefaultAbilityAiAbilitySearchQuery.Evaluate`](../../src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiAbilitySearchQuery.cs).
Its [ZOD-443 context API](ordinary-spell-context.md) accepts a captured matching
state/catalog pair, a request containing actor/slot/profile/scenario and explicit
RNG. It binds fresh `DefaultAbilityAiWork` and calls
`DefaultAbilityAiOriginQuery.Build` once. It accepts no cached work or rank.

Then follow
[`DefaultAbilityAiAbilityEvaluation.AiAbilityOutcomeEvaluate`](../../src/Zodiac.Framework.Ai/DefaultAbilities/Selection/DefaultAbilityAiAbilityEvaluation.cs)
→ `DefaultAbilityAiPassEvaluation.OriginReachableEvaluate`
→ `OriginAimTilesEvaluate` → shared `AiActionSimulateAndScore`.
The new caller passes its private current best into the existing pass runner;
standalone pass and single-origin queries still start with a fresh best.

Each caller-pass diagnostic records its source grouping, RNG interval, origin
traces, remaining aims, and cumulative best at entry/exit. `DefaultAbilityAiAbilityBest`
also identifies which pass produced the retained action. `ImprovedBest` means
an actual improvement over the whole caller's current best, including earlier
passes. An empty group retains the earlier best and spends no candidate draws.

## Traced caller contract

At the pinned revision, `ai_ability_outcome_evaluate` first handles admission,
map/Math, reflected and useful-self alternatives. The admitted fresh ordinary
domain has no random-use, special map/Math, Reflect recipients or useful self
path. A Reflectable ability's empty reflected prefix still reaches
`ai_best_rank_insert`, which resets the already-empty fresh best and spends no
draws. That no-op does not permit retained caller work or general reflection.

The ordinary branch then behaves as follows:

1. FollowTarget plus completed ConsumeAim uses one unit-target group.
   Otherwise it saves useful membership and evaluates moving-by-deadline units
   first, still-at-deadline units second. The first group sets explicit aim
   consumption; the second clears it. Empty groups remain part of caller order.
2. It restores the saved useful mask. A zero-area FollowTarget ability submits
   the unit result at the early return and does not enter tile search.
3. For tile search, `target_enemies_with_move_opportunity_exclude` removes useful
   enemies predicted to move by the considered charge deadline. If any useful
   targets remain, tile search follows. Without FollowTarget, this is the only
   ordinary pass. FollowTarget tile masks XOR-remove the retained targets' own
   panels; the nonfollowing branch does not.
4. `ai_cast_aim_tiles_build` clears/rebuilds the aim rows and cast-origin masks
   on each pass. Consumption in one pass does not delete the next pass's aims.
   Exhaustive scans still clear their explicit consumption setting.
5. `ai_action_evaluate` updates shared `best_action` through strict
   `ai_rank_is_higher`. There is **no best reset between the ordinary groups**.
   Rank precedes final-destination proximity; exact ties keep the earlier pass.
6. `ai_best_rank_insert` follows the completed ordinary passes, or the zero-area
   early return. It first requires rank strictly above the movement baseline,
   then tests/inserts into the ranked list and finally resets best. This query
   reports only the final rank-only baseline gate and retained best before that
   insertion/reset. It performs no list insertion or lottery and adds no draws
   for either operation.

Pass usefulness determines valuable membership. Other units remain actual
formula recipients, included accounting targets when eligible, and contributors
to absolute scoring. For example, both unit centres in the split-deadline test
hit both adjacent enemies, although each unit pass has only one useful target.
Both casts therefore consume eight formula draws. Narrowing usefulness must
not filter those recipients from the simulation.

Nonvaluable attempts retain their real draws and do not erase an earlier best.
An unresolved candidate is compared with hit255 before stopping only its current
origin without consumption. Remaining origins and later passes continue.
Every hypothesis restores private HP, MP and unit placement. Any unsupported
work or overflow rejects the whole entry RNG with no partial passes/best.

## Independent regression evidence

Sixteen new cases in
[`DefaultAbilityAiAbilitySearchTests`](../../tests/Zodiac.Framework.Ai.Tests/DefaultAbilities/DefaultAbilityAiAbilitySearchTests.cs)
use source geometry, recipient and draw-count literals:

- Immediate range4/area2 at enemy(2,2): one movement prefix13, empty moving
  group, one five-draw unit cast, then the prior114-draw tile vector. Pass
  boundaries13/13/18 and final132 prove shared RNG and one prefix. The unit
  best survives every equal tile result; tile search starts with twelve fresh
  aims despite complete unit-aim consumption.
- Default range4/area1: a later tile cast at origin(1,4), aim(3,2), retains the
  same priority49 but improves final proximity6→5. Immediate and delayed totals
  are40 and35 draws. A two-enemy vector instead improves priority-436→-401 with
  a tile centre at(4,3), origin(0,3), spending59 total draws.
- Split deadlines restore membership before excluding the moving enemy from
  tile usefulness. Both unit casts hit both peers, so boundaries are13/21/29.
  The moving enemy's panel can still be a tile aim and it remains a recipient.
- Forced consumption uses one unit pass and twelve freshly rebuilt tile aims:
  prefix13 plus thirteen five-draw casts gives78. Nonfollowing area2 includes
  the enemy centre in its thirteen tile aims and spends132 draws. Zero-area
  FollowTarget returns after one unit pass with20 draws.
- Nonvaluable self hits retain all aims and restore resources across ten unit
  origins and147 tile attempts at twelve origins. Source diamond intersections
  give tile counts9/10/10/15/16/11/16/10/15/16/9/10 in map order; total draws
  are13 + (10+147)×4 =641. No best is fabricated.
- A fourteen-fast/one-slow enemy roster with considered CT99 keeps eighteen
  origin-local unresolved attempts across moving/still/tile groups. Boundaries
  are7/1,537/3,067; final draws4,597. Remaining aim counts14/1/4 prove fresh masks,
  comparison-before-exit and no unresolved consumption. The earlier hit255 best
  survives equal later results.
- A late tile overflow after the prefix and scored unit pass restores the
  original entry. Empty complement origins, equal-baseline rejection, replay,
  roster-order parity and unknown priority rejection cover the caller boundary.

BIOS seed57 results are independently checked by affine composition, without
managed RNG replay:35→1008742540,40→2064222289,59→3135962964,
78→1125473843,132→399997557,641→3390763518 and4,597→971947738.
The managed tests assert selected seeds as well as draw counts and candidates.

Full validation: zero-warning/error Release build; **5,263 .NET tests**
(1,458 AI;1,835 Framework) and **802 Python tests** pass. The source audits also
verify24 origin descriptions, four prepared routine ranges, thirteen instruction
anchors and four prior rank-iteration descriptions. Existing one-pass and
single-origin tests remain unchanged and pass. Owner drafts/JobStats, controller
formatting and disabled CI are preserved.

## Remaining work

This query covers one owned slot and one explicitly selected fresh scenario in
the inherited offensive lower-flat domain. ZOD-442 will trace fresh list/scenario
integration and the owned ranked-list boundary before its next runtime child.
Full mixed ability dispatch, insertion/lottery, autonomous magic commands,
useful-self/healer/Reflect pathways, retained workspaces and general status,
equipment, terrain, mounts, pickups, Golem, terminal/corpse/frame RNG remain
separate. The review programme and ZOD-432 owner readability walkthrough remain
open. Knight slot mapping, editor, cone and broad interfaces are unchanged.
