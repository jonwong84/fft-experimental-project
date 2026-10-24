# Zodiac / FFT

This repository develops **Zodiac**, a reusable C#/.NET engine, alongside a separate **Final Fantasy Tactics implementation** that uses it. The application target is a complete, playable Windows reimplementation of the North American PS1 release, `SCUS-94221`.

The project is an early prototype. A bounded two-unit battle is playable, and one prepared original map and sprite pose can be inspected together. The original game is not yet playable as a complete campaign.

## Engine, framework and content

The design has three parts:

- **Zodiac.Engine:** reusable facilities independent of FFT rules, formats and assets.
- **FFT framework:** FFT simulation rules, compatibility policies, content schemas and runtime systems. These currently live in `Fft.Core` and parts of `Fft.Content`.
- **FFT content:** particular definitions, scenarios and assets used by that framework, including invented examples and locally prepared original data.

FFT keeps its own `Fft.*` namespace and project space. FFT depends on Zodiac; Zodiac does not depend on FFT. Rendering, input and UI belong to `Fft.Client`, while original-format preparation belongs to `Fft.AssetTool` and `Fft.AssetTool.Cli`. Simulation stays independent of graphics, input, wall-clock time and file I/O.

**Zodiac's implemented scope is currently small:** grid positions/bounds, an unobstructed flat-grid movement calculation, and deterministic RNG contracts. FFT owns the PS1 RNG implementation and gameplay draw ordering, terrain-aware movement, turn scheduling, combat and scenario outcomes. The [Relay sample](samples/Relay/README.md) demonstrates the engine's movement calculation with independent, invented rules. Broader engine capabilities will be extracted incrementally as their reusable contracts become clear.

See the [architecture overview](docs/architecture/overview.md) and [ADR 0004](docs/adr/0004-zodiac-engine-and-fft-game-boundary.md) for the ownership boundary and intended direction.

## What works today

- **Playable synthetic battle:** control both units with mouse or keyboard; select, inspect legal moves/targets, move, attack, wait, advance turns and restart. HP, choices, rejections and the first-KO result are visible. [Controls and validation](docs/development/playable-battle.md).
- **Deterministic headless battle:** the same supported scenario runs through typed commands, with reproducible state/RNG results and replay checks. [Scenario and rules](docs/behavior/two-unit-battle-scenario.md).
- **Bounded save/load proof:** versioned snapshots restore all thirteen authored checkpoints against selected definitions and continue with identical state/events. Strict validation, synthetic migration and safe file replacement are tested. [Save contract and headless check](docs/development/two-unit-battle-saves.md).
- **Bounded synthetic events:** prepare/load authored PS1-format conditional events and execute them through pure FFT transitions. Separate profiles add frame waits and scene task barriers with explicit shared ticks, immutable suspension/resumption, replay and atomic interpreter/task rollback. [Decoder](docs/development/prepared-scenario-event.md), [conditional execution](docs/behavior/conditional-event-execution.md), [frame waits](docs/behavior/timed-event-waits.md) and [event barriers](docs/behavior/scene-event-barriers.md).
- **Scene task/barrier proof:** immutable FFT task instances, dark-screen phases and scene-wide barriers advance through explicit logical facts and ticks. Scene tasks survive interpreter exit/restart; actual presentation workers remain future work. [Contract and checks](docs/behavior/scene-task-barriers.md).
- **Prepared original scene:** MAP003 and a held Chocobo pose render from prepared files with a fixed authored camera. Per-polygon palettes, native terrain placement and sprite piece order are preserved; lighting and depth behavior use documented approximations. [Scene guide and comparison limits](docs/development/prepared-scene.md).
- **Offline asset preparation:** verified ordinary-file extraction, bounded sprite/map conversion, hash-verified prepared loading and narrow typed content-package validation. [Map preparation](docs/development/prepared-map-slice.md), [sprite-pose preparation](docs/development/prepared-sprite-pose.md), [content packages](docs/architecture/zod-016-content-packages.md).

The playable battle uses invented units, art and a small height-10 map. MAP003 remains a separate static inspection scene with its native terrain intact. Rendering that map does not expand the supported combat domain.

Full original combat, campaign, AI, campaign save/load, audio, general animation playback and authoring UI remain future work. Independent original/emulator visual comparison is also pending; current source-derived and synthetic checks do not establish complete original-game equivalence. Original behavior is the default compatibility target, with intentional new mechanics and custom content kept separate.

## Build, test and play

Requires the exact .NET SDK selected by `global.json`, currently **10.0.402**. The graphical commands currently support Windows. Build/test and the headless scenario require no original game files or GPU. See the [SDK and dependency policy](docs/development/build-reproducibility.md) for reproducible setup and deliberate upgrades.

From the repository root:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --battle
```

In the playable window, `N` advances to the next turn, `M` selects Move, `A` selects Attack, `W` waits, and `R` restarts. Click units/cards and destinations/targets, or use the keyboard cursor. The player controls both units. See the [complete controls and example victory sequence](docs/development/playable-battle.md#controls).

To run the headless script or the separate static synthetic window:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --scenario two-unit-battle
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --window
```

The latest recorded implementation milestone, ZOD-074, passed locked restore, a Release build with zero warnings and **1,230 tests** across Engine, Core, Content, AssetTool, AssetTool CLI and Client. Windows GPU/UI checks are separate opt-in checks; physical input timing, mixed-DPI behavior and clean-machine validation have remaining manual follow-ups. See the [playable check](docs/development/playable-battle.md#automated-windows-check) and [synthetic host checks](docs/development/windows-synthetic-host.md).

ZOD-034 adds an independent Windows CI job alongside Linux/Sonar, downloadable test
reports and a private self-contained client folder. Local bundle/headless and
published GPU checks passed; remote CI and clean-machine graphical launch remain
pending. See [Windows launch validation](docs/development/windows-launch-validation.md)
for producing the folder, prerequisites and the manual check.

## Inspect prepared original assets

After following the [map](docs/development/prepared-map-slice.md) and [pose](docs/development/prepared-sprite-pose.md) preparation guides, launch their default output roots together:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --prepared-scene --map-root scratch/prepared-map003 --pose-root scratch/prepared-chocobo-idle
```

Use the directories you actually prepared if their names differ. The [scene guide](docs/development/prepared-scene.md) describes the fixed camera, finite captures, rendering policy and unsupported PS1 effects. The client also supports `--content-root <directory>` with `--asset-id`, `--sprite-pose` or `--map` for headless prepared-file load checks. With no arguments it prints an identifying line and exits.

The running application loads prepared files. The original BIN is an offline import input; runtime does not read it or invoke the reference C code. No original media is included in this repository. Keep the BIN, extracted files, decoded/prepared assets, generated inventories and captures under ignored `scratch/`; never commit them. The local reference checkout at `C:\repositories\fft_decomp` is for read-only source study and validation.

## Offline tooling

The import tools verify the supported local image profile and compare paths, LBAs and file hashes against the reference inventory. Ordinary-file extraction supports selected paths or all `data` entries; mixed XA/STR entries remain unsupported. Choose a fresh output directory under `scratch/`.

```powershell
dotnet run --project src/Fft.AssetTool.Cli -c Release --no-build --no-restore -- inventory --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue
dotnet run --project src/Fft.AssetTool.Cli -c Release --no-build --no-restore -- extract --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue --all-data --output scratch/all-data-extraction
```

See the [asset inventory proof](docs/asset-inventory-poc.md) and [sprite decoder proof](docs/sprite-attack-image-decoder-poc.md) for earlier extraction/diagnostic workflows. Prepared map and pose conversion commands are in their linked guides.

Structural content metadata can be validated without original files:

```powershell
dotnet run --project src/Fft.AssetTool.Cli -c Release --no-build --no-restore -- validate-content --package docs/fixtures/content/metadata-synthetic.json
```

Repeated `--addon <path>` options validate explicitly selected additions against the baseline. This checks metadata structure and references; it does not establish battle readiness or original-game equivalence. See [content packages](docs/architecture/zod-016-content-packages.md) for authoring, export and diagnostics.

## Project documentation

- [Project plan](PROJECT_PLAN.md): complete intended scope, phases and unresolved decisions.
- [Architecture](docs/architecture/overview.md) and [ADRs](docs/adr/): ownership and significant design choices.
- [Backlog](docs/BACKLOG.md): current work order, dependencies and acceptance criteria.
- [Original bug catalog](docs/KNOWN_BUGS.md): reports and proposed corrections for later ZOD-070 work.
- [Baseline coverage inventory](docs/coverage/README.md): current capability, source/evidence,
  tests, manual checkpoints and unresolved gaps across the original-game target.
- [Event/script investigation](docs/architecture/zod-040-event-script-investigation.md):
  script families, a bounded conditional event, tool assessment and next decoder/executor slices.
- [Original BattleStart trace](docs/architecture/zod-066-battle-start-event.md): verified event identity, activation, opcode dependencies and completion; original playback remains unsupported.
- [Scene task/barrier contract](docs/architecture/zod-072-event-task-contract.md): source trace, identities, phases and polling order; [ZOD-073 execution proof](docs/behavior/scene-task-barriers.md).
- [Synthetic event barriers](docs/behavior/scene-event-barriers.md): separate E5 profile, shared ticks, atomic VM/task transitions and retained scene lifetime.
- [First scene effect selection](docs/architecture/zod-075-general-banner-effect.md): source-traced logical general-banner phases and Circle skip; implementation remains ZOD-076.
- [Campaign save investigation](docs/architecture/zod-067-campaign-save-state.md): persistent fields, original save entry paths, RNG lifecycles and proposed native workflow.
- [Project handoff](docs/PROJECT_HANDOFF.md): latest checkpoint followed by historical development context.
