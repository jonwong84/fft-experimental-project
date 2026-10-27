# Closed sword candidate scoring — ZOD-127

`SwordAiCandidateScoring.Evaluate` is the additive opt-in entry for one supplied
ordinary sword candidate. It validates an explicit scoring certificate, calls the
unchanged ZOD-125 projection once, and scores that fresh result. It accepts no
cached or external projection. No live command, ranking, search, policy, receipt,
save, editor or authoritative RNG integration is activated.

The [ZOD-126 contract](../architecture/zod-126-ai-score-contract.md) pins the source
arithmetic and admission. Fresh clean read-only decomp remains
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[implementation evidence](../evidence/ai/score-implementation-v1.json) records
actual managed execution separately from historical source inventories. This is
source-derived managed validation, not original-game execution equivalence.

## Explicit certificate

`SwordAiScoreProfile` snapshots exactly two distinct valid unit certificates.
Each `SwordAiScoreUnitCertificate` contains stable ID and `SwordAiScoreFacts`:

- `NonMonster` certifies the original non-monster unit classification.
- `AllSevenEquipmentSlotsUnchanged` covers every baseline/current serialized
  equipment entry, including unchanged empty entries.
- `StableOpponents` certifies both units are initially targetable, cached original
  allied-actor/hostile-target classification is valid for either acting ID, and
  team/control changes or stale target flags are absent.
- `ZeroCurrentAndBaselineGolem` covers both snapshots on both scored teams; current
  zero alone would omit the original Golem-loss penalty.

`ClosedProfile` is the named combination of all four assertions. These are managed
certificate bits, not original packed flags. Missing, default/None, unknown or
additional bits reject as `UnsupportedScoreProfile`, before projection or draws.
Both ID sets must match the original state and candidate profile. Malformed
certificate collections throw argument errors. No fact is inferred from a sword
ID; unrepresented original flags/equipment/team/Golem data remain the caller's
responsibility. A future importer must derive these assertions from actual data.
The existing `AttackUnitState` constructor enforces MP=maxMP=0; it is not expanded.

```csharp
var scoreProfile = new SwordAiScoreProfile([
    new(actorId, SwordAiScoreFacts.ClosedProfile),
    new(targetId, SwordAiScoreFacts.ClosedProfile)
]);
var result = SwordAiCandidateScoring.Evaluate(original, candidate, weapon,
    candidateProfile, scoreProfile, decisionRng);
decisionRng = result.RngState; // Explicit decision progress; the live state is unchanged.
```

Call this wrapper instead of first projecting and then trying to submit that
result. Its public signature accepts original inputs only. Source inspection
confirms one projection call site; tests compare the complete returned projection
and exact RNG progress with the unchanged standalone query. No validation replay,
new mockable decision interface or generic authentication/serializer is added.

## Result and arithmetic

`SwordAiScoringResult` distinguishes Rejected, NotValuable and Scored:

- Unsupported scoring certificate: no projection or score, exact incoming RNG.
- Projection rejection: preserve the fresh projection and its typed reason, no
  score, and its unchanged input RNG. Late draw/tick exhaustion stays atomic.
- NotValuable: preserve the projection's four draws and absent outcome; no score.
- Scored: retain the fresh projection, signed16 `Priority`, independent positive
  byte `HitPercent`, and original TieKey-order per-unit diagnostics.

Each contribution exposes HP128 integer floor, status weight (None 0, Critical −32,
Dead −192), equipment 357, unit total before enemy sign, signed contribution and
running signed16 priority. The final score is absolute projected allied-minus-
hostile state score. Equal human equipment terms cancel only after both units
are scored. A real score 0 remains Scored. With actor 50/50, target 25/50 gives 64;
Critical target 10/50 gives 135; Dead gives 320. Initial Critical actor 10/50 and
target 25/50 give −71, not the 64-point improvement from its earlier −135 state.
Partial accuracy changes hit percent without probability-weighting that priority.

Only the fresh private projection is scored, so consistent HP/status and source
order are already guaranteed. Newly Dead units stay present and receive their
unchanged equipment term. The score stage does not reapply a living-target guard,
compute campaign victory, add CT/choice/tile/facing terms, or mutate any state.
Source narrowing follows each unit; admitted priority −160..320 cannot overflow.
No broader status/equipment/MP/Golem scoring is silently approximated.

The score stage consumes **zero additional RNG draws/ticks/choices**. The complete
wrapper still consumes the projection's four formula draws and any upkeep draws;
the returned stream is copied verbatim from the retained projection. A successful
projection ending at `ulong.MaxValue` draw count still scores. There is no live
battle RNG commit, active turn or command authority in the result.

## Validation and reproduction

Locked restore, zero-warning/error Release build and **3,957 .NET / 432 Python
tests pass**. All 14 ASC cases have coverage in 133 new managed cases across 15
methods. The tests address all 14 normalized scoring proposals:13 execute the new
wrapper; the MP proposal is rejected by existing live-model construction, because
maxMP 1 cannot enter that API. Other normalized unsupported facts are represented
by missing certificate assertions or mismatched IDs. Ten source post-state kernels
are reached in 40 cases across both actors and collection orders. An isolated Dead
kernel is materialized as pre-hit HP1 with a one-damage attack, preserving initial
Dead rejection. Historical fixtures are unchanged; those adapters are explicit.

Additional checks cover canonical and negative absolute scores, unequal/large
maxHP, independent 1..100 accuracy, source order with enemy first, both action
orders, every short formula/upkeep budget, long corpse upkeep, MAX draw/tick
boundaries, certificate/identity/authority failures, null guards, replay and
independent decision streams. Input/profile snapshots remain unchanged. The first
build caught a test factory passing an enumerable where the map requires a read-
only list. Materializing its tiles fixed the test; production code was unchanged.
The premature test dispatch had no assembly to run. The final focused 133 and
full 3,957 cases pass; there were no failing executable scoring assertions.

```powershell
$artifacts = Join-Path (Get-Location) 'scratch/ai-score-127-artifacts'
dotnet restore Fft.sln --locked-mode "-p:ArtifactsPath=$artifacts"
dotnet build Fft.sln -c Release --no-restore "-p:ArtifactsPath=$artifacts"
dotnet test Fft.sln -c Release --no-build --no-restore "-p:ArtifactsPath=$artifacts" --logger trx --results-directory scratch/ai-score-127-tests-replay
$env:TEMP = Join-Path (Get-Location) 'scratch'
$env:TMP = $env:TEMP
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_ai_score_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --executable scratch/all-data-extraction/SCUS_942.21 --output scratch/ai-score-127-source-replay.json
```

Use new output paths for replays/audits. Managed tests need neither prepared EXE
nor decomp. The old ZOD-126 auditor deliberately retains its historical
unimplemented status and unexecuted ASC inventory; this milestone's new evidence
records the completed production coverage. Three matching source/data/history
audits cover 11 files / 14 functions, 101 preserved and 29 managed baseline files,
plus two hash-verified metadata records. New evidence fingerprints 106 unchanged
historical artifacts. No old API/fixture/evidence was rewritten.

No original/C/emulator/native execution, new manual presentation proof or remote
CI check. Owner Common/Ability drafts remain untouched and excluded; SDK builds
included those drafts. Editor UI redesign and ZOD-106 battle checkpoints remain
deferred. Next: ZOD-128, source/design comparison of two supplied candidates and
the source proximity tie, without full enumeration, ranking lists or policy.
