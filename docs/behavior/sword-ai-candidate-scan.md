# Ordinary sword AI candidate scan

ZOD-131 implements one certified ordinary Attack pass over a **supplied movement
scenario**. It returns hypothetical candidate diagnostics and a retained index.
It does not select scenarios, insert ranked actions, choose commands or commit
live RNG. Source: read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; [ZOD-130's trace and erratum](../architecture/zod-130-ai-scan-contract.md)
remain the source contract. This is source-derived managed coverage, not original
or emulator execution.

## Inputs and behavior

[`SwordAiCandidateScan.Evaluate`](../../src/Zodiac.Framework/SwordAiCandidateScan.cs)
takes the original snapshot, `SwordAiScanScenario`, weapon, existing candidate/
score/comparison profiles, `SwordAiScanProfile`, and explicit decision RNG.

The scenario copies its origin collection. It must be a unique subset of the
original movement query's `AiOrigins`; a nonempty mask needs a destination in
that mask, and an empty mask needs null. Unsorted input is accepted. Occupied,
wrong-layer, out-of-bounds and unavailable movement cells reject before scoring.

The new profile uses named boolean certificates: ordinary Attack tile policy,
original scenario mask, baseline target selection and fresh ability-best reset.
Each defaults false. `TargetActive` is a required boolean argument; explicit
false is supported inactivity. Existing comparison provenance and candidate/
score certificates still apply. These facts assert source context absent from
live state; IDs cannot establish it.

Admission checks the existing score, living/current-action, sword, movement and
signed-envelope domains even when the scan is empty or inactive. It calls pure
profile/movement validation, without projecting a throwaway candidate. Unsupported
facts reject; they do not become guessed successful emptiness.

On admitted lower-layer ground, cast origins are the supplied mask intersected
with the sole target's actual range-one sword panels. AoE-zero aims at that
original occupied target panel. The source distance-zero diagnostic cannot enter
this public query because the movement profile excludes the living target tile.
The query sorts by layer/y/x, never by final destination:

- An origin different from the actor's original tile emits MoveThenAct there.
- The actor's origin emits ActThenMove to the scenario destination at its source
  scan position. Stay emits once. Distinct origins sharing a destination survive.

Baseline actor/target delay and foe proximity are derived once from original
state. Each emitted candidate calls the unchanged scorer once on that state,
carrying only the preceding evaluation's decision RNG. Projected HP/KO/CT never
become the next input or rebuild the cache. Zero accuracy keeps its four draws
and permits later origins. Discarded scored candidates keep their draws too.

The private incumbent starts absent. Greater signed priority, then greater hit,
then strictly lower baseline proximity wins; exact ties retain the earliest
scored candidate. Zero/negative scores are real. The closed same-weapon profile
does not exercise the general rank domain: signed extrema and broader source
ordering remain isolated Python descriptions, not public C# conformance claims.

## Results and failures

`NoCandidates` has an empty diagnostic list; `NoScoredCandidate` has evaluations
but no retained score; `Scanned` has a retained index. Successful outcomes expose
origin, target panel, candidate order/destination, fresh score/projection, RNG
and baseline cache/proximity. They confer no executable state or receipt.

`UnsupportedScanProfile`, `UnsupportedState` and `InvalidScenario` fail before
scoring. State/profile errors include an existing typed scoring/projection reason
where applicable. A rejected emitted evaluation returns `EvaluationRejected`,
its emission index and nested reasons. Every failure returns the original whole-
query RNG and no partial outcome/cache/incumbent. That is managed transaction
policy, not original source cursor rewind. Supported empty/inactive scans need
no draw or tick headroom; late evaluation overflow rolls back the whole query.

## Acceptance coverage and validation

[`SwordAiCandidateScanTests`](../../tests/Zodiac.Framework.Tests/SwordAiCandidateScanTests.cs)
executes 60 cases, including all 30 existing normalized scan proposals once.
The historical fixture's proposal/unexecuted labels remain unchanged; this
implementation records current execution separately in the [evidence receipt](../evidence/ai/scan-implementation-v1.json).

- ASN-001/008: explicit certificates, bound pair, inactive/empty admission checks.
- ASN-002: scenario snapshot, subset, uniqueness and destination rules.
- ASN-003/004: actual range/target tile, source order, reversed inputs, both IDs,
  original slot order and the original map edge.
- ASN-005/006: own-origin order, stay-once and duplicate final destinations.
- ASN-007/010: empty/filtered/inactive versus all NotValuable, including MAX bounds.
- ASN-009/012/013: evaluate-once draw streams, fresh original HP/KO and stable cache.
- ASN-011: hit/proximity ordering, earliest ties and real zero/negative priority.
- ASN-014/015: late draw/tick rollback, failed index, exact fit, replay and branches.
- ASN-016: unchanged old evidence/fixtures; synchronous API with no source frame
  scheduler or checkpoint implementation. No structural reflection assertion added.

Locked restore, Release build (zero warnings/errors), **3,608 .NET / 477 Python
tests** pass. The initial 56 focused cases and subsequent full 60-case scan set
passed without corrections. Old scorer/projection/comparison production files
are unchanged. The historical scan contract was audited at managed snapshot
`667ed4688540069a924962ba59ad2f2ac723f320` against the fresh clean reference:
16 files, 55 functions, three prepared metadata records, 119 preserved artifacts
and 33 then-current managed files passed. This audit does not execute current C#.
Raw original data and logs/TRX/source reports remain in `scratch/ai-scan-131-*`.

Use the [current validation workflow](../development/test-maintenance.md). A focused
run after building is:

```powershell
dotnet test tests/Zodiac.Framework.Tests/Zodiac.Framework.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~SwordAiCandidateScanTests
python tools/verification/verify_historical_contracts.py --checkpoint verify_ai_scan_contract
```

For the optional reference/prepared-data audit, export the recorded source-design
checkpoint under ignored scratch and run its original verifier there. The reference
must still be at the recorded revision; do not modify it to satisfy an audit:

```powershell
git archive --format=zip --output=scratch/scan-130-source.zip 667ed4688540069a924962ba59ad2f2ac723f320
Expand-Archive -LiteralPath scratch/scan-130-source.zip -DestinationPath scratch/scan-130-source
python scratch/scan-130-source/tools/verification/verify_ai_scan_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --executable C:/repositories/fft-experimental-project/scratch/all-data-extraction/SCUS_942.21 --output scratch/scan-130-source-audit.json
```

No scenario/content/save/editor/runtime policy activation or owner draft edits.
Original source resume ownership remains documented, with no native scheduling
equivalence claim. [ZOD-138 composition](sword-ai-composition.md) now owns movement
scoring, fresh baseline reuse and one-pass ranked admission around this scan.
Lottery, the replaceable decision interface and authoritative execution remain
separate work. Editor UI and ZOD-106 remain deferred.
