# ZOD-030 — Prepare and load MAP003

The offline tool prepares the base day/dry/default MAP003 scene selected by
[ZOD-029](../architecture/zod-029-map003-source-map.md). Runtime loads immutable
geometry, indexed textures, palettes, frozen presentation metadata and two terrain
layers from three prepared files. This is a loading milestone; ZOD-031 owns rendering
the map with the prepared Chocobo pose.

Evidence is source-derived plus synthetic tests and local deterministic conversion.
Reference checkout: `11464943bbdbd2a0af022d90cb7f10455cf911df`, verified 2026-10-23.
The source map records the traced functions, structures, arithmetic and limitations.
No executing C/emulator comparison or original-rendering equivalence is claimed.

## Prepare and load locally

After the verified SCUS-94221 ordinary-file extraction, run from the repository root:

```powershell
dotnet run --project src/Fft.AssetTool.Cli -c Release --no-build --no-restore -- prepare-map --gns scratch/all-data-extraction/MAP/MAP003.GNS --mesh scratch/all-data-extraction/MAP/MAP003.9 --texture scratch/all-data-extraction/MAP/MAP003.8 --output scratch/prepared-map003
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --content-root scratch/prepared-map003 --map base:map/003/day-dry-frozen
```

Build first using the locked restore/Release commands in the README. Inputs and output
must be below repository `scratch/`; reparse-point paths are rejected. Choose a fresh
output directory. Existing output is preserved. The command verifies exact lengths and
SHA-256 identities before parsing:

```text
MAP003.GNS   568     3df4a492a1ae6c6aca645a9b58b5984cb8dbf13b2631a3819215d8a516327d57
MAP003.9     41852   227172783f50289e4a7425a63966fad7cbfd4599af80b76870ea094ff2f56ff4
MAP003.8     131072  746af063e812f591e8c72f0510764316e005621eab73ae5fbe99565eac9fa5a4
```

The selected GNS rows must resolve to texture LBA 12658/read length 131072 and mesh
LBA 12722/read length 43008. Mesh parsing uses its actual 41852-byte file length,
avoiding the original sector-rounded overread. The selected file rows, 20-byte
separator and bounded 0x85/0x88/0x8a command sequence are validated, including the final
command ending at EOF without a zero terminator. This is a bounded selection reader,
not a general GNS interpreter or a choice of arbitrary weather/arrangement.

Output is staged in a unique sibling directory, loaded through the runtime validator,
then published as the requested directory. It contains only:

- `map.json`: asset `base:map/003/day-dry-frozen`.
- `texture.indices`: asset `base:map/003/day-dry-indices`.
- `assets.manifest.json`: versioned paths, byte counts and SHA-256 identities for both assets.

CLI exits: 0 success, 1 unsupported/corrupt input or file errors, 2 command usage errors.
All inputs, output and any derived assets stay ignored; none belong in Git.

## Ownership and prepared format v1

`Fft.AssetTool.OriginalBattleMap` owns the eight original mesh chunks and packed texture
decoder. It validates chunk offsets, uniqueness/alignment, exact spans/padding, count
budgets and terrain/material references before reading variable sections. Original
capacity/packing rules remain at this boundary. `MapContentExporter` in the CLI maps
decoded records into the prepared Content model. The projects and package locks are
unchanged; no original parser, graphics dependency or I/O enters Core or Engine.

`Fft.Content.PreparedBattleMap` uses format `fft.prepared-battle-map`, version 1,
profile `SCUS-94221/day-dry/default/frozen-base`. It requires:

- Distinct logical map/texture IDs, dimensions and one dense layer-major tile array
  containing exactly two terrain layers. The profile permits up to 256 cells per layer. Runtime Core storage is sized
  to the map; it does not retain the original fixed 256-record layer stride.
- Ordered polygon records with kind, source index, signed XYZ positions and textured
  normals. Native vertex/UV order is preserved without triangulation or axis changes.
  Textured records have normalized page 0..3 and palette bank 0..15, tile association
  when present, raw material words, source terrain tag and original polygon flags.
  Tag 0xfffe explicitly has no tile association. Untextured records retain their raw
  material words without inventing normals, UVs or palette references.
- A 1024x256 row-major index texture: the four native 256x256 packed pages are expanded
  low nibble first and placed side by side. Values are 0..15. All 256 raw unsigned
  palette words remain intact, including STP; the unit-sprite STP fixup is not applied.
  No RGBA expansion or new blend/channel policy is selected here.
- Stable surface IDs `base:terrain/ps1/{code:x2}` for the 6-bit source surface code;
  native height, half-height, water depth, E/W/S/N slopes, ceiling depth and normalized
  Blocked/Untargetable/CannotStop flags. Eight original bytes per tile remain opaque
  provenance, including unknown bits. `ToBattleMap()` uses the validated normalized
  fields, never reparses those original bytes or infers traversability from polygons.
- Eighteen signed light-matrix values, nine ambient/gradient bytes, 640 raw animation
  metadata bytes, 256 palette-animation words and 256 marked-tile palette words.
  These are retained as frozen presentation inputs; no animation scheduler runs.
- SCUS-94221 identity, full reference SHA and the three original file SHA-256 values.

Runtime rejects missing/duplicate/unknown fields, wrong types, invalid bounds/counts,
unsupported versions/profiles and metadata over 4 MiB. `LooseAssetStore` applies the
metadata/texture byte budgets before allocating file buffers, then checks actual size
and manifest hash. The texture must have exactly 262144 indices in range. Content and
Client have no AssetTool dependency; runtime receives only a prepared content root
and logical ID. Simulation and movement behavior are unchanged.

## Validation and local evidence

Synthetic tests cover malformed/truncated headers, offsets, overlap and counts;
GNS selection/stride/EOF; all four polygon kinds; signed positions/normals; UV/page/
palette and native corner order; low-first texture page/row boundaries; fixed source
terrain stride versus dense prepared layers; packed terrain flags and unknown bits;
strict JSON and budgets; deterministic export; manifest tampering; and prepared-only
loading in a temporary directory containing no original files or reference checkout.
CLI tests also verify scratch confinement, rejected inputs and preservation of existing
output. The synthetic tests require no original media or GPU.

Locked restore and Release build passed with zero warnings. All **796 tests** passed:
Core 293, Content 213, AssetTool 151, AssetTool CLI 91, Engine 24, Client 24.
This adds 76 synthetic cases to the previous 720-test baseline.

Local verified preparation was run twice into ignored `scratch/prepared-map003-030-a`
and `scratch/prepared-map003-030-b`. All three outputs matched byte for byte by SHA-256:

```text
map.json              310613  1686640164dde4480b3b48c266b17a555aefeaff6b986b2e58ebc4ddbd99375a
texture.indices       262144  68963921aec354a8304ed434bee76bc0af5c32f3cfa8c14b56846ad651d95d27
assets.manifest.json  469     80bc0b46dc2362961f42e0bcb85f0317974cc6823b0d7347b098aa3b387a99de
```

The client loaded that three-file root: 9x13, two layers, 536 polygons (80 textured
triangles, 430 textured quads, 26 untextured quads), 262144 indices; both asset hashes
verified. It was launched with the prepared root as its working directory and no
original-file/reference paths supplied. Original files were not renamed or removed.

The four lower cells (3,1), (4,1), (3,2), (4,2) preserve raw `1d00010000002000`:
surface Rug/29, height 1, dry/flat, normalized movement flags 0. Their upper cells
preserve `0000000000000100`, blocked. Quads 217/218/226/227 preserve the source-map
positions, downward normals, flags 0x8000, page 2, palette bank 8 and tags
0x0304/0x0404/0x0402/0x0302; quad 227 UVs remain
(88,227), (88,208), (107,227), (107,208). Ambient is (32,16,16), both gradients zero.
These are source-derived alignment checks, not a rendering comparison.

## Remaining boundaries

The native height-1 checkpoint is not rewritten to satisfy M1 combat's height-10
restriction. Map loading does not make this a supported battle setup. Complete slope,
water, layer-transition and movement rules need separate source-traced work.

Alternate weather/night/layout resources, animation playback, event terrain changes,
camera calibration and graphics/GTE/blend/occlusion equivalence remain unsupported.
The window continues to show the synthetic scene. ZOD-031 adds a fixed authored scene
using this map and the prepared pose; ZOD-026/033 retain independent comparison gates.
