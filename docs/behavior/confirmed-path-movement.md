# ZOD-123 — Confirmed flat path movement

Date: 2026-10-27. Managed base: 2d5a933, tracked files clean on entry. Three
owner-authored untracked Common editor classes were present and remain outside
this change. The SDK build includes those independent drafts; the adapter does
not depend on them. Read-only decomp was freshly checked clean at
11464943bbdbd2a0af022d90cb7f10455cf911df before translation and after validation.
See the [source/design contract](../architecture/zod-122-voluntary-move-contract.md)
and [completed implementation mapping](../evidence/ai/voluntary-move-implementation-v1.json).

## Entry and API placement

`FacingTurnPathMovement.MoveAlongPath(state, command, movementProfile)` executes
one confirmed voluntary Move over an existing FacingTurnState. Begin, Advance,
Attack, WaitWithFacing and FinishFacing remain on FacingTurnBattle. No public
state/receipt constructor, attach/restore method or caller-supplied path is added.

The source/design proposal named the entry FacingTurnBattle.MoveAlongPath.
**Implementation placement adjustment:** the new method lives on the additive
FacingTurnPathMovement companion so the existing facing wrapper and its historical
whole-file audit baselines remain byte-for-byte unchanged. The state, receipt,
validation and event contract is retained. The old design/fixtures are historical;
this guide and implementation evidence own the actual entry name. No historical
fingerprint, verifier or public method body was rewritten to make audits pass.

The caller selects the existing FlatOrdinaryMovementProfile, certifying its two
stable living opponents and ordinary ground. For confirmed execution it also
certifies no unrepresented post-movement benefits, charging cancellation, traps,
pickups or scripted/map effects. These original flags do not exist in the small
current snapshot model; the profile is not a general raw-data validator.
The complete current query domain remains: one open dry flat height10 lower
rectangle, dimensions1..16, body6/Normal/no traits, byte Move/Jump and explicit
cost1 for both units. No default terrain costs or post-KO movement mode.

## Atomic command and facing settlement

PathMoveResult contains Accepted, FacingRejection, TurnRejection, PathRejection,
State, Events and Path. Null arguments throw programmer errors. Pending facing
and exhausted revision reject first; TurnCycle.Apply then validates known actor,
active ownership and unused Move. Its result is staged privately. A prior Act
alone permits Move. The current FlatOrdinaryMovement query rebuilds the path;
unsupported profiles and bad endpoints reject before anything is published.

Every rejection retains the identical input wrapper, empty events, no partial
path and unchanged CT/RNG/revision/receipt. No cached query or externally built
path can authorize movement. Stay, occupied, outside, unreachable and over-budget
destinations remain invalid commands. The old Manhattan policy is never a fallback.

Success constructs a new body at the endpoint with HasMoved, the actor's combat
state with the path's final heading, and synchronized choice/CT state. It retains
map/profile references, opponent body/combat, actor HP/status/KO/stats/equipment,
other turn data, RNG and elapsed ticks. It increments revision exactly once and
publishes these events in order:

1. Existing wrapped BattleMovementEvent.
2. VoluntaryPathCompletedEvent(actorId, path, previousFacing), including same-heading walks.
3. Existing wrapped TurnChoiceConsumedEvent(ConfirmMove).
4. Existing wrapped TurnEndedEvent only if both choices are now consumed.

Move-first keeps the turn active with no receipt. Act-first then Move caps CT
once through the existing clock and creates a pending receipt only after the
complete snapshot includes tile, real path-facing, flags and CT. Its expected
battle is that exact snapshot instance, with ended actor, new revision and the
same lineage. No public legacy Move is called and patched afterward.

Existing FinishFacing handles this state unchanged. KeepCurrent means the settled
path heading; Fixed/ChooseClosedEnemy use their existing behavior and current
positions. Same-facing completion consumes once and emits FacingSettledEvent;
it does not repeat movement, CT, damage or RNG. Pending blocks all legacy commands
and the new path entry. Old/cross-branch/cross-run/earlier-turn receipts reject.
Replaying the original immutable input is valid and yields fresh branch receipts.

The Move consumes zero RNG draws, including at ulong.MaxValue. Readiness and
Attack retain their separate draws. Revision MAX rejects; MAX-1 may publish MAX,
including pending facing, whose Finish then rejects exhaustion exactly as the
existing wrapper does. No silent reservation or counter wrapping is added.

After a nonlethal push, rebuilding observes the new blocker and frees the old
enemy tile. Dead opponents leave the certified query; post-KO Move rejects.
Existing pending facing-only Fixed/Keep can finish after a lethal Attack, while
the living-enemy chooser rejects. The authored first-KO scenario remains terminal
and is not connected to this entry. No corpse traversal or death scheduler is added.

AI preview placement is unchanged: it must preserve facing when moving candidate
coordinates. The pure query and old Move keep their prior behavior. No content,
scenario, save, native/editor path, policy enum or automatic original-mode switch
activates this adapter. Path animation, tentative movement/cancel UI and general
post-move effects remain outside its scope.

## Validation and reproduction

Locked isolated restore and zero-warning/error Release build pass. All **3,696
.NET / 396 Python tests pass**, with 129 new managed cases across 17 methods.
All 18 VME cases have completed mappings in the new evidence; the ZOD-122 fixture's
unexecuted statuses remain unchanged. Thirty-six cases execute nine positive
normalized Move proposals for both actor IDs/collection orders; 12 execute the
completion proposals. Thirty-four additional cases execute 17 ZOD-120 selected
paths for both actors. Other source helpers, invalid normalized states and UI undo
remain descriptions, not newly executed original behavior.

Normalized Act-flag proposals use the existing ConfirmAct component and internal
friend wrapper setup; revision cases also use the existing friend boundary.
Malformed-profile/inconsistent-receipt fixtures use internal construction to reach
invariants that public Begin rejects. Separate public Begin/Advance/Move/Attack/
Finish chains cover both real choice orders, actual critical push/KO and complete
receipt binding. No public state-injection API was introduced.

Tests cover exact paths/headings/event values/order, private-clock rejection,
profile boundaries, current post-push occupancy, all pending barriers, stale and
earlier-turn receipts, same-heading/same-facing completion, exhausted RNG/revision
and prefix/suffix replay. Both focused and full runs passed on their first run.
Full Python/source audits retain the legacy wrapper, query, policy and scenario
files unchanged. Two final audits match the before report: 13 source files / 33
functions, 82 preserved artifacts and 24 managed baseline files. New evidence
additionally fingerprints the historical source contract/tooling files.

No original/C/emulator or native execution; no additional manual headless proof
was launched. This is source-derived managed validation, not observed original
equivalence. Builds, logs, TRX and builders stay ignored under
scratch/voluntary-move-123-* and scratch/build_voluntary_move_123_evidence.py.

```powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/voluntary-move-123-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/voluntary-move-123-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/voluntary-move-123-artifacts --logger trx --results-directory scratch/voluntary-move-123-tests-final
$env:TEMP = Join-Path (Get-Location) 'scratch'
$env:TMP = $env:TEMP
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_voluntary_move_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --output scratch/voluntary-move-123-source-final-a.json
```

Use new audit output filenames. The verifier still describes ZOD-122's historical
source/proposal inventory and correctly reports its own productionImplemented
false; ZOD-123 evidence owns the completed production mapping.

**Recommended next: ZOD-124**, source/design one closed sword AI candidate's
coordinate-only placement and turn-end/CT simulation boundary, starting from a
prevalidated action projection. Specify copy/restore ownership, both choice orders,
RNG carry and rejection limits before composing a production evaluator. Actual
Move execution must not be reused as AI preview placement. Full search/roster/
policy/interface, general terrain and authoritative decision RNG remain separate.
Editor UI and ZOD-106 stay deferred.
