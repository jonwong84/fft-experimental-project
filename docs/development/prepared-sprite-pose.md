# ZOD-028 — Prepare and load one ordinary sprite pose

Date: 2026-10-23. Compatibility target: North American PS1 `SCUS-94221`.
Reference HEAD observed before implementation:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [ZOD-027 source map](../architecture/zod-027-chocobo-idle-pose.md) records the exact
files, offsets, callers, arithmetic widths, table entries and limits used here.

This slice prepares Chocobo sheet 134, primary frame 9, first pose of sequence 6,
palette bank 0, wait 6. It produces render input and a headless runtime load check.
The pose is frozen: no SEQ player, gameplay input, map renderer or original-game
visual comparison is added. ZOD-031 owns rendering the prepared map and pose together.

## Reproduction on Windows

Use the pinned SDK and existing verified extracted files beneath ignored `scratch/`:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet run --project src/Fft.AssetTool.Cli -c Release --no-build --no-restore -- prepare-sprite-pose --spr scratch/all-data-extraction/BATTLE/CYOKO.SPR --shp scratch/all-data-extraction/BATTLE/CYOKO.SHP --seq scratch/all-data-extraction/BATTLE/CYOKO.SEQ --output scratch/prepared-chocobo-idle
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --content-root scratch/prepared-chocobo-idle --sprite-pose base:battle/cyoko/idle-front-pose
```

Choose a fresh output directory each time. The command checks the exact three source
lengths and SHA-256 identities from ZOD-027 before parsing. Input and output must stay
under repository `scratch/`; existing outputs and reparse-point paths are rejected.
It validates a temporary bundle before publishing it by directory move. An invalid
source does not create a prepared output directory. Exit codes: 0 success, 1 file/data
or unsupported-profile failure, 2 invalid command syntax.

Output contains only `pose.json`, `ordinary.rgba`, and `assets.manifest.json`:

- `base:battle/cyoko/idle-front-pose` addresses the metadata.
- `base:battle/cyoko/ordinary-rgba` addresses the 262,144-byte 256x256 atlas.

The client composition path loads these through `Fft.Content.LooseAssetStore`, verifies
both manifest hashes, validates metadata/atlas format, and reports the frame, wait,
palette and piece count. It does not load original SPR/SHP/SEQ, the disc BIN/ISO,
BATTLE.BIN, the reference checkout or any AssetTool code. Original files remain local
import inputs. No change to `--window` is made in this ticket.

## Contracts and ownership

`Fft.AssetTool.UnitShapeFile` parses packed little-endian SHP tables into immutable
frames/parts. It validates the installed 208 primary and optional submerged references,
normalizes `-1` to payload offset zero, checks complete frame headers/pieces, and rejects
overlapping/truncated secondary sets. Uninstalled entries 208..255 are not interpreted
as native frame references. Raw attributes and signed 8-bit anchor shifts are retained.

`UnitSequenceFile` copies the declared payload and validates all 256 signed offsets.
`FirstPose` reads only one ordinary `(frame, wait)` instruction; a leading opcode,
attack/SP2 selection or incomplete pair fails explicitly. Remaining sequence instructions
are not interpreted or validated as a playable program. A zero wait is preserved as a
held-pose value; there is no playback loop or simulation timing.

`OrdinaryUnitPoseDecoder` resolves the selected primary frame and source-traced size
indices 0, 2, 5 and 6. It rejects other sizes (including special split size 14), rotation,
frame blend flags, attack/SP2 frames, unsupported palette selection and out-of-atlas
rectangles. It emits pieces back to front by reversing native submission order. Signed
anchor offsets and per-piece horizontal/vertical flip flags are retained for a consumer;
whole-sprite orientation is fixed to ZOD-027's unmirrored front view.

Ordinary pixels use **low nibble first**, unlike the managed output convention of the
existing compressed attack-image fixture. Raw bank colors are retained in metadata.
Native loading sets STP on nonzero palette indices; this opaque primitive emits alpha
0 for the selected zero color and alpha 255 for the other entries, including a black
nonzero index. Channel expansion is `(fiveBits << 3) | (fiveBits >> 2)`, straight RGBA8.
It is a documented conversion choice, not PS1 dithering/modulation equivalence.

The CLI exporter maps decoded records into `Fft.Content.PreparedSpritePose` without
making Content reference AssetTool. No graphics, parser, animation or I/O package enters
`Fft.Core` or `Zodiac.Engine`; project references and package locks are unchanged.

## Prepared metadata v1

The format is `fft.sprite-pose`, schema version 1. It requires:

- Distinct stable pose/texture IDs; pixel format `rgba8-straight-5to8-replication`;
  dimensions exactly 256x256 for this bounded ordinary-sheet profile.
- Sequence/frame IDs, source wait, selected bank and exactly 16 raw unsigned colors.
  Native frame-index bounds and palette range 0..5 apply to this profile.
- One through eight `partsBackToFront`, each with its original source index, signed
  X/Y offset, bounded U/V/width/height and explicit boolean flips. Source indices
  must form the exact descending sequence; rectangles must use supported dimensions.
- Source edition, full pinned reference revision and three input SHA-256 identities.

The reader rejects duplicate/unknown/missing fields, wrong JSON types, unsupported
versions, invalid IDs, source identities, bounds/order and metadata over 32 KiB.
Runtime atlas loading additionally checks byte count, manifest hash and alpha 0/255.
This schema represents only a neutral, opaque, primary-sheet static pose; no rotation,
blend, palette animation or screen-origin approximation is silently substituted.
Future presentation coverage needs an explicit schema/profile decision.

## Validation and evidence

Tests use invented bytes and prepared files only. Coverage includes malformed headers,
signed offsets/sentinels, truncated records, secondary-set overlap, independent source
and destination buffers, zero wait, low-first color/alpha vectors, signed anchors/flips,
unsupported paths, source rectangles, strict JSON, stable export, tampered assets and
runtime load without original files. An overlapping two-piece synthetic sample checks
that native head insertion leaves source piece 0 painted last after export/load.

Locked restore and Release build passed with zero warnings. All **720 tests** passed:
Core 293, Content 180, AssetTool 123, AssetTool CLI 76, Engine 24, Client 24.
This adds 64 synthetic cases; no tests need the user's game files or a GPU.

Local retail preparation was run twice into ignored
`scratch/prepared-chocobo-idle-028-a` and `scratch/prepared-chocobo-idle-028-b`.
All three output hashes were identical:

```text
pose.json             512b9e70105522c64eeb572ad3f38fbd3fb66957a3b600e186bb9df3cb7e26c0
ordinary.rgba         1fab0dfa4381d4e63757faaaaf7bed1ef31d4e9fd5d9e100d077ffce61828121
assets.manifest.json  e22e3a2fa5b836d9fca09d06b1e31fb9835d3940cd5dbe345dd101b88a0d2f4a
```

The client loaded the three-file prepared root successfully: frame 9, wait 6 (frozen),
palette 0, six pieces, 262,144 RGBA bytes, with both asset hashes verified. The files
are local game-derived outputs and must remain ignored. No original media is committed.

Evidence remains source-derived plus local deterministic conversion; the existing
attack-image digest is unchanged. No executing C/emulator oracle, original-rendering
comparison, animation cadence, camera calibration, map occlusion or GPU rasterization
has been established by these checks. ZOD-026 and ZOD-031/033 retain those separate gates.

Implementation follow-up: [ZOD-031](prepared-scene.md) now displays this held pose with
MAP003 in a fixed prepared scene. Animation and independent original comparison remain deferred.
