# Fresh Attack / Throw Stone preparation — ZOD-195

Source investigation 2026-10-29 at clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base `692ae14`.
The [receipt](../evidence/ai/pair-preparation-contract-v1.json) pins five files /
18 functions, inherits the pair/scan/refresh receipts, and checks additional
Frog Attack semantic facts against hash-verified prepared SCUS-94221 data.
Four [isolated descriptions](../fixtures/behavior/ai-pair-preparation-contract-v1.json)
cover summary arithmetic and D0's distance bound, not refresh, spread, scored
destination selection, managed execution, C or the original game.

## Initialization differs from usability

The bounded lists for both ordinary humans contain Attack, learned Throw Stone,
appended Frog Attack367, then a terminator. `usable_abilities_build` always appends
Frog Attack. `ai_unit_abilities_init` counts all three entries before preparation
rejects Frog Attack on a non-Frog. Attack/Throw Stone are evadeable, Frog Attack
is not; none costs MP or is affected by Silence. Thus the source quarter-share
modifiers are Silence0, MP0, evasion floor(2×4/3)=2, with highest MP cost0 and
lowest MP cost255 (no paid-MP ability). Do not divide by the two usable entries
and report evasion4. A managed summary can represent absent lowest paid cost
as null, with the original sentinel mapping documented here.

The prepared executable's Frog record confirms no evasion/Silence and MP0/CT0.
Its hash is `287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`,
size356352. Raw extracted records remain ignored. Attack summary facts still
require the ordinary range1/physical/zero-cost/no-extra-effects certificate;
they are derived from weapon summaries in C, not original ability0's static flags.

## Fresh prefix and restored policy

The both-unused NonBlue order remains actor initialization/early delay cache,
fresh refresh, `ability_considerations_set`, full reachable-tile build, team
policy, target consideration, then action selection. The existing
`SwordAiRefresh.EvaluateUnmoved` has no Attack-only-list assertion and is reusable
under its unchanged living/None-or-Critical/effect-free/zero-cached-MP facts.
It refunds both unused choices, caps temporary CT at60, draws one upkeep roll
and returns diagnostics. Later preparation must read original live units/CT,
not those temporary clock units. Stale actor-delay cache, original teams/list
membership and unsupported status/behavior facts remain explicit certificates.

`ability_considerations_set` first clears usability and tests every present unit's
entries. Non-Frog skips Frog Attack; ordinary zero-cost Attack and Throw Stone
remain usable. Each unit has two harmful and zero helpful entries, can heal no
allies, and gains HasRangedAbility from Throw Stone's direct trajectory. The sole
enemy contributes two physical/harmful threats, no magical/reflectable/Earth ones.
Only allied MostlySupport is rebuilt: the actor is not mostly support. Do not
invent a freshly rebuilt MostlySupport value for the enemy's retained flag.

The second pass is over the actor only. Attack is exhaustive and does not consume
aim; Throw Stone is non-exhaustive and consumes aim because CannotHitCaster makes
its effect on self None. Initialization cleared the low runtime policy bits;
do not reuse static byte4 as runtime usage. No random-use entries gives threshold0.
Both reaches are minima: enemy Move+min(1,4)=Move+1, ally Move with no ally ability.
Throw Stone does not change the aggregate enemy minimum to Move+4. If ACT were
used, the source clears actor usability after computing reach; the next managed
child supports both choices unused only. Do not derive peer consume-aim policy
from the actor-only second pass.

Full ordinary reachability uses restored state. A single ally cannot supply
retreat support; NonBlue sets the actor's TeamSupportUnavailable, preserves the
two existing cached policies otherwise, selects manual autobattle and focuses
the only living enemy. General ANY/target0 active selection keeps the enemy
Considered/targetable even outside range. Preparation adds no draws after refresh.
Keep the existing raw-Move<=124 compatibility envelope for the later source
Move+1/spread query; this is not an authored JobStats limit.

## Scenario consequences and remaining facts

`target_reach_get` reads the focused enemy's minimum reach above. In-reach scenario1
keeps the ordinary candidate mask; out-of-reach filters toward the focus and may
draw one NonBlue reach-halving roll after movement scoring. D1 still comes from
fresh score/distance/proximity ordering. In the focused, in-reach, ACT-unused
branch D0 calls `destination_with_range_floor_select(distance-1)`. HasRangedAbility
raises a bound below3 to3 for an enemy. This relaxes the maximum-distance bound;
it does not force the unit to stand exactly three tiles away. Out-of-reach D0
copies D1 instead. Both complete destination ties still favor later coordinates.

The old `OrdinaryScenarioPolicy` explicitly excludes ranged overrides and cannot
be truthfully passed to the Attack-only scenario query for this pair. Fresh
movement scores, target distances, D0/D1, first/best/list defaults, no Defend,
clear direct paths and terminal-list facts are not all established by preparation.
No status/general list port or cached-result shortcut is justified by equal reach.

## Selected child — ZOD-196

Implement fresh both-unused NonBlue pair preparation. Admit matching family
profiles, actor sword ownership/envelope and explicit Throw Stone grants for both
units before refresh. Require both initialized lists exactly as above, no special
behavior and a clean early delay cache. Reuse fresh refresh and factor shared
restored-state reachability/policy derivation without asserting Attack-only lists.
Return immutable initialization/usable summaries, actor entry toggles, ranged
facts and common preparation diagnostics with original-state focus/delays/active
target. Preserve input state/RNG; unsupported or exhausted work returns reasons
only and incoming whole-query RNG. Check literals, grants/profile mismatch,
choice/team guards, exact/short budgets and replay with few scenario tests.

Stop before movement-score prefix, D0/D1/scenario derivation, either action scan,
lottery or command authority. A subsequent child must implement the ranged D0
branch explicitly before composing preparation with ZOD-194 selection.

## Reproduction

`verify_ai_pair_preparation_contract.py --reference C:/repositories/fft_decomp
--prepared-executable scratch/all-data-extraction/SCUS_942.21 --output <new report>`
checks pinned/inherited source, descriptions and selected semantic data. Two
Python scenarios cover literals/input preservation and malformed/tampered claims.
No new managed implementation belongs to ZOD-195.
All 526 Python tests and the clean source/prepared-data audit pass; ignored
logs/report: `scratch/ai-pair-preparation-195-196/`. Last full runtime validation
remains ZOD-194's clean build and 4,024 passing .NET cases.
