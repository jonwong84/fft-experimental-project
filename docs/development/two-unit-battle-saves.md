# ZOD-035 — Versioned saves for the bounded two-unit battle

The supported synthetic scenario now has a versioned save/load proof. Loading a
checkpoint against freshly composed definitions and continuing produces the same
state, RNG and ordered events as uninterrupted play. This is a managed application
format, with explicit persistence DTOs; it is not an original PS1 memory-card format.
There is no save-slot UI or campaign persistence yet.

## Run the proof

After the normal pinned-SDK locked restore and Release build, choose a new ignored
directory from the repository root:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --save-check scratch/save-check-001
```

This headless command needs no GPU, original assets, BIN, BIOS, decomp executable,
importer or network. It writes `checkpoint-00.json` through `checkpoint-12.json`
and `check.json`. Each checkpoint is loaded with a fresh selected content binding;
the remaining authored commands must match the uninterrupted state and ordered
events at every step. Exit 0 means success, 1 means a file/check failure, and invalid
CLI syntax retains exit 2. Existing output files/directories are rejected. A failed
check may leave partial diagnostic output; use a new directory on retry.

The result remains first-unit victory, HP `25/0`, tick `20`, RNG word
`2473755763`, draw count `22`. These are the existing
[synthetic scenario expectations](../behavior/two-unit-battle-scenario.md).
The Windows playable window's controls and session journal remain separate;
`--save-check` proves the persistence API, not an interactive save/load menu.

## Ownership and selected definitions

`Zodiac.Content.Saves.TwoUnitBattleSaveProfile` binds a **caller-selected** profile ID,
profile version, content version and immutable `TwoUnitBattleDefinition`. The
current sample uses `sample:profile/two-unit-battle`, profile version **1**, content
version **1**. The file must exactly match that binding and the scenario ID
`sample:scenario/two-unit-battle`; a file cannot select or load a different profile.

`TwoUnitBattleSave.ToJson`/`ReadJson` map explicit DTOs to/from the simulation.
`Save`/`Load` add bounded file access in Content. `definition.Restore(snapshot)` in
Core validates the resolved snapshot and derives its first-KO outcome without I/O,
events or RNG draws. Client owns command-line routing and receipt output. Zodiac
gains no FFT-specific or persistence responsibilities.

Immutable map geometry, movement profiles/costs, maximum HP, PA/evasion/Brave,
zodiac sign, weapon definitions, Speed and tie keys come from the selected
definition. They are never deserialized from the file. Unit instance IDs resolve
to the definition's two templates; equipped weapon IDs must resolve and match
their selected templates. The scenario ID resolves its map and other setup inputs,
so the format needs no invented standalone map-definition ID. Save-array order
does not alter authored turn/unit ordering. Restored map/profile references belong
to the freshly selected definition, not the source objects used to create the save.

Content/profile versions are explicit owner-maintained compatibility numbers.
Change the content version whenever an immutable setup or referenced definition
changes incompatibly; the codec does not infer a content hash or inspect packages
to detect a version that an author forgot to advance. Broader package dependency
fingerprints and campaign version policy belong to ZOD-049/ZOD-053.

## Current schema and legal checkpoints

The format ID is `fft.two-unit-battle-save`, current schema **2**. The envelope
contains `format`, `schemaVersion`, `sha256` and `snapshot`. Snapshot fields are:

- Profile ID/version, content version, scenario ID and explicit RNG algorithm ID
  `fft.ps1:rng/bios-v3-us`.
- Battle ticks, turn phase (`add-ct`, `pick-unit`, `unit-turn`), nullable active
  unit ID, complete unsigned 32-bit RNG word and unsigned 64-bit draw count.
- Outcome (`in-progress`, `first-unit-won`, `second-unit-won`). It must agree
  with the outcome derived from restored HP.
- Exactly two units with stable unit/weapon IDs, X/Y/layer, current HP/MP, facing,
  healthy/Critical/Dead projection, KO count, CT and move/action consumption flags.
  MP remains zero under the existing bounded sword profile.

One active-unit field derives `HasTurn`; one movement-consumed flag reconstructs
both movement and clock projections. One RNG snapshot reconstructs both battle
and clock RNG copies. This avoids storing redundant authorities that can disagree.

The checkpoint is **between complete `TwoUnitBattleScenario.Apply` calls**. All
commands are atomic in this slice. Saving before readiness, during an active turn,
after movement but before attack, after attack but before movement/Wait, after a
turn ends, and at the first-KO result is supported. Initial `AddCt` is valid only
with the unchanged initial state. An unfinished active turn at a terminal KO is
retained: loading never forces Wait, CT refund or extra death processing.

No partial attack, animation timeline, asynchronous action, charged action,
pending reaction, campaign state, UI selection, preview, input, event journal or
wall-clock time is persisted. Save/load does not expand the underlying movement,
knockback or post-KO scheduling domain.

## Corruption, versions and migration

Readers reject unsupported format/schema/profile/content versions; unknown,
duplicate, missing or null required fields; invalid stable IDs; extra/missing or
duplicate units; mismatched equipment; invalid ranges/geometry/turn ownership;
HP/status/outcome disagreement; and checksum mismatch. JSON is case-sensitive,
strict UTF-8, limited to **64 KiB** and depth **16**. Counters retain their full
unsigned widths, including exhaustion states; load consumes no draws or ticks.

The SHA-256 covers the snapshot serialized in the explicit DTO field order with
the codec's compact `System.Text.Json` settings. Whitespace changes do not alter
the checksum. It detects accidental edits/corruption; it is not authentication or
proof that a snapshot is reachable through legal play. Structural/domain validation
still runs after a matching checksum. No repair or guessing is performed.

Schema **1** is a synthetic predecessor used to exercise migration. It has the
same battle/profile fields but an implicit FFT BIOS RNG algorithm. Load first
validates its own schema/checksum, then supplies that known algorithm ID and
reconstructs the current state. The result reports `SourceSchemaVersion = 1` and
`WasMigrated = true`; a subsequent explicit save emits schema 2. Loading never
rewrites the input file. Unknown old or future versions fail explicitly. This is
not a claim that a historical released save format exists.

Committed [version-1](../fixtures/saves/two-unit-active-v1.json) and
[version-2](../fixtures/saves/two-unit-active-v2.json) fixtures independently specify
the active second-unit checkpoint after authored command five: both HP 25, tick 10,
RNG `2937909499/14`, action consumed and movement available. They contain invented
state only. Their [provenance](../fixtures/saves/README.md) distinguishes this from
original-game observations.

## Preserving the last valid file

Saving validates and serializes before touching the destination. It writes a unique
sibling `.tmp` with exclusive creation, flushes it to disk, then renames it over the
destination on the same filesystem. It never opens/truncates the previous save.
Failed validation, temp writing or promotion leaves the previous destination intact;
best-effort cleanup removes the sibling without masking the original error. Ordinary
file/access exceptions remain ordinary exceptions; malformed save data uses
`BattleSaveValidationException` with a clear diagnostic.

The caller supplies an existing writable parent directory. This proof does not
provide save-slot discovery, concurrent-writer arbitration, backup rotation, cloud
sync or a guarantee against power loss, faulty media or every filesystem's durability
behavior. A crash can leave a recoverable temporary sibling; automatic recovery
policy is deferred to ZOD-053. No existing save is deleted as a fallback.

## Validation recorded on 2026-10-23

Pinned-SDK locked restore, Release build with zero warnings/errors, and all
**954 tests** passed: Core 293, Content 297, AssetTool 151, CLI 91, Engine 24, Client 98.
The 86 added cases cover all thirteen checkpoints and every remaining event/state,
schema migration, both winners, terminal active turns, Critical state, ID/version/
corruption rejection, full unsigned counters, safe writes and the headless command.
Windows deletion-sharing lock and directory-promotion failures preserved old data
and cleaned temporary files. The deletion-sharing case is Windows-specific; Unix
does not enforce that Windows rename lock. Other codec/file checks are headless.

`scratch/save-035-check-001` records the successful finite command run. No new C
behavior was translated; reference checkout HEAD was rechecked read-only as
`11464943bbdbd2a0af022d90cb7f10455cf911df`. Existing compatibility limits remain
those of the M1 scenario. This completes ZOD-035's bounded proof, not full original
save/load or the campaign persistence epic.
