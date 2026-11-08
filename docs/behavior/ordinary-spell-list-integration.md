# Fresh spell list/scenario integration — ZOD-442

This source study maps the enclosing ownership around the
[one-slot caller](ordinary-spell-ability-search.md). It defines the context
boundary and the prerequisites for later list integration. It implements no
new managed gameplay, dispatch, insertion, lottery or commands.

Managed base: `fdbe15cb551b456b8cb35d83ed242abf05109951`, with the owner's two
uncommitted review edits preserved separately. The clean read-only reference is
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[receipt](../evidence/ai/spell-list-integration-source-v1.json) pins source files,
function ranges/hashes, managed owners and validation. The
[literal descriptions](../fixtures/behavior/spell-list-integration-v1.json) are
independent supplied-input protocol/rank descriptions; they do not execute C,
managed gameplay or the original game and are not a retail equivalence oracle.

## Outer decision lifetime

The complete supported-turn entry remains `AiTurnController`, then
`AiDecisionMaker`, selection and `AiCommandExecutor`. The ordinary spell queries
are still diagnostic entry points outside that command pathway.

Original `ai_command_build` admits compact active units, initializes actor facts,
starts private simulation, refreshes decisions, completes ability considerations,
builds movement reachability and updates target facts before `action_select`.
An already-decided continuation can reuse the original plan under its move/act
conditions. Frame suspension resumes at saved stages instead of repeating
initialization. Neither retained commands nor frame suspension belongs to the
fresh ordinary query domain.

Fresh `action_select` resets eight ranked records plus current, best, baseline
and selected rank fields, then fills foe proximity. The reset does **not** clear
the whole action payload: it writes decided0, priority-32767, hit0, default wait
facing and reflection state. Status-imposed, conditional-retreat and auto-battle
policies run before general search and can return early. They must not silently
fall through when an eventual combined adapter cannot model them.

## General search and scenario ownership

Fresh `general_select` calls `scenarios_build` once, seeds the move-only baseline
from scenario0, checks continued charging in scenario1, then evaluates the acting
unit's list in **scenario1** if active targets exist. It next runs `ranked_select`.
Only failed ranked selection invokes movement fallback in scenario0.

Scenario0 contains the full reachable domain. Scenario1 is the general candidate
domain, with the actor origin explicitly restored after construction. Scenario2
is the XOR complement of the earlier filtered scenario1 mask within scenario0;
candidate2 copies candidate1. The baseline is priority from candidate0's written
tile, marker0/hit100. It is a gate and fallback reference, not an automatically
inserted ranked entry.

**General search does not automatically evaluate a scenario2 ability list.**
`OrdinarySpellAiOriginScenario.Complement` currently allows an explicit diagnostic
selection of that constructed mask; it grants no original general-dispatch
authority. Special policy callers have their own meanings: Fight for Life and
Protect Allies use `targeted_build`, scenario2 baseline/fallback and scenario0/1
target-group searches; Retreat uses `retreat_build` and its own self/other passes.
Their masks must not be substituted with the general query's Main/Complement
masks. These policies remain outside ordinary admission.

The normal preparation/refresh, reachability, target distance, priority writes,
proximity and baseline are decision-owned, not repeated for every ability or
every unit/tile pass. Existing one-slot queries legitimately derive their own
fresh prefix. Calling those public queries repeatedly would rerun that prefix
and consume extra RNG; a full dispatcher needs shared private preparation.

## Original list versus an owned spell subset

`ai_unit_abilities_init` calls `usable_abilities_build` during initialization.
For ordinary humans the source order is:

1. Structural Attack, using weapon-derived range/element/flags (bare Attack
   defaults to range1 and enemy targeting).
2. Primary skillset's admitted action slots in source order.
3. Secondary skillset's admitted action slots in source order.
4. Structural Frog Attack, ID367, for every unit.
5. A skillset255 terminator, only when space remains.

Sixteen primary and sixteen secondary slots plus the two structural entries
fill all34 original records, so a full list has no terminator. The enclosing
scan must retain its capacity bound. These are observed original packing bounds;
they are not proposed universal sizes for managed catalogs/custom content.
Ability ID0 in the Attack entry is valid and does not terminate a list. Packed
IDs use low ten bits for ability and physical unit slot above bit9; the physical
slot is distinct from the compact unit index. Duplicate abilities from distinct
primary/secondary slots remain distinct ordered entries.

The clean ordinary path skips empty action slots and filters by static AI
usability, known actions and **maximum** MP affordability (with Half MP). Current MP is checked
later. Original sword/Materia Blade requirements, Throw inventory expansion,
Jump/Charge/Math special menus, monster fourth-slot flags and the stale considered
Faith/innate-Innocent quirk require separate facts. They cannot be generalized
from a clean default-menu spell list; current catalog restrictions remain.

Managed `OrdinarySpellAiCatalog`/`OrdinarySpellAiLists` own selected primary
default-menu spell rows. Their statistical owner separately includes structural
Attack/Frog terms. They are not a complete heterogeneous original list. Existing
`AiHumanAbilityLists`/physical selection own a different admitted semantic list.
A future bridge must account explicitly for Attack, primary/secondary ordering,
Frog restrictions, shared statistics and every unsupported entry. It cannot
append spells to an existing list and claim the original list is complete.

## Three usage phases and entry loading

`ai_unit_abilities_init` derives aggregate initialized statistics and recomputes
the low three working policy bits after masking with0xF8. Monster-skill, opaque
bit4, team restrictions and usable bits are preserved by that mask. Trajectory
entries bypass the ordinary derivation. Exhaustive shape or range+area<3 with
secondary evasion selects exhaustive scanning before force-consumption tests.
Otherwise cannot-hit-caster, enemy targeting in secondary data or zero area can
force consumption. Static AI metadata's fourth byte is not this working byte.

`ability_considerations_set` then recomputes present-unit usability from current
facts: move/act/status timing, current MP and special inventory/equipment checks.
Its actor pass builds capability/reach summaries and completes ConsumeAim for
usable entries from force, exhaustive policy, CT/deadline and self effect. An
already-taken action clears usable after those calculations. Initial max-MP
admission therefore does not establish current usability.

`ability_list_evaluate` scans from index0, stops at skillset255 or34 entries,
loads each record and calls `ai_ability_outcome_evaluate`. It does not itself
skip the load/outcome call for an unusable entry; outcome's usability/sign gate
prevents its search. A suspended outcome resumes without reloading the record.

`ai_ability_entry_load` clears considered parameters and calculator command
fields, decodes the physical slot and dispatches by menu. The default path loads
primary/static flags but overwrites considered flags4 with the **whole working
entry usage byte**, then secondary range/area/MP/CT/status/element data. Half MP,
weapon-range overrides, Persevere and the known AI charge-support quirk retain
their existing source rules. It does not reset shared best/ranked records or
spend an RNG draw itself.

Current one-slot `OrdinarySpellAiWork` rejects an unavailable or unaffordable
selected slot atomically. A later list dispatcher must classify unusable entries
before invoking candidates and continue as the source does. It must also
distinguish those skips from an eligible but unsupported entry; silently dropping
the latter could change the chosen action.

## Best, ranked slots and RNG

Within one admitted ordinary ability, unit/tile passes rebuild aim masks while
retaining one best and RNG, as ZOD-441 implements. Reflected/useful-self branches
have their own submission/reset sites. Ordinary completion or its zero-area
return submits to `ai_best_rank_insert`, which:

1. Requires the best rank strictly above the movement baseline. Proximity cannot
   rescue an equal baseline rank.
2. Requires it to beat the eighth ranked slot, then inserts at the first strictly
   worse slot, shifting later entries and discarding the tail.
3. Resets best even when either insertion gate fails.

`rank_compare` orders decided marker, signed priority and unsigned hit. Fresh
managed candidates certify marker0. Equal rank then favors strictly lower final
destination foe proximity. Within an ability an exact tie retains the earlier
best. Across submitted abilities, exact ties preserve list order **and both can
remain if there is space**; there is no deduplication. A ninth exact tail tie is
rejected when all eight slots are occupied. Hit255 can beat equal-priority hit100.

Reset payload coordinates remain unknown when a comparison would tie the fresh
reset rank itself. Existing bounded score domains avoid that read. The study
checker rejects an unproved reset-coordinate tie instead of treating every null
slot as automatically worse. General priority-wrap/retained-payload expansion
needs separate evidence.

Insertion/reset call no RNG. Refresh and movement scoring, optional non-Blue
reach adjustment, eligible candidate simulations and random-use gates own their
respective draws. `ranked_select` later uses one raw `rand()%100` draw for each
visited slot, including hit0. All failed draws with a nonzero hit entry select
the first ranked entry; all-zero hits choose fallback. Reuse existing
`AiCandidateRank`, `AiRankedInsertion` and `AiRankedLottery` in later composition,
with the same decision RNG. A baseline check alone is not actual ranked admission.

## Agreed context and next implementation

The narrow context should capture only the immutable `OrdinaryMagicBattleState`
and matching `OrdinarySpellAiCatalog`, validating reference identity of their
definitions. This prevents mixing content and state from different definitions.
It does not admit an actor, prove a profile or cache prepared work.

The request owns actor, slot, explicit diagnostic scenario and supported-profile
declaration. RNG stays an explicit input and result. The resulting intended call
is `Evaluate(context, request, rng)`. Raw immutable input ownership is separate
from private decision preparation, considered entries, target masks, priorities,
best/ranked records and RNG progress; those must not grow into public context
properties or caller-supplied candidate authority.

Bind the context to its captured state. Create a new context when commands or
clock advancement produce a new state; an old context can still support a pure
replay of that old snapshot. It cannot authorize a command against newer state.
Each call still performs fresh actor, ownership, MP, phase and profile admission.
Same definition identity alone does not establish those current facts.

**Next bounded runtime child: ZOD-443**, introduce that validated context and
migrate the ability-caller query/request to the three-argument boundary. Preserve
the owner's formatting and all behavior/RNG/rejection regressions. Keep other
query migrations, shared list preparation and actual dispatch separate unless
independently scoped. This is an API ownership change; it activates none of the
missing original list families or policies.

## Validation and remaining gates

Twenty source-only descriptions and eight Python tests cover sparse/full list
order, duplicate identity, terminator/capacity stops, physical-slot packing,
working-bit initialization/completion, general scenario/call order, baseline/
tail gates, stable exact ties, hit255 and tamper/unknown-read rejection.
The checker also verifies pinned file/function fingerprints against the clean
read-only checkout. Literal inputs deliberately exclude general statuses,
inventory, monsters and retained workspace history; supplied rank tapes do not
calculate actual ability effects.

Full current validation and owner-edit blob preservation are recorded in the
receipt and handoff. ZOD-428, full inventory groups and ZOD-432 readability review
remain open. Full lists/scenarios, magic command/turn integration, broader
statuses/equipment/terrain/mounts/pickups/Golem, terminal/corpse/frame RNG, owner
drafts/JobStats, Knight slot mapping, editor, cone and broad interfaces retain
their existing gates.
