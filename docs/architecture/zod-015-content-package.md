# Editable FFT content package decision and prototype (ZOD-015)

Date: 2026-10-22. Recommend **a loose UTF-8 JSON file per metadata package**, using explicit typed-ID references and strict versioned readers. A test-only prototype demonstrates round-trip authoring, validation and explicit baseline/add-on composition using the [ZOD-014 definitions](zod-014-typed-definitions.md). There is no production loader/export API yet; ZOD-016 owns that implementation. The candidate format may change deliberately before that work establishes a public compatibility contract.

Implementation update: [ZOD-016](zod-016-content-packages.md) now supplies the production API, authoring/export path and CLI validation command. This note preserves the format-selection evidence and test-only prototype; use the ZOD-016 API for production callers.

This is a format investigation using invented content, not a new source-behavior investigation. Source-derived field meanings are inherited from [ZOD-003](../behavior/job-ability-slice.md), pinned to decomp `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. No retail row bytes, BIN import or original-game execution were used. A package's `baseline` role does not prove its content was imported from the original game.

## Representation comparison

**JSON:** .NET already supplies `System.Text.Json`, and the repository already uses JSON for a prepared asset manifest and synthetic fixtures. It supports nested job/skillset/ability references directly, provides explicit null slots, and needs no new dependency. Stable formatting is straightforward. Its verbosity and lack of comments are acceptable for this bounded slice; explanatory source/provenance notes remain separate metadata/docs. Select this option.

**YAML/TOML:** potentially pleasant for hand editing, but neither has a bundled parser in the current projects. Adding a dependency and settling scalar/duplicate-key interpretation adds work without a demonstrated content need. Do not select them merely for comments or shorter files. This is a repository/dependency observation, not a claim about any particular library version.

**CSV/TSV:** useful later for spreadsheet interchange, but ordered skillset slots, optional references, flag sets and multiple related collections would need extra files plus join conventions. Use a structured package as the authority rather than inventing a relational format now.

**C# definitions:** remain a useful authoring input for ZOD-016, but require a build/runtime to inspect and do not provide a stable non-code interchange format. Export typed C# objects into the same validated JSON package instead of creating a second wire contract. Database services, SQLite and archives provide no necessary capability for this prototype and remain deferred.

## Proposed layout and boundaries

Use a dedicated directory for each package, containing `content.package.json`. A prepared development tree can have an existing `assets.manifest.json`/prepared asset files alongside a separate `content/packages/<chosen-safe-directory>/content.package.json` hierarchy. Baseline and add-on package files live in different directories. Directory names are storage details; never convert an arbitrary package ID directly to a filesystem path or use directory scanning order as composition priority.

The selected enabled package list and target profile must be explicit at the composition root. Simply placing a file beside the baseline does not activate it. The prototype receives strings and explicit package objects; it performs no filesystem discovery, extraction or file writes. Production export must validate before replacing an output file, preferably with a temporary sibling plus atomic replacement; that disk-write behavior is a ZOD-016 requirement, not a feature demonstrated here.

Keep these contracts distinct:

- **Content package:** immutable job/skillset/ability metadata and references; candidate format identifier `fft.content-metadata`.
- **Prepared asset manifest:** existing logical asset-to-prepared-file mapping with file hashes. Do not add content definitions to its schema or reinterpret its schema version.
- **Behavior fixture:** `fft.behavior-fixture`, separate scenario/evidence/expected-result contract. Package round-trip tests do not establish gameplay equivalence.
- **Future saves:** unit instances, HP/MP, progression, learned slots, equipment and selected content identity; never save the package's definition object graph as mutable unit state.
- **Import provenance:** complete original records, edition verification, hashes, original indices and decode versions remain a separate importer envelope. Game-derived files stay under ignored `scratch/`. This package preserves its selected semantic fields and opaque primary type/AI bytes; it does not replace the full raw-record envelope.

No archive, compression, SQL service, renderer, BIN or C runtime dependency is required to read the candidate metadata format.

## Candidate v1 document

All objects have exact, case-sensitive property names; every listed property is required, including explicit nulls/empty arrays. The root fields, in canonical writing order, are:

1. `format`: exactly `fft.content-metadata`.
2. `schemaVersion`: integer `1`.
3. `packageId`: stable namespaced ID, such as `sample.base:package/recruits`.
4. `profileId`: explicit target profile ID, such as `sample:profile/metadata`.
5. `kind`: `baseline` or `addon`.
6. `requiredBaselinePackageId`: null for baseline; a valid explicit package ID for an add-on.
7. `jobs`: definition array.
8. `skillsets`: definition array.
9. `abilities`: definition array.

Jobs contain `id`, `primarySkillsetId`, integer `baseMove`, integer `baseJump`, boolean `steppingStone`, and ordered string `innateAbilityIds`. Skillsets contain `id` and ordered `slots`; each slot has integer `index` and nullable string `abilityId`. The spelling `index` denotes a slot's logical index, not a retail table address. Definition arrays may exceed original capacities; this format does not introduce a 160-job, 512-ability or 22-slot authoring limit.

Abilities contain `id`, integer `jpCost`, integer `learnOnHitRateRaw`, integer `serializedClassRaw`, named-string `flags`, integer `unknownTypeBitsRaw` and four integer `aiFlagsRaw` bytes. Width checks follow the existing definition constructors: JP 0..65535, raw rate 0..255, Move 0..255, Jump 0..127. Each AI byte is 0..255; the low serialized class is 0..15 and remains opaque metadata even for unnamed values. Slot indices are nonnegative Int32 values. A null ability reference is an explicitly empty slot; a missing/invalid string is not an empty slot.

The ability representation separates confirmed named flags from source bits with unresolved meaning:

- Known, case-sensitive names: `learn-on-hit` (`0x20`), `show-name` (`0x40`), `cannot-learn-with-jp` (`0x80`). The last retains its negative polarity. Unknown or repeated names are errors.
- `unknownTypeBitsRaw` permits exactly `0` or `16` (`0x10`), preserving the remaining unnamed bit. It cannot smuggle a known flag bit or a new mechanic into the package.
- Reconstruct the complete type byte as `serializedClassRaw | unknownTypeBitsRaw | namedFlagBits`. This preserves all 256 possible byte values. Opaque AI bytes remain unchanged.

An unrecognized named flag is an unsupported authoring instruction; a retained unnamed raw source bit is uninterpreted data. Accepting the latter does not mean the runtime implements an effect for it. A later schema that names this bit must migrate it explicitly and reject contradictory encodings rather than silently changing meaning.

## IDs, references and baseline isolation

Package, profile and definition IDs use the existing lowercase ASCII `namespace:path` grammar. Job, skillset and ability references are typed when constructing `DefinitionCatalog`; neither file paths, localized names, array positions nor original numeric indices establish identity. A package can declare definitions only in **its package ID's namespace**. References may point outside that namespace and resolve through the explicit selected composition; this does not authorize overwriting the referenced definition.

Load a baseline as a complete standalone metadata graph. It cannot depend on add-on-only definitions. Load an add-on into an immutable, unpublished draft because external references may need the selected baseline/other add-ons. Explicit composition requires exactly one baseline in the baseline parameter, add-ons whose `profileId` and `requiredBaselinePackageId` match it, and unique package IDs. It then constructs a new validated combined catalog. Missing references fail composition. The existing baseline object/catalog is never edited.

There is **no last-write-wins** behavior: duplicate job, skillset or ability IDs anywhere in the selected set fail. An add-on declaring a baseline ID from another namespace fails ownership validation even before composition. Multiple add-ons from the same namespace may coexist only if their declared IDs do not collide. Content additions use their own IDs; changing a baseline definition under its existing ID requires a separately designed explicit override/profile mechanism, outside v1.

The prototype's only package dependency field is the required baseline ID. It does not auto-load dependencies, infer add-on priority, fetch packages or persist a resolved dependency graph. References to another add-on require it to be explicitly selected and present; otherwise composition fails. Exact baseline content-digest pinning, cross-add-on dependency manifests, package content revisions, profile capability checks and trust/signatures are deferred. Before save compatibility or distributed packages depend on them, define those contracts rather than interpreting the baseline ID as proof of matching content bytes. File composition proves metadata isolation, not complete PS1 versus Lion War ruleset isolation.

## Strict reading, diagnostics and versioning

The prototype rejects malformed JSON, comments, trailing commas, a non-object root, duplicate object properties, unknown fields at every supported object level, missing fields, wrong primitive/container types, invalid IDs, unsupported format/version, duplicate named flags, unknown named flags, scalar overflow and duplicate slot indices. JSON numeric strings and fractions are invalid integer fields. Limits include the JSON parser's explicit depth 32; production byte/count resource budgets must be chosen in ZOD-016 instead of silently borrowing original table capacities.

Duplicate definition IDs are rejected locally even for add-on drafts. Baseline references are validated immediately; external add-on references are validated during composition before publishing a catalog. `InvalidDataException` diagnostics preserve the field context or definition owner/slot from structural validators. The prototype reports the first error; it does not collect a full authoring error list or guarantee a complete JSON pointer for every constructor failure. Those are explicit production diagnostic improvements for ZOD-016.

Unknown schema versions fail closed. No automatic migration, default filling or unknown-field ignoring is permitted. Keep schema version separate from package/content identity and profile identity. A deliberate breaking schema change needs a new version and an explicit migration/read-support decision; do not label a package editing revision as a schema upgrade. Pure value/formatting changes do not bump the schema. The repository's earlier `fft.definition-design` sample was a design illustration, not an existing production package format requiring automatic migration.

## Deterministic output

Write UTF-8 without BOM, two-space indentation, LF newlines and exactly one final LF. Use the fixed property order described above and the prototype's fixed per-definition property order; numeric fields are integer JSON values and boolean/null values are explicit. Sort the job, skillset and ability arrays by stable ID using `StringComparer.Ordinal`, independently of input collection order. Emit flags in `learn-on-hit`, `show-name`, `cannot-learn-with-jp` order. Preserve skillset **slot sequence** and innate **reference sequence**, including empty slots, repeated references and sparse explicit indices. They are content, so sorting them would change observable metadata.

During composition, sort add-ons by ordinal package ID and the resulting catalog's definition arrays by ordinal ID. Selected add-on order cannot determine precedence or results. No wall-clock timestamps, machine paths, object hashes or dictionary enumeration enter output. The prototype uses the installed `Utf8JsonWriter` encoding rules; this is reproducible output for the selected SDK, **not** a cross-implementation canonical-JSON/fingerprint standard. Define canonical bytes and a named hash separately before persistent content digests or replay/save fingerprints rely on them.

## Executable evidence and next step

The new test-only [prototype](../../tests/Fft.Content.Tests/ContentPackages/ContentPackagePrototype.cs) and [tests](../../tests/Fft.Content.Tests/ContentPackages/ContentPackagePrototypeTests.cs) contain an inline synthetic recruit/practice/focus package and a separate trainee add-on. Data stays next to its executable checks rather than being duplicated in a fixture file requiring new project-copy configuration. The sample uses raw type byte `0xff`, maximum JP/rate values, opaque AI bytes, repeated innates, explicit empty slots and nonascending slot indices. No retail values are claimed.

Focused verification on Windows, .NET SDK 10.0.402:

```powershell
dotnet test tests/Fft.Content.Tests/Fft.Content.Tests.csproj --no-restore --filter FullyQualifiedName~Fft.Content.Tests.Packages
```

Result: **22 passed, 0 failed, 0 skipped**. Tests round-trip typed values and deterministic bytes, exercise **all 256 primary type-byte encodings**, reject malformed/version/unknown-field/flag/duplicate/reference errors, and demonstrate explicit add-on selection, ownership rejection, collision rejection, profile/dependency rejection, immutable baseline and selection-order independence. Tests ran with host access needed by the installed Windows SDK; no dependency changes were performed. The coordinator subsequently verified locked restore, zero-warning Release build and all **281** solution tests after geometry integration. Linux CI was not executed locally.

ZOD-016 should turn this candidate into a production `Fft.Content` reader/validator/exporter shared by JSON and C# authoring, select production resource limits and improve field-path diagnostics. Keep the production API independent of the test-only helper and evaluate exact package-baseline digest requirements when introducing persisted/distributed content. No file on disk, BIN-derived definition, actual game battle or production loading workflow has been validated by this spike.
