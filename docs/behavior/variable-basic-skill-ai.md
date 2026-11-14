# Variable supported-list AI — ZOD-265–270

Implemented 2026-11-04 from managed base `1136eec`, following the
[ZOD-264 study](../architecture/zod-264-variable-action-lists.md) and
[six-ticket design](../architecture/zod-265-variable-search-extension.md).
Read-only fft_decomp was checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/variable-search-source-v1.json) pins 42 functions
across 14 files; the
[implementation receipt](../evidence/ai/variable-search-implementation-v1.json)
records validation. No original or C program was executed.

## Owned preparation — ZOD-265

`BasicSkillAiListPreparation` consumes owned `AiHumanAbilityLists`. It admits
Attack, zero or more supported self/Stone occurrences in source order, then
ordinary unusable Frog Attack. It preserves source/skillset/slot provenance and
duplicates. Original-compatible traversal is limited to34 entries here, without
limiting managed catalogs. Canonical preparation retains its four-entry guard;
no old certificate bypasses the new owned-data requirement.

One refresh supplies common focus/cache/active-target preparation. Separate
state admission avoids invoking absent abilities. Each listed action must have
its executable grant; repeated occurrences need no duplicate grant, and omitted
AI actions may still have gameplay grants. The existing Content setup can supply
these lists, including metadata-disabled self actions. No content schema or
learning model changes were needed.

Initialized counts include Frog; usable harmful/helpful summaries exclude it.
Peer threats derive from the peer's own list. Helpful share is
floor(helpful*128/(helpful+harmful)); the source threshold77 and lowest-share
ally override leave the sole allied actor's final MostlySupport false, including
self-heavy lists whose share exceeds77. The peer's retained flag remains unknown.
Attack fixes hostile reach at Move+1; ally reach is Move with or without self.
Per-occurrence runtime policy and reaches are derived before post-ACT clears
actor usability. Preparation adds no self usefulness coins.

## Shared fresh scenarios — ZOD-266

`BasicSkillAiListScenarios` owns preparation and invokes the existing
`AiMovementScenarios` once. Stone presence controls the ranged D0 floor; absent
Stone removes it. Source masks, actor insertion, team adjustment and written
versus unwritten priorities remain shared. There is no public cached prefix input.
For Move3, adjacent focus and word1/count7, the14 scored tiles end at
word2111915288/count22. Without Stone D0 is(2,2), versus(1,3) with it;
both profiles retain D1(1,4).

## Occurrence composition — ZOD-267

`BasicSkillAiListComposition` visits every initialized occurrence in order and
skips unusable ones before command assignment. Attack, self and Stone use the
existing entry/scoring/ranked-insertion owners and one shared private movement
prefix. Diagnostics retain each occurrence's entry/exit RNG and inserted rank.
Absent actions have no fabricated legacy outcome. Every hypothetical self
projection starts from actual PA; two entries can both project PA6, never PA6
then PA7. Every Stone occurrence performs a fresh aim pass, including duplicates
encountered after the eight ranked slots fill.

Readable cast keys extend only to range1 without Stone and range4 with Stone.
Fresh writes or owned retained knowledge resolve them; unknown readable keys
reject. Unread actor keys require neither guessed zero nor rescoring. After ACT,
no action reads cast keys. Whole-query failure restores incoming RNG and workspace.

## Terminal selection — ZOD-268

`BasicSkillAiListSelection` uses `AiRankedLottery` over the shared eight slots
and retains selected occurrence identity. Equal duplicate candidates can occupy
multiple slots. Ten listed Stone occurrences still evaluate ten times even when
only the first eight enter ranks. The last entered usable command kind is
derived from traversal, including unvaluable entries; the unusable final Frog
does not overwrite it. No universal terminal-Stone assertion is required.
Unrepresented NoDefend and ordinary selection defaults remain explicit.
Chosen actions carry their own typed payload; an all-empty lottery takes eight
draws and uses D0. Late lottery failure publishes no partial output or knowledge.

## Actual first command — ZOD-269

`BasicSkillAiListCommandExecution.Execute` privately selects from the current
Framework callback state/RNG. Canonical and variable entries share the extracted
typed proposal mapper for Move/Attack/Stone/self/Wait. Framework remains the
authority for actual rules, paths, RNG adoption, continuations and receipts.
Pure diagnostics cannot be submitted as executable plans. Only an accepted
actual command publishes successor priority knowledge.

For duplicated Stone, Move0, target(5,1), evade97 and word27/count7, selection
ends word3910582746/count22 and chooses occurrence2. The actual evasion then
misses and ends word3526304166/count26, leaving HP50 despite projected damage10.
Only one actual Stone command runs.

## Complete turns and recovery — ZOD-270

`BasicSkillAiListTurnExecution` composes actual commands, the existing frozen
Move continuation and actual-result facing. It shares stage routing with the
canonical driver. ACT-first refreshes actual boosted/damaged state exactly once
through `ExecuteAfterAct`: retained summaries/reach, all actor entries skipped,
fresh movement, eight empty lottery draws, remaining Move/END and facing.
No projected PA/HP/CT is adopted and no next-turn clock runs.

The opaque recovery cursor freezes immutable configuration, accepted workspace,
the exact returned session and existing continuation. Retry runs only the
failed stage; an equal replay is a foreign session and rejects. Late retained,
post-ACT or facing failure preserves already accepted effects/events/RNG and
knowledge. A failed private first command restores the entire incoming state.
Lethal ACT-first stops at unsupported post-ACT terminal input; lethal Move-first
stops at the existing facing boundary, preserving actual KO progress.

Literal complete-turn fixtures start at word1/count7, PA5, targetHP50 and
actorCT15 unless stated. All settle facing without advancing ticks:

- Attack/Frog, Move0, evade0: word675780521/count31, HP40, PA5, CT35.
- Attack/self/self/Frog, Move0: word2473755763/count29, HP50, PA6, CT35.
- Attack/Stone/self/Frog, Move0: word1116175964/count34, HP50, PA6, CT35.
- Repeated self at PA99: word1343201072/count30, HP50, PA99, CT35.
- Repeated Stone, Move0, evade0, target(5,1): word4015624929/count39,
  HP40, PA5, CT35.
- Attack/Frog, Move0, target(6,1): word180171308/count18, empty END,
  HP50, PA5, CT55.

These literals follow pinned source call ordering and independent LCG arithmetic;
they are managed regressions, not observed original-game executions. The older
source-only16 list descriptions deliberately do not claim full RNG costs.

## Validation and limits

Twenty-six new managed cases cover preparation, asymmetric lists, missing grants,
AI-disabled metadata, occurrence order/coins/aim/ranks, range-aware provenance,
literal streams, exact/late budgets, actual results, post-ACT clearing, all
recovery stages, terminal boundaries, both teams/actors and immutable replay.
Canonical query and complete-turn parity retain the previous APIs and streams.

Locked restore and Release build pass with zero warnings/errors;
**4,298 .NET cases** (1,467 Framework, 1,086 AI) and **542 Python tests** pass.
Pinned source fingerprints, variable-list descriptions, inherited prepared-data,
algorithm-guide and coverage checks pass. Seven full TRX reports and other logs
are in ignored `scratch/variable-search-265-270/`. Python uses the
[existing Pillow setup](accumulate-ai.md#validation). Historical checkpoint
snapshots and UI were not rerun.

The domain remains two living ordinary opponents, supported sword/self/Stone
effects, flat maps, admitted Blue/NonBlue policy and explicit missing context.
The [ZOD-271–276 follow-up](ai-roster-targeting.md) now supplies shared read-only
roster targeting queries and pair consumers; full-roster command/search remains open. Broader
learning/menus/effects/maps/policies, Defend, terminal encounter routing, original
frame/VSync RNG and host activation remain open. Owner definition drafts,
AbilityToggles consolidation, editor work, replacement interfaces and ZOD-106
remain deferred. No project/lock/package/dependency boundary changes.
