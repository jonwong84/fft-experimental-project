# Zodiac / FFT Project Handoff

**Latest checkpoint (2026-10-23):** ZOD-023's bounded headless battle and ZOD-025's synthetic
Windows host are implemented; ZOD-027's [Chocobo idle-pose trace](architecture/zod-027-chocobo-idle-pose.md)
and ZOD-028's [static pose preparation/loading](development/prepared-sprite-pose.md) are complete.
ZOD-029's [MAP003 source map](architecture/zod-029-map003-source-map.md) selects a frozen
day/dry scene and exact four-cell geometry/terrain checkpoint. ZOD-030's
[map preparation/loading](development/prepared-map-slice.md) prepares that scene repeatably
as geometry, indexed textures/palettes, frozen presentation metadata and both native terrain layers.
ZOD-031's [fixed prepared scene](development/prepared-scene.md) renders that map and the held
Chocobo with an authored camera, neutral texture lighting and modern depth/cutout policy.
ZOD-032's [playable two-unit battle](development/playable-battle.md) adds mouse/keyboard
selection, legal highlights, move/attack/wait/advance, status and restart for synthetic M1.
Start with the current [backlog](BACKLOG.md) and
[Windows host instructions](development/windows-synthetic-host.md); the original handoff sections
below are historical. Locked restore, zero-warning Release build and all **868 tests** passed for ZOD-032.
The selected six-piece pose and 9x13 map are prepared under ignored scratch and loaded by Client without
original-format parsing; native terrain heights remain intact. The original synthetic view remains available.
Two finite prepared-scene GPU runs produce identical images and unchanged input hashes.
Two programmed Windows battle runs also match the original headless result and submitted-command
replay, including after a resize. ZOD-034 now implements independent Windows CI,
TRX/JUnit reports and a self-contained folder check with separate locked publish
graphs and dependency notices. Local publishing, headless launch and the published
GPU check passed; remote CI and clean-machine interactive validation remain pending.
See the [launch guide](development/windows-launch-validation.md). ZOD-035 completes
the [bounded save/load proof](development/two-unit-battle-saves.md): versioned DTOs,
selected content bindings, active-turn/RNG restoration, strict validation, synthetic
migration and safe file replacement. All thirteen checkpoints continue with identical
state/events. Latest locked restore, zero-warning Release build and all **954 tests**
passed. ZOD-040's [script investigation](architecture/zod-040-event-script-investigation.md)
now maps script families, one source-derived synthetic event and pinned tool
capabilities/limits. ZOD-045's [coverage inventory](coverage/README.md) records 58
planning entries across nine domains, with scoped evidence/checks and open gaps.
The source/link/view checker passed against the pinned read-only reference; these
documentation tasks do not rerun the 954-test suite or implement script execution.
Next: ZOD-064's decoder, then ZOD-065's pure executor; ZOD-067's campaign-save and
checkpoint trace can proceed independently. Original captures (ZOD-026), comparison
checkpoints (ZOD-033), and remote CI/clean-machine evidence (ZOD-034) remain open.
MAP003's static scene does not broaden M1
combat's height-10 domain. Gameplay is limited to the synthetic local two-unit battle;
source-derived checks do not establish original-rendering equivalence. Physical input timing
remains a manual follow-up. Original media/captures remain ignored.

**Prepared:** 2026-10-20  
**Workspace:** `C:\repositories\fft-experimental-project`  
**Branch:** `feature/initial-development`  
**Last committed project change observed at original handoff creation:** `48b4772 Add battle simulation and movement model foundations`
**Engine name:** Zodiac  
**First content set / compatibility target:** North American PlayStation release of Final Fantasy Tactics, `SCUS-94221`

This document is a restart point for the owner or another coding agent. It distinguishes completed work, local-only prerequisites, current uncommitted work, and future intentions. The goal is not to imply that the project is already a game engine capable of playing FFT: it is an early .NET foundation with useful asset-pipeline proofs and small simulation models. Amendments dated 2026-10-20 extend the architecture to make Zodiac reusable and separate it from the FFT game layer.

## 1. Mission and confirmed direction

Build **Zodiac**, a native, managed C#/.NET game engine and Windows application. Final Fantasy Tactics is the first game/content set to run on it. The original C decompilation is a research and validation reference only; the finished program must not link, invoke, or depend on it at runtime.

The user confirmed this is a personal project with no intent to distribute. A locally owned original BIN is available for local asset import. Keep it and all extracted/decoded game data under ignored `/scratch/`; never commit or upload those files. This personal scope does not establish the upstream decompilation's license or settle all local-use questions.

Priorities:

1. Preserve the PS1 baseline behavior and campaign as accurately as practical. Record confidence, deviations, and observable quirks instead of silently fixing them.
2. Keep the simulation deterministic, headless, and independent from display, audio, input devices, filesystem access, and wall-clock time.
3. Separate game/content definitions from mutable runtime state and save data.
4. Support additional jobs, abilities, items, characters, monsters, maps, encounters, campaigns, and eventually mechanics without copying original fixed table capacities as accidental constraints.
5. Provide authoring tools so the user can inspect and edit game data, rather than requiring hex editing. C# should be a first-class way to define typed data (for example `JobDefinition` with named properties), with a path to visual editors for content and assets.
6. Treat The Lion War / War of the Lions integration as a later explicit profile/content set after its exact variant and feature scope are chosen.

## 2. Current working-tree state

At original handoff creation, `git status --short` showed the movement-policy/documentation work below. That work was subsequently committed and pushed as `ed72bf7 Add terrain movement proof and project handoff`. The current architecture clarification is a new, separate documentation change; check `git status --short` for its exact files before continuing.

```text
 M PROJECT_PLAN.md
 M README.md
 M docs/architecture/overview.md
 M docs/behavior/movement-inputs.md
?? src/Fft.Core/TerrainAwareMovementRange.cs
?? tests/Fft.Core.Tests/TerrainAwareMovementRangeTests.cs
```

The committed movement slice:

- Adds `TerrainAwareMovementRange`, a deterministic, same-layer, cardinal movement-cost propagation policy using a priority queue.
- Charges the cost of the tile being entered; accepts a tile when minimum entered-tile cost is no greater than effective `Move`.
- Does not propagate into tiles flagged Blocked or surfaces whose effective cost is blocked.
- Filters Cannot Stop and Untargetable from final destinations, while allowing propagation through them in this deliberately limited model.
- Throws when a traversed surface has no cost in the supplied effective movement profile; it does not silently invent a cost.
- Does **not** modify or replace the `BattleSimulator`'s prototype `FlatGridMovementRange` behavior.
- Updates the movement source note and plan, and records Zodiac as the engine name in README and architecture documentation. Assembly and namespace names remain `Fft.*`.

The terrain policy is an algorithmic simplification informed by `move_range.c`; it is not a line-by-line port and is not a claim of full FFT pathfinding equivalence. Source trace details and exclusions are in [`behavior/movement-inputs.md`](behavior/movement-inputs.md).

### Last verification

The most recent complete solution run succeeded:

```text
dotnet test Fft.sln -c Release --no-restore -m:1
Core: 52 passed; AssetTool: 66 passed; AssetTool.Cli: 10 passed; Content: 6 passed
Total: 134 passed, 0 failed, 0 skipped
```

`git diff --check` completed without whitespace errors. In this Codex environment, testhost needs normal local process communication and temporary files were directed to ignored `scratch/test-temp`; a normal local Windows terminal may not need those workarounds. The full test command also builds the projects. Last explicit `dotnet build` before this new policy was successful; run both build and tests again before merging the new handoff/documentation changes.

There are no test fixtures containing the BIN or extracted images. Game-derived data remains local under ignored `/scratch/`.

## 3. Repository and toolchain snapshot

### Projects present

- `src/Fft.Client`: .NET 10 console composition/bootstrap. It can read one prepared asset through `Fft.Content`; it is not yet an interactive game.
- `src/Fft.Core`: stable IDs, map/tile/unit/battle state models, movement inputs, the typed battle transition proof, prototype flat movement range, and the just-added standalone terrain-cost movement policy.
- `src/Fft.Content`: versioned manifest-based loose-file reader by logical asset ID, with size/hash verification.
- `src/Fft.AssetTool`: managed raw-sector reader, ISO/PVD inventory, ordinary-file extraction, manifest generation, and bounded unit `.SPR` parser/attack-image decoder.
- `src/Fft.AssetTool.Cli`: command host for inventory, comparison/extraction, and sprite verification/preview.
- Tests: `tests/Fft.Core.Tests`, `tests/Fft.AssetTool.Tests`, `tests/Fft.AssetTool.Cli.Tests`, `tests/Fft.Content.Tests`.
- Solution: `Fft.sln`, targets `net10.0`. Require .NET 10 SDK.

There is not yet a graphics framework, real-time game loop, Windows rendering backend, input/audio adapter, campaign system, save system, data editor, extension loader, or full FFT rules implementation.

### CI

`.circleci/config.yml` has a CircleCI 2.1 job using `mcr.microsoft.com/dotnet/sdk:10.0`. It restores, builds Release, tests, and runs SonarQube Cloud analysis. PR builds query GitHub for the actual target branch. The job expects Sonar credentials from the configured `global-v1` context and a GitHub token for PR base lookup. The CI container is Linux; it currently verifies the platform-neutral .NET projects, not a Windows graphics/application package. Publishing is not configured.

### Local reference and game data

- Decomp/reference checkout: `C:\repositories\fft_decomp`; read-only.
- Recorded source snapshot used for current behavior notes: `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. It is a snapshot, not a claim about current upstream. Before a new source-specific study, verify its exact current local HEAD and record that SHA. The upstream changes frequently.
- The user-provided `scratch/fft-clean-base-game.bin` is documented as matching the `SCUS-94221` target profile (541,315,152 bytes; SHA-256 `29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9`). CUE is local alongside it. Do not copy it to the decomp repo or stage it.
- `/scratch/` is in `.gitignore`.
- The decomp inventory has 2,464 ISO entries. Managed inventory matches paths and LBAs. The 2,457 ordinary `data` entries were extracted locally and hash-checked against the reference inventory (162,790,593 bytes). Seven mixed XA/STR entries remain unsupported.
- One ordinary file (`MENU/TUTO7.SCR`) was independently extracted from BIN and matched reference SHA-256.
- `BATTLE/CYOKO.SPR` completed a local extract → manifest → hash-verified `Fft.Content` load → parse → bounded attack-image decode flow. A source-derived golden hash and 16-palette diagnostic PNG path exist; the preview is not an emulator screenshot or final GPU result.
- BIN extraction is an offline preparation workflow only. Runtime should load prepared files from disk and not access the BIN.

Relevant detailed notes: [`PROJECT_PLAN.md`](../PROJECT_PLAN.md), [`asset-inventory-poc.md`](asset-inventory-poc.md), [`sprite-attack-image-decoder-poc.md`](sprite-attack-image-decoder-poc.md), [`architecture/overview.md`](architecture/overview.md), [`battle-transition-poc.md`](battle-transition-poc.md), and [`behavior/`](behavior/).

## 4. Architecture direction

The current architecture note is [`architecture/overview.md`](architecture/overview.md); the ADRs record significant boundaries. The user has named the reusable engine **Zodiac** and clarified that the project has two parts: the Zodiac engine and an FFT game/content implementation that runs on it. Proposed ADR 0004 records this one-way boundary. Current project names are still FFT-specific and the code has not yet been split; do not rename all projects/namespaces in a broad refactor. Decide and prove the product/API split incrementally.

Target dependency shape:

```text
Zodiac Windows client / composition root
    ├── Zodiac application/use cases (only when concrete needs arise)
    ├── Zodiac engine core: deterministic simulation and generic runtime contracts
    ├── FFT content/rules profile: FFT-specific jobs, formulas, actions, maps, scripts
    ├── platform adapters: rendering, audio, input, file/save services
    └── authoring/import tools: data editor, asset converters, validators
```

The intended dependency is one-way: the FFT game/content layer depends on Zodiac engine contracts; Zodiac cannot depend on FFT. The current `Fft.*` solution does not satisfy this clean split yet. [`adr/0004-zodiac-engine-and-fft-game-boundary.md`](adr/0004-zodiac-engine-and-fft-game-boundary.md) records the new proposed decision.

Important boundary decisions:

- Keep deterministic simulation headless and deterministic; commands in, state/events out.
- Definitions (jobs, abilities, formulas, items, text, maps, animations, encounter/campaign data) are immutable validated content. Battle/campaign unit values and save progress are mutable runtime state, with explicit versioned persistence later.
- Use stable namespaced IDs and content-sized collections. Preserve original fixed widths only at import/compatibility boundaries where evidence requires them.
- Baseline FFT-compatible profile stays distinct from extensions and Lion War content.
- Load prepared content via manifest/logical IDs. Do not leak sector/LBA handling into game runtime.
- Avoid arbitrary code execution or plugin buses until typed declarative content and real extension needs are understood.
- `Fft.Core` currently contains FFT-flavored values. The intended engine/content split is a design goal, not yet an implemented separation; decide what is genuinely reusable before renaming or relocating it.

## 5. New requirement: content authoring and editing tools

The user wants a way to manage/edit the full body of game data: flags, battle formulas, text, sprites, animations, and other records. They would like to author some data in C# (example: a `Job`/`JobDefinition` class with typed properties), not be confined to a GUI or hex editor. This is now a core product requirement, not merely a possible convenience.

### Proposed approach (not yet implemented or fully decided)

1. **Typed C# domain definitions.** Define immutable classes/records such as `JobDefinition`, `AbilityDefinition`, `ItemDefinition`, `StatusDefinition`, `BattleFormulaDefinition`, `MapDefinition`, `EncounterDefinition`, `AnimationDefinition`, `TextEntry`, and campaign/script references. Give every record a stable ID, explicit fields, validation, and references by ID. Avoid anonymous integer indexes as the public content API.
2. **Editable package representation.** Serialize definitions as versioned human-reviewable data (JSON or YAML is not chosen). The C# types are the validated object model, regardless of the chosen text format. Support diffs, schema versions, migration, validation diagnostics, and reproducible package builds.
3. **C# authoring API.** Allow trusted local authoring projects to construct definitions in C# and run validators/package builders. Do not load arbitrary third-party C# into the game process at first. A project can compile C# authoring code at build time to produce validated package data.
4. **Visual editor.** Build a Windows editor over the same schemas/validation APIs after the first useful content model exists. Start with inspect/search/edit/validate/preview for one well-defined category; do not create a giant editor before the content schemas and engine contracts stabilize. Data and asset preview tools can be separate tabs or focused editors.
5. **Simulation formulas.** Separate original-compatible formulas (implemented and regression-tested in managed C#) from authored parameters and custom mechanics. For extensible formula behavior, first consider typed operations/expressions or a constrained DSL that the C# engine evaluates. Arbitrary DLL/script execution is deferred until trust, determinism, versioning, and sandbox requirements are designed.
6. **Text, sprites, animation.** Use localized text records with stable keys and font/layout preview. Treat image/sprite import and animation/event timeline editing as asset-editor concerns over prepared files and decoded metadata. An asset preview must distinguish raw decoded bytes from faithful PS1 presentation (CLUT selection, STP/blending, ordering, frame transforms, timing).
7. **Flags.** Replace unexplained bitmasks in authoring surfaces with named typed flags and descriptions, but retain a lossless import/export mapping to original packed fields where needed. Tests must verify round-trips and reject unknown/invalid combinations explicitly.

### Existing community tools to investigate

Initial web research on 2026-10-20 found:

- [Glain/FFTPatcher](https://github.com/Glain/FFTPatcher) is a continuation of the FFTPatcher suite and is GPL-3.0. Its repository lists `FFTacText`, `EntryEdit`, `ShishiSpriteEditor`, and patcher components. Its README describes editors for abilities, items, jobs, job levels, skill sets, monster skills, status effects, battle/event (ENTD) data, move-find items, and store inventory. It is a strong reference for editor workflows and data categories. Its GPL license means do not copy code into this project casually; inspect licensing before adapting code. Investigate which subtool handles which script format before relying on it.
- [Nax/TacticsCrafter](https://github.com/Nax/TacticsCrafter) describes itself as both an API and patch-builder application for the PSP game *Final Fantasy Tactics: The War of the Lions*. The repository identifies an MIT license and a C++/Qt build. It may inform typed APIs/mod workflow, but targets a different release and is not a direct PS1 data-format dependency.

These findings are preliminary. Inspect exact capabilities, supported editions, source/asset licensing, maintenance/build status, and format compatibility before deciding whether Zodiac imports, interoperates, or only borrows workflow ideas. The user mentioned community script-editing tools; identifying the exact relevant editor and script encoding is a concrete research task. Avoid inventing script compatibility from tool names.

## 6. Testing and behavioral-equivalence strategy

No single oracle is sufficient. Use a ladder of evidence:

1. **Build/CI:** reproducible .NET 10 Release build and all tests; later add Windows-targeted build/package checks when UI or platform dependencies appear. Keep Linux CI only where it reflects supported platform-neutral layers.
2. **Unit tests:** one deterministic rule per test group, normal/boundary/invalid cases; exact integer widths, signedness, truncation, rounding, order of updates, and RNG draw counts where relevant.
3. **Source-traced behavior fixtures:** each fixture records decomp URL/path, exact commit SHA, relevant callers/data tables, test input, expected observable output, and confidence. Do not cite moving line numbers alone.
4. **Differential harness:** wherever a reference executable or trustworthy extracted behavior vector exists, run the same structured scenario against C# and compare state transitions/logs. Static C code is research material, not a callable runtime oracle. Decomp byte-match checks prove rebuilt C machine code, not C# rule equivalence.
5. **Scenario/regression tests:** battle snapshots covering turn, move, action, status, reaction, victory, loot, campaign outcomes; replay seeds and command sequences deterministically. Add cases for known quirks, not just idealized rules.
6. **Asset tests:** synthetic malformed/edge-case files, source hash/size checks, golden decoded-byte hashes, manifest integrity, and parser fuzz/bounds cases as formats expand. Keep copyrighted data out of fixtures.
7. **Visual/audio validation:** render deterministic scenes at fixed simulation ticks and compare against documented captures where the original/emulator can be run. Start with geometry/pixel regions and tolerances; supplement automated image metrics with human review because scaling, color, blending, timing, and filtering can make strict whole-frame hashes brittle. Track simulation correctness separately from presentation correctness.
8. **Manual checkpoints:** emulator/original-game recordings or screenshots for features not independently executable from source; note game version, scenario, setup, and capture conditions.

Reference `make check` is valuable for proving C-source byte matches; its Go tooling tests do not constitute battle-rule tests. A source-derived C# vector should be marked as such until checked against an executing original/emulator.

## 7. Work completed, in sequence

- Inspected the `adamrt/fft_decomp` reference and established that the application repository started nearly empty. Documented source organization, platform constraints, modules, data/media omissions, and source/license questions.
- Added a Windows-first .NET 10 solution, console client, Core library, xUnit test projects, CircleCI build/test/Sonar workflow, README and project plan.
- Added `TilePosition`, immutable map/tile structures, movement-relevant terrain fields, typed movement flags, stable surface IDs, slope packing, and effective `UnitMovementProfile`; profile owns a defensive/read-only surface-cost map and has structural equality.
- Added `BattleState`, unit IDs/state, move command, transition/event types, and a deliberately simplified `BattleSimulator` with immutable successful transitions and rejection results. Tests cover immutability, invalid commands/occupancy/bounds, events, and determinism. This is not an FFT-faithful movement/turn simulator.
- Added `FlatGridMovementRange` as a narrow cardinal flat-grid prototype.
- Added source notes for map boundary and movement-input fields. Added the latest standalone terrain-cost propagation proof (uncommitted at handoff).
- Added managed `Fft.AssetTool`/CLI components in milestones: raw Mode 2 sector reading; PVD/ISO inventory; image profile/hash/CUE checks; reference-inventory comparison; bounded safe extraction; path collision validation; manifest writing; narrow `.SPR` fixed-prefix/attack-image parsing/decoding.
- Added `Fft.Content` versioned manifest and logical-ID loose-file loading with size/hash validation. Client can load a prepared file; no BIN runtime access.
- Extracted all 2,457 ordinary data entries locally and verified hashes. Mixed XA/STR entries are deferred.
- Used `BATTLE/CYOKO.SPR` for a managed extraction/load/parse/decode proof and output-hash fixture, plus all-palette diagnostic PNG preview. Visual result is explicitly approximate; no native/emulator render equivalence is established.
- Added architecture overview and ADRs for headless determinism, compatible baseline vs extension profiles, and offline import/loose runtime assets. ADRs 0001 and 0002 are proposed; ADR 0003 is accepted/implemented.

## 8. Roadmap and measurable acceptance criteria

The detailed roadmap lives in `PROJECT_PLAN.md`; the sequence below emphasizes next work and missing engine/editor foundations. Do not estimate a full completion timeline from current evidence.

### Immediate: preserve and review the current slice

- Review the uncommitted `TerrainAwareMovementRange` and docs as one bounded change; the full test suite passed (134 tests) before preparing this handoff.
- Ensure the project handoff itself and current README/plan changes are included in the next deliberate commit or PR. Do not stage `/scratch/`.
- Re-run clean Release build/tests and review the final `git status` and diff. Keep PR scope reviewable.

### Next simulation slice: movement geometry

- Trace one constrained height/slope/jump case through callers, source fields, arithmetic units, and direction ordering at a freshly recorded decomp SHA.
- Add a synthetic source-mapped map/unit fixture and exact expected reachable set; include boundary and negative cases.
- Add only that rule to the movement policy (or a separate focused service); keep unsupported movement types explicit.
- Acceptance: deterministic tests match the documented fixture; each field used has source provenance; out-of-map/invalid profile behavior is tested; unsupported cases are documented; existing 134 tests remain green.
- Do not integrate terrain policy into `BattleSimulator` until the command API, occupancy/path reconstruction, and policy selection are deliberate.

### Simulation foundations

- Resolve effective unit movement inputs from job, equipment, statuses, weather, and abilities, with tables/precedence traced. Model source-width conversion at import boundaries.
- Choose a first vertical slice such as one battle setup plus turn advancement and a basic physical attack; source-trace CT/turn order, action legality, damage, RNG, update ordering, and victory checks before implementing.
- Define deterministic RNG state and recorded input/command/replay format before RNG-dependent combat grows.
- Acceptance: headless scenario fixture with deterministic replay, observable expected transitions/events, and no client/platform/asset I/O dependency.

### Content model and authoring

- Inventory source records/tool formats for jobs, abilities/formulas, flags, equipment/items, text, sprites/SHP/SEQ/animation events, maps/terrain, encounter/ENTD, AI and campaign scripts.
- Create one typed C# definition model end-to-end (recommend a Job definition plus linked action/ability IDs), validator, schema version, serialization/package round-trip, and CLI diagnostic output. Separate original imported definitions from authored extensions and runtime state.
- Decide serialized format (JSON/YAML/other), extension conflict/override rules, stable ID namespaces, package versioning, localization model, provenance, and whether tool input can be C# source, declarative files, or both. C# authoring should not imply runtime arbitrary-code execution.
- Prototype editor UX only after schema stabilizes: browse/search, inspect/edit, reference navigation, validation errors, and preview. Make editor call shared validators, never duplicate runtime rule semantics.
- Investigate FFTPatcher script/text/sprite tooling and exact script format; inspect TacticsCrafter only for ideas applicable across versions. Record licenses before copying/adapting anything.
- Acceptance: user-authored package with at least one new job and linked ability loads in a test without editing original content or rebuilding engine; invalid references get clear diagnostics; package round-trips; baseline definitions cannot be silently overwritten.

### Assets and presentation

- Confirm the `.SPR` source-derived output hash against an executing original routine/emulator when practical; parse SHP/SEQ metadata and trace frame/palette selection.
- Continue one format at a time: battle map/model, font/text, effects, sound/music, movies only when required by the chosen campaign slice. Keep raw extraction and decode separated.
- Select a Windows graphics/audio/input stack only when a minimal interactive battle view is scoped. Rendering has its own reference image/manual checks and must remain separate from deterministic state updates.
- Acceptance: show a small authored/extracted battle scene at fixed ticks using disk-loaded assets; record manual and automated visual checks; CI can build the Windows client.

### Persistence, campaigns, extensibility, Lion War

- Add explicit versioned save DTOs/migrations after campaign state exists.
- Support content packages and declarative mechanics before untrusted executable plugins; define resource limits, trust, versioning, and determinism before scripts/extensions execute.
- Pin the exact Lion War/TLWotL variant/revision and build a feature-by-feature inventory. Distinguish PSP-origin WotL data from adaptations. Keep original PS1 profile as regression baseline.
- Acceptance for campaign end state: complete original campaign and save/load path, broad content/accounting coverage, source-backed critical rules, manual presentation checkpoints. Acceptance for Lion War: explicitly selected feature list and scenario checks; it remains separate from baseline.

## 9. Risks and open decisions

| Risk / unknown | Why it matters | Next evidence/action |
|---|---|---|
| Decomp is continuously changing and local checkout can lag | Findings can be based on stale code | Record local HEAD for each new source trace; never equate local pin with upstream main |
| Static source is not executable behavioral oracle | C# could reproduce source transcription errors or miss runtime interactions | Find emulator/original-run capture workflow and build scenario vectors |
| Current movement is intentionally narrow | Real FFT movement includes elevation, slope, jump, water, units, mounts, statuses and special modes | Grow one source-traced fixture/rule at a time |
| No complete content schema/editor exists | Adding content manually becomes error-prone and difficult for third parties | Start with typed Job + linked Ability package and shared validation |
| “Battle formulas” can imply arbitrary code | Unconstrained extensions threaten deterministic execution and compatibility | Separate baseline C# rule implementations from declarative parameters/typed effects; postpone arbitrary plugins |
| Asset formats are only partially decoded | Extracted files are not necessarily render-ready | Inventory formats; parse and validate formats one by one |
| Sprite preview is not faithful rendering | STP blending, layer order, animation, timing, camera and scaling affect appearance | Capture fixed-tick reference scenes and compare with tolerances/human review |
| Windows app framework is undecided | UI/rendering choices affect deployment and authoring workflows | Build a headless simulation slice first; choose stack with an actual battle-view prototype |
| Existing CI is Linux and Sonar credentials are environment-dependent | It cannot catch all Windows client packaging issues | Add Windows runner/build once Windows-only packages are chosen; keep secrets in CircleCI contexts |
| Community tooling has edition and license differences | Directly reusing a PSX/PSP tool may be incompatible or impose license obligations | Inspect FFTPatcher license and target formats; use as reference until a deliberate reuse decision |
| Exact Lion War variant is unspecified | Content scope and compatibility behavior differ by release | Select and pin variant/revision before integration planning |
| Project/decomp licenses and local media obligations | Personal/no-distribution intent is not a formal source license | Review upstream and mod licenses before code/data reuse or distribution |

## 10. Starting instructions for whoever continues

**Planning update (2026-10-22):** Use [BACKLOG.md](BACKLOG.md) for current work order and ticket acceptance criteria. Its first task audits current type ownership before any engine extraction; the next movement implementation remains gated on source tracing. The agreed naming convention is recorded in [the architecture overview](architecture/overview.md). Earlier working-tree and test-run snapshots in this handoff are historical; inspect the current checkout before continuing.

1. Read `AGENTS.md`, this handoff, `PROJECT_PLAN.md`, architecture overview, and relevant ADR/behavior note.
2. Check `git status --short` first. At this handoff, current work is uncommitted; preserve it and do not reset or rebase without user direction.
3. Keep `C:\repositories\fft_decomp` read-only. Verify and record its current HEAD before new source analysis. Do not rely on stale line numbers without checking the pinned commit.
4. Keep BIN, extracted files, decoded media, inventories, and previews inside ignored `scratch/`.
5. Run `dotnet build Fft.sln -c Release` and `dotnet test Fft.sln -c Release`; the expected test total is currently 134. Review diffs and run `git diff --check`.
6. Keep the next task narrow and source-traced. The next recommended simulation task is one height/slope/jump rule after reviewing the current cost-policy fixture. The next authoring-design task is a typed `JobDefinition` + ability reference + package validation spike, not a full editor.
7. Do not claim the work is a complete engine/game or that extracted assets are legally redistributable. State what is verified, source-derived, simulated, and still unknown.

## 11. Helpful commands

```powershell
dotnet build Fft.sln -c Release
dotnet test Fft.sln -c Release
dotnet run --project src/Fft.Client/Fft.Client.csproj -c Release --no-build
git status --short
git diff --check
```

For prepared Chocobo fixture verification (requires the ignored local prepared directory):

```powershell
dotnet run --project src/Fft.AssetTool.Cli/Fft.AssetTool.Cli.csproj -c Release -- verify-sprite --content-root scratch/prepared-chocobo-content
```

