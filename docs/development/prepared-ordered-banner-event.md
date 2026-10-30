# ZOD-082 — Authored preloaded ordered-banner profile

Implemented 2026-10-25. Managed starting revision:
`91de652fb0ef8a29d021e4dbb501993ddb63e122`. Read-only reference HEAD verified:
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The working tree was clean on entry.

This completes the authored preparation/Content/host gate selected by
[ZOD-080](../architecture/zod-080-banner-opcode-admission.md), using
[ZOD-081's ordered executor](ordered-banner-transactions.md) and the existing
[prepared banner](prepared-general-banner.md). It does not execute original events
4/401, emulate native asset-loading duration, or establish physical display/input
latency. Original/runtime boundaries and rendering policies remain unchanged.

## Separate format and preparation

`PreparedOrderedBannerEvent` v1 uses format `fft.prepared-ordered-banner-event` and
profile `sample:event-profile/non-tutorial-speed1-preloaded-banner-ordered-v1`.
It explicitly requires Battle, tutorial false and speed one, synthetic SCUS-94221
provenance at the pinned revision, one condition-8 display, and unsigned hold 0..255.
The condition and hold are separate JSON operands, decoded from separate bytes.
Logical event/artwork/owner IDs are required; the logical effect definition ID is
the event ID plus `/banner`. Event IDs are limited to 249 characters so derived
identities remain within Core's 256-character budget.

The old `PreparedScenarioEvent` enum, schema, profiles, parser and export methods
remain unchanged and cannot admit DisplayConditions. The new schema rejects
unknown, missing, duplicate and null required fields, unknown/numeric operations,
incorrect domains, bad provenance, invalid operands/targets/boundaries and extra
display records, including unreachable records. Its private structural validation
body substitutes a three-byte zero wait for the three-byte display solely to reuse
existing contiguous-offset, marker, operand and reachability checks. That body is
never executed: projection replaces the display with Core's typed admission.

`ScenarioEventExporter.CreateSyntheticOrderedBanner` re-inspects retained input
bytes before conversion. `prepare-ordered-banner-event` additionally requires
`--provenance authored`. This is a caller assertion, not a tool for proving that
arbitrary bytes are independently authored. Do not copy retail command slices and
present them as authored programs. Whole original events remain unsupported even
with a false assertion of authorship.

The preparer accepts one 8192-byte raw or processed block beneath `scratch/`;
processed input requires the preserved text boundary. It bounds the input before
allocation, rejects reparse points/existing outputs, validates a staged manifest
root through Content and publishes by directory rename. Errors preserve existing
files and remove unpublished staging files. Only `event.json`, its manifest and
provenance are published. The host reads these prepared files, never bytecode,
the BIN, the archive or decompilation code.

## Reproduce the bounded run

The independently authored regression program is `Zero(V3); Display(8,60);
E5(56); Add(V3,7); D4(1); DB`. Its source offsets are 4/7/10/13/18/20 and its text
boundary is 21. D4 remains the selected synthetic exit, not retail EventEnd.
The [test helper](../../tests/Support/AuthoredOrderedBannerEvent.cs) contains its
literal authored bytes. Local raw/processed copies used for validation are in
`scratch/banner-082-inputs-a`; original event inputs in that folder are rejection
probes only. Artwork was already prepared in `scratch/banner-078-a`.

From the repository root, choose fresh output roots:

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build -- prepare-ordered-banner-event --input scratch/banner-082-inputs-a/raw.block --encoding raw --id sample:event/banner-admission-v1 --artwork-id fft.ps1-us:banner/general-0 --owner-id sample:owner/banner --provenance authored --output scratch/banner-082-event-raw-a
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build -- prepare-ordered-banner-event --input scratch/banner-082-inputs-a/processed.block --encoding processed --text-offset 21 --id sample:event/banner-admission-v1 --artwork-id fft.ps1-us:banner/general-0 --owner-id sample:owner/banner --provenance authored --output scratch/banner-082-event-processed-a
dotnet run --project src/Fft.Client -c Release --no-build -- --ordered-banner --event-root scratch/banner-082-event-raw-a --event-id sample:event/banner-admission-v1 --artwork-root scratch/banner-078-a --precision five-bit --dither off --check scratch/banner-082-window-a
```

Omit `--check` for manual Space/false-Circle and C/true-Circle ticks; Esc closes
without manufacturing task facts. RGB8 remains available by omitting precision
options. The five-bit path requires explicit dithering, as in the existing preview.
These use existing renderer policies; no shader, material arithmetic or pixel-aspect
changes accompany event integration.

`OrderedBannerSession.Load` validates event metadata, manifest hashes, artwork
identity, indices/palette and typed projection before returning a startable session.
It creates no scene/task. The Windows window creates GPU resources before calling
`Start`, then holds only accepted ordered states. Draw/rebuild consumes snapshots;
it never acknowledges tasks or advances logical effects. A skipped display renders
an empty projection. An exited interpreter's live banner continues until terminal.

Finite checks stop when the interpreter has exited and any admitted banner is
terminal, with a hard budget of 4096 logical ticks. A valid program needing more
ticks is outside this finite check, not silently truncated into success. The
receipt records event/artwork/owner IDs, provenance, complete final state, full
trace hash, worker-clear and event-exit ticks, precision policy and capture hashes.
Selected GPU point/opaque readbacks must equal the CPU canvas exactly. Window draw
cadence is a check mechanism, not evidence of original loading or display timing.

## Evidence and results

[Metadata-only validation evidence](../evidence/events/ordered-banner-profile-validation-v1.json)
records the local receipts and input hashes without committing media or generated
assets. It supplements the unchanged historical preimplementation contract:
`BOP-028` is now exercised across exporter, Content, session and actual-host checks;
the original artifact's `executed: false` remains its historical state.

- Raw Windows runs `banner-082-window-a` and `-b` repeat all 17 files byte-for-byte:
  one receipt and 16 PNGs. Worker clear is 91; parent exit is 92, with V3=7.
- Processed preparation yields the same instructions, final state, complete trace
  hash and all 16 PNG bytes. Its source encoding/block hash intentionally differs.
- An authored branch bypassing admission completes at tick 1 with zero tasks and
  no banner. No phantom completion or renderer acknowledgment is generated.
- Missing event/artwork fails before check output creation. Unit tests additionally
  cover corrupt hashes, missing payloads, invalid index values and wrong identities.
- Whole original events 4/401 fail explicit retail provenance checks. They also
  fail on unsupported opcode 0x19 at offset 4 under an adversarial false authorship
  assertion; neither produces a prepared root or executes an instruction.
- The legacy RGB8 run repeats all 16 files, including its receipt, from
  `scratch/banner-078-preview-a` exactly. Historical evidence and profiles stay intact.

Locked solution restore, zero-warning Release build and all **1,629 .NET tests**
pass, including 75 new (CLI 24, Content 39, Client 12). All **163 Python tests** pass.
Snapshot omission/repetition tests compare complete VM/effect/task/identity state
and ordered traces while actually composing RGB8 and five-bit frames.

This is bounded authored integration. Remaining gates include additional event
capabilities/workers, Ready/other conditions, original startup and full original
event/campaign playback. Each expansion requires its own source/readiness contract.
The owner's standing commit/push permission applies after validation.
