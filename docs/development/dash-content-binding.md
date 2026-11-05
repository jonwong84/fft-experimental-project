# Prepared Dash content and AI setup — ZOD-329

Implemented 2026-11-05 from managed base `3f194e6`, read-only reference clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[Dash trace](../architecture/zod-317-dash.md) and
[static/working metadata trace](../architecture/zod-326-ai-behavior-metadata.md)
remain the rule evidence. [ZOD-333](../architecture/ability-effect-families.md)
also pins the caller/data boundaries and the primary/secondary evasion distinction.

## Prepared input and reusable binding

`TargetedAbilityContentPackage` schema1 now admits `dash-flat`, range1/Y4,
zero charge/MP alongside the existing range4/Y2 `throw-stone-flat`. The JSON
fields and old Stone serialization are unchanged. The effect is an explicit
closed rule binding; an ability ID, learning record or AI flag cannot select it.
Power is effect data. Animation is outside this executable companion.

`OriginalTargetedAbilityImporter` shares the canonical metadata, job/slot,
learning and provenance checks for both effects. The old
`OriginalThrowStoneImporter` API delegates to it and preserves its records and
JSON output. Dash checks original Squire Basic Skill slot1/ability147 and every
secondary byte: formula0x37, X0/Y4, range1/area0/vertical1, flags09/11/00/00,
no element/status/charge/MP. Verified import checks the edition size/hash first;
synthetic decoding reports no edition certification. It never rewrites metadata.

Reproduce with fresh ignored destinations:

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release -- import-metadata --exe scratch/all-data-extraction/SCUS_942.21 --output scratch/dash-metadata-new
dotnet run --project src/Zodiac.AssetTool.Cli -c Release -- import-dash --exe scratch/all-data-extraction/SCUS_942.21 --metadata scratch/dash-metadata-new/content.package.json --output scratch/dash-effect-new
```

Output uses the existing `targeted.package.json` / `targeted.import.json` names.
All prepared files, raw records and inventories remain under ignored scratch.
The runtime loads prepared definitions; it never reads the executable or BIN.

## Learning, AI summaries and actual authority

`TargetedAbilityBattleSetup.CreateDashProfile` shares grant validation with Stone:
exact metadata instances, unit, action slot, executable effect and zero equipment
bonus. Stone/Dash factories reject each other's effects and mixed packages.
`OrdinaryHumanAiAbilityListSetup.Create` has an overload accepting the explicit
Dash companion while preserving the existing self/Stone overload. It maps known
slots to supported effects and carries metadata traits into owned lists. Known
slots supply AI availability; actual learned grants remain independent.

The retail Dash primary AI bytes are 82/02/01/81, so its **AI** Evadeable flag is
true. `ai_unit_abilities_init` counts it in the evasion-share numerator. Its
secondary combat flags have Evadeable clear; actual Dash still bypasses evasion,
and the non-exhaustive/consumed-aim search still follows those secondary flags.
The existing list summary already reads the primary trait. Admission now allows
both authored Dash AI-share values without changing the combat effect. Earlier
explicit semantic defaults remain available and all prior source scenarios remain
unchanged. Other unsupported targeting/search edits still reject.

The public decision/command/turn path consumes these definitions and actual typed
grants. Both ACT-first and Move-first tests run with target class evade100,
hypothetical midpoint15 for PA5, and three actual effect draws (including frozen
Move continuation). Missing grants reject atomically even with a known AI slot.
No command authority comes from a public plan or metadata alone.

Validation: 31 new .NET cases across Content/CLI cover package discrimination,
all14 secondary bytes, canonical dependency, import failure publication and both
actual turn orders. Full suite: 4,562 .NET / 603 Python, locked restore and clean
Release build. Verified prepared Dash import succeeds. Saved prepared Stone
package/provenance remain byte-identical. [Receipt](../evidence/combat/dash-content-implementation-v1.json).
No new packages, Framework rules, owner drafts, JobStats, host/editor activation,
automatic facing, corpse/terminal/frame RNG, broad interfaces or ZOD-106 changes.
