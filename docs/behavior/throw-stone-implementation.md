# Bounded Throw Stone simulation — ZOD-109

Date: 2026-10-26. Managed base: 8ea184d, clean on entry. Before translation the
read-only reference checkout was rechecked clean at
11464943bbdbd2a0af022d90cb7f10455cf911df. The
[ZOD-108 contract](../architecture/zod-108-throw-stone-action.md) owns the
source/caller/data/width/order trace. Historical investigation evidence remains
unchanged; the [implementation evidence](../evidence/combat/throw-stone-implementation-v1.json)
maps TST-001..020 to executed tests. TST-021..034 remain future work.

## Explicit profile and pure API

ThrowStoneFormula exposes ordinary named C# arithmetic for damage and the strict
two-Brave-roll comparison. BoundedThrowStoneAbility explicitly selects the closed
range-4, Y-2 rule by stable ID. It is not a learning-only ability, arbitrary raw
formula, weapon fallback or general effect interpreter.

TargetedAbilityBattleProfile snapshots selected effects and per-unit
TargetedAbilityUnitGrant records. Grants require distinct resolvable IDs; each
unit explicitly selects zero equipment PA bonus. The caller also explicitly
certifies one stable terrain surface ID as ordinary ground. The pure layer does
not infer original lava/obstacle meaning from a name; ZOD-110 content projection
must validate the selected source terrain semantics.

Call TargetedAbilityBattleSimulator.Apply with an AttackBattleSnapshot,
UseTargetedAbilityCommand(actor, ability, target), and the selected profile.
The old self-action command, immediate profile, sword calculation and all
existing client/content/save paths are unchanged. This API is not yet wired into
TwoUnitBattleScenario, prepared packages or Windows controls. ZOD-110 owns that
composition and must explicitly retain ability-profile save/restore guards.

Supported state is exactly two ordinary body-height-6 units, PA 1..99, existing
Brave 10..100/class-evade 0..100 domains, same-sign neutral zodiac, zero MP,
equipment evade/PA bonus and no modeled support/reaction/status modifiers beyond
HP-derived Critical/Dead. All map cells must be the certified ground surface,
dry flat height 10, one lower layer, zero slope/depth/ceiling and no movement
flags. Distinct targetable/stoppable positions are required. This excludes
obstructed projectile paths, interception, elevation, falls, mounts and extra units.
Range uses current positions and Manhattan distance 1..4, including diagonals.

## Validation, arithmetic and RNG

Before publication validate actor/target, active turn, unused Act, distinct living
target, executable effect, explicit grant and supported state/range. Rejections
return the exact input snapshot, no events, unchanged Act/CT/HP/position/RNG.
Programmer null inputs throw explicitly. Act confirmation is calculated through
the existing pure turn transition, then published only with the complete result.

Four physical-evade rolls remain ordered accessory/right/left/class even when
zero. The first three chances are zero. Class applies from the front; either
facing toward the actor qualifies on a diagonal tie. Side/back clear class evade.
Front class evade 100 still consumes four draws and misses.

A hit draws damage, attacker Brave threshold and target Brave roll, totaling
seven resolution draws. Damage is PA for raw 0..16383 and 2*PA for raw
16384..32767. Current PA is read from the snapshot. The largest admitted damage
is 198, safely within the traced signed-16 storage and below the positive 999 cap.
There is no weapon power, critical or conditional status-proc roll.

The two independent Brave rolls compare
floor(targetRaw * targetBrave / 32768) < floor(attackerRaw * attackerBrave / 32768).
Equality does not attempt knockback. Both draws occur before edge/lethal
suppression; no draw-saving shortcut is taken. Draw-count exhaustion uses local
RNG state and rejects atomically at every branch. Both battle and turn snapshots
receive the same final RNG word/count.

Independent source-derived seed vectors include:

- Seed 1, PA 5, evade 0: damage 10, no push; seven draws, word 3655513600.
- Seed 3: damage 10, push attempted; seven draws, word 273505290.
- Seed 19, front class evade 10: miss; four draws, word 2162199151.
- Seed 19, side class evade 10: damage 5, push attempted; seven draws, word 3282209882.
- Seed 410, front evade 10: fourth raw 3276 scales to 9, so miss; four draws.
- Seed 3878, front evade 10: fourth raw 3277 scales to 10, so hit; seven draws.

These vectors are not an executed original-game comparison. RNG generation has
the project's separate supplied-BIOS verification; whole-game draw ordering and
presentation remain outside this proof. Repeated Apply probes discard their
immutable results and do not alter live state, history or input RNG.

## Settled damage, movement and events

Accepted hits reduce target HP, project Critical/Dead and wrap the byte KO count
as in the existing bounded attack model. Resolved damage may exceed actual HP
loss. A lethal hit cancels displacement after all seven draws.

On a successful nonlethal knockback roll, choose one tile away on the dominant
axis; Y wins ties. An off-map destination suppresses movement while retaining
damage and draws. Within the admitted open two-unit rectangle the destination
is ordinary, clear, same-height ground. Defensive occupancy checking is retained;
this does not enable a third-unit/interception profile.

The target's movement profile, voluntary HasMoved/Move/Act flags, CT, speed and
incoming facing are preserved. The actor also retains its incoming settled facing,
following the original restoration trace. Visual turn-to-target and knockback
animation are not represented. The known old sword facing discrepancy remains
documented under ZOD-038; its code/fixtures are unchanged.

Event order is:

1. Existing AttackTurnBattleEvent choice-consumed and optional turn-ended events.
2. ThrowStoneResolvedEvent with actor/ability/target, hit, formula damage and
   KnockbackAttempted (the Brave comparison passed, even if edge/lethal suppresses).
3. Optional AttackHpChangedEvent, AttackStatusChangedEvent and AttackKnockedOutEvent.
4. ThrowStoneKnockbackEvent only for actual displacement, carrying before/after tiles.

These are managed replay/presentation contracts, not original serialized fields.
Unused Move remains available. Move then Act ends through the existing turn rules;
Act then Wait refunds unused Move's 20 CT. Forced movement never consumes the
target's choices. Rechecking range after a push uses its new position. Existing
post-KO scheduler guards remain; first-KO scenario integration is ZOD-110.

## Validation and next milestone

Locked isolated restore and Release build pass with zero warnings. All
**2,608 .NET / 299 Python tests** pass, including **149 new managed cases**.
They cover source vectors/boundaries, all diagonal facing cases, four-direction/
tie/edge knockback, lethal cancellation, KO wrap, HP thresholds, rejection
identity, unsupported map cells away from the path, draw exhaustion, immutable
grants, both acting units, pure probes, legal continuation and accepted/rejected
journal replay. Existing sword/self-action/scenario/save/client suites pass.

Artifacts and full TRX stay under scratch/throw-stone-109-artifacts and
scratch/throw-stone-109-tests-full. Reproduce from the repository:

~~~powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/throw-stone-109-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/throw-stone-109-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/throw-stone-109-artifacts
~~~

Next is **ZOD-110**, the strict targeted content companion, verified offline import,
and explicitly granted Accumulate/Throw Stone authored scenario. ZOD-111 later adds
prepared Windows target controls with its own finite native plan and owner review.
No native process, original/emulator execution, import or UI review was performed
for ZOD-109. Battle checkpoints and editor work remain deferred.
