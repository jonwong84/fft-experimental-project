# ZOD-078 — Prepared general-banner preview

Implemented 2026-10-23 against read-only reference HEAD
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed entry commit `af61b01`.
The working tree was clean on entry. No automatic commit is made.

This is the bounded condition-8/image-0 artwork preview selected by
[ZOD-077](../architecture/zod-077-general-banner-assets.md), consuming the existing
[post-readiness logical effect](../behavior/general-banner-effect.md). It does not
admit opcode 78, play event 401, or implement original startup/loading latency.
Static source evidence and authored executable expectations are distinct from
original execution. No original-game visual or timing equivalence is claimed.

## Prepare and launch

From the repository root, with .NET 10 and the verified local input under scratch:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build -- prepare-general-banner --bin scratch/fft-clean-base-game.bin --output scratch/banner-078-a
dotnet run --project src/Fft.Client -c Release --no-build -- --general-banner --content-root scratch/banner-078-a
dotnet run --project src/Fft.Client -c Release --no-build -- --general-banner --content-root scratch/banner-078-a --check scratch/banner-078-preview-a
```

Choose fresh output names for repeated preparations/checks. Inputs and generated
assets, provenance and captures stay ignored under `scratch/`. The preparer refuses
existing output and reparse-point paths; it stages in a unique sibling directory,
validates the prepared root, then publishes by directory rename. Failure never
overwrites a previous output. The selected original tables/pixels are not fixtures.

Interactive preview: **Space** submits one explicit false-Circle tick; **C** submits
one explicit true-Circle tick. Keys are sampled on their press edge; held keys do
not repeat logical ticks. **Esc** closes the view. Closing does not manufacture a
task completion/cancellation. The title identifies the modern RGB8 preview. The
finite check runs the explicit H=60 schedule to tick 91 and exits, capturing 15
checkpoints. It writes `check.json`, including policy, tick/phase, projection and
RGBA hashes, and `tick-NNN.png` images. It checks GPU target readback against the CPU
canvas. Window scaling uses nearest-neighbor integer scale with black margins;
undersized windows clip. Physical input timing and device-loss recovery on other
hardware remain manual presentation checks.

## Boundaries and formats

- AssetTool's `GeneralBannerDecoder` reads only the selected 0x6800 image, six
  12-byte quads, six 4-byte pages, two range bytes and six 20-byte color records.
  It unpacks low nibble first, selects palette bank 4 at 0x6480, normalizes the
  u8 UV stores and screen origin, and retains piece identity and four RGB colors.
  Unused bytes, page flags other than blend bits, upload suffix and packet parity
  are not runtime data. Selected material/geometry and vertical gradients are
  checked before publishing decoded output.
- CLI verifies the 541,315,152-byte SCUS-94221 image hash, reads named extents with
  the existing Mode 2 Form 1 reader, verifies SYSTEM.CNF, executable, BONUS.BIN,
  REQUIRE.OUT, image 0, palette 4 and all five pinned REQUIRE range hashes from
  [the source metadata](../evidence/events/general-banner-assets-v1.json).
  It performs no C execution and needs no reference checkout at preparation time.
- Stable root ID: `fft.ps1-us:banner/general-0`; child IDs append `/indices` and
  `/palette-4`. Existing manifest length/SHA-256 verification binds `banner.json`,
  `indices.bin` (51,200 unpacked u8 indices) and `palette-4.bin` (32 RGB555/STP LE
  bytes). `provenance.json` records the offline input identity and policy.
- Content's `PreparedGeneralBanner` v1 is a closed schema: exact format/selection,
  dimensions/encodings, distinct references, six ordered pieces with triplet IDs,
  232x31 bounded rectangles, selected add/subtract pattern, and four RGB8 vertices
  with matching horizontal pairs. Unknown/missing/duplicate fields, invalid
  references, payload budgets/lengths/hashes and indices outside 0..15 fail before
  any loaded artwork is returned. Nested constructor parameters are required.
  Public definitions and loaded buffers are immutable. Authored tests carry
  `authored-test` provenance with no retail hashes; verified retail provenance
  pins the selected reference and two named input hashes.
- Client loads/validates prepared files and creates GPU resources before creating
  the logical scene and attaching its task. Core receives only the existing
  `GeneralBannerDefinition(Id,8,H)` values. No Core/Engine, event profile, opcode
  capability, save DTO, dependency graph, package or shader changes were needed.

## Rendering policy and lifetime

The CPU compositor uses an opaque RGBA8 256x240 authored blue-gray checkerboard.
Point samples cover half-open rectangles. RGB555 expands by bit replication;
word zero discards regardless of index, nonzero STP-clear texels replace opaquely,
and STP-set texels use the piece's saturating RGB8 add/subtract operation. STP-black
is not transparent. Pixel-center vertical gradients interpolate before modulation
by neutral 128; exact integer rational arithmetic floors after modulation, then
clamps to RGB8. Gradients span the currently cropped quad, matching the selected
quad/color projection rather than scaling glyph texels. Traversal is 0..5,
reversing source head-insertion call order, with no interleaved primitives.

These are explicit modern preview policies. PS1 5-bit intermediate precision,
dithering, affine triangle coverage, pixel aspect, original battle background,
draw-state reachability and initial packet parity remain unvalidated. No guessed
reservation suffix pixels or initial parity enter the normalized view.

`GeneralBannerPresentation.Project` reads accepted completed counters, with no
mutable renderer cache. For equal 31-row pieces the first triplet reveals
0/8/16/24/31 rows at updates 1..5; the second starts at update 10 and is full at 14.
Updates 20 and H+20 still submit the last reveal/hold geometry despite phase
changes. Hide update 1 is full, then 8/16/24/31 rows are removed. Natural ExitYield
submits six degenerate quads; Circle-skipped ExitYield, initial and terminal/cancelled
states submit nothing. `RunUntilBlocked` and rejected ticks do not change projection.
Repainting or omitting accepted snapshots never advances simulation or supplies
task facts. Resource rebuild callbacks only rebuild view objects. The scene retains
effect identity/start/progress across interpreter exit/restart as in ZOD-076.

## Verification and remaining gates

Subsequent [observed comparison](observed-general-banner-comparison.md) aligns a
restored original full-visible banner with the tick-20 preview. Placement appears
consistent; 1,460 of 1,577 managed-background-invariant samples differ, with the
original up to 16 RGB levels darker. This diagnostic does not establish native
precision, timing, transparency or fidelity. The implementation/policy and
historical validation below remain unchanged.

The later [precision study](../architecture/general-banner-native-color-study.md)
matches those samples using five-bit arithmetic and the frozen dither-off state.
It is developer evidence for a future explicit precision profile; this modern
RGB8 preview and its tests are not silently relabeled native-compatible.

The subsequent [explicit five-bit profile](five-bit-general-banner.md) now adds
`--precision five-bit --dither off|on`, while default RGB8 captures retain their
historical bytes. Fresh locked restore, zero-warning Release build and all 1,418
.NET tests pass. The native full-visible foreground matches the original at
1,577 samples; original timing and background-sensitive blending remain open.

Locked restore, zero-warning Release build and **1,388 tests pass** (86 new:
AssetTool 15, Content 38, Client 26, CLI 7). All 20 historical
[authored expectations](../evidence/events/general-banner-presentation-contract-v1.json)
are represented: four layout cases in `GeneralBannerDecoderTests`, sixteen
projection/material/lifetime/readiness cases in `GeneralBannerPresentationTests`;
the unsupported-input matrix is expanded in `PreparedGeneralBannerTests`.
The original JSON remains marked specification-only as historical evidence, not
retroactively relabeled original observation. Additional cases cover strict nested
fields, malformed formats/geometry/colors/provenance, hash/length/index failures,
opaque index zero, STP black, exact gradient precision, saturation, crop sampling,
rollback, replay with zero/one/two renders, retained effect restart and safe CLI
failures. Existing Core and opcode-78 rejection tests are unchanged and pass.

Two preparations (`scratch/banner-078-a` and `scratch/banner-078-b`) match all five
files byte-for-byte. Selected output SHA-256 identities:

- `banner.json`: `e1c61e66de5ca10f84743f286ec8fb7f34c0bdd14e41b5eb0cab704a629ffdac`
- `indices.bin`: `515778839288e154c76e51db57a44e82c63a8e27c079bf3a1803136e389191dd`
- `palette-4.bin`: `8aa38621ccfcd6386e23466a803999ef5ed44a1afc415f447a9fcc0054027739`
- `provenance.json`: `4aa342261e3d88f9c73f8555eb8eba35dea9d9807f1986812fd7b6e0817425a5`

Two finite Windows GPU runs reach Completed at tick 91, with exact CPU/GPU readback
at checkpoints. All 15 PNGs and the check report match byte-for-byte. First output:
`scratch/banner-078-preview-a`; second output:
`scratch/banner-078-runtime/scratch/capture-b`. The second run's working directory
contains only the four prepared content files and generated captures; no original
input/reference paths are supplied and neither AssetTool nor a C library belongs
to the client graph. This demonstrates prepared-only runtime use, not OS-level
access-denial testing. Full, partial reveal and hidden captures were visually
inspected locally. No original media or decoded records are staged.

The next scene capability needs a separately selected source-traced milestone.
Original startup/scheduling, opcode-78 admission, Ready/camera/dark-screen workers,
EventEnd and complete BattleStart playback remain separate. Original/emulator
capture comparison (ZOD-026/ZOD-033), broader device recovery and physical input
checks remain open; this slice does not complete M2 or the original game.
