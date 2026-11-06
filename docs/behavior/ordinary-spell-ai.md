# Ordinary spell AI

## Source boundary (ZOD-403)

Read-only reference `11464943bbdbd2a0af022d90cb7f10455cf911df` remains clean.
The [source receipt](../evidence/ai/ordinary-spell-ai-source-v1.json) pins ten
files. This study follows `usable_abilities_build`, `ability_usable_from_skillset_add`,
`ai_ability_is_known`, `ai_unit_abilities_init`, `ai_ability_entry_load`,
`ai_ability_outcome_evaluate`, `useful_targets_filter`,
`ai_target_classify_ability_effect`, `action_hits_simulate`, `strikes_simulate`
and `ai_action_simulate_and_score` through the shared formula owners.

Human default-menu lists retain known, action-class, static-AI-usable slots whose
cost fits **maximum** MP. Current MP is checked later. Action slots are0..15;
the learned row is MSB first. Generic skillsets 5..23 map to rows 0..18. Other
owned primary/base skillsets read row 0; an unowned special skillset returns known.
This lookup differs from learn-on-hit's current-job row selection. Do not replace
either with global ability membership when repeated or secondary slots matter.

The considered entry retains primary behavior bytes 1..3 but replaces byte 4 with
working usage. Static evade-with-motion is not consume-aim. Secondary parameters
supply range/area/element/cost/CT. Half MP applies before max/current-cost tests.
The source AI CT quirk tests Monster Skill and Defend, rather than the actual
cast's Short Charge/Non-Charge bits. Persevere bypasses that CT adjustment.
Initialized Attack and non-Frog Frog Attack still count toward quarter-share
denominators. Monster Skill entries count only toward the denominator.

In the bare ordinary human profile, harmful HP spells classify enemies Useful
and allies Adverse; healing allies is Useful only below the source half-HP flag
threshold. Primary preferences determine usefulness, not damage formula kind.
Reflection, timed statuses, affinity exceptions and transformed units require
their own future profiles. The selected empty-status units have none.

Ready damage formula 0x08 in AI mode still consumes four evasion draws, while the
execution-only conditional proc consumes none. Healing formula 0x0C consumes no
formula draws. The source strike stage ignores zero-accuracy results, applies
movement-opportunity exclusions before result publication, counts included
target/strike pairs and requires one useful target. Collateral hits cost one
point (four for stat work); intended-side ordinary HP hits add zero. HP effects
still count when clamping produces no numeric change.

Absolute scoring uses HP*128/maxHP, ordinary Critical=-32/Dead=-192, seven equal
equipment terms of 51, and MP. MP is floor(mp*64/maxMP), halved **before** the
quarter-share multiplier if MP is below the cheapest nonzero ability. Each unit's
signed contribution narrows the running priority to signed 16 in source order.
Golem and equipment changes are outside the selected bare profile.

## Ten selected milestones

ZOD-404..412 implement learned-slot admission, considered usage, initialized
statistics, ordinary usefulness, shared AI hit projection, strike accounting,
absolute MP scoring, composed ready-hit evaluation and prepared metadata binding.
Each owns fresh calculations; a caller's cached result never grants authority.

This is the **ready-hit** boundary: current supplied positions/resources and
recorded movement opportunities are evaluated without predicting the intervening
clock. Full source candidate search first simulates move/act order, charge ticks,
turn opportunities, reflections, ranked aim consumption, target enumeration and
lottery. A ready-hit score alone is not a full candidate rank or decision, and
will not be inserted into `AiDecisionMaker` as one. No autonomous magic turn or
player command is activated by these milestones. Deferred statuses, corpse/
Golem/terminal/frame RNG, editor, Knight slot mapping, automatic wait-facing,
ZOD-106 and the custom cone remain separate. Owner drafts stay unchanged.

## Validation

`verify_ordinary_spell_ai.py` checks pinned source and independent learning/CT/MP
descriptions; four Python cases exercise their boundaries. This is source study,
not original or C execution. Later milestones retain Fire/Cure/elemental/learning,
movement, replay and full solution regressions.

## Learned list admission (ZOD-404)

`OrdinarySpellAiLists` resolves the original known-row lookup independently of
learn-on-hit. It preserves the source unowned-special-skillset/monster known
shortcut, validates actual requested rows, filters source action slots 0..15 and
ability IDs below 422, then admits supported default-menu ordinary spells by
static AI usability, maximum MP and known bit. Serialized class metadata is not
that ID-range filter. Current MP is deliberately absent from initialization.
Half MP is applied before the maximum comparison. Repeated references survive
in slot order, including distinct primary/secondary entries. Eligible unsupported
item/Jump/Charge and monster menus fail closed instead of vanishing.

Eight managed cases cover row aliases, absent-row failure, special known behavior,
source order, repeated abilities, static usability, max-MP/Half-MP and action
slot/ID filtering. These entries grant no search, command or projection authority.

## Considered parameters and usage (ZOD-405)

`OrdinarySpellAiParameters` copies primary semantic behavior and secondary
geometry, MP and CT independently of named working usage. Initialization clears
the three low search bits, preserving high restrictions/usability and the opaque
reserved bit. Trajectory bypasses shape/range policy; special shape forces
exhaustive origins; short evadeable range precedes caster-exclusion/zero-area
force-consume policy. This records flags only and activates no new geometry.

Half MP truncates before use. Seven cases preserve Monster Skill before Defend,
Persevere bypass, Short Charge/Non-Charge having no AI-loader effect, retained
usage, copied behavior and branch ordering. Actual cast charge planning remains
unchanged, including its different support bits. No static fourth-byte flag is
accepted as working usage.

## Initialized ability statistics (ZOD-406)

`OrdinarySpellAiStatistics` owns quarter shares and nonzero MP extrema. Its
ordinary human entry derives every selected spell from the owned list, with
bare Attack and initialized Frog Attack included. Frog's primary AI Evadeable
is clear despite its secondary combat flag; the prepared source records are
recorded separately. Current MP and later Frog usability do not change counts.
The pure statistics entry also preserves Monster Skill denominator-only behavior,
no-cost minimum 255/maximum 0, empty divisor 0 and integer operation order.

Four literal managed scenarios cover Fire's three-entry shares1/1/1 and cost 6,
Monster Skill exclusion, zero/empty lists and repeated Half-MP entries. Managed
MP costs remain nonnegative int; original imported byte costs remain exact.

## Ordinary HP usefulness (ZOD-407)

`OrdinarySpellAiUsefulness` classifies current ordinary humans and derives masks
in source-unit order. Side-only working restrictions, Dead and cannot-hit-caster
precede primary preferences. Harmful enemies are Useful even at numeric Faith 0;
formula magnitude is not the classifier. Healing uses **HP < floor(maxHP/2)**,
so HP 50/max 101 is not useful. Harmful preference wins its source branch when
both ally/enemy flags are set. Critical and Charging remain admitted; unsupported
affinities/statuses, random/stat/MP/status work fail closed. This pure stage uses
no RNG and cannot publish a hit or a target decision.

Seven cases cover enemy/allied damage, zero Faith, odd half-HP boundaries,
healing enemies, both preference flags, absolute restrictions, Dead/self exclusion
and rejection of unsupported behavior/status combinations.

## AI formula and private HP projection (ZOD-408)

`OrdinarySpellAiProjection` derives fresh ready resource work in AiSimulation,
then calls the shared `MagicCastQuery`/`FaithMagicFormula` for selected source-order
current targets. Damage consumes four real evasion draws even with bare zero
evade, but no execute-only proc draw. Cure consumes no formula draws. MP is paid
once privately, including self-target normalization. A later target's counter
failure returns entry RNG with no partial cost or results. Unsupported status,
equipment, identity, order and resource contexts fail closed.

`OrdinaryMagicHitApplication` exposes only HP/Critical/Dead projections from the
same existing internal kernel used by actual battle execution; no duplicated
damage formula, live state adoption, career, counter or learning authority is
introduced. Strike accounting still decides which computed frames are applied.
Five AI scenarios cover the literal Fire54/four-draw result, Cure54/no draws,
Critical and KO/Charging transitions, late-target rollback and admission failure.
Retained ordinary battle/application regressions pass alongside them.

## Strike accounting (ZOD-409)

`OrdinarySpellAiAccounting` follows the source's post-formula, pre-application
ordering. Zero accuracy is excluded. A followed anchor with a move opportunity
keeps only itself; otherwise harmful-only spells skip individually moved units,
while ally preferences retain them. Map-target behavior bypasses those exclusions.
Positive-accuracy included pairs add intended-side 0/contrary-side -1 (stat 3/-4),
contribute to the unsigned 16 total and require one Useful recipient. This stage
is independent of numeric HP change and consumes no RNG. Formula draws have
already occurred even for a subsequently excluded target. At 255 entries the
source disables accounting; that sentinel boundary is explicitly unsupported.

Five managed scenarios cover collateral/zero accuracy and truncated averaging,
follow versus tile motion, ally preference, untargetability/map bypass, stat
credits, empty and no-valuable-target outcomes. No supplied tally becomes live
state or a whole-candidate score; the composed query derives these facts fresh.

## Absolute state and MP scoring (ZOD-410)

`OrdinarySpellAiScoring` adds HP, ordinary Critical/Dead, all seven unchanged bare
equipment slots and MP for every unit in source order. MP scales by 64/maxMP,
then halves below the cheapest initialized nonzero cost, then multiplies by the
quarter share and divides by 4. No maxMP scores 0; no MP abilities means share 0.
Enemies negate the whole contribution. Running priority narrows after each unit,
starting with the private strike credit; the score is absolute, not HP delta.
No Golem or equipment-change term is guessed.

Ten cases cover cost equality/below-cost boundaries, odd MP rounding, no MP,
Critical/Dead, source order, strike credit and signed 16 overflow. The literal
Fire ready-hit example scores49: actor HP128 + equipment357 + remaining MP14,
minus enemy HP93 + equipment357. This remains a ready-hit diagnostic until the
full delay/movement clock and candidate comparison are implemented. All 1,263 AI
cases pass, preserving the prior decision/execution paths.

## Owned current-aim ready-hit query (ZOD-411)

`OrdinarySpellAiCatalog` binds exact executable spell objects and primary slots
to one battle definition. The generic primary-only profile rejects missing
learned rows, special/Mime aliases, foreign definitions and inferred secondary
grants. `OrdinarySpellAiReadyQuery` derives fresh initialized lists/statistics,
selected known slot, considered usage, current MP, usefulness, initial blue aim,
green area, source-order AI frames, strike accounting, included HP effects and
absolute score. Only the query's own fresh data flows between those stages.
It returns hypothetical units, diagnostics and decision RNG; no adoption path,
command, reward, EXP/JP/growth or learn-on-hit scan exists in this path.

The request explicitly supplies movement-opportunity IDs at the ready boundary.
Those facts are not predicted by this slice. It models one initial-aim hypothesis
on current positions/resources and validates blue range there; it is **not** a
stored delayed cast whose followed target can leave initial range. Pending
casts, learning reports, spent actions, unsupported statuses/shapes, foreign
catalogs, unknown slots and wrong actors fail closed. NotValuable retains formula
RNG already consumed but returns restored unit facts and no score. A failure
returns entry RNG and no partial outcome even after earlier private draws.

Twelve scenarios cover Fire 49, self/collateral area score -22, followed-anchor versus
tile exclusions after eight formula draws, zero-damage valuable hits, Cure and
no-useful-target exits, current MP, pending casts/reports, slot/aim/actor/catalog
binding, primary FollowTarget admission and late counter rollback. Complete live-field receipts remain identical.
No result is inserted into the existing decision maker as a whole candidate rank.

## Prepared metadata bridge and inspection (ZOD-412)

`PreparedOrdinarySpellAi.CreateCatalog` copies behavior and static AI usability
from the existing `AbilityMetadataDefinition` instances, retains exact executable
objects and ordered skillset slots, and binds the resulting catalog to the
prepared battle definition. No duplicate editable flag source is introduced.
The three reviewed original skillset IDs (Basic 5, White 10, Black 11) are mapped
explicitly; authored stable IDs require an explicit numeric compatibility map.
Schema1, unresolved static AI bits, missing/incorrect mappings and unrepresented
secondary grants fail closed for this query. Player package loading is unaffected.
Serialized class bits do not replace ID-range action filtering, and static
EvadeWithMotion is not working ConsumeAim or secondary combat Evadeable.
A followed-unit hypothesis now requires primary FollowTarget as well as ordinary
initial aim legality. Tile hypotheses remain separate choices.

Six Content cases cover exact metadata/definition references, map snapshotting,
static disabled usability, unknown flags, legacy and explicit source IDs.
Fifteen CLI/import cases cover all twelve elemental tiers, source MP differences,
AI versus execution evasion flags, unchanged live state/career, a newly learned
primary slot, unsupported secondary Cure and malformed inspector arguments.
The Cure/Fire player's secondary grant does not encode a separate owned learned
row/loadout. This AI bridge rejects it; it does not infer a known secondary row
from a global spell grant. Pure formula 0x0C/Cure projection is supported and player
Cure remains validated. Fresh list initialization is explicit here; retained AI
workspace refresh after learning still belongs to full decision integration.

`inspect-magic-ai --content-root <prepared-directory>` needs only prepared files
and prints independent initial-aim/ready-hit hypotheses for the first actor's
known primary slots against the scene's hostile recipient. Every case starts
from the same private state/RNG, so this is not a source-order candidate search
or lottery. [Usage and interpretation](../development/ordinary-spell-ai-inspection.md).
No command, projection adoption or runtime reference/BIN dependency is added.

## Batch validation

The [implementation receipt](../evidence/ai/ordinary-spell-ai-implementation-v1.json)
records locked restore, a zero-warning/error Release build, **5,010 .NET cases**
and **754 Python tests**. All 1,275 AI cases pass. The source audit pins ten files,
checks the structural Frog records against the verified executable hash, and
independently checks fifteen actual managed prepared-data hypotheses: Fire 1,
elemental 12 and Fire/Fire4 learning 2. Amounts, MP, absolute priorities and four
real AI evasion draws match the independent arithmetic model. The source index
also rechecks 105 core functions/eight external entries; structural coverage is
not full behavior equivalence. No original or C execution is claimed.

All four retained Windows native scenarios pass eighteen complete replay
checkpoints, movement, restart and state-preserving drawing. Their final gameplay
hashes are identical to ZOD-402, confirming this read-only AI work preserves
player casting/learning. These are programmed key/pointer routes, not physical
input or original-media fidelity checks. Ten owner-file blobs and the independent
unstaged targeting-format edit are preserved. All 467 public Framework declarations
retain matching files and existing partial parts. MP costs stay int; JobStats,
AbilityToggles, Knight EquipmentSlot intent, editor, automatic wait-facing,
full statuses/reactions/corpses/Golem/terminal/frame RNG, broad interfaces,
ZOD-106 and custom cone remain separate. Charge-clock search is the next study.

## Subsequent charge-clock work

[ZOD-413–417](ordinary-spell-ai-clock.md) now add a source-timed fixed-origin
candidate query with captured unit/tile aims, derived movement opportunities and
the unresolved source event-cap score. Both paths share private fresh admission
and stored-hit owners; the ready-hit query still models its explicit current-fact
boundary. The separate prepared delayed inspector validates timing and RNG.
Target/origin enumeration, rank insertion, lottery and autonomous magic turns
remain separate. The formatting edit mentioned in the historical checkpoint
above was subsequently approved and committed as `b7a222d`.
