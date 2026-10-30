# Settled sword facing correction (ZOD-113)

Implemented 2026-10-26 against fresh clean read-only decomp
11464943bbdbd2a0af022d90cb7f10455cf911df. All twelve source fingerprints from
[ZOD-112](../architecture/zod-112-sword-settled-facing.md) were rechecked before
translation. This corrects the managed settled-state fidelity gap; it does not
establish independent original-game execution or animation equivalence.

## Result

AttackBattleSimulator.Attack now builds the actor result with actor.Facing.
The temporary attack-facing calculation is removed; target-front classification
remains unchanged. The closed profile has a living actor and no actor-death effect,
so source reward_grant restoration is represented directly at the atomic command
boundary. No transient saved-facing field, new event, extra draw, content/profile
version, serializer format or presentation change is added.

Hits, misses, criticals, zero damage and target KO retain the incoming actor facing.
Target facing remains unchanged. Current-action damage, HP/status/KO arithmetic,
Act/Move/CT ordering, event order and RNG retain their previous behavior. The
source dead-unit restore branch is not generalized into the managed profile.
Reactions, mounts, continued strikes and explicit Wait-direction selection remain
outside scope. The sword's adjacency and off-map-beyond-target guard remain.

Later behavior can change: the contract's two-tile active-turn journal now ends
with a reverse hit25 at RNG595337866/count13, rather than the old frontal evade
at RNG180171308/count11. The reachable AddCt scenario ends HP25/25 at
RNG2937909499/count14. Six existing-format save/replay boundaries with fresh
definitions reproduce that corrected continuation. A separately stored East
facing remains authoritative and still produces the frontal miss at
RNG836760821/count12; the save loader does not infer or rewrite attack history.

## Explicit fixture migration

Nine new schema-3 files named sword-*.v3.settled-facing.json use policy
fft:combat/bounded-sword-settled-facing-v1. They retain the earlier fixtures'
inputs, damage, events, turn state and RNG and explicitly correct eight stored
actor facings. The diagonal rejection already preserved facing. The
[supersession manifest](../fixtures/behavior/sword-facing-supersession-v1.json)
maps all nine old IDs/hashes to distinct replacement IDs and paths.

Manifest identity hashes use canonical UTF-8/LF repository text. The managed hash
test accepts Git's LF/CRLF checkout conversion; a dedicated test covers this.
Only the file-identity hash normalizes line endings. Executable fixture assertions
are compared as stored, and the local artifact audit checks historical raw bytes.

The test-only AttackFixtureRunner validates all old-policy v3 data before
returning not-executed with a superseded/settled-facing diagnostic. New-policy
v3 executes against the corrected default simulator. Old schema-2 descriptions
remain unexecuted. Both policies retain strict malformed-data checks; new-policy
mismatch tests verify facing, damage, RNG, outcomes and rejection fields. There
is no runtime legacy-bug switch and no in-memory rewriting of fixture assertions.

All nine historical fixture files, both historical sword saves and all eight
historical combat evidence files (including the ZOD-112 contract) remain unchanged.
Earlier execution reports describe their original implementation revision. They
are not upgraded to new execution evidence.

## Validation

Locked isolated restore and Release build pass with zero warnings. All
**2,804 .NET / 317 Python tests** pass (81 new managed cases, six Python).
Focused suites also pass. New tests cover 32 source-derived cardinal combinations,
critical/KO/zero/cap results, every four/six/seven-draw budget boundary, both actor
IDs and combatant orders, probe purity, action orders, exact later evade/RNG,
accepted/rejected prefix replay, fixture supersession and six save continuations.
Existing invalid-command, HP/KO, terminal and unsupported ability-save guards pass.

The 13 built-in sword-save boundaries and their check report repeat exactly
against each other and a retained pre-correction ZOD-111 build: all 14 files,
same profile/content versions, final RNG2473755763/count22. The built-in scenario
uses aligned facings and needs no format or profile-version change. The 46-command
Basic Skill route and 47 fresh-definition continuations repeat all four files
against each other and the historical ZOD-110 output, final RNG4200531631/count58.

Two independent Python audit reports match. They verify the migration's allowed
static delta, historical identities, repeated headless inventories, independent
Throw Stone damage/push/RNG, four unchanged prepared files and 29 unchanged native
files. This static migration audit does not normalize values in the executable
runner. Managed suites separately execute the corrected vectors and continuations.

No native window, original executable or emulator was launched. SceneUnit has no
facing input and presentation code is unchanged; earlier GPU reports remain
historical evidence. Editor work and ZOD-106 battle checkpoints remain deferred.
The new save checks exercise existing sword persistence; ability saves remain
unsupported. Generated outputs/TRX stay ignored under scratch/sword-facing-113-*.
See [implementation evidence](../evidence/combat/sword-facing-implementation-v1.json).

## Reproduce

From the repository in PowerShell:

~~~powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/sword-facing-113-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/sword-facing-113-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/sword-facing-113-artifacts
dotnet scratch/sword-facing-113-artifacts/bin/Fft.Client/release/Fft.Client.dll --save-check scratch/sword-facing-113-save-new
dotnet scratch/sword-facing-113-artifacts/bin/Fft.Client/release/Fft.Client.dll --basic-skill-check scratch/sword-facing-113-basic-new
python tools/verification/verify_sword_facing.py --save-baseline scratch/sword-facing-113-save-baseline --save-first scratch/sword-facing-113-save-a --save-repeat scratch/sword-facing-113-save-b --basic-baseline scratch/throw-stone-110-headless-a --basic-first scratch/sword-facing-113-basic-a --basic-repeat scratch/sword-facing-113-basic-b --output scratch/sword-facing-113-audit-new.json
~~~

Choose fresh output names. The audit's baseline directories are local historical
artifacts; it fails if they are absent, rather than inventing historical results.
Ordinary test/build operation has no dependency on those scratch artifacts, C
source or original data. Python verification tests use the documented environment's
Pillow and repository scratch TEMP/TMP when the default temp directory is restricted.
