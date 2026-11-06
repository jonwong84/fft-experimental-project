# Ordinary spell AI

## Source boundary (ZOD-403)

Read-only reference `11464943bbdbd2a0af022d90cb7f10455cf911df` remains clean.
The [source receipt](../evidence/ai/ordinary-spell-ai-source-v1.json) pins nine
files. This study follows `usable_abilities_build`, `ability_usable_from_skillset_add`,
`ai_ability_is_known`, `ai_unit_abilities_init`, `ai_ability_entry_load`,
`ai_ability_outcome_evaluate`, `useful_targets_filter`,
`ai_target_classify_ability_effect`, `action_hits_simulate`, `strikes_simulate`
and `ai_action_simulate_and_score` through the shared formula owners.

Human default-menu lists retain known, action-class, static-AI-usable slots whose
cost fits **maximum** MP. Current MP is checked later. Action slots are0..15;
the learned row is MSB first. Generic skillsets5..23 map to rows0..18. Other
owned primary/base skillsets read row0; an unowned special skillset returns known.
This lookup differs from learn-on-hit's current-job row selection. Do not replace
either with global ability membership when repeated or secondary slots matter.

The considered entry retains primary behavior bytes1..3 but replaces byte4 with
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

Ready damage formula08 in AI mode still consumes four evasion draws, while the
execution-only conditional proc consumes none. Healing formula0C consumes no
formula draws. The source strike stage ignores zero-accuracy results, applies
movement-opportunity exclusions before result publication, counts included
target/strike pairs and requires one useful target. Collateral hits cost one
point (four for stat work); intended-side ordinary HP hits add zero. HP effects
still count when clamping produces no numeric change.

Absolute scoring uses HP*128/maxHP, ordinary Critical=-32/Dead=-192, seven equal
equipment terms of51, and MP. MP is floor(mp*64/maxMP), halved **before** the
quarter-share multiplier if MP is below the cheapest nonzero ability. Each unit's
signed contribution narrows the running priority to signed16 in source order.
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
shortcut, validates actual requested rows, filters source action slots0..15 and
ability IDs below422, then admits supported default-menu ordinary spells by
static AI usability, maximum MP and known bit. Serialized class metadata is not
that ID-range filter. Current MP is deliberately absent from initialization.
Half MP is applied before the maximum comparison. Repeated references survive
in slot order, including distinct primary/secondary entries. Eligible unsupported
item/Jump/Charge and monster menus fail closed instead of vanishing.

Eight managed cases cover row aliases, absent-row failure, special known behavior,
source order, repeated abilities, static usability, max-MP/Half-MP and action
slot/ID filtering. These entries grant no search, command or projection authority.
