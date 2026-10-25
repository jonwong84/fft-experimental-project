# ZOD-086 — Prepared Ready artwork and authored preview

Date: 2026-10-25. Edition: `SCUS-94221`. Managed starting revision:
`5aac0982fe2e391846d7a7857aca163fb8db15ba`; working tree clean on entry.
Read-only reference HEAD verified: `11464943bbdbd2a0af022d90cb7f10455cf911df`.

The [ZOD-085 policy](../architecture/zod-085-ready-rendering.md) now has a separate
offline decoder/preparer, immutable Content formats and prepared-files-only Windows
preview. It composes accepted [ZOD-084 Ready snapshots](ready-display-transactions.md)
with four-corner RGB5 shading and per-texel opaque/average STP behavior. Existing
general-banner schemas, profiles, renderers and Core execution remain unchanged.

The [validation metadata](../evidence/events/ready-profile-validation-v1.json)
records local artifact identities and bounded results. Original media, decoded
artwork, captures, galleries and scratch scripts remain ignored. No original Ready
visual/timing comparison or emulator run is claimed.

## Preparation and schema boundaries

`prepare-ready-artwork --bin ... --output ...` uses the existing verified-disc
import route: exact SCUS-94221 BIN length/hash, SYSTEM.CNF and executable identity,
then named BONUS/REQUIRE length/hash checks and selected image/table/range hashes.
This implementation reads the verified local BIN directly offline rather than
requiring an already extracted import manifest. It emits a fresh prepared manifest;
no original input is read by the game. This is the selected preparation workflow
clarification to ZOD-085's import-manifest proposal, with the same edition/identity
and resource-readiness gates.

`ReadyArtworkDecoder` selects image 0, bank 2, range `[0,6)`, six 12-byte quads,
six 20-byte color records and the palette rectangle. Ready normalization preserves
source V, translates XY by `(128,120)`, and rejects negative/out-of-atlas or wrapping
endpoints. The decoder retains TL/TR/BL/BR RGB values while ignoring unused prefix
and code bytes. Low-nibble-first unpacking reuses the proven pixel-format helper;
there is no general-banner crop/tpage interpretation.

`PreparedReadyArtwork` is the separate `fft.ready-artwork` schema 1. It requires
condition 0/image 0/palette 2; three distinct logical IDs; exact 256x200 unpacked
index bytes and 32-byte RGB555/STP palette; six ordered source indices, width 16/17,
height 24, safe XY/UV bounds, average material and four independent byte colors.
JSON is capped at 32 KiB/depth 12 with unknown, duplicate, missing, null, wrong-type
and unsupported data rejected. Source provenance is pinned authored-test or
verified-retail. Prepared manifests verify file length/hash and safe loose paths;
loaded arrays are immutable. Provenance labels are not a substitute for separately
bound original evidence.

`prepare-ready-event` requires explicit authored provenance and an 8192-byte raw
or processed block. `ReadyEventExporter` reinspects retained bytes and decodes
opcode 78's unsigned condition and hold independently. All records, including
unreachable ones, must belong to the bounded vocabulary; exactly one condition-0
display is required. A separate `fft.prepared-ready-event` schema/profile retains
authored source framing, stable event/artwork/owner IDs, offsets and targets.

The event shares normalized instruction records and the existing private
structural validation route: display spans validate as a three-byte zero wait,
then project to typed `DisplayReady` for execution. The old prepared event parsers
still reject this format; no legacy execution path receives a Ready display.
BATTLE/non-tutorial/speed-one is required. Condition 1..8, unsupported records,
unknown profiles and retail provenance remain rejected. Original events 4/401
are not admitted as executable event content. Event definition hold and artwork
remain separate; a branch can skip the syntactic display without allocating tasks.

Both commands publish through a private sibling staging directory only after
strict loading succeeds. Inputs/outputs stay below scratch, reparse paths reject,
and an existing destination is preserved. Failed validation cannot publish a
partial prepared root. No package, runtime C dependency or schema relaxation was
introduced.

## Runtime and renderer

`ReadySession.Load` loads the event, selected artwork and both immutable payloads
before returning a session. `ReadyWindow.LoadContent` creates SpriteBatch, texture
and render target before calling `Start`; missing files, mismatched IDs or failed
graphics initialization cannot start a scene. Successful start supplies the trusted
logical definition/artwork/owner readiness binding. Device rebuilds retain accepted
state and use validated data; they cannot consume ticks or Circle.

`ReadyPresentation.Project` returns all six full glyphs in order 5..0 for nonnull
`DrawScale`, otherwise none. The compositor scales the four corners before Q12
triangle interpolation, uses one-to-one point samples and shades each half-open
rectangle sample once. Palette word zero discards, nonzero STP-clear words replace
opaquely, and STP words average the quantized RGB5 source and destination by one
joint-sum shift. Output is opaque RGBA with zero-padded RGB5 channels. The source
derivation and bounds remain ZOD-085's contract.

Every draw carries `ReadyRasterState` for `ready-glyph-rgb5-v1`: dither disabled,
zero texture window, mask set/evaluation disabled and a valid explicit physical
framebuffer origin. Point/modulated/average behavior, full local clip and
normalized draw origin are fixed by this closed policy. Dither-on, alternate
materials, invalid scales, duplicate/out-of-range draw identities and unsupported
state reject before composition. An explicit background must be 256x240 opaque,
zero-padded RGB5 RGBA and is never modified; the default checker is synthetic.

The window uploads the C# CPU buffer and presents it with opaque point sampling.
Interactive Space advances one logical tick, C advances with Circle, and Escape
closes. A finite `--check` advances at most 4096 ticks, captures every accepted
tick, checks exact CPU/GPU readback and writes `check.json` only after the event
and aggregate finish. `--circle-tick` supplies one explicit sample for finite
checks. Finite checks also recreate resources once at accepted tick 5, when reached,
without advancing state; stdout records that exercise. Receipts retain terminal times, final state, complete-trace hash, per-tick
draws/state/pixel hashes and zero renderer task acknowledgments. They explicitly
state that original comparison was not performed. Paint/read/rebuild frequency
cannot change the event, dispatcher, child or ledger.

At hold 60, draws are absent starting at tick 67's extra yield. Child, dispatcher
and waiting event complete at 68/69/70. Early release resets scale to 128, and
Circle after release cannot shorten fade-out. A skipped display reaches its empty
barrier/exit at tick 1 with no managed task.

## Reproduce and review

Create a fresh directory for authored input, then prepare original artwork using
the owner's verified local BIN. This byte sequence is an authored display, E5(56),
synthetic exit and unreachable SearchEnd sentinel, not a retail event slice:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore

$readyInput = 'scratch/ready-example-input'
if (Test-Path $readyInput) { throw 'Choose a fresh input directory.' }
New-Item -ItemType Directory $readyInput | Out-Null
$readyBlock = New-Object byte[] 8192
[BitConverter]::GetBytes([uint32]13).CopyTo($readyBlock, 0)
([byte[]](0x78,0,60,0xe5,56,0,0xd4,0,0xdb)).CopyTo($readyBlock, 4)
$readyBlock[13] = 0xfe
[IO.File]::WriteAllBytes((Join-Path (Get-Location) "$readyInput/authored.block"), $readyBlock)

dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build --no-restore -- prepare-ready-artwork --bin scratch/fft-clean-base-game.bin --output scratch/ready-example-artwork
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build --no-restore -- prepare-ready-event --input scratch/ready-example-input/authored.block --encoding raw --id sample:event/ready-preview --artwork-id fft.ps1-us:ready/image-0 --owner-id sample:owner/ready-preview --provenance authored --output scratch/ready-example-event
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --ready --event-root scratch/ready-example-event --event-id sample:event/ready-preview --artwork-root scratch/ready-example-artwork --check scratch/ready-example-preview
```

Processed blocks use the existing processed header and explicit `--text-offset`.
Use fresh output roots when repeating. The local gallery is
`scratch/ready-086-review/review.html`: birth, fade-in, full, release, fade-out,
last submission, extra yield and child/dispatcher/event clear. These are raw
256x240 diagnostic canvases enlarged with nearest-neighbor sampling, with no CRT
aspect/shader treatment. The owner approved this gallery on 2026-10-25: "Looks good
to me." This accepts the prepared preview's appearance; original comparison remains
separate.

## Executed checks and validation

All 32 RPR cases from the unchanged
[presentation expectations](../evidence/events/ready-presentation-contract-v1.json)
now have checks across AssetTool, Content, CLI and Client tests plus actual finite
host audits. RDY-029..032 from the unchanged
[Ready contract](../evidence/events/ready-display-contract-v1.json) execute as the
separate profile/conversion/presentation/session gates. Historical flags describe
the authored specification checkpoint and remain unchanged.

The 112 new .NET tests cover packing/normalization and malformed records, palette
selection/four corners, literal interpolation/material vectors, closed JSON and
payload failures, raw/processed unsigned operands, profile/domain isolation,
output preservation, finite timing/input, no-draw gaps, accepted-state projection
and complete replay independent of rendering. Source-evidence scope assertions
preserve the queued/displayed distinction; they do not establish original fidelity.

Actual local checks:

- Two artwork preparations and two hold-60 Windows runs repeat every output byte.
  Raw/processed hold-60 final state, trace and pixels agree.
- Twelve finite Windows scenarios cover holds 0/1/60/255, Circle 1/5/60, ignored
  Circle after release and at zero hold, and skipped admission. All 626 PNGs pass
  CPU/GPU readback and an independent Python cross-product/Q24 material model.
- The final built host runs twice from isolated working directories containing no
  original input or reference checkout; explicit prepared roots and resource
  recreation at tick 5 reproduce all 72 hold-60 artifacts. The executable still uses the normal managed host libraries;
  no PS1 emulator/toolchain is a dependency.
- Missing event, wrong artwork and deliberately unavailable SDL video driver
  return failure without creating a check root or success receipt. This checks
  real host readiness failure, separately from pure data-loading tests.
- Legacy RGB8, RGB5 and ordered-banner Windows checks preserve all 16, 16 and 17
  historical artifacts respectively, including receipts.

Locked restore and full Release build pass with **zero warnings/errors**. All
**1,794 .NET tests** pass: Engine 24, Framework 621, Content 488, AssetTool 192,
CLI 186, Client 283. All **163 Python tests** pass. Coverage inventory/view,
source/evidence/report hashes, documentation links and whitespace checks pass.

The original Ready queued-state studies remain distinct from these C# diagnostic
canvases. Next ZOD-087 selects/binds original queued-to-displayed Ready references
and background evidence before claiming displayed-pixel agreement. Natural/input
continuations, loading duration, physical display latency, arbitrary native
scheduling, combined general→Ready retail playback, EventEnd/full BattleStart and
the owner's CRT/backend/audio work remain separate gates.

### ZOD-087 follow-up (2026-10-25)

The later [displayed Ready comparison](displayed-ready-comparison.md) now records
six exact original/C# scanout comparisons with palette-transparent background
controls and source-bound display history. It leaves displayed birth/next frame
unsupported at interrupted checkpoints, and original timing/full playback open.
Its original-comparison gallery requires separate owner review. The earlier
validation and specification flags above retain their historical meaning.
