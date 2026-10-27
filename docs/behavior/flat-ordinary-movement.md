# ZOD-121 — Certified flat ordinary movement query

Date: 2026-10-26. Managed base: afa4799. Tracked files were clean on entry;
three owner-authored untracked classes under Zodiac.Content/Definitions/Common
were present, left untouched and excluded from this commit. The SDK solution
build included those drafts; the new query has no dependency on them.
Read-only decomp was freshly checked clean at
11464943bbdbd2a0af022d90cb7f10455cf911df before translation and after validation.
See the [source contract](../architecture/zod-120-flat-ai-movement-contract.md)
and [implementation evidence](../evidence/ai/flat-movement-implementation-v1.json).

## API and certification

`FlatOrdinaryMovement.Evaluate(snapshot, actorId, profile, destination = null)`
is an opt-in geometric query over the existing AttackBattleSnapshot. With no
destination it returns ranges; with a destination it also returns an exact
physical path. It is independent of active actor and clock phase. Either living
actor may query; a previous Move filters AI origins but does not forbid asking
about physical geometry. The result grants no command authorization.

`FlatOrdinaryMovementProfile(ground, firstOpponent, secondOpponent)` explicitly
certifies two distinct stable opponents and an ordinary ground surface. The
caller also certifies no mounts, stepping-stone flags, traps, pickups, changing
teams or unrepresented movement/status effects. No raw original terrain/team
flags are inferred from IDs. The bounded input model cannot represent those
general conditions; this certificate is an admission requirement, not a detector
for arbitrary original unit records.

The query checks the full state on every call: exactly those two distinct,
nonoverlapping living combatants, one lower layer, dimensions 1..16, all tiles
clear/dry/flat at height10 with the certified surface and no half-height/slope/
ceiling/movement flags. Both movement profiles must be Normal, body6, no traits,
Move/Jump in 0..255 and an explicit unblocked effective cost1 for this surface.
Empty prototype cost dictionaries reject; no defaults are synthesized. Extra
unused surface costs are immaterial on the single-surface rectangle.
Living Critical units are admitted because that status does not change this
movement geometry; Dead units reject. Current combat metadata remains unchanged.
The map bound belongs to this compatibility profile, not BattleMapDefinition.

Null snapshot/profile and malformed certificate constructor arguments throw
programmer errors. Runtime rejections are typed UnknownActor, UnsupportedProfile
or InvalidDestination and return no partial outcome plus the incoming RNG.
Unknown actor takes precedence over profile checks; profile checks precede path
validation. Invalid path requests include the origin, occupied enemy, unreachable
or over-budget endpoint, nonlower layer and outside coordinates.

## Outputs and original rules

Successful results carry immutable arrays of:

- Physical destinations, excluding origin and enemy, in lower-layer y/x order.
- Positive remaining ranges including origin: min(Move,124)+1-distance.
- AI origins, adding origin; only origin remains when that actor has moved.
- Reverse target-distance rows toward the other opponent, every x for each
  nonempty AI-origin row. Omitted rows mean unqueried, not zero filled.
- Optional path tiles including origin and endpoint, one packed byte per adjacent
  step, and final execution heading as separate data. No facing is applied.

EffectiveMove caps at124; JumpHalf is min(Jump,7)>>1. Ordinary living enemies
block traversal and cannot be jumped across at equal height. Unit-cost shortest
distances are computed with a private managed queue over the certified graph.
Path selection is a separate backward pass: prefer the previous predecessor
direction, initially East; otherwise first E/N/W/S. This avoids relying on the
queue's discovery order. Straight steps remain separate bytes. Names explicitly
map to source-packed East0, North192, West64, South128 and managed BattleFacing.

The reverse source pass removes all occupants, so its distances reduce to
Manhattan here even when the physical route requires a detour. This reduction
is used only after full closed-profile validation; it is not a general terrain
pathfinding replacement. Jump does not change the selected adjacent path on this
open flat graph.

The query reads current settled coordinates each time and caches nothing across
states. Tests perform an actual existing critical sword push, query both actors
afterward, verify the old enemy tile is free and the new tile blocks, then verify
a lethal result leaves the living-opponent profile. RNG word/count, combat stats,
facing, CT/ticks/phase, Move/Act choices and completion receipts are never changed.
An exhausted ulong draw count is accepted because this query consumes zero draws.

Original AI preview movement preserves facing while real walking publishes its
last-step heading. This API supplies heading as data and performs neither operation.
Existing BattleSimulator, AttackBattleSimulator and FacingTurnBattle Move APIs
remain unchanged. The default prototype can still overestimate range behind an
enemy and preserve incoming facing; regression tests retain both behaviors.
No movement-policy enum, content/scenario/save/native activation, renderer, AI
selector/interface, candidate clock or authoritative decision RNG runner is added.

## Validation

Locked isolated solution restore and Release build pass with zero warnings/errors.
All **3,567 .NET / 378 Python tests pass**, including 152 new managed cases.
Ninety-six cases execute all 24 admitted ZOD-120 flat-route vector IDs for both
actor identities and both collection orders. Other source-description families
remain descriptions; the query implements no dispatch or destination scoring.
All 18 FMP cases have completed mappings in the new implementation evidence.
The ZOD-120 fixture's historical unexecuted statuses remain unchanged.

Coverage includes literal byte/path/heading expectations, caps and maximum map,
independent Floyd-Warshall distances, enumeration/ranking of all shortest routes
on small maps, disconnected row/column corridors, both consumed-choice orders,
current post-push coordinates, full-profile rejection and no partial results.
Real terrain-cost and adjacent-step prototypes match admitted membership in the
detour cases. The default Manhattan prototype's extra cells are compared with
every admitted source vector. Repeated probes at exhausted RNG preserve state.
The first focused run failed in a new snapshot test helper's typed dictionary-key
serialization and a noncritical seed used for the push setup; both test fixtures
were corrected before the final passing run. No runtime change was needed for
those failures. Initial reports are retained under ignored scratch/.

Two final source/history audits match the before report: 15 source files / 42
functions, 68 historical files, 21 managed baseline files and two prepared metadata
records. Implementation evidence additionally fingerprints unchanged historical
source artifacts and legacy files. No C reference/original/emulator or native
execution; no additional manual headless proof was launched. Prepared assets,
builds, logs, TRX and builders remain ignored under scratch/flat-movement-121-*
and scratch/build_flat_movement_121_evidence.py.

```powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/flat-movement-121-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/flat-movement-121-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/flat-movement-121-artifacts --logger trx --results-directory scratch/flat-movement-121-tests-final
$env:TEMP = Join-Path (Get-Location) 'scratch'
$env:TMP = $env:TEMP
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_flat_movement_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --overlay scratch/all-data-extraction/BATTLE.BIN --executable scratch/all-data-extraction/SCUS_942.21 --output scratch/flat-movement-121-source-final-a.json
```

Use new report names for audit reruns. The audit is still the historical ZOD-120
source-description verifier and correctly reports its own productionImplemented
false/unexecuted inventory; ZOD-121 evidence owns the completed C# mapping.

**Recommended next: ZOD-122**, a bounded source/design contract for an opt-in
voluntary Move execution adapter. It must validate command authority, settle tile
and real path heading before automatic completion receipts, preserve AI preview
placement, and decide admission after KO without silently widening the query.
Specify atomic rejection, both command orders, replay and unchanged legacy APIs
before changing execution. Full AI/search/candidate clock and broader terrain
remain separate work. Editor redesign and ZOD-106 remain deferred.
