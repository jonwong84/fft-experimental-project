# Selected original metadata import (ZOD-017)

Date: 2026-10-22. Reference checkout HEAD:
`611b504d25d4a9b03fab8023d7dbea75fe381d5d` (read-only
`C:\repositories\fft_decomp`). This implements the bounded import selected by
[ZOD-003](job-ability-slice.md), using the existing typed definitions and package
validator. It imports learning/movement metadata, not executable ability effects.

## Verified input and address mapping

The command accepts only the prepared `SCUS_942.21` executable of 356,352 bytes
with SHA-256 `287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
Those values match pinned `target/main.yaml`. The original image was verified
and extracted previously; this command needs neither that BIN nor the C checkout.
The exact executable hash is its edition gate. It rejects modified or other
edition inputs instead of applying these addresses speculatively.

The PS-X EXE header identifies initialized text/data load address `0x80010000`,
payload size `0x56800`, and header size `0x800`. The command validates the header
and complete initialized length. For each selected record:
`fileOffset = virtualAddress - loadAddress + 0x800`.
Every read must lie within initialized payload data. There is no compression,
overlay loader, runtime-memory capture, or executable invocation in this path.

The pinned source labels and layouts are:

- `g_job_data`, `0x800610b8`, stride 48 (`include/fft/job.h`). Squire index74
  resolves to address `0x80061e98`, file offset `0x52698`.
- `g_ability_human_skillset_abilities`, `0x80064a94`, stride25
  (`src/main/party.c::ability_human_skillset_t`). The job's actual skillset byte
  selects its row; no edge is substituted from a familiar name.
- `g_ability_data`, `0x8005ebf0`, stride8 (`include/fft/ability.h`). Select only
  nonzero IDs referenced by the skillset or four innate words.
- Secondary records follow `party.c::ability_get_data_and_type`, not the primary
  type nibble: default `<368` uses `g_ability_range_data` (`0x8005fbf0`, stride14);
  item368..381 uses `g_item_ability_item_ids` (`0x80061010`, stride1);
  throw382..393 uses `g_ability_throw_item_types` (`0x80061020`, stride1);
  jump394..405 uses `g_ability_jump_data` (`0x8006102c`, stride2);
  charge406..413 uses `g_ability_charge_data` (`0x80061044`, stride2);
  math/RSM414..511 uses `g_ability_math_rsm_data` (`0x80061054`, stride1).
  Secondary indices subtract each family start; default indices are unchanged.

The raw parser has no content or simulation dependency. Conversion and filesystem
orchestration live in the existing offline CLI, which already references content.
No new project or package dependency is introduced.

## Mapping, preservation and validation

`OriginalMetadataRecords` decodes exact 48/25/8-byte inputs. Job innates are four
little-endian UInt16 values; human skillset IDs use three MSB-first high-bit bytes
and 22 low bytes. Slots remain ordered, duplicated references remain duplicated,
and zero slots become explicit null. Innate zero entries are omitted from semantic
references while all four original words remain in the source record. Nonzero
innates above511 and nonhuman job skillsets fail explicitly.

Semantic definitions preserve Move as a byte, Jump's low7 bits plus its high
stepping-stone flag, primary skillset identity, ordered nonempty innates, ability
UInt16 JP cost, byte learn rate, full type flags and four raw AI bytes. No clamp to
100, deduplication of slots/innates, type-class reinterpretation, or inferred
combat effect is applied. Only referenced ability definitions are deduplicated.
The ordinary content package validator resolves all references before export.

Stable IDs identify Squire, Basic Skill when row5 is actually selected, and
Accumulate146. Other referenced abilities use `fft.ps1:ability/source-NNNN` until
separately named; original identity is still explicit in provenance. No original
capacity becomes an authored-content limit.

The separate `import.provenance.json` records edition, input length/hash, reference
SHA, converter version `fft.selected-metadata/1`, original table/index, mapped
virtual address/file offset, byte length and **every raw byte** of each selected
record. The package holds semantic definitions; provenance holds source indices
and uninterpreted data. These files must travel together for import auditing.

The job's equipment bytes0x09..0x0c, growths0x0d..0x16, evade0x19,
statuses0x1a..0x28, affinities0x29..0x2c and presentation0x2d..0x2f are opaque.
Diagnostics report that boundary, each ability's uninterpreted AI/secondary data,
unknown type bit0x10, unclassified low nibbles, and unused low two bits in the last
skillset high-byte when set. Raw bytes preserve them even when zero. Import does
not assert complete knowledge of all masks or a round-trip binary writer.

`DecodeSelection` exists for unverified synthetic layout tests and inspection;
it does not establish input edition. The command always uses `ImportVerified`,
which checks exact size and hash first. Missing/malformed input is an error,
never an invitation to fabricate a default row.

## Local workflow

Run from the repository using prepared files already below ignored `scratch/`:

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build --no-restore -- import-metadata --exe scratch/all-data-extraction/SCUS_942.21 --output scratch/imported-squire
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build --no-restore -- validate-content --package scratch/imported-squire/content.package.json
```

Use a fresh output directory. Both input and output must remain below repository
scratch; traversal and existing reparse-point paths are rejected. Existing output
is preserved. After complete decode/validation, a fresh staging directory receives
both UTF-8/LF files and is renamed to the requested destination. Failure before
publication leaves no partial package at that destination. Exit codes are0 for
success,1 for input/filesystem errors and2 for command usage errors.

Output ordering is deterministic (original slot order and numeric ability order).
Provenance includes no timestamps or destination paths, allowing byte-identical
imports in different scratch directories. The generator/converter versions are
explicit; rerun import after changing converter behavior.

## Validation and result

Two local imports produced byte-identical files in
`scratch/imported-squire-check-a` and `scratch/imported-squire-check-b`:

- Package SHA-256: `62d7a4c41e163f6f5d7514f3ef900cc51ac9724afd26255ac665f2f8d7438435`.
- Provenance SHA-256: `63aecf54bad557c9f019d36f91da175f3d52b134a3329c47dbc8b9a43fc57d9f`.

The selected Squire row resolves to Basic Skill, and the package contains one job,
one full22-slot skillset and ten distinct referenced ability definitions. It
retains22 source records: job, skillset, ten primary and ten secondary records.
An independent read compared every source record/mapped offset, every slot, job
movement fields and all ten primary ability projections with the input bytes;
no discrepancies were found. The exported package passed the shared validator.
Generated original records and provenance remain ignored and are not committed.

Windows locked restore and Release build passed with zero warnings/errors; all
379 solution tests passed. Synthetic tests cover endian/width limits, packed slot boundaries/unused bits,
duplicates/empty slots, all secondary-family boundaries, raw snapshot immutability,
determinism, header/read bounds, invalid references, exact-edition rejection,
missing files, fresh-output enforcement and scratch confinement. They require no
game files. This is data/layout verification, not combat or original-game execution.
