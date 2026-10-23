# Zodiac — FFT Native C# Reimplementation Plan

**Status:** Zodiac has a runnable .NET 10 console client, a headless Core library with immutable battle-map/state types and a typed movement transition proof of concept, an AssetTool that inventories/extracts ordinary files and emits a versioned asset manifest, and a separate Content library that verifies and loads prepared files by ID. The client can load one asset from a prepared content directory by ID and report its verified size; it is still only a command-line composition proof, not a game loop or renderer. The local `/scratch/` extraction contains 2,457 ordinary files with reference-matching SHA-256 hashes; no assets are checked into the repository. The transition simulator still uses simplified flat-grid movement; a separate terrain-cost policy now proves one bounded rule. Source-faithful movement/combat rules, campaign logic, broad asset decoding, authoring tools, and UI framework remain unimplemented.

## Executive summary

At initial discovery, this checkout was an empty project scaffold, not the referenced decompilation: its tracked files were `.gitignore` and a 35-byte `README.md` containing only the repository name and `Test.`. Since then, it has gained a .NET solution, console bootstrap, Core library, and unit-test project. The configured remote is `jonwong84/fft-experimental-project`; the only observed commit at discovery was `Initial commit`. The local C decompilation remains a separate reference checkout.

The authoritative local reference is `C:\repositories\fft_decomp` (confirmed by the user), a matching decompilation of the North American PS1 release (`SCUS-94221`). It describes itself as 100% decompiled and byte-exact, but excludes game files and proprietary Sony tools. Its `make check` compiles code against per-function hashes; `make build` additionally requires the matching user-provided BIN. The byte match is valuable evidence for original machine code, but it does not by itself provide a callable C# reference implementation or behavioral test oracle.

**Recommendation:** keep the C decompilation outside the runtime and use it as a research and test-data source. The user confirms this is a personal project with no intent to distribute. Start with a deterministic, headless C# battle-rules kernel and a narrow behavior slice that can be independently checked against source and controlled retail-game observations. Keep rendering, audio, input, file access, content packages, and save handling behind separate interfaces.

## Evidence and confidence

### Verified in the application checkout

- `README.md`: only project name and `Test.`
- `.gitignore`: standard Visual Studio/.NET ignores; it has no rules for source assets, game data, or build outputs beyond common IDE patterns.
- `git ls-files`: only `.gitignore` and `README.md`.
- `git log --all`: one initial commit; branch `feature/initial-development` points to it. `origin` is the user's experimental-project GitHub repository.
- No tracked test, build, dependency, licensing, or architecture files were found.

### Verified from the local reference checkout (`C:\repositories\fft_decomp`)

- The reference project's [README](https://github.com/adamrt/fft_decomp/blob/master/README.md) states all game functions are C and compile to the original bytes; the rebuilt disc is byte-for-byte identical. It says game files and proprietary Sony tools are excluded.
- Its [Makefile](https://github.com/adamrt/fft_decomp/blob/master/Makefile) wraps development commands in Docker. `make bootstrap`, `make check`, `make build`, `make diff`, `make test`, formatting and metadata checks are available. `make test` vets/tests Go tooling; no game-logic unit test suite is advertised there. `make check` works without a BIN; `make build` and source-to-original byte diffs require `scus-94221.bin`.
- Its [AGENTS.md](https://github.com/adamrt/fft_decomp/blob/master/AGENTS.md) describes code by program under `src/`, shared systems under `src/lib/`, game headers in `include/fft/`, PS1 interfaces/libraries under `src/platform/psyq/`, and metadata/tooling in `target/` and `tools/`. It instructs contributors not to commit game assets and to preserve retail bugs and ABI differences.
- Its [ARCHAEOLOGY.md](https://github.com/adamrt/fft_decomp/blob/master/ARCHAEOLOGY.md) describes a resident executable plus BATTLE, WLDCORE, WORLD, OPEN, EVENT overlays, and effect programs loaded into fixed/overlapping PS1 memory regions. It records distinct GCC/ASPSX compiler fingerprints for program groups. Original source filenames/build scripts were not recovered.
- Its [QUIRKS.md](https://github.com/adamrt/fft_decomp/blob/master/QUIRKS.md) is a large catalog of observed oddities and bugs. These need to inform compatibility tests, not automatically become design constraints in the new engine.

### Additional direct local inspection

- Reference HEAD observed during discovery: `611b504d25d4a9b03fab8023d7dbea75fe381d5d` (2026-10-18, `Keep battle scene entry and route state with their owners`). The user reports that upstream `main` is updated continuously and their local checkout was 209 commits behind at the time of that report; this lag has not been independently fetched or verified. Treat the observed local commit as a snapshot, not as current upstream. Every source-based analysis or behavior fixture must record the exact commit SHA; line numbers and `master` links alone are not stable references.
- The local checkout has 1,114 visible files and 843 C files across `src/battle` (45), `effect` (24), `event` (69), `lib` (151), `main` (17), `open` (8), `platform` (441), `wldcore` (20), and `world` (68). `include/fft/` has 107 game headers. `target/` has seven module files: main, battle, world, wldcore, open, event, effect.
- Battle interfaces include `include/fft/action.h`, `formula.h`, `turn.h`, and `move.h`. Candidate study entry points are `turn_run_clock` (`src/battle/turn_order.c:30`), `move_range_build` (`src/battle/move_range.c:573`), `formula_calculate_physical_damage` (`src/battle/formula_effects.c:941`), and `formula_calculate_critical_hit` (`src/battle/formula_core.c:1311`). These need caller/data tracing before they become port specifications.
- `target/battle.yaml` records BATTLE.BIN size/hash, load address, compiler profile, and per-function address/size/hash/source bindings. The other six target files cover the corresponding modules.
- No `scus-94221.bin` exists at the reference checkout root. Per README/Makefile, `make check` does not need it; extraction, `make build`, and original-byte diffs do. No builds or tests were run during discovery.
- The user has since supplied `C:\repositories\fft-experimental-project\scratch\fft-clean-base-game.bin`. Its size is 541,315,152 bytes and SHA-256 is `29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9`, matching `tools/target.go`'s configured `SCUS-94221` profile exactly. It is stored under the newly ignored `/scratch/` directory. The reference extractor currently expects its conventional filename at the reference repo root; avoid copying/moving the 541 MB image just to meet that convention—asset tooling should accept a source path or use a local wrapper.
- Extraction has now been run successfully in the pinned reference Docker image. The reference checkout was mounted read-only; its `tools`, `target`, `src`, and `include` inputs were copied to ignored local `scratch` workspace and all extraction/build products were written there. Output is `scratch/fft-decomp-work/build/extracted/files`, with `disc.xml` and `inventory.json` alongside it. The tool verified the BIN hash and inventoried exactly 2,464 ISO entries with path, byte count, SHA-256, and LBA. Inventory-listed payloads total 471,719,585 bytes. `dumpsxiso` also produced a 28,032-byte `license_data.dat` sidecar in the file tree that is not one of the 2,464 ISO inventory records.
- The extracted inventory contains 138 `.SPR` files. Most unit sheets are 37,377 bytes, consistent with the 0x9200-byte header/data prefix and a one-byte compressed tail. `BATTLE/20M.SPR` is a candidate example for the decoder spike; `BATTLE/OTHER.SPR` is a smaller special case and should not be the first fixture.
- `tools/go.mod` declares Go 1.24.0 and `gopkg.in/yaml.v3`; the Makefile runs tool commands in Docker, and `make test` runs Go vet/tests. Neither checkout contains a .NET project. `dotnet.exe` is present on the Windows host, but its installed SDK version has not been established.
- No top-level `LICENSE`/`COPYING` file or license notice was found in the local reference scan. `AGENTS.md` says not to commit graphics, sound, movies, text, or script dumps. The personal/no-distribution scope is confirmed, but does not itself establish an upstream source license or resolve every local-use legal question.
- `tools/main.go` verifies the BIN's configured size and SHA-256, runs `dumpsxiso` to extract the ISO filesystem under `build/extracted/files`, and writes `disc.xml` plus a SHA-256/size/path/LBA inventory. It expects 2,464 ISO files. This confirms the BIN can become a named file tree using existing tooling when supplied.
- The game mostly requests raw sector ranges (`src/main/file_requests.c`, `src/main/heap.c`, `include/fft/file.h`); prepared files now use a manifest mapping stable IDs to relative paths, sizes, and hashes rather than exposing LBAs to the reader. Formats differ: unit `.SPR` files contain raw 4bpp blocks plus a variable-length nibble-RLE attack image (`include/fft/sprite.h`, `src/battle/battle_scene.c:842`); `.LZW` text uses LZ-style distance/length backreferences per `ARCHAEOLOGY.md`; movies use PS1 MDEC/VLC decode (`src/open/movies.c`, `src/platform/psyq/libpress/DecDCTvlc.c`). A complete codec inventory is not yet established.

### Assumptions and unanswered questions

- Confirmed: `C:\repositories\fft_decomp` is the intended local C reference. The observed HEAD is recorded above; pin it in project docs/tooling before relying on source-specific details.
- The public docs do not establish the copyright/license grant for the decompilation itself, nor the permissions needed to redistribute game-derived tables, text, scripts, artwork, audio, or disc data. Review the reference repository's current license and applicable terms before copying code or data; no license was found in this project's checkout.
- The North American PS1 release `SCUS-94221` is the verified behavioral baseline. The user also wants eventual integration of content from The Lion War mod, which brings War of the Lions-era material to the PS1 base. The exact mod variant/revision and included-content scope have not yet been selected.
- It is not yet known whether source-readable battle algorithms, data definitions, and quirks cover every behavior needed, or whether execution on original hardware/emulation and hand-logged observations will be required.
- Confirmed personal scope: no intent to distribute. Exact lawful source-media/import path and upstream code license remain unanswered; treat these as local-use constraints/questions. This is not legal advice.

## Repository and dependency assessment

| Area | Finding | Consequence |
|---|---|---|
| Current source | None in this checkout | There is no codebase to port yet. |
| Reference source | Local matching C decompilation at `C:\repositories\fft_decomp`; not configured as remote/submodule | Treat as a separately versioned research dependency; record a pinned URL/commit and review its license. |
| Build | No build system here. Reference uses GNU Make + Docker and its own tool image. | New app should build with supported .NET SDK on Windows; avoid a runtime dependency on C, Docker, PS1 SDK, or proprietary tools. |
| Reference build inputs | Optional exact North American BIN for extraction and disc build; no game files/tools are included upstream. | Build reproduction is not a substitute for runtime asset loading or license clearance. |
| Tests | None here. Reference exposes byte matching and Go-tool tests, not game-rule unit tests. | Create managed tests and explicit behavioral fixtures from the start. |
| Assets | None here or in reference repo; user can provide BIN for local use | Extract ISO files into a local tree, map sector references to paths, then decode selected formats to disk. |
| License | No license notice in current project or top-level reference files | Personal/no-distribution intent is confirmed; do not infer a source license or rights to redistribute derived material. |

### What is decompiled versus not supplied

The local reference claims complete function-level C decompilation and byte-exact reconstruction for the North American release. Its own docs say binary/source readability and semantic cleanup remain ongoing. It does not include game disc contents, graphics, audio, movies, script/text dumps, or proprietary Sony build tools. Thus “100% decompiled” means code reconstruction coverage for that release, not that a playable, asset-complete, portable C# game is already available.

The matching build system includes extraction and analysis utilities, hashes/configuration for functions, module build/check operations, and Go tooling tests. Those are useful for code archaeology and proving C edits preserve original bytes. They do not validate a new C# simulation result against a battle outcome.

## Major subsystems and likely dependencies

This decomposition is based on upstream `AGENTS.md`, `ARCHAEOLOGY.md`, and the listed reference program roles; the dependency edges below are an initial model to validate during source study.

| Subsystem | Responsibilities | Likely dependencies / notes |
|---|---|---|
| Resident/runtime | memory, file loading, shared services, sound driver | PS1 SDK/runtime and hardware abstractions; replace with .NET services. |
| Battle simulation | turn order/CT, movement, height/elevation, action legality, targeting, hit/damage, status, reaction/support abilities, AI, victory/loot/XP/JP | Unit/job/item/ability/map definitions; deterministic RNG; event scripts; status/action rules. Keep headless and pure where possible. |
| Battle presentation | isometric map, sprites/animation, camera, effects, battle UI | Rendering backend, asset decoder, timing; simulation must not require rendered frames. |
| World/campaign | world map travel, encounters, shops, propositions, party management, story flags | Campaign state, encounter/map content, event scripts, menus. |
| Events and scripts | dialog/story scenes, battle setup, triggers, scripted actions, overlays | Script interpreter, battle/world state, text localization; define compatibility and sandbox boundaries. |
| Menus and UI | title/opening, formation, job/equipment, shops, status screens, help | Input, text/font, layout, save/campaign state, asset services. |
| Audio | music, sound effects, sound driver | Audio backend, asset decode, event timing. |
| Persistence | memory-card records and settings | Replace PS1 card/file APIs with versioned, validated .NET save format. |
| Content pipeline | original and custom jobs, abilities, items, maps, encounters, campaigns | Versioned schemas, validation, IDs, references, package loading and provenance. |
| Extensions | custom mechanics and third-party content | Stable public contracts, capability/security model, versioning, deterministic simulation constraints. Defer executable plugins until content-only extension is proven. |

The PS1 implementation is not a single monolithic program: resident code and several programs/overlays reuse fixed/overlapping address regions. That arrangement and low-level interfaces are implementation evidence, not a reason to reproduce the original memory model in managed code.

## Goals, scope, and requirements

### Project goals in priority order

1. **Build both Zodiac and its first game.** Zodiac is a reusable managed C#/.NET tactical engine/framework; the FFT game layer supplies the North American PS1 release `SCUS-94221` rules and data and is the first complete playable Windows application. The game fidelity target is also the exact edition verified from the supplied BIN. Zodiac must not depend on FFT-specific types or data, while FFT must run through Zodiac's reusable simulation contracts. Reach these goals subsystem by subsystem; do not generalize unproven abstractions in advance.
2. **Use the BIN only as a local import source.** Extract its file system, inventory and verify files, convert/decompress supported assets to disk, and have the application load those files from disk. The game runtime must neither read the original BIN nor invoke/link the C reference.
3. **Preserve original behavior as the default experience.** Rules, content, campaign flow, bugs, timing, and presentation should match observable PS1 behavior where practical. Differences and confidence should be documented; compatibility fixes should be explicit.
4. **Allow expansion without changing the original campaign.** Definitions and mutable state are separate; additional jobs, abilities, items, characters, monsters, maps, encounters, campaigns, and mechanics can be added as separate content/rule sets. This extension boundary is also a prerequisite for the user's later goal of integrating The Lion War content.
5. **Make data authorable and inspectable.** Provide typed C# definitions and validation for content such as jobs, abilities, items, flags, formulas, text, sprites, animations, maps, scripts, and encounters, with a staged path to visual editing tools.
6. **Keep the project personal and local.** There is no distribution goal. Game media, extracted data, and generated assets remain in ignored local directories.

### Required for the first complete game

- All game logic runs in managed C#/.NET on Windows; no original C at runtime.
- A one-time asset preparation workflow verifies the supported BIN, extracts the disc files, and writes needed decompressed/converted data to disk. The application reads the prepared files by stable logical IDs or manifest paths and does not require the BIN to be present after preparation.
- A playable start-to-finish original campaign, including its battle, world/campaign, event/script, menu, save/load, and ending paths. The original jobs, abilities, items, characters, monsters, maps, encounters, text, and presentation assets required by that release are in scope.
- Original mode is the default and has regression coverage for formulas, turn/action flow, movement, status, rewards, scripts, save data, and important presentation behavior.
- C# content definitions are separate from battle/campaign/save state. Original content can be loaded from validated data files, with no arbitrary original table capacity baked into the new engine.
- Core content categories have typed C# definition models and validation diagnostics; a visual editor can be staged after the schema/tool APIs stabilize. C# authoring must not imply arbitrary user-code execution in the runtime.
- Automated tests and reproducible build/run instructions from the first implementation milestone. Significant architecture decisions and deviations from baseline are recorded.
- Original BIN, extracted files, and converted assets are ignored by Git and stay local.

### Required for extensibility, staged after the original slice is stable

- Add-on content for jobs, abilities, items, characters, monsters, maps, encounters, and campaigns loads without changing simulation code when it uses existing mechanics.
- New mechanics can be added through a documented, versioned extension boundary. Begin with data-driven rules and a constrained effect/action model; consider third-party executable scripts only after determinism, compatibility, and security requirements are clear.
- Custom content and rule changes must not silently alter the default original-mode behavior.
- The owner intends to fix selected original-game bugs later. Keep those corrections
  in an explicitly selected FFT rules/profile option, with documented original and
  corrected behavior and separate regressions. Choose the bugs and timing after
  their affected systems have a stable compatibility baseline; see ZOD-070.

### Explicitly outside the initial target

- Other regional releases, multiplayer, non-Windows platforms, mod browser/marketplace, arbitrary third-party code plugins, and a complete multi-domain visual authoring suite. The Lion War/War of the Lions material is excluded from the initial PS1 compatibility baseline, but is a stated later project goal after the foundations and baseline are stable.
- Exact PS1 machine-code, hardware, and cycle-level reproduction. Match player-visible behavior and timing where it matters; replace the original platform implementation.

### Fidelity acceptance is layered

- **Rules:** exact expected results for selected formulas, turn order, movement, status, RNG sequences, and edge cases, supported by source-traced vectors and scenario replays.
- **Campaign:** all baseline campaign paths are completable, with the expected battles, story events, party transitions, and endings.
- **Content:** every original record needed for the baseline game is represented and validated against the supplied release; inventory mismatches are tracked explicitly.
- **Assets:** prepared files render/play correctly and can be loaded after the BIN is unavailable to the running app.
- **Presentation:** compare screen states, text, animations, and audio at defined checkpoints; exact hardware timing is not a requirement unless it changes observable gameplay or presentation.

### Definition of a complete original-game target

Implementation inventory (2026-10-23): [ZOD-045 coverage](docs/coverage/README.md)
maps the major baseline families, current supported subsets, evidence, tests,
manual checkpoints and unresolved gaps. It drives bounded child tickets; exact
record/event/resource subcatalogs remain open and no completion percentage is inferred.

For the baseline, “complete” means more than reaching the ending once. The target is the playable North American PS1 release, including its opening/title flow, main story, optional/special battles and side content, party/job/equipment progression, world and menu systems, event/dialog scripts, endings, save/load, and the original presentation resources those paths use. Multiplayer, other regional editions, and War of the Lions additions are excluded from this baseline. The Lion War integration is a separate later compatibility/content target, not a reason to blur baseline acceptance. Inventory each subsystem and content family; completion is accepted only when every baseline entry is accounted for as implemented, intentionally unused, or explicitly unresolved.

The original compatibility mode is the reference behavior. Any quality-of-life changes or new mechanics belong to a separate expanded mode or content/rules package. This keeps the vanilla acceptance target measurable while still supporting the engine-extension goal.

### Asset preparation and runtime contract

The supplied BIN is a local source archive, not a game runtime dependency. The import/preparation workflow should:

1. Verify that the source is the supported `SCUS-94221` image by size/hash.
2. Extract and inventory the ISO files. This step is already proven: 2,464 inventory records, plus the extractor's 28,032-byte `license_data.dat` sidecar. The current output is local under `scratch/`.
3. Preserve extracted originals as immutable source inputs and identify their roles through directory, metadata, and reference-code usage, not extension alone. The extracted inventory has 1,575 `MAP` files, 512 `EFFECT` files, 184 `BATTLE` files, 103 `SOUND` files, and smaller event/menu/world/open/runtime sets.
4. Convert or decompress formats that the managed app needs into documented, versioned disk assets. Keep an import manifest with source path/hash, logical ID, output path/hash, format/version, and conversion status. A failed or unsupported conversion must be reported rather than silently omitted.
5. Load prepared assets by logical ID/path. Runtime must work with the BIN missing and must not perform raw sector reads or call the C reference.

The inventory gives an initial format profile: 138 `.SPR`, 121 `.GNS`, 14 `.SEQ`, 11 `.SHP`, 100 `.SMD`, seven `.LZW`, ten `.TIM`, one `.TM2`, and six large `.STR` streams (about 250 MB total). There are also 546 `.BIN` files, so `.BIN` is not synonymous with the original disc image or one uniform resource type. Some data is already uncompressed. The reference currently proves ISO extraction and documents individual codecs; it does not provide one universal game-asset decompressor.

**Recommended implementation order:** start with one typical unit sprite sheet (`BATTLE/20M.SPR`) and its palette/pixel blocks; then add the minimum map files needed for a battle-map slice; then text/script and sound. Treat `.STR` movies and streamed XA audio as a separate later decoder/presentation task because they account for most inventory bytes and use different playback paths.

**Storage decision:** keep the extracted originals and generated prepared assets side by side in ignored local storage. The prepared version should use simple documented managed formats where practical, but preserve the source bytes and hashes so every conversion can be repeated and audited. The client reads the prepared asset tree; it does not need a copy of the BIN after import.

### Decisions to revisit as proofs produce evidence

- Whether every optional/special encounter must be completable in the first “complete game” acceptance, or whether those are tracked as a later completeness gate. Current recommendation: include them in the final baseline because the goal is a full original-game reimplementation.
- Whether normalized assets should be packed into a local cache bundle or remain individual files. Start with ordinary files plus a manifest; bundle only if load-time or organization evidence justifies it.
- Which compatibility defects should receive an opt-in fix. Default remains original behavior; do not let this block exact vanilla behavior work.
- The exact target UI/render/audio technology and .NET SDK version. Defer until the headless simulation and first asset decode demonstrate the needed interfaces.

## Proposed architecture

The detailed, current proposal is in [`docs/architecture/overview.md`](docs/architecture/overview.md), with initial decisions in [`docs/adr/`](docs/adr/). It describes intended boundaries and explicitly distinguishes them from projects/services already implemented.

The current project structure is intentionally small: `Fft.Core` holds the initial domain/simulation types, `Fft.Content` loads prepared content, `Fft.AssetTool` and its CLI perform offline asset work, and `Fft.Client` is the composition root proof. Do not split `Fft.Core` or add application/platform projects until a concrete boundary needs an independent assembly.

The proposed simulation contract is an explicit deterministic transition from state plus command and RNG state to a resulting state and events. Immutable definitions remain separate from mutable battle/campaign state. Original-compatible behavior stays identifiable; additions and The Lion War content use explicit profiles/packages. Asset import remains offline and the client reads prepared files by logical asset ID. These directions do not select a complete campaign save format, plugin ABI or scripting language. The bounded [ZOD-035 save proof](docs/development/two-unit-battle-saves.md) now uses versioned state DTOs and selected content definitions; ADR 0005 records the implemented Windows host choice.

### Platform-specific replacement areas

Replace PS1 executable/overlay loading, memory-card/file APIs, PsyQ graphics and display lists, controller polling, CD/file streaming, sound driver, fixed-point/platform math assumptions, interrupt/timing services, and memory/address overlays with managed services and Windows backend adapters. Preserve arithmetic widths, rounding, signedness, integer overflow behavior, RNG sequence, and update ordering in the simulation where those affect observable outcomes.

## Behavioral validation strategy

Validation has multiple layers; the decompilation's byte-match check covers only the original C build.

1. **Source traceability:** each implemented rule records source function(s), relevant data/layout, edition/module, and any uncertainty in a rule note or test metadata.
2. **Characterization tests:** translate small, isolated formulas and state transitions from C into hand-reviewed expected vectors (boundary values, signed values, rounding, overflow, invalid targets, status interactions). Do not blindly translate C tests if they merely mirror the same mistake.
3. **Differential harness:** where a small reference callable can be built, run selected cases through reference and C# in a test/development harness, normalize outputs, and compare. Keep that reference adapter out of shipped application/runtime. If the C project only exposes executable modules, build a temporary test shim or use data extracted from controlled local runs.
4. **Retail observation fixtures:** for behaviors not exposed cleanly by a function, record edition, setup/save state, action sequence, random seed/controlled outcomes if available, and observed result from a legally owned copy/emulator. Mark observations and confidence separately from source-derived facts.
5. **Replay tests:** record deterministic commands and RNG state; assert exact state/event sequence after each command. These become the primary regression suite as coverage grows.
6. **Golden scenario tests:** small encounters exercise move/act/wait, elevation, turn order, damage/status, reaction, victory, and rewards. Keep fixtures minimal and explain expected values.
7. **Reference integrity:** preserve upstream `make check` as a separate reference-maintenance workflow if the upstream checkout is authorized and available; it shows C-to-original byte identity, not C# equivalence.
8. **Manual presentation checks:** once a client exists, validate layout/animation/audio separately from simulation outcomes.

Never use the C decompilation as a runtime library. A developer-only bridge may be considered only after licensing/source policy is resolved; otherwise use manually checked vectors and legal retail observations.

## Risk register

| Risk / unknown | Impact | Mitigation / evidence needed |
|---|---|---|
| Reference repo is separate and upstream `main` changes rapidly (user reports local checkout was 209 commits behind) | Source assumptions and line references drift between investigations | Record the exact source commit with every analysis/vector; use immutable commit links or saved local excerpts; update the reference only at deliberate checkpoints and re-review affected findings. Do not silently assume local source matches `main`. |
| Copyright/license and derived-data rights unclear | May block redistribution or code/data reuse | Keep original assets out of repo; obtain legal review; define clean-room boundaries and user-supplied asset model. |
| “100% decompiled” does not mean high-level semantics are fully understood | Incorrect rule inference | Trace callers, types, data formats, and quirks; record confidence; independently validate edge cases. |
| No direct C# behavior oracle | Regressions can reproduce misunderstood logic | Build independent vectors and controlled scenario captures; create a narrow non-shipping differential harness if permitted. |
| Determinism affected by PRNG, tick order, overflow, rounding, or timing | Divergence from original despite similar formulas | Specify arithmetic and update order; seed/capture RNG; add boundary and replay tests early. |
| Region/version differences | Wrong data and behavior baseline | Define baseline release; treat other editions as explicit compatibility profiles. |
| Asset formats, codecs, script formats and extraction workflow | Content scope may dominate engineering | Use reference extraction then inventory formats; add tested managed decoders incrementally and keep outputs local/ignored. |
| Event scripts and overlay behavior are tightly coupled to UI/runtime | Full story scope is much larger than battle rules | Build battle kernel first; define event interpreter later against captured script semantics. |
| Extension APIs ossify too early | Mods become fragile or unsafe | Start with versioned declarative packages; do not promise binary plugin compatibility in v1. |
| Testing only source-derived vectors creates correlated errors | False confidence | Mix source inspection, retail observations, metamorphic/property checks, and regression fixtures. |
| Windows rendering/audio framework choice | Can constrain development and deployment | Decide after the headless proof; test backend options separately from simulation architecture. |

## Phased roadmap and acceptance criteria

The actionable work breakdown is maintained in [`docs/BACKLOG.md`](docs/BACKLOG.md), with stable ticket IDs, priorities, prerequisites, and acceptance criteria. It refines the phases below into incremental work and records the current recommended execution order.

These are milestone gates, not calendar estimates.

### Phase 0 — Discovery and project setup

- Pin intended decompilation URL/revision, target game edition, .NET SDK target, and initial distribution assumptions.
- Review source and asset licensing boundaries; record allowed reference/use practices.
- Establish repository documentation and build/test workflow.
- **Accept when:** a contributor can build an empty solution and run tests on Windows from documented commands; the reference revision and unresolved rights questions are recorded.

### Phase 1 — Deterministic simulation proof of concept (recommended first implementation)

- Implement C# value types and minimal definitions for a tiny grid map, two units, movement range, turn readiness/selection, one physical action, and wait/end turn.
- Keep it headless, deterministic, and independent of asset loading or UI.
- Add source-traced vectors for the selected calculations and command replay regression tests.
- **Accept when:** identical input command/RNG streams yield identical state/events; invalid moves/actions are rejected consistently; selected outputs match reviewed source-derived vectors or retail observations; tests run in Windows CI/local workflow.

### Phase 2 — Battle rules vertical slice

- Add height/elevation, CT/turn ordering, action targeting, hit/damage, HP/KO, one status ailment, and victory detection.
- Create scenario fixtures with reproducible traces and a differential/observation harness where feasible.
- **Accept when:** documented scenarios match their reference vectors at each transition, replay is deterministic, and no presentation dependency is required to execute them.

### Phase 3 — Content pipeline and save boundary

- Define schemas/validators for jobs, abilities, items, maps, encounters, and asset references; separate immutable definitions from mutable save/battle state.
- Add stable IDs, package versioning, save versioning, validation errors, and migration tests.
- **Accept when:** a small external content package loads without code changes, invalid references fail clearly, and a save survives serialize/load/migration tests.

### Phase 4 — Windows client and disk-based asset pipeline

- Choose graphics/audio/input technology; implement a minimal battle view with placeholder assets and client adapters.
- The managed inventory parser and CLI now enumerate the target file tree and compare it with the reference inventory. Resolve or explicitly classify the seven `mixed` host-size differences described in [`docs/asset-inventory-poc.md`](docs/asset-inventory-poc.md) before treating reference extracted sizes as ISO logical lengths.
- Then build a C# asset conversion tool that writes selected source files in documented decoded formats. Start with one battle map and the sprite/palette data needed for the vertical slice. The runtime loads these by logical asset ID from disk without reading the BIN or calling C.
- Compare converted output with source data and renders; preserve source files so conversion can be repeated reproducibly.
- **Accept when:** a Phase 2 scenario can be played/rendered through Windows adapters using disk-loaded converted assets, and simulation tests remain headless and green.

### Phase 5 — Complete original game coverage (required end state)

- Add campaign state, events/dialog, shops/formation, AI breadth, full battle content, menus, audio, and localization incrementally.
- Preserve regressions by subsystem and edition.
- **Accept when:** the original campaign can be played from new game through its ending; required side modes and save/load paths work; all baseline jobs, abilities, items, units, maps, encounters, events, text, and assets are accounted for; critical rules and paths have automated regression coverage and presentation checkpoints have documented manual checks.

### Phase 6 — Expanded content and mechanic extensions

- Add custom jobs, abilities, items, characters, monsters, maps, encounters, and campaigns through versioned data packages; then add declarative mechanics under a stable rule extension boundary.
- Add a constrained scripting model only if campaign/content needs cannot be met by validated data and existing mechanics.
- **Accept when:** a custom campaign using new content loads without modifying the original campaign, invalid packages fail safely, and extension documentation/versioning is covered by tests.

### Phase 7 — The Lion War content integration (stated long-term goal)

- Select and pin the intended Lion War variant/revision and document its relationship to the PS1 baseline and War of the Lions content. Produce a feature-by-feature scope inventory; distinguish imported WotL content from Lion War-specific adaptations or additions.
- Establish provenance and a lawful local workflow for any required reference data. Do not assume that the PS1 BIN or decompilation contains the later content.
- Express the integration through the established content, campaign, script, and mechanic extension boundaries where practical. Add explicit compatibility profiles for any intentional behavioral differences; retain the original PS1 mode as a regression target.
- **Accept when:** the agreed Lion War feature inventory is represented, enabled as a distinct profile/content set, and its selected scenarios pass automated state/content checks and documented gameplay comparison. Exact feature scope remains to be defined before implementation.

## Recommended next milestone in concrete scope

The first headless transition boundary is implemented and documented in [`docs/battle-transition-poc.md`](docs/battle-transition-poc.md): it validates state/command/event flow and deterministic replay using an explicitly simplified flat-grid movement policy. One source-backed invariant—rejecting out-of-map movement candidates—is recorded in [`docs/behavior/movement-boundary.md`](docs/behavior/movement-boundary.md). Managed terrain and effective unit movement inputs are modeled in `Fft.Core` and traced in [`docs/behavior/movement-inputs.md`](docs/behavior/movement-inputs.md). A separate `TerrainAwareMovementRange` policy applies entry costs, blocked surfaces, and basic final-destination flags under flat same-layer assumptions; it is not yet integrated into `BattleSimulator` and does not reproduce FFT's complete movement behavior. The next movement step is to add a source-traced height/slope/jump eligibility fixture.

The supported BIN/CUE pair has been verified against the decomp profile; the ignored reference inventory contains 2,464 ISO entries. The managed AssetTool inventories all paths and LBAs, extracts all 2,457 ordinary `data` files, and verifies every extracted SHA-256 against the reference inventory. Seven `mixed` XA/STR files remain explicitly unsupported, with their sector representation still to be confirmed. The bounded unit `.SPR` attack-image decoder, fixed-prefix model, manifest writer, and independent logical-ID content reader are implemented; a selected Chocobo file passed the full extraction-to-load path. The console client now consumes the prepared content reader for a requested asset ID. The AssetTool CLI verifies the Chocobo decoded-output hash fixture and can create an all-palettes diagnostic PNG. Details and limitations are recorded in `docs/sprite-attack-image-decoder-poc.md`. The next asset step is to compare against output produced by an executing original routine or emulator and then source-trace frame/palette selection; the current fixture is based on a source-derived transcription, not such a run. The existing movement-range prototype remains a deliberately limited simulation proof; richer FFT rules should wait for source-traced behavior notes and tests.

The attack-image decoder, fixed unit SPR prefix model, versioned manifest writer, and logical-ID loose-file reader have been checked against synthetic data; they also completed extraction, manifest generation, hash-verified loading, parsing, and decoding for `BATTLE/CYOKO.SPR`. The console composition root now reads a selected prepared asset by ID, and the CLI checks the full decoded-byte hash against `docs/fixtures/chocobo-attack-image.json`. The expected digest was cross-checked against a temporary source-derived transcription of the C routine; it still needs confirmation from an executing original routine or emulator. Keep mixed XA/STR extraction, complete story scripts, broad asset conversion, original audio/video, and a UI framework deferred until narrow proofs pass. The first extraction and inventory proofs establish ordinary loose files; deterministic battle behavior still needs separate source-traced proof work.

## Legal asset-loading engineering requirements

- The local decompilation says it does not contain game files. The user confirms personal/no-distribution intent and can provide the original BIN locally. Reference tooling supports extracting the supported disc filesystem after checking exact size/hash.
- The desired path is feasible in two steps: (1) extract ISO files from the BIN into a local file tree; (2) decode selected game formats into local files and configure C# to load them by path/logical ID. Extraction does not decompress every resource: some are raw, and different formats need separate parsers/decoders.
- Do not check in disc images, extracted textures, sprites, music, sound, movie, dialogue dumps, or bundled copyrighted game data.
- Keep BIN, extracted and decoded files, and generated inventories in ignored local directories. Validate the supported edition by size/hash and check extraction counts/hashes. Never upload or redistribute input/output.
- Keep the simulation/content schema usable with original or user-authored replacement assets so missing rights do not prevent engine testing.
- Keep any importer local and avoid legal conclusions about copying/circumvention. No-distribution intent reduces distribution concerns but does not settle every local-use question or upstream source licensing.

## References

- External decompilation: [adamrt/fft_decomp README](https://github.com/adamrt/fft_decomp/blob/master/README.md)
- Build/check targets: [Makefile](https://github.com/adamrt/fft_decomp/blob/master/Makefile)
- Source organization and contributor constraints: [AGENTS.md](https://github.com/adamrt/fft_decomp/blob/master/AGENTS.md)
- Program layout/compiler archaeology: [ARCHAEOLOGY.md](https://github.com/adamrt/fft_decomp/blob/master/ARCHAEOLOGY.md)
- Compatibility quirks: [QUIRKS.md](https://github.com/adamrt/fft_decomp/blob/master/QUIRKS.md)

