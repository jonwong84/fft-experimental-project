# ZOD-110 — Throw Stone content and combined Basic Skill scenario

Date: 2026-10-26. Managed base efdbfa9, clean on entry. Read-only reference
C:\repositories\fft_decomp was freshly rechecked clean at
11464943bbdbd2a0af022d90cb7f10455cf911df before conversion. The
[source contract](../architecture/zod-108-throw-stone-action.md) and
[pure implementation](../behavior/throw-stone-implementation.md) retain their
historical evidence. This [new evidence](../evidence/combat/throw-stone-content-v1.json)
covers TST-021..028; Windows cases TST-029..034 remain future ZOD-111 work.

## Separate executable companion

TargetedAbilityContentPackage uses format fft.targeted-ability-content, schema 1.
It selects package/profile/metadata-dependency IDs and immutable ability records:
id, effect throw-stone-flat, target occupied-unit, range 4,
maximumDamageMultiplier 2, chargeTicks 0 and mpCost 0.
This is the closed immediate occupied-panel subset, not arbitrary formulas,
full projectile targeting or a replacement learning-metadata format.

Unknown/missing/duplicate fields, unsupported versions/types/values, duplicate
IDs, wrong namespaces and definition/JSON size limits reject. Serialization
sorts by stable ID with UTF-8/LF and round-trips deterministically. The
TargetedAbilityDefinition typed constructor also rejects unimplemented variants.
ValidateAgainst requires the exact selected metadata package/profile and matching
ordinary-action learning records. Metadata alone enables no executable action.

TargetedAbilityBattleSetup requires job setup to resolve the selected metadata
instances. Every learned grant resolves a unit, explicit action slot and supported
effect; duplicates and nonzero equipment PA bonus reject. Empty grants remain
empty even when the job lists Throw Stone. Authored slot 2048 is tested; original
table capacities are not a runtime limit. The ground-surface argument is an
explicit authored certification, not a decoder for original terrain semantics.

## Verified original import

OriginalThrowStoneRecords checks every field of the selected 14-byte secondary
record. OriginalThrowStoneImporter verifies the exact SCUS executable size/hash/
header, generic Squire job 74, Basic Skill row 5 slot 2, ability 148 primary and
secondary records, default menu and empty status row. It also compares the entire
canonical selected Squire metadata dependency to a fresh decode without rewriting
that dependency. Raw six-record provenance stays under scratch.

The existing metadata ID is **fft.ps1:ability/source-0148**. Preserve it in the
executable companion; do not rename historical metadata to a display label.
The imported companion's package is fft.ps1:package/throw-stone-effect.
It grants no learned ability and supplies no original unit stats, encounter,
terrain, rewards, AI or presentation. The first combined scenario remains authored.

The synthetic DecodeSelection entry point tests semantic byte mutations after
the edition gate. Its provenance explicitly has no verified edition/hash.
All 14 secondary fields and job/slot/menu/status/learning/header/dependency
negatives are exercised; these tests are not merely hash-mismatch checks.
Companion JSON has no checksum: semantic negatives mutate parsed fields directly.
Verified import always retains the original strict edition/hash gate.

From the repository with a fresh output destination:

~~~powershell
dotnet scratch/throw-stone-110-artifacts/bin/Zodiac.AssetTool.Cli/release/Zodiac.AssetTool.Cli.dll import-throw-stone --exe scratch/all-data-extraction/SCUS_942.21 --metadata scratch/imported-squire-check-a/content.package.json --output scratch/throw-stone-review-import-new
~~~

The existing scratch/path/reparse guards and preflight validation apply.
targeted.package.json and targeted.import.json publish together through a fresh
staging directory. Existing output and invalid inputs are preserved.
Original metadata, sword and Accumulate formats/converters are unchanged.

## Authored lifecycle composition

TwoUnitBattleDefinition now accepts an optional targeted profile alongside the
existing immediate profile. Initial setup validates both and rejects an ID
selecting conflicting self/targeted meanings. Existing overloads retain behavior.
TwoUnitBattleCommand.UseTargetedAbility carries actor/ability/target and routes to
the pure ZOD-109 implementation. The self command remains unchanged. Unselected
profiles reject the new command, with no implicit action fallback.

SyntheticBasicSkillBattle authors a 7-by-5 flat ordinary-ground scene with two
units, PA 5, HP 50, one sword and explicit A-only grants for Accumulate/Throw Stone.
The learning metadata and separate companions use sample IDs, not imported
original encounter data. A raises PA to 6, moves after Act, then uses Throw Stone
through legal turns. Its damage is 6 or 12. Knockback updates target positions;
first KO completes the existing authored lifecycle. All six command kinds reject
after completion. Restart derives PA/HP/positions/RNG/grants from immutable setup.

Existing sword restrictions, including its conservative destination guard, remain.
The combined profile checks the reachable PA-99 sword-power envelope at setup.
The known old sword facing discrepancy is documented and unchanged.

Battle restore and save read/write/file entry points reject **either** ability
profile before touching files, including initial, boosted, terminal and
targeted-only states. No save schema or supported checkpoint is added. ZOD-106
remains deferred; the headless continuation below replays commands into fresh
definitions without saving/loading a battle.

## Headless validation and reproducibility

BasicSkillBattleCheck runs a finite 46-command journal: one PA increase, six
Throw Stone hits, three pushes, and eight deliberate duplicate/ungranted/terminal
rejections. All 47 prefix checkpoints recompose metadata/effect definitions,
replay the prefix, then compare every remaining state/event/rejection/RNG result.
Final RNG is word 4200531631, draw count 58. Restart returns the starting state.
Mutable-state diagnostics record unit IDs/positions/HasMoved explicitly rather
than serializing immutable movement-profile dictionaries.

~~~powershell
dotnet scratch/throw-stone-110-artifacts/bin/Fft.Client/release/Fft.Client.dll --basic-skill-check scratch/basic-skill-review-new
~~~

Output is three authored content JSON files and check.json. These diagnostic
files are not a save format, public persistent replay or prepared manifest bundle.
ZOD-111 owns manifest-based Windows preparation and input/visual validation.

Both actual imports match their two files byte-for-byte. Both headless runs
match all four files. The independent Python audit verifies six selected retail
records, 22 historical metadata records, the learning/effect/dependency mappings
and six preserved metadata/sword/Accumulate files. It independently calculates
every Throw Stone delta and all readiness/nonrandom draws, including final RNG,
and verifies atomic rejection. Two final audit reports repeat exactly.

~~~powershell
python tools/verification/verify_basic_skill_content.py --exe scratch/all-data-extraction/SCUS_942.21 --import-first scratch/throw-stone-110-import-a --import-repeat scratch/throw-stone-110-import-b --check-first scratch/throw-stone-110-headless-a --check-repeat scratch/throw-stone-110-headless-b --output scratch/basic-skill-audit-new.json
~~~

Locked isolated restore and Release build pass with zero warnings. Full suites:
**2,700 .NET / 305 Python tests** (92 new managed, six new Python).
The initial focused run exposed a diagnostic serializer traversing a terrain-key
dictionary; explicit mutable-state projection fixed it. Retained discovery TRX:
scratch/throw-stone-110-focused; passing rerun: scratch/throw-stone-110-focused-b.
Full TRX: scratch/throw-stone-110-tests-full.

~~~powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/throw-stone-110-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/throw-stone-110-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/throw-stone-110-artifacts
~~~

No graphics window, emulator or original executable was launched. Source-derived
checks do not establish original-game equivalence. **Next: ZOD-111**, prepared
Windows target/confirm/cancel controls with a finite native plan and separate owner
approval. Editor polish and battle checkpoints remain deferred.
