# Default ability and menu pathway names — ZOD-444

Owner direction: replace the `OrdinarySpell` prefix with `DefaultAbility` to name
the default ability-parameter loading use case. Current AI folders are now
`DefaultAbilities`. This is a naming change; it does not expand the supported
formulas, skillsets, actors, targeting, battle domains or command authority.

The starting managed revision is `887668f9a7a846aeef7a9b31a88ab47c3c6cb03c`.
The clean read-only reference remains
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The original menu enum is in
`include/fft/command.h`, with parameter loading in
`src/battle/ai/turn.c::ai_ability_entry_load`. The
[rename receipt](../evidence/maintenance/default-ability-rename-v1.json) records
every changed code path, before/after normalized hashes, the exact substitution,
validation and preservation.

## Naming and current scope

`DefaultAbility` identifies the loader/use-case family. It is not a formula ID,
an elemental category, or a new content flag. Guts, Black Magic and Battle Skill
use the default loader in the original game, but current implementations retain
their individual supported mechanics. In particular, the renamed spell-bearing
catalog/search still admits its existing magic-focused subset. The name does
not make Guts, equipment breaks or all other default-menu abilities available
through that catalog.

The current context API is:

```csharp
var context = new DefaultAbilityAiContext(state, catalog);
var request = new DefaultAbilityAiAbilitySearchRequest(actorId, slot, profile);
var result = DefaultAbilityAiAbilitySearchQuery.Evaluate(context, request, rng);
```

Begin review at
[`DefaultAbilityAiContext`](../../src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiContext.cs),
then the request and
[`DefaultAbilityAiAbilitySearchQuery`](../../src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiAbilitySearchQuery.cs).
Shared targeting types are now `DefaultAbilityGeometry` and
`DefaultAbilityTargeting`. Source routine method names such as
`AiAbilityOutcomeEvaluate`, `OriginReachableEvaluate` and `OriginAimTilesEvaluate`
are preserved for source comparison. C# namespaces remain unchanged.

The [ZOD-445 execution rename](default-ability-execution-names.md) also aligns
the former `OrdinaryMagic` battle, prepared-content and execution-host types with
`DefaultAbility`. The encounter retains its existing magic-focused mechanics;
neither naming change turns it into a general ability battle model.

## Anticipated menu use cases

These are the owner's proposed pathway prefixes and use-case labels, mapped to
the pinned original menu values. They describe future ownership, not newly
implemented modules or independent copies of all AI/combat logic:

- `DefaultAbility` (0x00): default ability-table parameters, including the normal
  Guts, Black Magic and Battle Skill loading cases.
- `Attack` (0x08): the standard Attack command.
- `Defend` (0x0B): the Defend command.
- `ChangeEquipment` (0x0C): the Equip Change ability.
- `ItemInventory` (0x01): the Item skillset use case.
- `Aim` (0x0A): the Charge skillset use case; original source calls this menu
  `ACTION_MENU_TYPE_CHARGE`.
- `Elements` (0x04): the Elemental skillset use case.
- `Jump` (0x09): Jump abilities.
- `KatanaInventory` (0x07): the Draw Out / Sword Spirit use case.
- `WeaponInventory` (0x02): the Throw skillset use case.
- `Mathematics` (0x03): the Math Skill use case; source calls the menu
  `ACTION_MENU_TYPE_MATH_SKILL`.
- `Monster` (0x06): the monster ability/menu use case.

The source's blank/unknown slots 0x05, 0x0D, 0x0E and 0x0F retain their original opaque
status. These anticipated names do not assign meanings or behavior to those
slots. No menu values, imported flags or original data encodings are changed.

Menu handling may prepare different parameters and then converge on shared
targeting, simulation, scoring and formula owners. A skillset/menu prefix does
not require a separate implementation of every later stage. Trace each loader,
callers, flags and effects before adding the corresponding pathway.

## Compatibility and evidence

The managed rename applies the exact substitutions `OrdinarySpells` →
`DefaultAbilities`, then `OrdinarySpell` → `DefaultAbility`. It covers matching
C# type/file names, references, prepared adapters, CLI implementation classes,
tests and two Python test-class names. It preserves every existing assertion and
algorithm under that substitution; no expected gameplay vectors are regenerated.
The receipt verifies the transformation for every affected code file.

Current Markdown symbols/source links and coverage paths use the new names.
Historical snapshots retain their prose/type identities while source-link
destinations follow the corresponding moved files. Existing hash receipts and
behavior fixture JSON are unchanged. Use this rename map and the recorded Git
revision when following their older paths; historical checksums remain evidence
for their recorded snapshots.

Historical document/tool slugs such as `ordinary-spell-*.md` remain stable.
Prepared JSON/schema/asset IDs and CLI command names, including
`inspect-magic-ai` and `inspect-delayed-magic-ai`, are unchanged. C# source callers
must use the new type names; no old-prefix alias layer is retained. Other existing
ability families keep their names until separately reviewed and scoped.

Full validation and the exact renamed path counts are in the receipt/handoff.
The next checkpoint remains owner review. Full original AI breadth, actual magic
dispatch/insertion/lottery/commands, the ZOD-428 programme and full-port ZOD-432
readability review remain open; existing deferred domains keep their gates.
