# Framework public type files

Owner-approved file organization from managed base
`29645899a265760dbf184a93fc1f764aa26d5440`, following the targeted spell geometry
split. This pass applies the matching-file convention throughout
`src/Zodiac.Framework` before continuing ZOD-364.

## Layout and scope

Fifty-nine source files contained multiple public namespace-level declarations.
The pass adds 275 focused files, removes eight aggregate files and updates 51
retained files. Framework grows from 111 to 378 C# files, including its unchanged
assembly metadata. This is the same code in individually discoverable files.

There are 373 distinct public types and 375 public declarations. The two existing
partial types, `RosterDecisionContext` and `RosterDecisionResult<T>`, retain their
separate parts in matching base files and `.PlannedMove.cs` files. No partial
bodies are merged or rewritten. Small internal helpers stay with their related
owners; the eight internal declarations retain their visibility and behavior.
Nested types remain within their enclosing types.

Nine sequential folder batches cover Combat/Magic, Combat, Commands, Movement,
State, Turns, Campaign, Events and Random. Each batch is compared with its planned
text and compiled before the next batch. The remaining single-public-type files,
the earlier targeting split and assembly metadata are unchanged.

All 383 namespace-level declarations retain their signatures, attributes,
documentation comments, defaults, constructor/member visibility and complete
bodies after line-ending and outer blank-line normalization. Each declaration
retains its namespace and compilation-unit imports. Attributed enums such as
`AttackUnitStatus` keep their attributes. No symbols, constants, interfaces,
formulas, arithmetic, commands, events, state ownership or RNG ordering change.
Project files and dependencies are unchanged; SDK recursive compilation includes
the new source paths.

## Verification and records

The SDK's bundled Roslyn parser inventories namespace-level syntax rather than
splitting on text patterns. A fresh parse after all moves compares declaration
multisets, including comments, attributes and bodies, against the baseline.
It also verifies one public declaration per file and the preserved imports and
namespaces. This offline helper lives under ignored
`scratch/framework-public-type-layout/`; it adds no application dependency.

The [move receipt](../evidence/maintenance/framework-public-type-files-v1.json)
records every original/destination declaration and normalized hash. Current
coverage and active documentation links follow the new files; historical evidence
JSON, fixtures and archived planning retain their recorded paths and revisions.
No historical receipt is regenerated to make the current layout match it.

All existing managed tests, behavior fixtures, build/dependency files and other
projects are unchanged. The nine owner drafts and JobStats are preserved.
No new tests are added for a mechanical file split; existing regressions validate
the compiled result. No original rule is translated, no reference checkout is
modified or executed, and no original/C or UI execution is performed.

ZOD-364 remains the next feature. AI, Content and host organization stay separate,
as do namespace migration, behavioral refactoring, the future 1/3/5 cone and all
documented deferred work. See the [source map](../../src/Zodiac.Framework/README.md).

## Validation

Locked restore and Release build pass with zero warnings/errors. All **4,670
.NET tests** and **695 Python tests** pass. Nine feature-folder builds and the
Roslyn preservation comparison pass, along with the 58-entry coverage inventory
and 2,154 local links. Seven TRX reports and logs are under ignored
`scratch/framework-public-type-layout/`. No tests or fixtures were added or changed.

The read-only reference remains clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`; only its revision/cleanliness was
checked for this maintenance work. No original rules were studied or translated.
