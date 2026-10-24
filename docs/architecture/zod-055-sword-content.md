# Bounded executable sword content (ZOD-055)

Version follow-up: [ZOD-058](../behavior/retail-sword-representation.md) adds explicit schema 2
capabilities, empty proc modes and raw/effective evade handling. The schema 1 defaults and
synthetic profile documented below remain unchanged.

Date: 2026-10-22. The FFT-owned `SwordDefinition` supplies the immutable content selected by [ZOD-021](../behavior/ordinary-physical-attack.md). It is separate from JP/learning-only `AbilityDefinition` and `DefinitionCatalog`; neither learning definitions nor metadata package v1 become implicitly executable. No mutable combat state or attack resolution is implemented.

Source field meanings are pinned to read-only decomp HEAD `611b504d25d4a9b03fab8023d7dbea75fe381d5d`, observed again for this implementation. `include/fft/item.h::weapon_data_t` supplies byte-width range, flags, formula, power, evade, element and proc fields; `src/battle/formula_weapons.c::formula_weapon_damage` supplies formula1; `src/battle/target_panels.c::target_range_calculate_strike_lunge` supplies striking behavior; `include/fft/status.h::status_infliction_type_e` defines all-or-nothing as `0x80`. This is source-traced field interpretation, not verified retail sword values. ZOD-057 still owns item/weapon row import and verification.

## Supported definitions

`WeaponId` uses the existing lowercase stable-ID grammar and is distinct from `AbilityId`. `SwordDefinition` requires every field explicitly: type Sword3, range1, power0..255, formula WeaponDamage1, targeting flags exactly Striking128, evade0..255, element Neutral0 and a nonnull `EmptySwordAddedEffect`. The latter requires typeRaw128 and exactly five zero status bytes, snapshots its caller collection and exposes an immutable array. Unrecognized enum casts, additional targeting flags, range changes, elements or nonempty/random/separate status configurations fail by parameter name in C# and exact field path in JSON.

Power/evade retain their source byte envelope, including 0 and255. Accepting those widths does not prove every resulting combat operand is supported by a later simulator: ZOD-022 must validate its arithmetic/state configuration and must not infer a percentage cap or original item record. No implicit source index, retail Broad Sword identity, damage rule defaults or game-derived bytes are included. The sample's power5/evade0 are invented values.

## Independent sword package v1

`SwordContentPackage` uses **`format: "fft.sword-content"`, `schemaVersion: 1`**, alongside the unchanged `fft.content-metadata` v1 format. The formats reject one another; there is no silent metadata upgrade, optional sword member or changed ability representation. Existing metadata readers/writers/fixtures retain their schema and composition behavior.

Every root field is required: `format`, `schemaVersion`, `packageId`, `profileId`, `requiredMetadataPackageId`, `swords`. Each sword requires `id`, `type`, `range`, `power`, `formula`, `targetingFlags`, `evade`, `element`, `addedEffect`; that nested object requires `typeRaw` and `statusesRaw`. Integers are JSON integer values, never numeric strings, booleans, null or fractional values. The [synthetic package](../fixtures/content/sword-content.synthetic.json) demonstrates the exact wire representation and is unrelated to the behavior-fixture envelope.

The metadata dependency is an explicit package ID, different from the sword package's own ID. `ValidateAgainst(selectedMetadata)` verifies exact ordinal package and profile matches. Callers must invoke it at their composition root before using both layers; reading a sword layer only validates its local content. It neither scans directories nor selects/activates a package nor mutates the metadata catalog. This first slice defines no sword add-on override/composition system or unit-equipment assignment. Namespace ownership follows metadata's rule: every declared sword ID must use its package's namespace, with unique weapon IDs.

`ReadJson`/`ToJson` support strings; `Load`/`Save` support bounded strict UTF-8 files. Export sorts swords by ordinal ID, uses fixed field order and appends LF. Save serializes before touching disk, writes/flushed a unique sibling temporary and atomically renames over the destination, cleaning up on failure. Filesystem errors remain I/O errors. All inputs reject duplicate/unknown/missing fields, malformed/unknown versions, invalid/default/overlong IDs, null definitions and unsupported values. Empty sword arrays are valid explicit empty layers; duplicate declarations fail.

The internal `PackageJsonValidation` is shared with the metadata reader for field presence, duplicate handling, arrays, strings, integer widths and IDs. Existing metadata diagnostic text/paths are retained. Both formats share limits: 8MiB UTF-8 input/export, depth32, 10,000 declarations and 256-character IDs. The sword reader has no arbitrary reference graph or formula dispatch; its sole external association is the explicit metadata dependency.

## Validation and limits

Added tests cover C#/JSON equivalence, immutable snapshots, byte boundaries, canonical file/string round trips, strict UTF-8, format/version separation, nested effect errors, duplicate/foreign IDs, mismatched dependencies/profiles, unsupported enum/flag/effect configurations and exact package/record/field diagnostics. Existing metadata tests remain the regression coverage for the shared-helper refactor. No build/test process was run by this parallel task; the coordinating agent runs the integrated checks.

Coordinator validation (2026-10-22): Windows locked restore, Release build with zero warnings/errors and all 464 solution tests passed. A Windows newline round-trip failure was corrected by explicitly selecting LF in the sword JSON writer.

There is no C/BIOS/BIN/runtime dependency, new external package, renderer/I/O dependency in simulation or retail-content claim. These definitions describe supported content; ZOD-056 supplies state/fixture shape and ZOD-022 supplies its bounded behavior. Callers cannot execute a learning definition merely because a sword package exists.
