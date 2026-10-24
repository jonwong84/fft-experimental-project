# Sprite attack-image decoder proof of concept

## Scope

Later evidence: [ZOD-026's original-instruction oracle](development/sprite-executable-reference.md)
now independently confirms this fixture's decoded-byte digest. The source-derived
statements below retain their historical meaning; rendered equivalence remains open.

This milestone implements the fixed prefix model and compressed attack-image
stream for a unit `.SPR` file, plus a loose-file reader addressed by logical
asset IDs. It does not parse SHP/SEQ records or image display metadata.

[ZOD-027's later idle-pose trace](architecture/zod-027-chocobo-idle-pose.md) selects an ordinary
Chocobo frame and documents its SHP/SEQ/palette composition at a newer pinned reference revision.
It distinguishes ordinary-sheet low-first pixel packing from this attack-image fixture's
managed high-first output contract; it adds no parser or rendered-equivalence claim.

The reference is the local source snapshot recorded at decomp commit
`611b504d25d4a9b03fab8023d7dbea75fe381d5d`. In that snapshot:

- `include/fft/sprite.h` defines a `0x9200` prefix followed by a variable stream.
- `src/battle/battle_scene.c`, `sprite_decompress_attack_image`, reads the high
  nibble first, treats nonzero nibbles as pixels, and uses zero-prefixed runs
  for transparent pixels. Run selectors 0, 7, and 8 consume one, two, and three
  additional nibbles respectively; extended values are assembled low nibble
  first. The decoder stops after `0x6400` output bytes.
- `src/battle/unit_animations.c`, `sprite_evtchr_load_unit`, calls the decoder
  for attack images. Frames before `first_attack_frame` use the ordinary sheet;
  later frames are routed through an EVTCHR image slot.
- `include/fft/sprite_sheet.h` defines `first_attack_frame` as a little-endian
  value at SHP offset `+0x04`.

## Managed behavior

`Fft.AssetTool.SpriteAttackImageDecoder` takes a bounded `ReadOnlySpan<byte>` and
returns 25,600 packed bytes (256 by 200 pixels at 4bpp). The first pixel is the
high nibble of each output byte. It follows the original run and nibble ordering,
clips a run that crosses the fixed output boundary, and ignores stream bytes
after the output is complete. It throws `InvalidDataException` if the supplied
span ends before the image is complete. This replaces the original routine's
global cursor and missing input-length check with a deterministic, reentrant API.

`Fft.AssetTool.UnitSpriteSheet` validates and copies the complete fixed prefix,
then exposes read-only views for the 512-byte palette block, 32,768-byte main
pixel block, 4,096-byte portrait/body block, and variable attack-image tail. Its
palette accessor reads raw little-endian 16-bit values; it does not apply the
runtime STP-bit transformation. `Fft.Core.AssetId` provides a validated,
namespaced identifier. The `Fft.Content` library reads schema-versioned
`assets.manifest.json` files and verifies file size and SHA-256 when loading by
ID. The extractor writes a deterministic `base:` ID for each extracted file,
derived from its lowercased ISO-relative path.

Example manifest entry:

```json
{
  "schemaVersion": 1,
  "assets": [
    {
      "id": "base:battle/cyoko.spr",
      "relativePath": "BATTLE/CYOKO.SPR",
      "byteLength": 49572,
      "sha256": "<64 hexadecimal characters>"
    }
  ]
}
```

## Validation fixture

The local extracted `BATTLE/CYOKO.SPR` is 49,572 bytes, leaving 12,196 bytes
after offset `0x9200`. Its paired `BATTLE/CYOKO.SHP` has `first_attack_frame =
61` at offset `+0x04`. The sprite file-table entry at sheet ID 134 points to LBA
57,494, which matches `BATTLE/CYOKO.SPR` in the extracted inventory; header ID
134 is `SPRITE_SHEET_ID_CHOCOBO`. The managed decoder completed the image from
the actual 12,196-byte file tail, without relying on the original loader's
fixed 12,500-byte copy that crosses the recorded file length for this fixture.

The CLI was also run against the verified local BIN/CUE to extract only
`BATTLE/CYOKO.SPR` into ignored `/scratch/prepared-chocobo-content/`. It wrote
schema version 1 with ID `base:battle/cyoko.spr`, path, byte length, and verified
SHA-256. `Fft.Content.LooseAssetStore.Load` then loaded and hash-checked that
file by ID, and the loaded bytes parsed and decoded successfully without opening
the BIN in the reader process.

`docs/fixtures/chocobo-attack-image.json` records the input identity and the
decoded output identity without storing any game data: 256×200 pixels, packed
4bpp with the first pixel in the high nibble, 25,600 decoded bytes, and SHA-256
`d07fff4e49bf5e0a8b91343b2a722521ac5309e34ec23d2d5810e579a741c24f`. Run the
check with:

```powershell
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- verify-sprite --content-root scratch/prepared-chocobo-content
```

To produce a local visual diagnostic containing the same decoded image under
each of the SPR's sixteen palette banks, run:

```powershell
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- verify-sprite --content-root scratch/prepared-chocobo-content --preview-output scratch/chocobo-palettes.png
```

The source loader copies sixteen 16-color CLUTs and sets the PS1 STP bit on
colors 1 through 15 while leaving color zero transparent (`unit_animations.c`,
`sprite_sheet_load_into_vram_slot`). The preview expands 5-bit channels by bit
replication and displays nonzero texture indices as opaque colors. It ignores
STP-dependent blending, GPU draw ordering, frame cropping/transforms, and
display scaling. All palette banks are shown because this fixture does not yet
establish which palette selection corresponds to each rendered attack frame;
the result is a diagnostic contact sheet, not a faithful game screenshot.

Before recording the fixture digest, the managed output was compared byte for
byte with a separate, temporary source-derived transcription of the original C
routine. The copied decomp checkout contains no runnable PS1 game binary, and
no native C compiler is available in this environment; therefore this is not a
comparison against an executing original C routine or emulator. The fixture
proves repeatability against that source-derived baseline, not definitive
original-runtime equivalence. The fixture cites the exact source commit and
function so a future executable reference run can replace or confirm it.

Automated tests in `tests/Fft.AssetTool.Tests/` cover high-first nibble reading,
packed pixel order, direct and extended transparent runs, run clipping at
output end, truncated streams, fixed-prefix section sizes, source-copy behavior,
and palette boundaries. Tests in `tests/Fft.Content.Tests/` cover manifest
round-tripping, schema and entry validation, path traversal rejection, content
hash verification, and loading a loose file by ID. The actual game asset
remains under ignored `/scratch/` and is not required by the test suite.

## Not yet established

- A parsed SHP/SEQ runtime model or rendered comparison against the original.
- Byte-for-byte comparison with output from an executing original C routine or
  emulator; the current golden digest is source-derived as described above.
- Whether all `.SPR` variants use this unit-sheet format. `WEP.SPR` and
  `OTHER.SPR` have separate layouts in the reference header and are out of scope.
