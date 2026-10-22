# Architecture overview

**Engine name:** Zodiac. Final Fantasy Tactics is the first planned content set. The agreed target namespaces are `Zodiac.Engine` for reusable engine logic and `Zodiac.Games.Fft` for FFT-specific logic and tooling. Existing projects and namespaces remain `Fft.*` during the prototype; migration is gradual and separate from behavior changes.

**Status:** Proposed architecture for review. The repository is still an early prototype; the diagrams below describe intended dependency boundaries, not a claim that these projects or services already exist.

## Goals

- Build Zodiac as a reusable C#/.NET tactical-game engine and run the North American PS1 release (`SCUS-94221`) as its first game/content layer.
- Keep compatibility behavior observable and testable, including quirks that affect play.
- Keep the original simulation deterministic and independent of presentation and operating-system services.
- Permit additional content and mechanics without imposing the original executable's fixed-size data limits.
- Use the C decompilation and locally prepared game files as development references and import sources, never as runtime dependencies.

## Current repository, verified

- `Fft.Core` contains common identifiers/value types, map data types, a typed battle transition proof, and flat-grid movement policies (one prototype and one bounded terrain-cost slice).
- `Fft.Content` loads a versioned manifest and hash-verified loose files by logical asset ID. It references `Fft.Core`.
- `Fft.AssetTool` and `Fft.AssetTool.Cli` handle offline image inventory, extraction, and a narrow unit `.SPR` parsing/decoding proof. The CLI composes the tools.
- `Fft.Client` is a console composition proof. It can load a prepared asset; it is not yet an interactive game client.
- Tests exist for the current projects. No complete campaign runtime, graphics/audio backend, input loop, or save system exists yet.

The architecture should evolve from this shape without splitting projects just for conceptual neatness. Add a new assembly when it creates a useful dependency boundary or an independently testable responsibility.

## Target dependency shape

```mermaid
flowchart TD
    Client[Windows client and composition root]
    App[Application use cases]
    Engine[Zodiac engine contracts and deterministic simulation]
    FftGame[FFT game rules and compatibility profile]
    FftData[FFT definitions and prepared assets]
    Win[Windows platform adapters]
    Tool[Authoring and asset-import tools]
    Files[(Prepared files and save files)]

    Client --> App
    Client --> Win
    App --> FftGame
    App --> Engine
    FftGame --> Engine
    FftGame --> FftData
    Win -. implements ports defined inward .-> App
    Tool --> FftData
    FftData --> Files
```

The system has two product concerns with a one-way dependency: **Zodiac engine/framework** and **Final Fantasy Tactics game implementation**. The FFT layer depends on Zodiac; Zodiac must not depend on FFT types, assets, formulas, IDs, or campaign scripts. The current `Fft.*` projects have not yet been split along this boundary; this is a target architecture, not a description of today's assembly graph.

### Namespace and project naming convention

- Use `Zodiac.Engine` as the namespace and project name for reusable deterministic simulation contracts and behavior. Do not add a `.Core` segment unless a later, concrete assembly boundary justifies it.
- Use `Zodiac.Games.Fft` as the root for FFT-specific rules and compatibility behavior. Put FFT content definitions and loading under `Zodiac.Games.Fft.Content`, offline import tools under `Zodiac.Games.Fft.AssetTool`, and the FFT host/client under `Zodiac.Games.Fft.Client` when those projects are migrated.
- Namespaces communicate ownership, but project references enforce the boundary: FFT projects may reference `Zodiac.Engine`; Zodiac projects must not reference `Zodiac.Games.Fft.*`.
- Assign a type to `Zodiac.Engine` only when its meaning and behavior are independent of FFT. FFT formulas, PS1 compatibility policies, job/ability/item definitions, game data identifiers, and FFT asset formats stay FFT-owned.
- Keep existing `Fft.*` projects and namespaces during exploration. Move types in small, reviewable steps after their ownership and contracts are understood; do not combine namespace migration with behavior changes.

The current intended mapping is therefore `Fft.Core` (mixed prototype) to be split by responsibility over time, `Fft.Content` toward `Zodiac.Games.Fft.Content`, and `Fft.AssetTool` / `Fft.AssetTool.Cli` toward `Zodiac.Games.Fft.AssetTool` / `Zodiac.Games.Fft.AssetTool.Cli`. This is a target naming map, not a claim that the projects have already moved. Shared asset-service abstractions may instead move into an engine-owned project if a concrete engine use case requires them.

### Zodiac engine/framework responsibility

- Provide reusable deterministic tactical simulation primitives and contracts: map/grid topology, spatial positions, runtime-state boundaries, commands/transitions, turn-scheduling hooks, action/targeting abstractions, movement/range policy contracts, status/effect application points, deterministic random-state interfaces, simulation events, and replay/logging seams.
- Keep behavior neutral where different games reasonably vary. Rules should be selected or supplied through explicit policies/profiles rather than FFT-specific branches in the engine.
- Provide content package and asset-service abstractions without embedding FFT identifiers, binary layouts, jobs, abilities, formulas, or copyrighted assets.
- Keep rendering, input, audio, storage, and operating-system specifics in adapters or host applications.
- Aim for a small tactical engine, not an all-purpose framework. Add abstractions when another concrete use case demonstrates value.

### FFT game/content responsibility

- Own FFT concepts and compatibility behavior: jobs and JP, abilities/skill sets, items/equipment, statuses, FFT formula implementations, progression, CT/turn semantics, map terrain meanings, encounters, battle/campaign/event scripts, localization, and PS1-specific asset conversion/selection.
- Translate validated FFT definitions and imported source data into Zodiac runtime contracts. Preserve exact compatibility semantics in an explicit PS1 profile; isolate later Lion War/WotL additions in a separate profile/content set.
- Keep a trace from important FFT rules/data fields to the pinned decomp source and, where available, original/emulator observations and regression fixtures.
- Keep the asset importer FFT-aware. The engine consumes prepared logical assets or game-provided rendering data, never raw BIN sectors.

This boundary is conceptual first. `Fft.Core` currently contains FFT-flavored map and movement models and is not yet a clean reusable engine package. Generalize or move a type only when its engine-level contract is understood and an FFT adapter can preserve behavior. Keep project/namespace renames separate from behavior changes.

`Fft.Core` is the current home for the domain and simulation boundary, but it is not yet cleanly split into reusable Zodiac contracts and FFT-specific rules. A future assembly split may introduce a Zodiac-owned engine library and an FFT content/rules library after seams are proven. An application project is a future option for client-facing use cases once those use cases exist; it should not be created only to add another layer around the current console command. Windows adapters and a rendering library are intentionally undecided.

The dependency rule is inward: simulation code must not reference `Fft.Client`, graphics/audio/input packages, operating-system APIs, the BIN, or the C reference. Platform adapters implement interfaces owned by an inward-facing layer. The client is the composition root that chooses implementations and connects input, use cases, content, and presentation.

## Responsibilities and contracts

### Zodiac engine domain and simulation

- Own reusable stable/runtime identity contracts, simulation state/transition abstractions, deterministic random state, generic tactical policies, and simulation events. It must not own FFT job definitions, formula tables, story state, campaign data, or PS1 binary layouts.
- Represent play as explicit inputs to a transition: current state plus a command and RNG state produce a new state, result/events, and updated RNG state.
- Avoid file I/O, wall-clock reads, global randomness, rendering, audio, and direct input polling.
- Use collections sized by content and explicit validation/resource budgets. Preserve original limits only when they affect compatibility or are an intentional safety limit.
- Record arithmetic width, rounding, RNG draw order, and update ordering where they affect observed behavior.

Commands express requested actions; the simulation validates them. Events describe accepted state changes for presentation, logs, and tests. They are not a second authority that can mutate simulation state. Start with simple typed C# models; do not introduce a generic ECS, event sourcing framework, or plugin bus until a concrete need appears.

### Content definitions and runtime state

- Keep immutable, validated game-specific definitions (FFT jobs, abilities, items, maps, encounters, campaign data) separate from mutable runtime instances (unit HP/MP, status, equipment, turn state, and story flags). These definitions belong to the FFT game/content layer, not Zodiac's generic engine package.
- Give definitions stable, namespaced IDs. Validate cross-references and schema versions when loading a package rather than failing during a battle.
- Layer original-compatible content and additions explicitly. Do not overwrite or silently alter baseline definitions when loading an extension pack.
- Keep save data as a versioned persistence contract made from stable IDs and explicit state DTOs, not a serialized in-memory object graph. Define migrations when save format becomes necessary.

### Asset import and runtime access

- `Fft.AssetTool` is an offline import/validation tool. It may inspect the user's local BIN and emit prepared files/manifests under ignored `/scratch/` during development.
- The running client loads prepared files by logical asset ID through `Fft.Content`; it does not open the BIN or invoke the extractor.
- Keep source assets, converted outputs, and manifests reproducible. Test parsers/decoders with synthetic inputs and source-traced vectors; keep copyrighted game-derived bytes local.
- Rendering consumes decoded content through a narrow asset/content boundary. It should not contain gameplay rules or ISO parsing.

### Application, platform, and presentation

- Application use cases coordinate requests such as starting a battle, issuing a simulation command, loading campaign data, and saving. They do not implement game rules or draw pixels.
- Platform adapters own Windows windowing, graphics, audio, controller/keyboard input, file locations, and OS timing. Exact libraries are deferred until a rendering proof identifies requirements.
- Presentation translates input into application commands and turns state/events into view models or render submissions. It never writes battle state directly.
- Keep the simulation executable headlessly in tests and command-line tools.

### Extensions and The Lion War

- Start extensions with validated, versioned declarative content packages and a typed set of supported actions/effects.
- Keep the PS1-compatible campaign/profile as the baseline. Enable extra jobs, items, characters, maps, encounters, campaigns, or mechanics through explicit profiles/package layers.
- Treat The Lion War as a distinct compatibility/content profile after selecting its exact variant and feature scope. Preserve provenance and identify intentional deviations from the PS1 baseline.
- Defer arbitrary managed-assembly plugins and general-purpose scripting. If data and typed effects cannot express required campaign behavior, design a deterministic sandbox with versioned APIs and explicit permissions.

## Behavioral validation

Use several independent kinds of evidence; none alone proves full equivalence:

1. Unit tests for arithmetic, edge cases, and source-traced rule vectors.
2. Deterministic replay tests: same initial state, commands, and random seed produce identical state and event traces.
3. Scenario regressions with recorded preconditions, inputs, expected transitions, and reference decomp commit.
4. Differential checks against executable source-derived or emulator-observed outputs when a suitable oracle exists.
5. Separate visual checks with fixed game state/frame captures, image diffs, and manual review. Do not treat screenshot similarity as a substitute for simulation tests.
6. Content validation and decode tests that do not require the BIN; real-asset integration checks remain local under `/scratch/`.

Tests should state whether the expected value comes from direct source tracing, an executing oracle, an emulator capture, or an assumption. The decomp's machine-code matching checks establish a different property than the C# behavior tests.

The initial state/command/event contract and its explicitly simplified movement policy are recorded in [`../battle-transition-poc.md`](../battle-transition-poc.md).

The terrain and unit movement input model, including its source mapping and unimplemented pieces, is documented in [`../behavior/movement-inputs.md`](../behavior/movement-inputs.md).

## Deferred decisions and open questions

- Windows graphics/windowing/audio/input libraries and deployment format.
- Whether turn/campaign runtime eventually warrants separate `Fft.Simulation` and `Fft.Application` assemblies.
- Exact save schema, migration policy, and backup behavior.
- Package archive format, signing/trust policy, conflict resolution, and hot reload needs.
- Script language and sandbox policy, if declarative mechanics prove insufficient.
- Exact The Lion War variant, revision, imported features, and intentional rule changes.
- Which original/emulator observations are available for validating behavior beyond static decompilation.

Resolve these only when a milestone has concrete requirements that depend on them.
