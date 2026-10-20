# Managed Disc Inventory Proof of Concept

**Status:** Inventory CLI and parser are implemented. The target image was inventoried and compared; all paths/LBAs match, while seven `mixed` extracted sizes are not directly comparable with ISO logical lengths. Extraction and payload hashing remain deferred.

## Goal

Build a small managed .NET command-line utility that reads the supported local `SCUS-94221` BIN/CUE pair and writes an inventory of the ISO primary-volume file tree. The utility is an offline preparation tool. It must not be a dependency of the game runtime, invoke the C decompilation, extract payloads, or decode game assets in this milestone.

Input and generated output remain under the ignored `/scratch/` directory. The input image is private local data and must never be staged or committed.

## Evidence and reference

- The supported BIN in this workspace is documented as matching the pinned decomp target profile by size and SHA-256 in `PROJECT_PLAN.md`.
- The workspace contains `scratch/fft-clean-base-game.cue` alongside the BIN. Its text describes one `MODE2/2352` track with `INDEX 01` at `00:00:00`. The implementation must still validate this input and its relationship to the BIN before relying on that layout.
- The reference repository at `C:\repositories\fft_decomp`, commit `611b504d25d4a9b03fab8023d7dbea75fe381d5d`, delegates ISO extraction to `dumpsxiso` in `tools/main.go`. Its `readMode2File` helper uses a 2352-byte stride, a 24-byte data offset, and 2048 bytes of user data (`tools/main.go`, around lines 968-983), but does not parse the PVD or directory tree. Copilot found no call sites for that helper in `tools/main.go`.
- The decomp-generated reference inventory is already present under `scratch/fft-decomp-work/build/extracted/inventory.json`. The reference inventory is derived from `disc.xml` and extracted payload files; it is an output oracle, not an ISO parser specification.
- Reference commit and inventory details must be recorded in validation output. Do not silently update the pinned source snapshot.

## Proposed implementation boundary

Keep the parsing library in `src/Fft.AssetTool/` and put command-line file access in a separate executable host such as `src/Fft.AssetTool.Cli/`. Both should use managed .NET APIs only and depend on no C code, Docker, `dumpsxiso`, or native ISO library. Keep ISO parsing separate from `Fft.Core` and the deterministic game simulation.

### Implemented foundation

`Fft.AssetTool` contains the raw-sector reader, PVD validator, and iterative PVD directory-tree reader. `Fft.AssetTool.Cli` verifies the pinned image size/hash and target CUE layout, writes inventory/report files under `/scratch/`, and compares against the existing reference inventory. The reader has synthetic tests; the CLI has been run against the supplied BIN/CUE. The comparison results and mixed-size caveat are recorded below. Extraction and asset decoding are not part of this implementation.

Initial command shape (final CLI syntax can be chosen during implementation):

```text
dotnet run --project src/Fft.AssetTool.Cli -- inventory --bin <path> --cue <path> --output <path>
```

The reader is scoped to the verified target image profile, not arbitrary BIN/CUE formats. It must validate the expected image size/hash and the supported track layout before scanning. It must fail with a clear diagnostic when the input is not the supported single-track `MODE2/2352` layout or required Mode 2 Form 1 sectors cannot be read safely. It must not assume that `.bin` alone identifies a sector format.

### Inventory contents

For each primary-volume file record, emit at least:

- ISO identifier as recorded, preserving enough information to diagnose version suffixes and name normalization.
- Normalized relative path using `/` separators, following an explicitly tested policy compatible with the reference extractor.
- Extent LBA using the target track's logical-block convention.
- File byte length.

Also emit a schema version, target profile identifier, source image hash, and inventory count. Hashing every payload is not required for the first parser milestone; it can be added as an independently testable validation option.

The inventory is metadata only. Do not write extracted files or decoded assets in this milestone.

### Reader rules and explicit limitations

- Locate the Primary Volume Descriptor by scanning the volume descriptor sequence, validating descriptor identifiers/versions, and stopping at the terminator. Do not assume sector 16 alone proves a valid filesystem.
- Read 2048-byte logical blocks from raw Mode 2/2352 sectors only after validating the target track assumptions and Form 1 user-data layout. For the verified single-track target, the expected payload mapping is `rawOffset = lba * 2352 + 24`; encode this as a named sector-layout policy and test it.
- Validate the PVD logical block size and volume bounds before following extents.
- Recursively traverse the PVD root directory record; path tables are not required for this reader and may be omitted.
- Parse variable-length directory records without crossing a logical-block boundary. Treat zero-length records as padding to the next block. Validate record lengths, identifiers, both-endian values, extents, and byte lengths before use.
- Handle current/parent directory identifiers and directory records correctly. Establish name normalization by comparing with the decomp inventory; preserve the raw identifier in output so normalization differences remain diagnosable.
- Inventory paths are metadata, not yet safe extraction paths. Before adding extraction, check collisions using Windows path comparison rules and reject or explicitly map names that contain invalid/control characters or reserved path components.
- Reject unsupported multi-extent files and interleaved records explicitly unless implementation evidence shows the target requires them. Do not silently emit incomplete entries.
- Reject malformed records, out-of-volume extents, duplicate normalized paths, unsupported track layouts, and unsupported sector forms with actionable errors.
- PVD names are the initial scope. Joliet/Rock Ridge/Supplementary Volume Descriptor naming is not required unless the target inventory demonstrates that it is needed.

## Comparison and acceptance criteria

The inventory command is accepted when all of the following hold:

1. `dotnet build Fft.sln -c Release` and `dotnet test Fft.sln -c Release` succeed, including new focused tests.
2. Synthetic raw-sector fixtures verify sector-offset mapping, PVD discovery/validation, root traversal, nested directories, file extents, block-boundary padding, names, empty files, and exact byte lengths.
3. Invalid PVDs, truncated images, malformed/cross-block records, invalid extents, duplicate normalized paths, unsupported multi-extent/interleaved records, and unsupported sector layouts fail with clear diagnostics.
4. Running against the verified local BIN/CUE produces 2,464 file entries, matching the decomp inventory's normalized relative paths and extent LBAs. Byte lengths are compared for `type: "data"` entries; mixed/other extraction types are reported separately when the host extraction size is not semantically comparable to the ISO logical length.
5. Any mismatch is reported as a useful diff; the tool does not rewrite or modify the source BIN, CUE, or reference inventory.
6. Output is written under `/scratch/` and remains ignored by Git. No BIN, CUE, extracted payload, or generated inventory is added to tracked files.

The reference comparison is an acceptance oracle for this target image and extractor output. It does not establish that the managed parser supports every ISO9660 extension or every PS1 disc layout.

### Initial target comparison

The first managed run found 2,464 entries, with no missing or extra normalized paths and no LBA differences. The 2,457 `data` entries matched in byte length. The remaining seven reference entries are marked `type: "mixed"` in `disc.xml` and `inventory.json`. Reference `bytes` is computed from the extracted host file size, while managed `ByteLength` comes from the ISO directory record. These values are not directly comparable and should not be reported as parser length errors.

All seven mixed reference files have lengths divisible by 2,336, and their corresponding ISO directory-record lengths are divisible by 2,048 with the same sector count. Their measured size ratio is exactly `2336 / 2048 = 1.140625`. This is consistent with the reference extractor retaining 2,336 bytes from each Mode 2 sector for mixed XA/STR content, rather than extracting the 2,048-byte Form 1 payload used by ordinary ISO data. This is an evidence-based hypothesis from inventory metadata; it does not yet prove the exact byte offset, subheader treatment, or `dumpsxiso` behavior. Mixed-file extraction remains out of scope until checked against actual bytes and tool documentation/source.

### First raw-file extraction check

A local check read the 92-byte `MENU/TUTO7.SCR` entry directly from the verified BIN using its inventory LBA (72,317), Mode 2 sector payload offset 24, and ISO byte length. The resulting file exactly matched the existing decomp reference extraction by SHA-256 (`ECAD1978610D681F30A36A70F687A1B17C47AF9D04D7A61367927E77C7E05D30`). The temporary result was written under ignored `/scratch/first-raw-extraction/`. The reusable `IsoDataFileExtractor` copies ordinary Form 1 files using the directory entry's extent and exact byte length, truncates the final sector, validates Windows-safe relative output paths, and refuses to overwrite existing files. Synthetic tests cover multi-sector reads, final-sector truncation, unsafe paths, and preservation of an existing output. The reusable API was also run against `MENU/TUTO7.SCR` from the verified BIN and produced the same length and SHA-256 as the reference extraction.

The CLI supports explicitly selected `data` files with repeated `--path` options and an opt-in `--all-data` mode. Before writing, it verifies the image and CUE profile, checks the complete inventory against reference paths and LBAs, rejects Windows path collisions and file/directory conflicts across the inventory, and confirms every selected entry is marked `data`. It verifies each extracted SHA-256 against the reference inventory. Output must go to a dedicated subdirectory under `/scratch/`, and existing files are never replaced. After successful extraction it writes `assets.manifest.json` (schema version 1), with deterministic `base:<lowercase-relative-path>` IDs, original relative paths, byte lengths, and verified hashes. Mixed entries are skipped by `--all-data` and rejected if selected explicitly.

The first full `--all-data` run extracted all 2,457 ordinary files (162,790,593 bytes; about 155.25 MiB). Every file's SHA-256 matched the reference inventory, the extracted file count and total byte count matched, and no mixed XA/STR files were written. The output and command log remain under ignored `/scratch/`.

## Deferred work

- Extracting every inventory entry automatically; current CLI requires explicitly selected paths.
- Hashing every payload in the initial reader milestone (may be added as a follow-up verification mode).
- Decoding SPR, GNS, TIM/TM2, text, sound, movie, and other resource formats.
- Supporting arbitrary BIN/CUE layouts, multi-track discs, Mode 2 Form 2 sectors, Joliet/Rock Ridge, multi-extent files, or interleaved files.
- Runtime access to raw sectors. A `Fft.Content` library can now load prepared loose files by manifest ID and verify their size/hash; the console client is not wired to it yet and must work when the BIN is unavailable.

## Open decisions

- Whether the reader stores source image identity in the output schema as a single profile hash or records a broader import provenance block.
- Whether unsupported ISO record features should be rejected immediately or represented as explicit diagnostics for later support.
- Keep the parser library and CLI host as separate projects so import-time file access cannot leak into the game composition root.
