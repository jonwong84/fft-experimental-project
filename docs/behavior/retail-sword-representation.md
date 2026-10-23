# Explicit retail sword representation (ZOD-058)

Date: 2026-10-22. Read-only reference checkout `C:\repositories\fft_decomp` HEAD
rechecked as `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. All source symbols below
refer to that revision. This extends the [verified record import](retail-sword-import.md)
and [bounded sword content contract](../architecture/zod-055-sword-content.md).
It supplies validated content and setup inputs, not attack execution or retail combat equivalence.

## Version and migration decision

Keep `fft.sword-content` schema 1 readable and writable with its original shape,
ordering and constraints: no capability field, targeting exactly 128, empty proc
type 128. Existing constructors and `SchemaVersion` retain that legacy default.
New callers select version 2 explicitly; `CurrentSchemaVersion` is 2 and each
package exposes its actual `Version`. `UpgradeToV2()` creates a new package,
retains every definition and adds explicit capability value 0 to serialization.
There is no implicit upgrade or lossy downgrade. Passing a v2-only definition to
the legacy package constructor fails. Metadata packages and fixture versions are unchanged.

Schema 2 requires separate numeric `targetingFlags` and `capabilityFlags` on every
sword. Targeting remains Striking 0x80. Supported capabilities are Throwable 0x02,
TwoHandsCompatible 0x04 and TwoSwordsCompatible 0x08; `RawWeaponFlags` reconstructs
their OR with targeting. ForcedTwoHands 0x01, alternate targeting and unknown bits
fail explicitly. The enum meanings come from the `WEAPON_FLAG_*` constants in `include/fft/item.h`.
Compatibility with an ability never grants that ability to a unit: Throw, Two
Hands and Two Swords mechanics are outside this profile.

An explicit `SwordEmptyProcMode` constructor supports None 0 or AllOrNothing 128
only, with exactly five zero status bytes. The existing integer constructor keeps
its legacy type-128 restriction. Schema 2 round-trips either mode verbatim;
nonempty status sets and all other modes remain unsupported.

## Raw and effective physical evade

`src/main/party.c::unit_set_equipment_stats` copies a weapon's raw power and evade
into equipment stats. This alone does not establish the evade used by an attack.
`src/battle/formula_core.c::formula_evade_store_physical` first clears hand/accessory
evades, loads class evade, and returns early for monsters. For a human's selected
hand it uses weapon evade only if the item is a weapon **and** Weapon Guard is
present **and** `unit_get_action_restriction(target)` is NONE. Otherwise a shield
may supply that hand's physical evade. Accessories use their separate path.
Subsequent facing, Concentrate and other modifiers can further change these values.

`PhysicalWeaponEvasion.GetContribution` implements only that weapon gate for a
known human/weapon hand and explicit reaction/restriction inputs. It validates the
source byte width, consumes no RNG and does not implement shield precedence,
reaction dispatch, hit resolution or those later modifiers. Source-derived tests
cover each Boolean gate and byte boundaries with invented values.

`AttackBattleSetup` keeps schema 1's rejection of nonzero raw weapon evade.
Explicit schema 2 accepts raw evade while retaining the closed no-reaction,
no-shield/accessory profile: Weapon Guard is absent, so effective hand evade is
zero. `AttackUnitState` still rejects nonzero effective hand/accessory evade.
Capability flags never activate Weapon Guard or support abilities. The existing
PA/power signed-16 damage envelope and other setup restrictions remain enforced.
This does not derive PA/HP/job stats from equipment or implement a reaction system.

## Empty proc trace and future RNG requirement

`src/battle/formula_weapons.c::formula_weapon_damage` calls
`formula_calculate_weapon_damage`, then `formula_apply_status` if the conditional
proc succeeds. In `formula_effects.c`, weapon damage still reaches the conditional
proc roll after successful hit/element handling. Empty status bytes are not grounds
to omit that roll. `formula_apply_status_to_action` selects branches by type bits:
type 0 selects none, while type 128 ORs the zero set. Neither branch itself draws
randomness. The empty result forces a scratch miss; `formula_apply_status` restores
the hit, accuracy and miss type it saved. Preserve the original mode nevertheless;
general status application and random/separate/cancel modes are not implemented here.
ZOD-022 must execute the declared hit/proc/RNG sequence; this change does not
execute or independently validate the combined combat chain.

## Explicit verified export

The default `import-sword` remains a source-record report. Add `--metadata` to opt
into a schema 2 sidecar using an explicitly selected prepared dependency:

```powershell
dotnet run --project src/Fft.AssetTool.Cli -c Release --no-build --no-restore -- import-sword --exe scratch/all-data-extraction/SCUS_942.21 --output scratch/imported-broad-sword-v2 --metadata scratch/imported-squire-check-a/content.package.json
```

CLI input, dependency and fresh output paths must remain below `scratch/`, with
the existing reparse-point/hash/length guards. All validation completes before a
staging directory publishes `sword.import.json` and `sword.package.json` together.
Existing destinations are preserved. The selected dependency must be the baseline
`fft.ps1:package/squire-metadata`, profile `fft.ps1:profile/scus-94221`. The sword
package has its own `fft.ps1:package/broad-sword` identity and explicitly validates
that dependency. No scanning, implicit package activation or metadata mutation occurs.

`OriginalSwordContentExporter.ExportVerified` verifies the executable itself.
Its separate `CreatePackage` mapping API validates a decoded selection without
certifying its input hash, enabling synthetic parser/mapping tests. The CLI calls
the mapper only after verified import. Mapping rejects nonzero/incomplete attribute
blocks, mixed/unknown item category flags (exact Weapon 0x80 required), unsupported
weapon flags, range/formula/element and proc content. Mixed armor category flags
would select extra equipment-stat paths even with a zero attribute block in
`party.c::unit_set_equipment_attributes`; they cannot be discarded safely.
Unused/padding and learning/menu fields remain in the raw report, not fabricated
runtime semantics. Item price/shop/equip eligibility and the rest of inventory are
outside this bounded combat definition.

Converter version `fft.selected-sword/2` updates the report diagnostic to describe
optional schema 2 export. The report remains a metadata-only artifact with schema 1
and the same seven original records; it does not attest that combat executed.
Its new hash is expected to differ from the historical converter-1 report.

## Local evidence and limits

Verified input remains 356352 bytes, SHA-256
`287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
Two explicit exports under ignored `scratch/imported-sword-v2-check-a` and `-b`
produced byte-identical outputs:

- Sword package SHA-256 `ea320010ad7130a5a8359a5eb4cd36efb599df00e2ef8a8628ee62af6ea1d059`.
- Converter-2 report SHA-256 `49816a4f21ee8174fb34cf678a41eef85a44df1349f20e061e455a83e05f59e4`.

The package preserves actual power 4, raw evade 5, targeting 128 plus capabilities
14 (raw 0x8e), element 0 and empty proc type 0. A direct comparison with converter-1
output confirmed all seven raw record entries unchanged. Existing Squire package
SHA-256 `62d7a4c41e163f6f5d7514f3ef900cc51ac9724afd26255ac665f2f8d7438435`
and provenance `63aecf54bad557c9f019d36f91da175f3d52b134a3329c47dbc8b9a43fc57d9f`
remain unchanged. No raw original data or generated packages are committed.

Regression tests use invented data and cover explicit migration, strict versioned
fields, capability reconstruction, type-0 retention, unsupported exports and the
raw/effective evade distinction. Existing v1 fixtures retain their behavior; attacks
remain not-executed. These are source-derived and synthetic checks, not an
original/emulator battle comparison or full retail equipment/combat implementation.

Final Windows validation: locked solution restore, Release build with zero warnings,
and all 553 solution tests passed on 2026-10-22.
