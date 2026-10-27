# Tessera Engine · Zodiac Framework · FFT Content

This repository develops **Tessera Engine**, reusable C#/.NET grid and simulation machinery; **Zodiac Framework**, the FFT-specific rules and runtime systems built on it; and **FFT Content**, the game definitions and prepared assets they use. The application target is a complete, playable Windows reimplementation of the North American PS1 release, `SCUS-94221`.

The project is an early prototype. A bounded two-unit battle is playable, and one prepared original map and sprite pose can be inspected together. The original game is not yet playable as a complete campaign.

## Engine, framework and content

The design has three parts:

- **Tessera Engine (`Tessera.Engine`):** reusable grid positions/bounds, movement and deterministic simulation contracts, independent of FFT rules, formats and assets.
- **Zodiac Framework (`Zodiac.Framework`, `Zodiac.Content`):** FFT simulation rules, compatibility policies, definition schemas, prepared-file loading and runtime systems. `Zodiac.AssetTool` and `Zodiac.AssetTool.Cli` provide offline import/preparation tools.
- **FFT Content:** particular definitions, scenarios and assets, including invented examples and locally prepared original data. Content packages are data; their schemas and loaders belong to Zodiac.

Zodiac depends on Tessera; Tessera has no dependency on Zodiac or FFT. `Fft.Client` remains the FFT application host and composition root for rendering, input, UI and selected content. Simulation stays independent of graphics, input, wall-clock time and file I/O. Existing `fft.*` data formats, logical asset IDs, compatibility profiles and save schemas retain their identities.

**Tessera's implemented scope is currently small:** grid positions/bounds, an unobstructed flat-grid movement calculation, and deterministic RNG contracts. Zodiac owns the PS1 RNG implementation and gameplay draw ordering, terrain-aware movement, turn scheduling, combat and scenario outcomes. The [Relay sample](samples/Relay/README.md) demonstrates Tessera's movement calculation with independent, invented rules. Broader engine capabilities will be extracted incrementally as their reusable contracts become clear.

The naming migration replaces `Zodiac.Engine` with `Tessera.Engine`, `Fft.Core` with `Zodiac.Framework`, `Fft.Content` with `Zodiac.Content`, and `Fft.AssetTool`/`.Cli` with `Zodiac.AssetTool`/`.Cli`, including their test projects and namespaces. The solution remains `Fft.sln`. Historical evidence retains its recorded names, paths and hashes; current commands and project links use the new names.

See the [architecture overview](docs/architecture/overview.md) and [ADR 0004](docs/adr/0004-zodiac-engine-and-fft-game-boundary.md) for the ownership boundary and intended direction.

Replacing either engine or framework through independent contracts and compatible
adapters is an architectural goal. [ADR 0006](docs/adr/0006-replaceable-engine-and-framework-contracts.md)
records that goal; public contract design is deferred until concrete usage
justifies it. Current assemblies are not yet interchangeable.

## What works today

- **First job/ability editor:** `Fft.Editor` opens prepared baseline metadata,
  creates a separate authored job/skillset/ability copy, edits Move/Jump/JP,
  validates/exports/reopens through shared APIs and compares movement through the
  existing simulator. Separate Jobs/Abilities pages now provide independent
  search, related-record navigation and shared package actions after owner
  feedback on the first UI. Owner review remains pending.
  [Current launch and review](docs/development/editor-workspaces.md).

- **Authored world menu:** `--world-menu` opens Home/New/live Resume, read-only
  Party, travel and safe New/Exit decisions. Keyboard/mouse/controller routes
  have headless and finite Windows checks. The owner approved the original
  navigation gallery and manual controls/resize/close/focus checks.
  [Launch and validation](docs/development/windows-world-navigation.md).

- **Native synthetic Save/Load UI:** the world menu now offers paged slots,
  validated primary/backup details, overwrite/discard confirmations, explicit
  recovery and refreshable errors. Repeated native UI/storage checks pass;
  the owner approved the gallery and reported the bounded interactive slot check passed.
  [Launch and review](docs/development/world-save-load-ui.md).

- **Synthetic world Save/Load:** a small authored idle-world checkpoint supports
  versioned saves, fresh-definition restore, validated slot summaries, stale-writer
  protection and explicit backup recovery. Two headless checks repeat all 14
  files. The native UI above builds on this proof; original campaign persistence
  remains future work. [Guide](docs/development/synthetic-world-checkpoint.md).

- **Playable synthetic battle:** control both units with mouse or keyboard; select, inspect legal moves/targets, move, attack, wait, advance turns and restart. HP, choices, rejections and the first-KO result are visible. [Controls and validation](docs/development/playable-battle.md).
- **Prepared introduction to battle:** an optional general-banner/Ready sequence hands off to that battle in one Windows host. Battle state stays frozen until the authored event exits; held inputs are baselined before commands become available. [Launch, review and validation](docs/development/introduction-battle-handoff.md).
- **Deterministic headless battle:** the same supported scenario runs through typed commands, with reproducible state/RNG results and replay checks. [Scenario and rules](docs/behavior/two-unit-battle-scenario.md).
- **First bounded job ability:** a separate authored profile executes Accumulate through validated content/grants, raises current PA to a cap of 99 and feeds later sword attacks. Prepared Windows controls have owner approval. Battle checkpoint support is deferred. [Behavior/import](docs/behavior/accumulate-implementation.md) · [Launch and interactive steps](docs/development/accumulate-battle-controls.md).
- **Targeted Basic Skill battle:** validated Throw Stone content/import now composes ranged damage and same-height knockback with Accumulate in an authored headless battle. Exact RNG, first KO, replay and restart pass; prepared Windows target controls are next. [Pure rules](docs/behavior/throw-stone-implementation.md) · [Content/import and headless check](docs/development/basic-skill-content.md).
- **Bounded save/load proof:** versioned snapshots restore all thirteen authored checkpoints against selected definitions and continue with identical state/events. Strict validation, synthetic migration and safe file replacement are tested. [Save contract and headless check](docs/development/two-unit-battle-saves.md).
- **Bounded synthetic events:** prepare/load authored PS1-format conditional events and execute them through pure FFT transitions. Separate profiles add frame waits and scene task barriers with explicit shared ticks, immutable suspension/resumption, replay and atomic interpreter/task rollback. [Decoder](docs/development/prepared-scenario-event.md), [conditional execution](docs/behavior/conditional-event-execution.md), [frame waits](docs/behavior/timed-event-waits.md) and [event barriers](docs/behavior/scene-event-barriers.md).
- **Scene task/barrier proof:** immutable FFT task instances, dark-screen phases and scene-wide barriers advance through explicit logical facts and ticks. Scene tasks survive interpreter exit/restart; actual presentation workers remain future work. [Contract and checks](docs/behavior/scene-task-barriers.md).
- **General banner:** a pure condition-8 driver handles reveal/hold/hide, Circle skips and termination. A separate authored prepared profile admits one preloaded banner through the ordered executor and Windows host, preserving event-before-worker timing with atomic rollback. Full original events and loading/display timing remain open. [Driver](docs/behavior/general-banner-effect.md), [ordered execution](docs/development/ordered-banner-transactions.md), [authored integration](docs/development/prepared-ordered-banner-event.md) and [artwork preview](docs/development/prepared-general-banner.md).
- **Dark-screen transition:** verified prepared delay/rotation data drives the pure worker and a standalone Windows flat-quad preview. Thirteen selected original entry/hold/release scanouts match actual C# exactly; full original animation and duration remain open. [Worker](docs/behavior/dark-screen-worker.md), [preparation/preview](docs/development/prepared-dark-screen.md), and [selected original comparison](docs/development/original-dark-screen-comparison.md). The [combined introduction](docs/behavior/dark-screen-introduction.md) shares one clock/ledger and gates battle on complete event exit. Its [prepared Windows integration](docs/development/prepared-dark-screen-introduction.md) now composes dark/general/Ready and hands off to the existing battle; 2,054 CPU/GPU/model frames match exactly. The owner approved the authored gallery on 2026-10-26.
- **Prepared original scene:** MAP003 and a held Chocobo pose render from prepared files with a fixed authored camera. Per-polygon palettes, native terrain placement and sprite piece order are preserved; lighting and depth behavior use documented approximations. [Scene guide and comparison limits](docs/development/prepared-scene.md).
- **Offline asset preparation:** verified ordinary-file extraction, bounded sprite/map conversion, hash-verified prepared loading and narrow typed content-package validation. [Map preparation](docs/development/prepared-map-slice.md), [sprite-pose preparation](docs/development/prepared-sprite-pose.md), [content packages](docs/architecture/zod-016-content-packages.md).

The playable battle uses invented units, art and a small height-10 map. MAP003 remains a separate static inspection scene with its native terrain intact. Rendering that map does not expand the supported combat domain.

Full original combat, campaign, AI, campaign save/load, audio, general animation playback and broader authoring UI remain future work. Selected banner/Ready/DarkScreen frames have exact original/emulator comparisons; the bounded source-derived and synthetic checks do not establish complete original-game equivalence. Original behavior is the default compatibility target, with intentional new mechanics and custom content kept separate.

## Build, test and play

Requires the exact .NET SDK selected by `global.json`, currently **10.0.402**. The graphical commands currently support Windows. Build/test and the headless scenario require no original game files or GPU. See the [SDK and dependency policy](docs/development/build-reproducibility.md) for reproducible setup and deliberate upgrades.

From the repository root:

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --battle
```

See [test maintenance and historical audits](docs/development/test-maintenance.md)
for focused AI groups, extension coverage and reproduction of recorded evidence
without freezing current code to historical hashes.

In the playable window, `N` advances to the next turn, `M` selects Move, `A` selects Attack, `W` waits, and `R` restarts. Click units/cards and destinations/targets, or use the keyboard cursor. The player controls both units. See the [complete controls and example victory sequence](docs/development/playable-battle.md#controls).

To run the headless script or the separate static synthetic window:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --scenario two-unit-battle
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --window
```

The latest recorded implementation milestone, ZOD-076, passed locked restore, a Release build with zero warnings and **1,302 tests** across Engine, Core, Content, AssetTool, AssetTool CLI and Client. Windows GPU/UI checks are separate opt-in checks; physical input timing, mixed-DPI behavior and clean-machine validation have remaining manual follow-ups. See the [playable check](docs/development/playable-battle.md#automated-windows-check) and [synthetic host checks](docs/development/windows-synthetic-host.md).

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
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build --no-restore -- inventory --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build --no-restore -- extract --bin scratch/fft-clean-base-game.bin --cue scratch/fft-clean-base-game.cue --all-data --output scratch/all-data-extraction
```

See the [asset inventory proof](docs/asset-inventory-poc.md) and [sprite decoder proof](docs/sprite-attack-image-decoder-poc.md) for earlier extraction/diagnostic workflows. Prepared map and pose conversion commands are in their linked guides.

Structural content metadata can be validated without original files:

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release --no-build --no-restore -- validate-content --package docs/fixtures/content/metadata-synthetic.json
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
- [First scene effect selection](docs/architecture/zod-075-general-banner-effect.md): source-traced logical general-banner phases and Circle skip; [ZOD-076 implementation](docs/behavior/general-banner-effect.md).
- [General-banner asset/readiness trace](docs/architecture/zod-077-general-banner-assets.md): verified image/palette/piece selection; [prepared preview](docs/development/prepared-general-banner.md), [ordered transaction proof](docs/development/ordered-banner-transactions.md) and [authored profile integration](docs/development/prepared-ordered-banner-event.md).
- [Ready display trace](docs/architecture/zod-083-ready-display.md) and [ordered Ready aggregate](docs/development/ready-display-transactions.md): condition-0 dispatcher/child lifetime and 28 executed Core expectations.
- [Ready presentation policy](docs/architecture/zod-085-ready-rendering.md) and [prepared Ready preview](docs/development/prepared-ready-event.md): separate four-corner RGB5 glyph rendering, strict readiness, 32 presentation expectations and finite prepared-only Windows checks; original displayed comparison remains open.
- [Campaign save investigation](docs/architecture/zod-067-campaign-save-state.md): persistent fields, original save entry paths, RNG lifecycles and proposed native workflow.
- [Project handoff](docs/PROJECT_HANDOFF.md): latest checkpoint followed by historical development context.
