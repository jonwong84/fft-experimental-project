# Bounded Accumulate implementation — ZOD-105

Date: 2026-10-26. Managed base: `402d4d9`; clean on entry. The read-only reference
checkout was rechecked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df` before
translation. The [ZOD-104 trace/contract](../architecture/zod-104-accumulate-action.md)
owns callers, fields, widths, ordering, original-data identities and exclusions.
The [implementation evidence](../evidence/combat/accumulate-implementation-v1.json)
maps ACC-001..020 to executed tests. ACC-021..032 remain future work.

## Implemented behavior

`AccumulateFormula.IncreasePhysicalAttack` is ordinary named C#: admitted PA 1..99
increases by 1 and caps at 99. In this closed profile equipment PA bonus is
explicitly zero, so current base and effective PA coincide. This does not generalize
the original stat calculation to equipment bonuses or change job definitions.

`ImmediateAbilityBattleSimulator` validates actor, active turn, unused Act, effect,
explicit learned grant and supported state before publication. It calls the shared
`ConfirmAct`, builds one immutable self update and emits turn events, ability
resolution, then PA-changed only if PA changed. HP/MP, position, facing, clock,
other units and RNG are retained. At cap it still consumes Act and emits resolution.
All rejections retain the input snapshot and emit no events. Direct probes are pure.

`ImmediateAbilityBattleProfile` snapshots closed effects and distinct per-unit
grants. Every battle unit has an explicit zero-equipment-bonus binding. Setup
requires PA 1..99 and sword power within the worst reachable PA-99 signed-16
envelope: `(2 * 99 - 1) * power <= 32767`. Existing sword-only setup retains its
PA domain and calculations. The original sword resolver and RNG draw ordering
are unchanged; later attacks read the boosted PA already in the combat snapshot.

The existing two-unit lifecycle accepts `UseAbility(actorId, abilityId)` only when
the definition explicitly selects this profile. It retains Move/Attack/Wait,
first-KO completion and restart. All post-completion commands reject. A separate
`SyntheticAccumulateBattle` sample selects this capability; the old sample remains
sword-only. Synthetic unit stats and explicit grants are authored inputs, not a
reconstructed original encounter or progression system.

## Content and import

Learning metadata remains `fft.content-metadata` schema 1. Its separate companion
is `fft.immediate-ability-content` schema 1, with package/profile/dependency IDs and
ability ID, effect, target, amount, charge ticks and MP cost. It admits exactly
`self-pa-increase`, `self`, 1, 0, 0. Parsing rejects unknown/duplicate/missing fields,
unsupported versions, nonintegral values, wrong namespaces, duplicate definitions,
oversized input and unsupported effects. Writing sorts by stable ID and uses UTF-8/LF.

`ValidateAgainst` requires the selected metadata package/profile and matching
ordinary-action learning records. `ImmediateAbilityBattleSetup` resolves the
selected immutable base-job setup against those metadata instances, then validates
each supplied learned grant against a declared skillset slot and executable effect.
Membership alone grants nothing. Authored slot indices are not restricted to
the original table's 16/22 slots; an authored slot 2048 is exercised. Original
slot encodings remain an offline import concern.

`OriginalImmediateAbilityRecords` decodes all fourteen unsigned secondary bytes;
its closed validator rejects any unsupported field. The offline
`OriginalAccumulateImporter` verifies the prepared executable's exact size/hash,
job74 → skillset5 → action slot0/ability146, primary learning metadata, all secondary
fields, default menu0 and empty status record0. It preserves six source records
with mapped offsets/raw bytes in scratch provenance. Its separate synthetic
layout-decoding entry point never certifies an edition/hash. The importer neither
grants the action nor derives unit stats/rewards/AI/presentation.

From the repository after building the CLI, choose a fresh output directory:

```powershell
dotnet scratch/accumulate-105-artifacts/bin/Zodiac.AssetTool.Cli/release/Zodiac.AssetTool.Cli.dll import-accumulate --exe scratch/all-data-extraction/SCUS_942.21 --metadata scratch/imported-squire-check-a/content.package.json --output scratch/accumulate-105-import-new
```

Output is `ability.package.json` plus `ability.import.json`. The existing importer
path/hash/reparse guards confine original inputs/outputs to scratch; both files
are staged and published together after validation. Existing destinations are
preserved. The runtime consumes prepared definitions; no C checkout, BIN or
original executable is called or linked by simulation.

## Persistence boundary

Ability-enabled states explicitly reject `TwoUnitBattleDefinition.Restore` and
existing battle-save JSON/file read/write entry points, even before any boost or
after an unchanged PA-99 use. File Save validates before touching the destination.
This prevents silently losing current PA while the old save schema reconstructs
PA from initial definitions. Existing sword-profile schema-1/2 readers and schema-2
output remain supported. **ZOD-106** adds the new profile's dynamic-PA persistence.
World checkpoints/slots and all save formats are unchanged in this milestone.

## Executed validation and limits

Locked isolated restore and Release build passed, with zero warnings/errors:

```powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/accumulate-105-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/accumulate-105-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/accumulate-105-artifacts
python -m unittest discover -s tools/verification -p 'test_*.py'
```

All **2,438 .NET tests** pass: Content 718, Framework 847, Engine 24, AssetTool 206,
CLI 237, Client 406. The **89 new tests** cover the first twenty contract cases
and their boundary/negative variants. All **293 Python tests** pass; none were
added. Existing sword fixtures, battle journals, saves and unrelated client checks
pass in the complete suite. Two initial focused failures were test issues:
ImmutableArray identity comparison and an incorrect assumed next actor after
Wait. Corrected tests retain the existing 20/40-CT refund ordering; those failed
results remain in scratch alongside the successful focused/final results.

The PA5/zero-bonus example proves a later noncritical power4 hit deals 24 instead
of 20 with identical six-draw attack RNG continuation. Move→Act, Act→Move and
Act→Wait preserve their existing turn consequences. A 100-use run saturates at
99, records rejected duplicate Acts and replays every state/event/RNG receipt
against fresh definitions. Readiness draws remain separate. A max-draw-counter
action succeeds without drawing; later readiness fails atomically as before.

Two actual verified imports under `scratch/accumulate-105-import-a/b` match both
files byte-for-byte. Independent audit compares each of six provenance records
and address mappings to the prepared executable and verifies the semantic effect.
Fresh metadata and sword imports match all four earlier files exactly. The
prepared executable and original metadata hash remain unchanged. All game-derived
files, reports, TRX and one-off audit scripts stay ignored under scratch.

There is no new native/interactive check or visual approval. Editor, Windows game
client and Tessera Engine production code, project/package locks and existing
package/save formats remain unchanged. ZOD-107 will add the Windows action after
persistence support. This is a bounded source-derived authored battle proof;
original/emulator execution, full ability equivalence, equipment bonuses,
statuses/reactions, learning/progression, rewards, AI and animation/timing are
not established. All documented deferred work remains deferred.
