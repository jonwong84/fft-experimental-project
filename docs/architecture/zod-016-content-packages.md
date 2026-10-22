# Shared content validation and C# export (ZOD-016)

Date: 2026-10-22. `Fft.Content.ContentPackages` now implements the strict JSON metadata contract selected by [ZOD-015](zod-015-content-package.md). The production implementation is independent of the test-only prototype. Hand-edited JSON and trusted C# authoring converge on the same immutable definitions, package validation and deterministic output. This supplies metadata, not executable ability effects, verified retail content or an original-game importer.

## Public API and validation stages

`ContentPackage` accepts package/profile IDs, `ContentPackageKind.Baseline` or `.Addon`, nullable required baseline package ID, and collections of the existing immutable `JobDefinition`, `SkillsetDefinition` and `AbilityDefinition`. Get-only properties expose these values and immutable snapshots of each collection. Definitions remain owned by `Fft.Content`; Zodiac does not learn FFT content formats or call these file APIs.

- `ReadJson(string)` reads the strict v1 format into typed definitions and validates the package.
- `Load(string)` performs a bounded, strict UTF-8 file read and calls the same reader.
- `ToJson()` writes deterministic JSON from the validated immutable package.
- `Save(string)` serializes before touching disk, writes a temporary sibling, flushes it, then renames it over the destination.
- `Compose(ContentPackage baseline, params ContentPackage[] addons)` explicitly resolves a selected package set into a new `DefinitionCatalog`.

The C# constructor and JSON reader share package-level ownership, duplicate-ID, resource-budget and reference validation. The JSON reader additionally checks wire types, field presence, duplicate properties and allowed field/flag names before constructing definitions. Existing typed definition constructors still enforce their own scalar/structural invariants when an author constructs them directly; errors that occur before calling the package constructor remain their normal argument exceptions. No authoring program is loaded as a plugin, and reading exported JSON never executes C# authoring code.

A baseline must have a null dependency and resolve all its references without add-ons. An add-on must name its required baseline package ID; it can be saved as a metadata draft whose external references resolve only during explicit composition. Such a draft is not a complete battle catalog. Composition requires the correct package role, matching profile/baseline ID and unique package IDs, rejects duplicate declarations across the selection, validates every reference and returns an immutable combined catalog. No existing baseline object is mutated. Package declarations must use their package ID's namespace; there is no last-write-wins override.

The only version accepted is `format: "fft.content-metadata"`, `schemaVersion: 1`. Missing fields, unknown fields, unknown/case-changed flag names, repeated named flags, duplicate JSON properties, wrong primitive/container types, invalid stable IDs, out-of-width integers and unsupported versions fail closed. Integer fields do not accept numeric strings, fractions or exponential notation. Explicit null is legal only for the baseline dependency and empty skillset ability slots. Slots and repeated innate references retain their order.

Known flag names represent bits `0x20`, `0x40` and `0x80`; `unknownTypeBitsRaw` preserves only unnamed bit `0x10`, while `serializedClassRaw` preserves the low nibble. This retains the entire original primary type byte without pretending an unrecognized named instruction is supported. AI flag bytes remain opaque. Source indices and complete original record bytes remain separate future importer/provenance concerns, as described by [ZOD-003](../behavior/job-ability-slice.md), pinned to decomp `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. This task introduces no new source-behavior equivalence claim.

## Diagnostics

`ContentPackageValidationException` carries nullable `PackageId` and `RecordId`, a nonnullable `FieldPath`, a descriptive `Message`, and an optional inner exception. The reader captures available package and record strings before checking their object's fields so a malformed field can still identify its owner. A syntactically malformed document or an oversized file may have no trustworthy package/record identity; those properties remain null rather than being invented.

Examples of exact paths are `$.jobs[0].baseMove`, `$.jobs[0].primarySkillsetId`, `$.skillsets[0].slots[0].abilityId`, and `$.abilities[0].flags[0]`. Array indices describe input sequence positions, not original table indices. Unknown fields report their own path, duplicate declarations report the repeated record's `.id`, and unresolved references report the owning field/slot/innate index. Composed-catalog errors retain the originating package and local input path. Package-wide resource errors use the collection or root path.

The API reports the first validation failure. It does not silently fix input or collect a complete editor error list. File-not-found, permissions, sharing and other filesystem failures remain ordinary I/O/access exceptions, separate from invalid content. Callers can display structured validation context without parsing exception message text.

## Explicit resource budgets

These limits are public constants on `ContentPackage` and apply to authored and file-authored input as appropriate. They are tooling safety budgets, not original-game capacities:

- `MaxJsonBytes`: **8 MiB** of UTF-8 input or exported output, including the final LF on output. String reads check UTF-8 byte count before parsing. File reads check length then read bounded chunks and recheck the cumulative count, so growth during a read cannot bypass the limit. Invalid UTF-8 bytes are rejected.
- `MaxJsonDepth`: **32**, enforced by the JSON parser.
- `MaxDefinitionsPerKind`: **10,000** jobs, skillsets and abilities individually, for both a package and a composed catalog.
- `MaxSlotsPerSkillset`: **4,096** ordered slot entries, including empty slots.
- `MaxInnatesPerJob`: **4,096** innate references.
- `MaxTotalReferences`: **100,000**, counting each job's primary reference, every innate entry and every skillset slot, including empty slots. Enforced per package and across composition.
- `MaxPackagesPerComposition`: **128**, including the baseline.
- `MaxIdLength`: **256** characters for package/profile/definition IDs and references, in addition to the existing ASCII ID grammar.

The package constructor enumerates each definition collection only into a bounded snapshot. JSON arrays are checked before allocating per-record models. An authored package within the structural budgets may still produce more than 8 MiB of pretty-printed output; the bounded writer rejects that export before any destination file is touched. Budget changes need deliberate review, but no new schema version is implied solely by raising a local tooling limit. Original-profile import limits still require their own evidence-backed validation.

## Deterministic export and replacement

Output uses UTF-8 without BOM, two-space indentation, an explicitly configured **LF** newline on every platform, and exactly one final LF. Properties use fixed order. Definition arrays sort by ordinal stable ID; flag names use fixed bit order (`learn-on-hit`, `show-name`, `cannot-learn-with-jp`). Skillset slots and innate references retain their original sequences. Composition sorts selected add-ons and resulting definition arrays by ordinal IDs, so caller selection order never establishes override priority.

No timestamps, machine paths, dictionary iteration or object hash codes enter exported bytes. This is deterministic output under the selected .NET SDK, not a cross-implementation canonical JSON or persistent fingerprint contract. Baseline content digests, content revisions, save compatibility and distributed package trust remain separate decisions.

`Save` first completes bounded serialization. It then uses `FileMode.CreateNew` for a uniquely named sibling temporary file, flushes the bytes to disk, closes it and calls same-directory `File.Move(..., overwrite: true)`. Validation/export errors therefore leave an existing destination untouched, and a failed write/rename also leaves it in place. Cleanup attempts remove only the temporary sibling; cleanup I/O/access errors do not mask the original failure. Destination directories must already exist. Replacement relies on the filesystem's same-directory rename semantics; it is not a multi-file transaction or a guarantee against storage failure/power loss. No target file is truncated before validation or rename.

## Authoring and runtime boundary

A trusted local author can construct typed definitions, construct a `ContentPackage`, and call `Save`. A user can hand-edit the same JSON and call `Load` to obtain equivalent typed metadata. Consumers use the exported definitions/catalog, not an authoring assembly. Neither path needs a BIN, the C decomp, game media, an archive loader, a database or new package dependencies. Unit HP/MP, progression, learned slots, equipment and effective movement remain absent from definitions and package state.

For example, a trusted authoring program can export this entirely invented metadata:

```csharp
using Fft.Content.ContentPackages;
using Fft.Content.Definitions;

var abilityId = new AbilityId("sample:ability/focus");
var skillsetId = new SkillsetId("sample:skillset/practice");
var package = new ContentPackage(
    "sample:package/recruits", "sample:profile/metadata", ContentPackageKind.Baseline, null,
    [new JobDefinition(new JobId("sample:job/recruit"), skillsetId, 3, 2, false, [])],
    [new SkillsetDefinition(skillsetId, [new SkillsetSlot(0, abilityId)])],
    [new AbilityDefinition(abilityId, 25, 0, 0x41, [0, 0, 0, 0])]);
package.Save("content.package.json");
```

A consumer then loads only the exported data, without running that authoring program:

```csharp
DefinitionCatalog catalog = ContentPackage.Compose(ContentPackage.Load("content.package.json"));
JobDefinition recruit = catalog.GetJob(new JobId("sample:job/recruit"));
```

The existing prepared asset manifest and behavioral fixture formats are unchanged. Exported definitions do not establish original-profile completeness or combat readiness. Unknown source bits remain data; source-derived effects and retail-row validation are still required before original-compatible gameplay can use them.

## CLI and integrated validation

The shared validator is exposed by `Fft.AssetTool.Cli`:

```text
validate-content --package <baseline.json> [--addon <addon.json> ...]
```

It returns 0 for a valid explicitly composed catalog, 1 for invalid content or file access failure, and 2 for command usage errors. Content diagnostics print package, record and field context. This route works without locating a repository root and does not load authoring code or game media.

The coordinator verified locked restoration, a Release build with **0 warnings and 0 errors**, and **309 passing solution tests**: 81 Content, 17 CLI, 123 Core, 22 Engine and 66 AssetTool. Production acceptance coverage includes C#/JSON parity, all 256 type-byte values, deterministic export/composition, strict input diagnostics, count/UTF-8 byte/ID budgets, round-trip file loading and failed replacement preserving existing output. A real CLI invocation from `C:/` returned 0 on the committed synthetic package and 1 on invalid synthetic data, with the expected package, record and `$.jobs[0].baseMove` path. Linux CI was not executed locally.

The production package implementation does not reference the old test-only helper. No game-derived example or production importer is included.
