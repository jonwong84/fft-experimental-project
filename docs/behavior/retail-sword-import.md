# Selected retail sword and Attack import (ZOD-057)

Follow-up: [ZOD-058](retail-sword-representation.md) adds explicit schema 2 sidecar export
and raw/effective evade semantics. The metadata-only result and converter-1 hashes below
record the original ZOD-057 milestone; converter 2 updates its diagnostic without changing raw records.

Date: 2026-10-22. Read-only reference checkout `C:\repositories\fft_decomp` HEAD:
`611b504d25d4a9b03fab8023d7dbea75fe381d5d`, rechecked before tracing. Target is
SCUS-94221. This imports the selected original records and documents their actual
values; it does not execute a battle or manufacture a runtime-supported sword.

## Verified input and selected records

`OriginalSwordImporter.ImportVerified` accepts the same exact prepared executable
as [ZOD-017](original-metadata-import.md): 356352 bytes, SHA-256
`287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
The shared `OriginalExecutableReader` checks PS-X EXE header/load/initialized-size
and maps `virtualAddress - 0x80010000 + 0x800`. All reads must remain within the
initialized payload. No BIN, C executable or running game is needed.

The importer reads exactly seven records, preserving every source byte:

- `g_item_primary_data` at0x80062eb8, stride12, item19. The name association
  `ITEM_ID_BROAD_SWORD=19` is pinned in `include/fft/item.h`.
- `g_item_weapon_data` at0x80063ab8, stride8, weapon19.
- `g_status_infliction_data` at0x80063fc4, stride6, selected by weapon proc ID.
- `g_item_attributes` at0x800642c4, stride25, selected by the primary item attribute
  ID. The full stat/movement/status/affinity block remains raw; this import does
  not derive equipped effective stats.
- `g_ability_data` at0x8005ebf0, stride8, ability0, plus its14-byte secondary record
  in `g_ability_range_data` at0x8005fbf0.
- `g_command_menu_types_by_skillset` at0x80065cb4, stride1, skillset1.

All addresses come from pinned `target/main.yaml`. The proc table's next label is
the attribute table: its768-byte extent holds128 six-byte records. The attribute
table extends2000 bytes to the human skillset table, giving80 records. Import
rejects out-of-range references rather than reading an adjacent table.

## Field and caller trace

`include/fft/item.h::item_data_t` defines bytes0..7 as palette, sprite, required
level, category flags, secondary index, item type, unused06 and attribute index;
price at8..9 is little-endian UInt16, shop availability at10 and padding at11.
Weapon records contain byte range0, flags1, formula2, unused03, power4, evade5,
element6 and proc ID7. `status_infliction_data_t` in `include/fft/status.h` is type
byte0 followed by five raw status bytes. Nothing is narrowed, clamped or discarded.

Source consumers matter: `party.c::unit_calculate_encounter_equipment` and other equipment
helpers can follow `secondary_data_id`, while `unit_set_equipment_stats`, battle
`target_range_calculate_weapon` and `action_calculate_target_result` index the
weapon table directly by equipped item ID. For this selected original item, both
indices are19. The importer explicitly checks that equality and rejects a different
synthetic mapping instead of silently choosing whichever row looks plausible.

`include/fft/skillset.h` identifies Attack as skillset1, and
`include/fft/command.h` identifies Attack menu8. The imported menu byte is8.
`action_preparation.c::action_prepare` sets the resolved Attack ability ID to0;
`action_resolution.c::action_calculate_target_result` copies its range data but
the Attack switch chooses **the weapon's formula**. Ability0's secondary formula
byte is13, whereas the selected sword formula is1. Treating ability0 as a generic
default-menu executable ability would therefore select the wrong formula.
Primary ability0 learning/type and AI fields are retained without inventing a
normal learnable ability or assigning the raw zero class a guessed meaning.

## Retail values versus the synthetic profile

The verified selected record has Sword type3, range1, formula1, power4, evade5,
neutral element0 and proc index0. Its flags are0x8e: Striking0x80 plus Throwable
0x02, Two Hands-compatible0x04 and Two Swords-compatible0x08, as named in the source
weapon flag enum. These capability flags are not proof a support ability is active.
The attribute selector is0; its25 raw bytes are all zero in this verified input.
Proc0 has type0 and five zero status bytes.

[ZOD-021](ordinary-physical-attack.md) deliberately supplied power5, evade0, exact
flags0x80 and an empty proc of type0x80. Four differences are reported individually:
power, evade, raw weapon flags and proc type. The existing synthetic fixtures remain
unchanged. Their25-damage example is not retroactively labeled a retail Broad Sword
result. Zero status bytes do not authorize replacing the proc type0 with128.

At ZOD-057, `SwordDefinition` required flags exactly128 and empty proc type128;
`AttackBattleSetup` additionally excluded nonzero weapon evade. That milestone's export
was explicitly **metadata-only**, with no runtime sword package. It includes the
decoded item/weapon/proc/ability/menu values, complete raw attribute/secondary data,
record identities/indices/offsets/raw bytes, edition/input hash, pinned reference,
converter version `fft.selected-sword/1` and diagnostics. Raw unused/padding bytes
are preserved, including weapon unused03; no meaning is guessed for them.

ZOD-058 now represents these verified differences and their supported setup
conditions in an explicit version. ZOD-022 may still implement its declared synthetic scenario. Neither
import nor metadata representation proves equipped-stat derivation, complete
attack execution, or a retail battle's RNG ordering.

## Workflow and validation

```powershell
dotnet run --project src/Fft.AssetTool.Cli -c Release --no-build --no-restore -- import-sword --exe scratch/all-data-extraction/SCUS_942.21 --output scratch/imported-broad-sword
```

The existing import command's guards/publication path is shared: input/output stay
below ignored repository scratch, existing outputs and reparse paths are rejected,
and a fresh staging directory is renamed only after complete decode/validation.
Output is `sword.import.json` with fixed order and UTF-8/LF. Missing/malformed input
or unsupported mappings produce diagnostics and no destination report; modified
executables fail the exact hash gate. Exit0 means a successful metadata import,
not a runtime-readiness or combat-equivalence assertion. Usage errors return2;
input/filesystem errors return1.

`DecodeSelection` remains available for synthetic parser tests. Its export has
null edition/hash and confidence `unverified-layout-decoding`; only `ImportVerified`
adds verified input identity. The command always uses that verified entry point.

Two local imports under `scratch/imported-sword-check-a` and `-b` produced identical
SHA-256 `6e4be4cee233009cb31e338e36f6d7dd86b9bbd118004e3acb0a1eeef5f8fa1f`.
A separate read checked all seven raw records/address mappings and every decoded
item/weapon field against the input; there were zero discrepancies. Generated
records and reports remain ignored. No raw retail rows are committed in tests.

The shared-reader refactor preserved the existing Squire import byte-for-byte:
package hash remains `62d7a4c41e163f6f5d7514f3ef900cc51ac9724afd26255ac665f2f8d7438435`
and provenance hash `63aecf54bad557c9f019d36f91da175f3d52b134a3329c47dbc8b9a43fc57d9f`.
Synthetic tests cover fixed-length/endian/byte fields, retained unknown bytes,
immutable snapshots, selector/record boundaries, unsupported index disagreement,
header/hash rejection, deterministic export and scratch confinement. Windows locked
restore, Release build with zero warnings/errors and all 519 solution tests passed.
