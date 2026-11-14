# Fresh both-unused pair preparation — ZOD-196

Implemented 2026-10-29 from the
[ZOD-195 source contract](../architecture/zod-195-pair-preparation.md), pinned
to clean read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.

`AttackThrowStoneAiPreparation.Evaluate` admits the original both-unused NonBlue
state, matching family profiles, actor sword ownership/envelope and explicit
Throw Stone grants for both units. The list certificate asserts exactly ordinary
Attack, learned Throw Stone, non-Frog Frog Attack and terminator for each unit;
no special behavior and clean early actor-delay cache remain separate assertions.
The raw-Move<=124 guard is the existing source compatibility envelope, not a
JobStats/content authoring limit. Clear projectile paths and terminal selection
defaults are not established by this query.

The query owns `SwordAiRefresh.EvaluateUnmoved`, then shared internal
`SwordAiPreparation.DeriveRestoredState`. The latter reads original positions,
CT, choices and full ordinary reachability. It does not adopt refresh's temporary
clock units. Old preparation entry points retain their Attack-only list guard;
the new query never asserts that guard for a two-action list. Shared metadata
retains the existing `SwordAiPreparationOutcome` type in the result's `Common`
field to avoid duplicating/renaming the public diagnostics. Matching-family
checks are shared internally with pair composition; public APIs accept no cached
outcomes, command receipts or trusted sessions.

Both unit summaries distinguish three initialized entries from two usable
harmful actions. Initialized quarter-share statistics are Silence0/MP0/evasion2,
highest MP cost0 and `LowestPaidMpCost=null` (source sentinel255). Each unit has
ranged ability and no ally healing. The result reports two enemy harmful/physical
threats. It does not invent a newly refreshed MostlySupport flag for the enemy.
Only actor second-pass entry policy is reported: Attack range1/exhaustive/no aim
consumption, Throw Stone range4/non-exhaustive/consume aim; both are instant,
zero-cost and usable. Peer consume-aim policy is not derived from the actor pass.

The prepared-data audit additionally checks menu dispatch at
`target/main.yaml::g_command_menu_types_by_skillset` (0x80065cb4): Attack skillset1
uses menu8/weapon summaries, Basic Skill5 and Frog Attack169 use menu0/default
table loading. This verifies that the non-evadeable Frog367 record actually feeds
initialization. The existing exact prepared executable hash protects those
table reads; the reference remains pinned and clean. Raw records stay ignored.

Literal default case: actor(1,1), target(2,1), HP50, CT15/95, Speed5, Move3,
ticks20, incoming RNG `(1,7)`. Refresh ends at CT55, advances one tick, picks the
peer and draws16838, returning `(1103527590,8)`. Its temporary actor CT60 is
diagnostic. Common preparation reports original actor/target delays17/1,
enemy minimum reach4, ally reach3, proximity9 and the full original movement
origins. Target stays active when originally five tiles away; minimum enemy
reach is Move+1 rather than Move+4. NonBlue team policy retains ConserveCt and
sets the actor's TeamSupportUnavailable without inventing changes to peer policy.
Preparation adds no draws after refresh.

Six scenario tests cover those literals, initialization/usage separation,
restored focus/reachability, Move0/124, policy retention, exact/short budgets,
ready-at-MAX/tick failure, list/team/choice/grant/profile admission and both-actor/
collection-order replay. Existing single-Attack preparation/selection regressions
cover the shared refactor. Any failed query exposes typed reasons only, no
partial diagnostics and the original whole-query RNG. This is managed atomic
failure policy, not original C search rewind.

Locked restore, zero-warning/error Release build, **4,030 .NET cases** across
seven projects (843 AI), **526 Python tests** and the clean inherited source/
prepared-data audit pass. Logs, `source-final.json` and seven full TRX files:
ignored `scratch/ai-pair-preparation-195-196/`. Owner content drafts participate
in validation and remain excluded from these commits. No original/C execution
or UI review; frame/VSync RNG equivalence remains unresolved.

ZOD-197 derives the fresh in-reach pair movement scenarios, including the ranged
D0 distance bound. This preparation query stops before movement scoring, D0/D1,
action scans or lottery. Scenario/selection composition, general lists/statuses,
blocked paths, live authority, replacement interfaces and presentation remain
separate work.
