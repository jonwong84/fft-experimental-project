# One supplied sword AI candidate projection — ZOD-125

`SwordAiCandidateProjection.Evaluate` is an opt-in, pure managed query over one
supplied actor/target/destination/order. It composes the existing ordinary movement
and sword formula queries with the [ZOD-124 source contract](../architecture/zod-124-ai-candidate-contract.md).
It returns hypothetical unit data and explicit decision RNG, without modifying
the battle, selecting a candidate, executing commands, scoring or issuing receipts.

Reference checkout freshly checked clean/read only:
**11464943bbdbd2a0af022d90cb7f10455cf911df**. Source, prepared status metadata,
preservation checks, managed tests and completed ACP cases are bound in the
[implementation evidence](../evidence/ai/candidate-implementation-v1.json).
These are source-derived managed checks, not original-game execution equivalence.

## Admission and explicit certification

Construct `SwordAiCandidateProfile` from matching existing flat sword/movement
profiles and exactly one `SwordAiUnitBaseline` death-counter byte per opponent.
The immutable profile certifies original UNIT_TURN/resume PICK_UNIT/current actor,
original slot order represented by the existing stable `TieKey`, and no charges,
mimes, timed status/upkeep/terrain/script effects, restrictions, immortality,
formation flags or mounts. Existing profile exclusions still apply. Unrepresented
original flags cannot be detected in the current models; the caller must supply
this certificate honestly. Profile constructors reject malformed certifications;
they do not import or infer original flags from IDs.

The query validates known actor/target, initially living units, an active unused
action, distinct opponents and supported order, matching profile IDs, complete
sword geometry and the existing movement query. Both profiles must certify the
same ordinary ground. Whole movement-profile restrictions remain effective,
including the 16x16 lower-layer bound, ordinary cost1 terrain and byte Move/Jump.
Candidate destination must be an admitted AI origin on the **original** state;
prior Move allows only stay. The formula then validates weapon, signed arithmetic
envelope and action range on a privately staged active snapshot. All admission
failures precede random draws. Null required arguments are programmer errors.

Original clock/command ordering is certified rather than guessed from a pending
UI or host state. Initial Dead input is `TerminalInput`; inactive/completed turns
or an already-consumed action are `UnavailableAction`. An authored host with its
own terminal policy must enforce that policy outside this API. This query does
not read a host scenario, facing receipt, save or editor session.

## Results and ownership

The RNG argument is the **decision stream**, independent of the live snapshot's
unchanged RNG. Callers explicitly carry the returned stream between supported
evaluations, including candidates they later discard.

```csharp
var profile = new SwordAiCandidateProfile(swordProfile, movementProfile,
    [new(actorId, actorDeathCounter), new(targetId, targetDeathCounter)]);
var candidate = new SwordAiCandidate(actorId, targetId, destination,
    SwordAiCandidateOrder.MoveThenAct);
var result = SwordAiCandidateProjection.Evaluate(battle, candidate, weapon,
    profile, decisionRng);
decisionRng = result.RngState;
// Inspect result.Outcome as hypothetical data. The live battle is unchanged.
```

- `Rejected`: typed rejection, no outcome and exact incoming decision RNG.
  Includes atomic rollback after a late upkeep RNG or hypothetical tick overflow.
- `NotValuable`: supported zero-accuracy formula, four retained evade draws,
  no outcome and no clock work. It is not an unsupported-input rejection.
- `Projected`: original actor/target facing, final candidate tile, HP/maxHP,
  None/Critical/Dead, death-counter byte, stable tie key, Speed, simulated CT and
  choice flags for each unit, plus accuracy/damage, pre-clock turn-end CT,
  elapsed/final hypothetical ticks, move-opportunity identity and upkeep pick
  trace/raw rolls. Units are ordered by stable tie key, independent of collection
  order. The result has no active turn, events, receipt or executable snapshot.

Move-first stages its action at destination; Act-first stages it at the input
tile. Private placement preserves facing and never uses a real path's heading.
Positive partial accuracy applies full normal damage; Critical uses floor(maxHP/5),
and newly Dead replaces Critical with death counter 3. No live KO count changes.
A lethal projection remains supported and reports `TargetKnockedOut`, without
claiming campaign victory or invoking the authored first-KO terminal policy.

Turn-end consumes Act, retains prior Move/stay behavior, refunds unused Move 20,
caps CT at 60, then checks already-ready units **before** a CT addition. Highest CT,
then lowest original tie key wins; CT is reduced modulo 100 and each pick consumes
one real upkeep draw. The selected living unit's choices reset, its move
opportunity is retained and its transient has_turn is cleared as in source.
Byte Speed additions cap CT at 254. A projected corpse still gains CT and consumes
upkeep draws, but has no turn and no death-counter decay. It is not passed back
through the initial living-opponent movement query. Positive living Speed bounds
the clock to at most 100 additions under this certificate; no general scheduler,
wall-clock suspension or arbitrary original table size is added.

No outcome is a new `AttackBattleSnapshot` or `TurnCycleState`. Thus a projected
clock result cannot authorize the next command, and carrying decision RNG does
not silently change live battle RNG. A future decision owner must define the
authoritative carry/commit boundary separately.

## Validation and reproduction

Locked restore and zero-warning/error Release build pass; **3,824 .NET / 414
Python tests pass**. All 16 ACP cases have managed coverage in 128 new cases across
18 methods. Eighteen historical normalized candidate proposals are materialized
and compared against production results; their scalar certificates did not encode
complete maps. Tests supply legal action origins for each order, and turn failed
placement/profile/terminal certificates into explicit invalid destinations,
unsupported geometry or preexisting KO states. These adapters are documented in
the test; no historical vector is rewritten or relabeled as original observation.
Additional regressions cover both actor IDs, collection orders, all incoming
facings, partial/certain evade, arithmetic/HP limits, target CT100..254,
cap/modulo/ties, corpse picks, every formula/upkeep budget, tick boundaries,
profile/identity/authority failures and discard/carry/replay.

The first focused run had 34 failures in test snapshot serialization, because a
typed terrain-key dictionary cannot be serialized as JSON properties by the
default converter. Projecting those dictionary entries explicitly fixed the test
helper; production code was unchanged. The final focused 128 and full 3,824 cases
pass. No broad serializer or runtime JSON change was added.

From the repository root (absolute artifact path avoids project-relative roots):

```powershell
$artifacts = Join-Path (Get-Location) 'scratch/ai-candidate-125-artifacts'
dotnet restore Fft.sln --locked-mode "-p:ArtifactsPath=$artifacts"
dotnet build Fft.sln -c Release --no-restore "-p:ArtifactsPath=$artifacts"
dotnet test Fft.sln -c Release --no-build --no-restore "-p:ArtifactsPath=$artifacts" --logger trx --results-directory scratch/ai-candidate-125-tests-replay
$env:TEMP = Join-Path (Get-Location) 'scratch'
$env:TMP = $env:TEMP
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_ai_candidate_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --executable scratch/all-data-extraction/SCUS_942.21 --output scratch/ai-candidate-125-source-replay.json
```

Use a new output path for each source audit. The prepared EXE and reference are
optional only for the old source/data audit; managed tests need neither. The
ZOD-124 audit deliberately reports its **historical** unimplemented status and
unexecuted ACP inventory; ZOD-125's separate evidence records production coverage.
Three audits match at 15 source files / 31 functions, 92 preserved artifacts and 27
managed baseline files, with one prepared status-table check. New evidence
fingerprints 97 unchanged historical artifacts including ZOD-124's guide, fixture,
tools and evidence. Existing implementations/fixtures/APIs remain unchanged.

No original/C/emulator/native execution, scenario/content/save/editor activation
or authoritative decision runner. Owner Common/Ability drafts remain untouched
and excluded from the commit; SDK compilation included them. Remote CI and a new
manual presentation run were not performed. Editor UI and ZOD-106 remain deferred.
Next: ZOD-126, a bounded source/design contract for closed candidate priority
scoring, keeping ranking, enumeration and policy/interface activation separate.
