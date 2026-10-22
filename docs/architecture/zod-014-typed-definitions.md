# Minimal typed FFT definitions (ZOD-014)

Date: 2026-10-22. This implementation adds immutable job, skillset and ability **metadata** in `Fft.Content.Definitions`. It does not add retail content, an importer, a JSON package format or combat effects. It follows the [ZOD-003 source investigation](../behavior/job-ability-slice.md) pinned to decomp revision `611b504d25d4a9b03fab8023d7dbea75fe381d5d`; there is no new claim about retail Squire row values or original-game execution.

## Ownership and identities

The existing `Fft.Content` assembly owns the definitions and catalog. No project or dependency change was needed. These FFT concepts do not enter `Zodiac.Engine`, in accordance with [ADR 0004](../adr/0004-zodiac-engine-and-fft-game-boundary.md).

`JobId`, `SkillsetId` and `AbilityId` are distinct readonly value types with ordinal string identity. They use the existing `AssetId` validation grammar (`namespace:path`, lowercase ASCII, nonempty slash-separated path segments, no `.`/`..` segment). They do not wrap original numeric indices or use display names. They cannot be assigned across definition kinds without explicitly constructing another type. Struct defaults are possible in C#, so definition/reference constructors reject them; nullable `AbilityId` is reserved for an explicitly empty skillset slot.

Source indices, record addresses, profile hashes and complete raw-record provenance are importer metadata, absent from these semantic objects. The original game table capacities impose no limit on catalog size or stable identities. They must be checked by a future original-profile importer, independently of these authored definitions.

## Definitions and preserved values

All definitions are sealed classes with get-only properties. Incoming collections are copied to `ImmutableArray`; a caller cannot mutate a definition by changing its original array/list. IDs and slot objects are immutable too.

- `JobDefinition` holds its ID, primary skillset ID, base Move (0..255), base Jump (0..127), the separate stepping-stone flag, and an ordered innate ability reference sequence. Those scalar bounds follow the selected source encodings; the original four innate slots are not a general authoring capacity limit. Repeated references remain ordered and are not deduplicated.
- `SkillsetDefinition` holds an ID and an ordered sequence of `SkillsetSlot` objects. Each slot has a nonnegative explicit integer index and an optional ability reference. Duplicate slot indices are invalid; empty slots, repeated ability references and sparse slots are valid. Input order is preserved without sorting. The original human 22-slot completeness and packed high-bit encoding are importer/profile rules; authored skillsets can exceed 22 slots. No effect category is inferred from a slot, string name or numeric source index.
- `AbilityDefinition` holds an ID, JP cost (0..65535), raw learn-on-hit rate (0..255), an entire raw type byte and exactly four copied raw AI bytes. Values above 100 in the rate and JP values above a unit's normal cap are retained rather than clamped. `SerializedClassRaw` exposes the low nibble, without assigning it the ID-range classification enum or requiring an executable handler. Unnamed nibble values remain representable as raw metadata.

`AbilityLearningFlags` names only confirmed high bits: learn on hit `0x20`, show name `0x40`, cannot learn with JP `0x80`. Boolean projections preserve the negative polarity of the JP flag. `UnknownTypeFlags` exposes unnamed bit `0x10`, and `TypeFlagsRaw` retains all eight bits. The AI bytes are opaque; there is no invented interpretation. Secondary effect records remain outside this metadata slice and must be retained by a future importer envelope rather than silently discarded. These objects alone make no byte-round-trip claim.

The source's ability-486 classification disagreement between lookup and list-filter entry points is deliberately not resolved by a single `AbilityCategory` property. A later compatibility policy must preserve each traced call site's behavior.

## Catalog validation and access

`DefinitionCatalog` snapshots all supplied collections, creates immutable typed lookups, and validates the complete supplied reference graph before construction succeeds. It rejects duplicate IDs independently for jobs, skillsets and abilities, unresolved primary skillsets, unresolved innate references and unresolved nonempty skillset references. Diagnostics name the offending owner, referenced identity and slot/innate index. Constructors also reject invalid/default IDs, out-of-width scalar values, duplicate/negative slot indices, missing collections and null definition/slot entries.

Validation fails at the first error in deterministic input order: duplicate jobs, skillsets and abilities first, then job references, then skillset references. No incomplete catalog escapes construction. There is no global mutable registration, lazy reference resolution, I/O, randomness or culture-dependent ID conversion. The public immutable arrays preserve supplied order; typed `GetJob`, `GetSkillset` and `GetAbility` methods resolve identity without exposing dictionary enumeration as an ordering contract. Missing lookups throw `KeyNotFoundException`.

An empty catalog and empty skillsets are valid metadata. Whether an ability or skillset can be used in battle needs a future rules/completeness contract; catalog validity means a structurally sound metadata graph, **not** battle-ready content or original-profile completeness. No schema version/profile package model is added before ZOD-015.

## Separation from units and saves

Definitions contain no unit HP/MP, job levels, current/total JP, learned abilities, equipment, current job assignment, effective movement, statuses or AI working state. Units and future save DTOs must retain their own state and refer to these immutable definitions by typed stable IDs. For example, `JobDefinition.BaseMove` is a source/content value; equipment and movement abilities must calculate a unit's effective movement elsewhere. `AbilityDefinition.JpCost` is not a unit's current JP balance.

No runtime model is introduced merely to demonstrate this separation. Existing simulation remains unchanged and no content definition invokes the decomp or reads a BIN/prepared asset. Future import work must retain unknown source bytes under ignored `scratch/` and verify the original edition/hash before claiming baseline row reconstruction.

## Validation

Focused command:

```powershell
dotnet test tests/Fft.Content.Tests/Fft.Content.Tests.csproj --no-restore --filter FullyQualifiedName~Fft.Content.Tests.Definitions
```

Result: **32 passed, 0 failed, 0 skipped**, .NET SDK 10.0.402 on Windows. The sandboxed .NET invocation failed during host initialization; the same focused command succeeded with the authorized host access required by the installed Windows SDK. No network restore or dependency update was performed.

Tests cover malformed and default typed identities, ordinal lookup, scalar boundaries, preservation of named/unknown flags and opaque AI bytes, collection immutability, explicit empty/sparse/repeated slots, duplicate IDs/slots, unresolved references with diagnostic context and null inputs. A complete synthetic graph with **200 jobs, 300 skillsets, 600 abilities and 30 slots per skillset** exceeds all selected original table/slot capacities, including five innate references per job. All test content is invented and no game-derived row values are committed. The coordinator owns final solution validation alongside concurrent work.
