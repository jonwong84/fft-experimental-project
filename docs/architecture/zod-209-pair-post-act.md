# Pair post-ACT movement and settlement — ZOD-209

Source/design investigation, 2026-10-29. Clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, managed base `2bfc621`.
The [receipt](../evidence/ai/pair-post-act-contract-v1.json) pins five files /
22 functions and inherits the [command study](zod-203-throw-stone-command-boundary.md).
Six [descriptions](../fixtures/behavior/ai-pair-post-act-contract-v1.json) use
supplied actual checkpoints from the committed ZOD-208/210 scenarios. They
describe refresh/preparation facts, not a full decision or movement oracle.

## Re-entry and actual state

`turn.c::ai_command_build` tests decided/saved-choice equality before any fresh
search. Saved Move0/ACT0 versus actual Move0/ACT1 passes this guard; ACT used
then forces simulation, refresh, ability preparation, reachability, team/target
policy and fresh selection. Unchanged saved0/1 versus current0/1 instead emits
END before refresh. ACT used alone does not prove `FreshRefreshEntry`.

Use actual HP/status, settled positions/facing, CT and post-command RNG. A miss
still consumes ACT; a push changes focus, distances, reachable occupancy and
proximity. No old predicted D1 or selected-action payload authorizes post-ACT
movement. Refresh's skillset-zero/ACT-first simulation adds only the unused-Move
refund: `clock.c::turn_end` computes min(60, liveCT+20) before the temporary clock.
Moving later uses min(60, liveCT); staying uses min(60, liveCT+20). Preserve the
source u16 addition/byte-store ordering and actual choice flags.

## Pair preparation after ACT

`abilities.c::ai_unit_abilities_init` still describes Attack, learned Throw Stone,
non-Frog Frog Attack and terminator. Initialization counts three, with evasion
quarter-share2, not4. The first pass of `turn.c::ability_considerations_set`
rebuilds both living units' usability, harmful/helpful counts and ranged flags.
For the bounded pair it reports two harmful entries, no helpful entries and
HasRangedAbility. The enemy retains its own first-pass facts.

The actor-only second pass computes usage policies and minimum reaches before
clearing each usable bit because ACT is spent. Enemy reach remains Move+min(1,4)
=Move+1; ally reach remains Move. Both actor Attack and Throw Stone become
unusable; Frog Attack was already unusable. Ranged summary and first-pass counts
are not erased by that later clear. No peer consume-aim policy is inferred from
the actor-only pass. Preparation itself adds no draw after refresh.

The existing `SwordAiRefresh.EvaluateAfterAct` does not certify Attack-only lists
and can be reused under its living, ordinary status/upkeep/cache/profile guards.
Shared restored-state derivation is valid for this pair's minimum enemy range1.
`AttackThrowStoneAiPreparation.Evaluate` cannot simply be called on post-ACT
state: its live targeted-command admission returns `ActionAlreadyTaken` **before**
ability/grant/setup/range checks. Ignoring that result would skip required checks.
Separate content/grant/profile admission from live choice availability; do not
fabricate ACT-unused state or weaken actual command validation.

## Movement, empty abilities and settlement

`turn.c::general_select` and `scenarios_build` still build fresh movement scores.
Post-ACT always copies D1 to D0; it never calls the ranged distance-floor branch.
In-reach score/distance/proximity ordering remains; out-of-reach still filters
first and takes the NonBlue modulo7 reach draw **after** scoring. Actor insertion
and diagnostic source masks retain their ordering. Share existing post-ACT clock,
absolute scoring, ordering and lottery kernels rather than rebuilding them.
The current Attack-only scenario/list certificates cannot be asserted for a pair;
factor the applicable internals behind a truthful pair entry.

`ability_list_evaluate` loads all three entries, but `ai_ability_entry_load` carries
their cleared usable flags into `ai_ability_outcome_evaluate`. Its sign-bit guard
returns before target/formula evaluation. No action scan, formula draw or unknown
cast-priority read follows. This avoids the pre-ACT out-of-reach cast-key problem;
it does not assign zero to retained workspace cells or resolve their history.
Empty ranks still cost eight `ranked_select` rolls. With explicit NoDefendSupport,
`fallback_select` chooses skillset0, move-first and D0. Fresh dispatch emits MOVE
for a different tile, otherwise END.

The [existing Framework boundary](zod-183-post-act-command-boundary.md) already
supports both outcomes: own fresh selection inside `FacingDecisionBattle`,
propose turn-ending Move or Wait, adopt verified decision RNG once, and resolve
facing from actual placement/receipt. A failed later query/command preserves the
already published ACT. No Move-first continuation token or second ACT is needed.
Keep facing deferred, avoid repeated adoption/refresh, and stop after settlement.

## KO and terminal boundary

Refresh marks the killed sole enemy DeadWithoutReraise; ability threat/proximity
passes exclude it. `ai_target_find_nearest(AI_NEAREST_ELIGIBLE_ENEMY)` initializes
its result to the actor and retains that default when no enemy qualifies;
`decision_target_focus` then copies the actor's coordinates. This is an actual
source fallback, not permission to keep treating the dead unit as living.
Its complete dead-unit scoring and battle-end/controller routing are outside
the living-pair model and remain unresolved here. The first implementation child
must explicitly reject terminal input without undoing the completed ACT or
inventing a living target/facing choice. No claim that retail automatically
ends this battle or chooses a particular final heading follows from this trace.

## Checkable witnesses and selected child

All supplied living checkpoints have actor/peer CT15/95 and Speed5/5. Fresh
refresh temporarily ends at35, advances one tick, and reports actor40/peer0;
live restored delays stay17/1. Actor remains (1,1):

- Actual power2 Attack leaves HP40 at RNG `(995405759,49)`; one refresh ends
  `(2102736524,50)`. Move3 gives enemy reach4, distance1, in-reach.
- Move0 Stone hit leaves HP40 at `(3823029677,27)`; refresh ends `(3914758370,28)`.
- Stone miss leaves HP50 at `(675780521,31)`; refresh ends `(744964398,32)`.
- Stone push leaves HP40 at target(3,1), `(2928661168,27)`; refresh ends
  `(3381560617,28)`. Distance2 now exceeds Move0+1: out-of-reach.
- Stone KO leaves HP0 at `(3914758370,28)`; selected managed admission rejects
  without consuming query RNG. The no-progress description likewise draws zero.

**ZOD-211: implement pure fresh pair post-ACT preparation.** Add an explicit
ACT-used/Move-unused entry, retaining truthful initialized-list/cache/team facts
and using cohesive inputs from ZOD-210. Reuse post-ACT refresh and shared restored
state. Factor necessary content/grant checks independently of live ACT admission;
preserve both grants, matching profiles, weapon/geometry/PA envelope and current
state guards. Return first-pass summaries/reaches plus cleared actor entries,
original-state focus/geometry and one-draw refresh diagnostics. Check actual
hit/miss/push, choice isolation, terminal rejection, exact/exhausted draw/tick
budgets, replay and rollback with a few meaningful scenarios. Stop before pair
movement scoring, scenarios, lottery, dispatch or execution. No interface or host
activation; later children connect the reusable movement/settlement kernels.

Reproduce with `verify_ai_pair_post_act_contract.py --reference C:/repositories/fft_decomp
--prepared-executable scratch/all-data-extraction/SCUS_942.21 --output <new report>`.
Two new Python scenarios validate descriptions, immutability and malformed claims;
all **534 Python tests** and the pinned source/prepared audit pass. Logs are in
ignored `scratch/ai-pair-post-act-study-209/`. Managed source/tests are unchanged; last
runtime validation remains ZOD-210's clean Release build and **4,085 .NET cases**.
No original/C execution, managed post-ACT pair execution or UI review is claimed.
