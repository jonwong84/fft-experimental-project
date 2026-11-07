# Targeting public type files

Owner-approved file organization, 2026-11-05, from managed base
`3d39ed24cf16fb2a0a8d1b3adf4ef333fe69c72f`.

Thirteen supporting public types move from `OrdinarySpellTargeting.cs` into
matching files under `src/Zodiac.Framework/Battle/Combat/Targeting/`. The static
calculation owner remains in `OrdinarySpellTargeting.cs`. The folder now has one
public type per file, including its enums and small request/result records.
This makes geometry, caller profiles, opaque panels, roster inputs and cast
requests individually discoverable by filename.

All 14 declarations, documentation comments, constructor visibility, member types,
defaults and algorithm text are preserved after line-ending normalization.
Namespaces remain `Zodiac.Framework`. Only compilation-unit headers change as
needed for imports. Moving `SpellTargetPanels` does not broaden its internal
constructor or map visibility. SDK recursive compilation finds the new files;
no project or dependency edits are required.

The [source map](../../src/Zodiac.Framework/README.md) records the default for
future work: public types normally have matching files; small implementation-only
helpers may stay with their owner. This change applies that default to targeting.
Other existing grouped files remain separate cleanup work. It changes no source
rule, interface, formula selection, public namespace or command/state boundary.
ZOD-364 remains next, and all deferred work and owner drafts/JobStats are preserved.

The [move receipt](../evidence/maintenance/targeting-file-layout-v1.json) records
the original path, each destination and the unchanged declaration hash. A separate
baseline/current comparison under ignored `scratch/targeting-file-layout/` checks
all 14 declarations, the retained algorithm, unrelated production files, tests,
fixtures, project/dependency files and the nine owner drafts plus JobStats.
Historical receipts retain their original file paths and hashes. No tests are
added for this file-only refactor; existing behavior regressions remain unchanged.

Validation: locked restore, Release build with zero warnings/errors, all **4,670
.NET tests** and **695 Python tests** pass. All 14 declarations and the retained
algorithm match the baseline text exactly after line-ending normalization.
Coverage, local links and owner preservation checks pass. No original/C execution
or UI review was performed. No new behavior tests or fixtures were needed.
