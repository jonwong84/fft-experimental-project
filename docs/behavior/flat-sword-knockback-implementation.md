# Opt-in flat sword critical knockback — ZOD-115

Date: 2026-10-26. Managed base: dd6210a, clean on entry. Read-only reference
C:/repositories/fft_decomp was freshly checked clean at
11464943bbdbd2a0af022d90cb7f10455cf911df. All 17 file/41 function fingerprints in
the [ZOD-114 source contract](../architecture/zod-114-flat-sword-knockback.md)
matched before translation and again after implementation. The C reference is
never linked or executed at runtime.

Zodiac Framework now supports settled one-tile sword critical knockback through
an explicitly selected flat-ground profile. The existing three-argument Attack
entry point and every current scenario/content/native/save selection remain
edge-only. No UI, content format or persistence feature is activated by this proof.
The [implementation evidence](../evidence/combat/sword-knockback-implementation-v1.json)
maps SKB-001..020 to executed managed checks and records the regression artifacts.

## API and admission

~~~csharp
var profile = new FlatSwordBattleProfile(ordinaryGroundSurface);
var result = AttackBattleSimulator.Attack(snapshot,
    new AttackUnitCommand(actorId, targetId), selectedSword, profile);
~~~

The caller certifies OrdinaryGroundSurface as ordinary ground and selects the
existing ordinary-unit and BoundedSwordWeapon projections: no immortality,
importance-3, mounts, reactions, support/affinity modifiers, charged actions or
nonempty weapon procs. These are explicit semantic projections, not raw
original records whose missing flags are inferred from an ID. There is no new
content projection in this milestone. The profile's stable rules ID is
fft:rules/sword-flat-knockback-v1; it is not a serialized save-profile version.

Before drawing or publishing Act, validate the existing actor/target/turn/weapon
and cardinal range-1 gates plus a stricter complete-map domain: exactly two
distinct ordinary body-height-6 units, normal movement with no traits, a single
lower-layer rectangle, and every tile on the certified surface at height10,
half-height/slope/depth/ceiling zero and no movement flags. Current byte-range
PA0..255, power0..255, same-sign neutral zodiac, zero MP/equipment evade,
Brave10..100 and class evade0..100 restrictions remain.

This rejects blocked/untargetable/cannot-stop cells even away from the attack,
unknown surfaces, upper layers, extra or overlapping units and modified movement.
The cannot-stop restriction deliberately bounds this profile; the original
forced-movement helper does not itself inspect that voluntary stopping bit.
The strict profile also rejects a blocked upper layer that the older edge-only
geometry validator admits. Do not remove the old guard without selecting this
profile. Null programmer arguments throw; unsupported commands return the exact
input snapshot with empty events and unchanged RNG/choices.

## One shared resolution and settled result

Both entry points call the same private AttackCore. The damage arithmetic and
draw sequence are unchanged: accessory/right/left/class evade, critical check,
conditional critical bonus, and the empty-set conditional proc check. Miss,
normal hit and critical hit consume four, six and seven draws respectively.
Retain the signed-16 worst-product gate before any draw and the later 999 cap.

Only an admitted profile plus a nonlethal critical can change position. Candidate
is target+(target-actor) for the required cardinal adjacency. An off-map or occupied
candidate suppresses displacement; the open two-unit rectangle guarantees an
ordinary clear same-height in-bounds landing. Occupancy remains a defensive check,
not support for a third unit. No Brave check or additional RNG draw is introduced.
Lethal equality cancels movement after the full critical budget. A zero-power
critical may push with no HP change. A successful empty proc does not cancel it.

The atomic command collapses the source's knockback follow-up into one settled
snapshot. Actor position is unchanged; both incoming settled facings remain.
Only the target body position changes. Target HasMoved, movement profile,
Move/Act flags, CT, speed and tie key are preserved. Existing actor Act/turn
completion behavior and both RNG copies remain synchronized. Later attacks and
moves use the displaced position; no cached original range is retained.

Events remain ordered: existing turn choice/optional turn end, AttackResolvedEvent,
optional HP change, optional status change, optional KO. A new SwordKnockbackEvent
with target ID and Before/After TilePosition is appended only when displacement
actually occurs. It does not emit a voluntary movement event or transient facing
event. Misses, normal hits, lethal hits and edge-suppressed criticals add none.
The old event shapes and values remain exact under legacy selection.

## Validation

Locked isolated restore and Release build pass with zero warnings/errors.
All **3,044 .NET / 329 Python tests** pass, including 240 new managed cases in
FlatSwordKnockbackTests. No existing C# test or behavior fixture was rewritten.

- All 35 immutable description vectors execute through a test-only projection,
  preserving their stored expectations. The historical description and its
  unexecuted-at-ZOD-114 case inventory stay byte-identical. The existing behavior
  fixture runner/policies are unchanged; this does not turn that description
  into a general executable fixture format.
- 128 cardinal/facing/seed combinations cover both incoming facings, all four
  directions, frontal misses, side/back hits and critical pushes. Four edges,
  equal/over-lethal damage, survive-one, KO-byte wrap, PA0, power0, cap, both
  Brave extremes and the successful-empty-proc seed are covered.
- Critical raw1310/1311 and bonus raw0/6553/6554/32767 are exercised by seeded
  managed attacks. Every four/six/seven-draw exhaustion point checks atomic
  rejection; exact budgets can finish at ulong.MaxValue.
- Both actor IDs and reversed collection orders, all target Move/Act flag pairs,
  Act-then-Move and Move-then-Act, CT refunds, event order, discarded pure probes,
  and every accepted/rejected journal prefix/suffix pass. After a push the target
  cannot attack its old adjacent opponent until moving back; movement range also
  starts at the new position.
- Unsupported geometry and invalid command tests use an exhausted input RNG to
  prove validation precedes draws. A dedicated legacy-vs-profile check preserves
  exact edge-case state/events; historical fixture/save tests remain active.

Exactly four managed headless CLI processes ran, with no native window. The two
sword-save runs repeat 14 files exactly against each other and ZOD-113's retained
output: 13 checkpoints plus check.json, final RNG2473755763/count22. The two
Basic Skill runs repeat four files exactly against each other and the historical
ZOD-110 output: 46 commands/47 fresh-definition continuations, final
RNG4200531631/count58. Existing ability-save guards remain intact.

Two legacy artifact audits match byte-for-byte. They independently check the
existing fixture migration/history, headless damage/push/RNG and four prepared
files/29 historical native files. Two source/history audits also match, verifying
all 17 reference files/41 functions and 34 historical files. Their report still
says the ZOD-114 implementation inventory was unexecuted: it audits that preserved
historical contract, not the new execution status. This milestone's evidence owns
the executed SKB mapping. The eight managed baseline files were checked before
translation only; later intentional source changes are not historical corruption.

No original executable/emulator, new GPU capture or physical-input check was run.
Source-derived vectors plus managed execution do not establish complete original
combat or whole-stream equivalence. Falls, general terrain/layers, extra units,
reactions, rewards, animations and content/native/save activation remain outside
this profile. Editor and ZOD-106 battle checkpoints remain deferred.

## Reproduce

From the repository root in PowerShell, using SDK10.0.402 and Python3:

~~~powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/sword-knockback-115-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/sword-knockback-115-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/sword-knockback-115-artifacts --logger trx --results-directory scratch/sword-knockback-115-tests-new
python -m unittest discover -s tools/verification -p "test_*.py"
dotnet scratch/sword-knockback-115-artifacts/bin/Fft.Client/release/Fft.Client.dll --save-check scratch/sword-knockback-115-save-new-a
dotnet scratch/sword-knockback-115-artifacts/bin/Fft.Client/release/Fft.Client.dll --save-check scratch/sword-knockback-115-save-new-b
dotnet scratch/sword-knockback-115-artifacts/bin/Fft.Client/release/Fft.Client.dll --basic-skill-check scratch/sword-knockback-115-basic-new-a
dotnet scratch/sword-knockback-115-artifacts/bin/Fft.Client/release/Fft.Client.dll --basic-skill-check scratch/sword-knockback-115-basic-new-b
python tools/verification/verify_sword_facing.py --save-baseline scratch/sword-facing-113-save-a --save-first scratch/sword-knockback-115-save-new-a --save-repeat scratch/sword-knockback-115-save-new-b --basic-baseline scratch/throw-stone-110-headless-a --basic-first scratch/sword-knockback-115-basic-new-a --basic-repeat scratch/sword-knockback-115-basic-new-b --output scratch/sword-knockback-115-legacy-new.json
python tools/verification/verify_sword_knockback_contract.py --reference C:/repositories/fft_decomp --output scratch/sword-knockback-115-source-new.json
~~~

Choose fresh report/headless paths. Historical scratch baselines are required
only by the explicit local artifact audit, not ordinary builds/tests. Python tests
in this environment use the bundled runtime with TEMP/TMP directed to scratch.
No runtime C, BIN, original assets or desktop is needed by the managed tests.

Next: ZOD-116, a bounded source/interface contract for original AI Move/Attack/Wait
decisions using the now-supported combat domain. Define the replaceable decision
boundary from traced inputs, candidate simulation, ordering, ties, RNG and
resumable state; do not substitute an invented approach-and-attack heuristic.
