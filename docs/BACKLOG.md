# Zodiac / FFT development backlog

**Created:** 2026-10-22

**Scope:** Reusable Zodiac engine, PS1-compatible FFT game, asset preparation, content authoring, validation, and development infrastructure.

**Tracker:** Repository-local planning document. These IDs are not tickets created in Jira or another service.

This is the ordered work breakdown for the goals in [PROJECT_PLAN.md](../PROJECT_PLAN.md). The [architecture overview](architecture/overview.md) owns architectural intent; [ADR 0004](adr/0004-zodiac-engine-and-fft-game-boundary.md) records the engine/game boundary. The [handoff](PROJECT_HANDOFF.md) preserves earlier implementation and research context. This backlog owns future work order and ticket dependencies; its order supersedes the handoff's older next-task recommendations.

## Starting evidence and limits

Repository inspection confirms the following starting point:

- `Fft.Core` has immutable map/battle models, a typed command transition proof, and two bounded movement policies. [BattleSimulator](../src/Fft.Core/BattleSimulator.cs) still calls `FlatGridMovementRange`; [TerrainAwareMovementRange](../src/Fft.Core/TerrainAwareMovementRange.cs) is a separate proof and omits height, slopes, jumps, occupancy, water, movement modes, and layer transitions.
- `Fft.Content` loads hash-verified files through a [prepared asset manifest](../src/Fft.Content/PreparedAssetManifest.cs). AssetTool and its CLI support ordinary-file extraction and a bounded unit sprite decode. This is not yet a general game-definition package format or full asset decoder.
- There is no `Zodiac.Engine` project, typed job/ability/item catalog, rendering backend, playable battle loop, campaign runtime, save system, or editor.
- [CircleCI](../.circleci/config.yml) restores, builds, tests, and invokes Sonar analysis in a Linux .NET 10 container. Windows application validation and publishing are not configured. The owner previously reported a Sonar failure; the current remote analysis result was not checked for this backlog.
- The handoff records **134 passing tests** from the previous complete run. That is historical evidence, not a newly executed test result.

Source-related tasks must inspect and record the current local decomp HEAD. Existing notes often cite `611b504d25d4a9b03fab8023d7dbea75fe381d5d`; that is a historical reference, not an instruction to assume that every new finding uses that revision. The decomp checkout remains read-only. BIN files, extracted content, captures, and decoded game media stay under ignored `scratch/`.

All future contracts and project moves below are proposals until their ticket is completed. In particular, namespaces communicate ownership; project references must enforce FFT-to-Zodiac dependencies. No broad rename is scheduled as a prerequisite.

## How to use these tickets

- **P1:** Foundation needed to make the next implementation decisions and begin the engine split.
- **P2:** First deterministic battle, content authoring slice, and Windows presentation proof.
- **P3:** Required breadth and fidelity for the complete original PS1 game.
- **P4:** Expanded content/mechanics, later authoring breadth, and Lion War integration.
- **P5:** Optional convenience or delivery work without a current requirement.

Priority expresses sequence, not whether a feature is mandatory. P3 includes required original-game features. P4 includes the required extension goal, the stated Lion War goal, and some optional mechanisms that need their own decision.

**Ready** means the task has a bounded starting scope and no unfinished backlog prerequisite. **Waiting** means named prerequisite tickets are unfinished. **Needs breakdown** means an epic must be divided into source-traced implementation tickets before coding. A research spike completes with findings, evidence, unknowns, and a recommendation; it does not require creating production abstractions.

IDs stay stable when priorities change. No calendar estimates or story points are assigned: the source investigations and first implementations should establish a basis for estimating individual tasks. Dependencies below are planning assumptions to refine during the relevant spike. Within a priority, use the recommended queue and dependency order; a high-numbered Ready investigation can proceed alongside an earlier one when it does not edit the same files.

For every implementation ticket: preserve the existing test suite, add meaningful regression coverage for new behavior, update its behavior/architecture note, and run the relevant build/tests. Record evidence as source-derived, executable-reference, emulator/original observation, or synthetic expectation. Source-derived expectations alone must not be called proven original-game equivalence. Maintain the managed runtime boundary, prepared-files-only runtime, immutable definitions/state separation, and original-mode isolation throughout.

## Recommended next queue

1. **ZOD-001:** Audit current type ownership and select the first real engine slice.
2. **ZOD-002:** Establish the evidence format before adding more behavior fixtures.
3. **ZOD-003:** Trace one job and the ability references needed for the first content slice.
4. **ZOD-004:** Resolve one adjacent-tile height/slope movement rule.
5. **ZOD-007:** Implement the engine slice selected by ZOD-001.
6. **ZOD-008:** Route the existing FFT transition through that boundary.
7. **ZOD-009:** Prove engine independence with a neutral sample and dependency check.
8. **ZOD-011:** Add the fixture runner using the evidence format.
9. **ZOD-012:** Integrate the bounded terrain-cost movement policy explicitly.

ZOD-005 and ZOD-006 can proceed independently. ZOD-014 through ZOD-016 form a useful parallel content/authoring lane after ZOD-003. ZOD-024, ZOD-026, ZOD-029, and ZOD-045 are independent P2 research/inventory tasks. Delegate tickets only when the owner chooses to do so; give each agent one bounded ticket, its allowed files, and acceptance criteria.

## Milestone gates

- **M0 — Demonstrated engine boundary:** ZOD-001, ZOD-007, ZOD-008, and ZOD-009 complete. A neutral scenario runs without FFT references, and the existing FFT proof uses the extracted engine slice.
- **M1 — Deterministic headless battle:** ZOD-002, ZOD-004, ZOD-005, ZOD-010 through ZOD-023 complete. Two units can advance turns, move under the documented movement subset, resolve one source-traced attack, and reach a result through replayable commands. This is a bounded battle, not full FFT combat.
- **M2 — Playable Windows battle slice:** M1 plus ZOD-024 through ZOD-034. Prepared map/sprite files render, input issues commands, comparison captures are recorded, and the client runs with the BIN unavailable.
- **M3 — First authoring tool:** ZOD-014 through ZOD-018 and ZOD-047 complete. A job can be inspected, edited, validated, exported, loaded, and used by the bounded battle without changing simulation code.
- **M4 — Original-game completeness:** ZOD-035 through ZOD-046 and ZOD-053, decomposed as needed, satisfy the baseline coverage matrix. All original content, optional encounters, campaign paths, and presentation requirements are accounted for. M0–M3 do not imply this gate has passed.
- **M5 — Extensions and Lion War:** ZOD-049 establishes content isolation; ZOD-051 defines the exact Lion War scope before ZOD-052 integration. ZOD-050 is conditional on a demonstrated scripting need. ZOD-048 is staged by authoring need; ZOD-054 is optional.

Milestones can have concurrent research and implementation lanes. These are acceptance gates, not a requirement to finish all simulation work before inspecting a renderer or content workflow.

## Engine boundary, evidence, and development foundations

### ZOD-001 — Audit current types and select the first engine slice

**Type:** Spike · **Priority:** P1 · **Status:** Ready · **Depends on:** None

**Scope:** Classify every current `Fft.Core` type as FFT-owned, reusable candidate, or unresolved. Inspect its actual consumers in Content, tools, client, and tests. Evaluate one small extraction that performs useful work; use the existing map/movement transition as the concrete example.

**Acceptance criteria:** A committed ownership map cites the relevant files and dependencies; the proposed engine contract can be explained without FFT; FFT terrain semantics and compatibility policy remain game-owned; the smallest extraction and FFT adapter are specified with preserved behavior and unresolved issues. Do not declare a type generic solely because its name is generic. If no viable extraction is supported yet, identify the exact missing evidence instead of inventing an API.

### ZOD-002 — Define behavioral fixture provenance and comparison rules

**Type:** Design task · **Priority:** P1 · **Status:** Ready · **Depends on:** None

**Scope:** Define a small versioned scenario/expected-result format using the movement proof as an example. Record target edition, decomp SHA and function, evidence kind, preconditions, inputs, expected state/events, exclusions, and confidence.

**Acceptance criteria:** One synthetic movement fixture and a source-traced fixture description demonstrate the format; comparison rules specify stable IDs, ordered events, integer values, and explicit state fields; unresolved expectations cannot appear as verified results. Define where local captures belong and how committed metadata references them without including game media. Do not use .NET object hash codes as persistent replay fingerprints.

### ZOD-003 — Trace a minimal job and ability definition slice

**Type:** Spike · **Priority:** P1 · **Status:** Ready · **Depends on:** None

**Scope:** Select one baseline job and only the ability/skill-set fields needed to represent it. Trace original records, field encodings, references, consumers, and meaningful flags. Compare selected FFTPatcher field names/workflow with the PS1 source, verifying the exact tool revision and edition support if consulted.

**Acceptance criteria:** A field-by-field map identifies source evidence, original indices, proposed stable IDs, signedness/ranges, flag meanings, and unknown values; `JobDefinition` is separated from a unit's job level, JP, HP, and equipment state; the minimum next schema and synthetic sample are specified. Record the community-tool reference/reuse decision and preserve unknown source bits at the import boundary. This ticket produces a design, not a full job catalog.

### ZOD-004 — Trace one adjacent-step height/slope eligibility rule

**Type:** Spike · **Priority:** P1 · **Status:** Ready · **Depends on:** None

**Scope:** Extend [movement-inputs.md](behavior/movement-inputs.md) for one ordinary same-layer cardinal step. Trace source callers, height units, slope orientation, effective standing height, comparison widths, and the exact unit/profile assumptions.

**Acceptance criteria:** Select a bounded rule with expected accept/reject vectors at the threshold, above it, and in both directions; identify whether slope affects this particular rule and how; explicitly exclude horizontal jump spans, water, mounts, occupied tiles, and special movement modes unless the selected rule requires one. Record source commit and remaining ambiguity before implementing ZOD-010.

### ZOD-005 — Trace FFT RNG state, arithmetic, and call ordering

**Type:** Spike · **Priority:** P1 · **Status:** Ready · **Depends on:** None

**Scope:** Find the RNG routines used by the proposed physical attack and record state width, update arithmetic, range conversion, seeding, and draw ordering, including failed/rejected actions where relevant.

**Acceptance criteria:** Provide source-derived vectors including overflow/boundary values; identify which parts can be a neutral engine state contract and which are PS1 compatibility behavior; document a viable independent verification method or its blocker. Do not substitute `System.Random` without evidence of equivalence.

### ZOD-006 — Make SDK selection and dependency restoration reproducible

**Type:** Chore · **Priority:** P1 · **Status:** Ready · **Depends on:** None

**Scope:** Inspect the installed SDK, project package versions, and CircleCI image. Choose an explicit SDK selection/roll-forward policy and a package restoration strategy appropriate to this solution.

**Acceptance criteria:** A clean Windows checkout and Linux CI can restore/build/test using documented versions; deliberate upgrades have one documented process; package drift is either prevented or explicitly detectable. No publishing or version-number automation is required by this ticket.

### ZOD-007 — Extract the first useful Zodiac engine slice

**Type:** Story · **Priority:** P1 · **Status:** Waiting · **Depends on:** ZOD-001, ZOD-002

**Scope:** Implement exactly the extraction justified by ZOD-001 in `Zodiac.Engine`, with an engine test project. Prefer the smallest coherent functionality over a general framework of empty interfaces.

**Acceptance criteria:** The engine slice runs on synthetic data, has meaningful independent tests, and references no FFT assemblies, definitions, assets, or formats; deterministic behavior and any ordering contract are explicit; no platform/file/input dependencies enter simulation. Update solution/CI references only as required. Keep behavior changes out of the move and retain FFT-specific types in their current home until their own migration is justified.

### ZOD-008 — Adapt the existing FFT transition to the engine slice

**Type:** Story · **Priority:** P1 · **Status:** Waiting · **Depends on:** ZOD-007

**Scope:** Connect the current FFT battle proof to the new engine functionality while preserving the documented prototype movement behavior. This is the first actual game-to-engine integration.

**Acceptance criteria:** Existing accepted/rejected commands, input immutability, resulting state, and event ordering remain covered; FFT selects/provides its policy without FFT branches in the engine; the reference graph is acyclic and one-way. Record which current files remain a prototype and any project names migrated as part of this bounded move.

### ZOD-009 — Prove engine independence with a neutral sample and dependency check

**Type:** Story · **Priority:** P1 · **Status:** Waiting · **Depends on:** ZOD-008

**Scope:** Add one tiny synthetic tactical scenario using the extracted engine functionality and no FFT types. Add a focused architectural check for forbidden engine-to-FFT project references.

**Acceptance criteria:** The sample executes meaningful simulation behavior with invented content; changing an FFT profile does not change its result; CI detects an introduced engine-to-game dependency. Keep the sample small and avoid creating a second full game or plugin system.

## First deterministic battle and typed content

### ZOD-010 — Implement the selected adjacent-step geometry rule

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-004, ZOD-012

**Scope:** Implement only the eligibility rule selected by ZOD-004 through the established movement boundary.

**Acceptance criteria:** Source-derived threshold/direction/slope vectors pass; tests combine geometry with entry cost and blockers; rejected moves preserve state; the policy's unsupported mechanics remain explicit. A geometry change does not silently replace the original compatibility target with a generic rule.

### ZOD-011 — Add a deterministic scenario fixture runner

**Type:** Story · **Priority:** P1 · **Status:** Waiting · **Depends on:** ZOD-002, ZOD-008

**Scope:** Execute the versioned fixture inputs through the current FFT transition, compare explicit state fields and ordered events, and report the first meaningful mismatch.

**Acceptance criteria:** Accepted and rejected movement scenarios replay deterministically; deliberate state/event mismatches produce useful diagnostics; fixture metadata retains source/evidence provenance; tests require no BIN, emulator, or network. Extend the runner incrementally as new commands and state fields are introduced.

### ZOD-012 — Integrate the bounded terrain-cost policy explicitly

**Type:** Story · **Priority:** P1 · **Status:** Waiting · **Depends on:** ZOD-008, ZOD-011

**Scope:** Allow the battle transition to use the effective movement profile and existing terrain-cost policy through the established engine/game boundary. Preserve an explicitly selected flat prototype mode where needed for existing tests.

**Acceptance criteria:** Commands respect terrain entry costs, blocked surfaces, destination flags, occupied destinations, and available movement under the documented subset; missing costs fail deliberately; accepted/rejected transitions have fixtures. Specify traversal through occupied tiles rather than accidentally inheriting it from range generation. Preserve separate compatibility/prototype labels; do not claim full FFT movement.

### ZOD-013 — Implement explicit deterministic RNG state

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-005, ZOD-007, ZOD-011

**Scope:** Introduce the neutral state/transition seam and the source-traced FFT RNG implementation justified by ZOD-005.

**Acceptance criteria:** Recorded vectors pass with explicit integer/overflow semantics; a transition returns updated RNG state; replay compares it; draw counts/order are testable and no wall-clock or global RNG enters simulation. Rejected commands consume draws only if source evidence requires it for the selected behavior.

### ZOD-014 — Implement minimal typed job and ability definitions

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-003

**Scope:** Create FFT-owned immutable definitions and typed IDs for the selected job/ability slice, including references and named flags justified by source. Use the agreed `Zodiac.Games.Fft` ownership convention when a new project boundary is warranted.

**Acceptance criteria:** Synthetic definitions preserve field values and structural invariants; invalid/duplicate IDs and unresolved references have clear diagnostics; unit progression/runtime state remains separate; collections accept a synthetic catalog beyond original table capacities. Original numeric indices are import metadata, not the public storage or identity limit. Full catalogs and every FFTPatcher field are outside this slice.

### ZOD-015 — Decide and prototype the editable content package format

**Type:** Spike · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-014

**Scope:** Compare simple file-based representations for the minimal definitions. Recommend a format, package layout, schema version policy, stable ID/reference convention, and separation from the existing asset manifest and future save data.

**Acceptance criteria:** A synthetic job/ability package round-trips through a small prototype; malformed data, unknown versions, unknown fields/flags, duplicate IDs, and unresolved references have documented handling; deterministic output and baseline/add-on isolation are specified. Prefer a simple choice supported by the evidence. No SQL service or archive format is required to prove this workflow.

### ZOD-016 — Build the shared content validator and C# authoring export

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-015

**Scope:** Let a trusted local authoring program construct the selected C# definitions and export the same versioned data that a hand-edited package uses. Expose validation for both CLI and later editor consumers.

**Acceptance criteria:** C#-authored and file-authored equivalent data produce equivalent definitions; diagnostics identify package, record, and field; a validation command returns a nonzero exit on failure; repeated exports are deterministic; invalid input does not overwrite a valid package. Runtime consumes exported data without loading authoring code. Commit only synthetic examples.

### ZOD-017 — Import and validate the selected original job/ability records

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-003, ZOD-016

**Scope:** Read only the selected source records from local prepared/extracted files and convert them to the new definitions. Identify any additional container or executable-table parsing needed before implementation.

**Acceptance criteria:** Imported fields and original-index mappings match the traced source records; unknown bits/data are reported and preserved in import metadata; output provenance includes edition, input hashes, and converter version; repeated import is deterministic. Generated records stay in `scratch/`; synthetic parser tests run without game files. Missing or malformed input produces diagnostics rather than guessed defaults.

### ZOD-018 — Resolve content definitions into bounded battle units

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-012, ZOD-017

**Scope:** Build a unit for the selected scenario from typed definitions and explicit runtime inputs. Source-trace any calculation needed to derive its effective movement/combat values; retain explicitly supplied values where the calculation is not yet implemented.

**Acceptance criteria:** Two units can share a definition while retaining independent state; stable IDs resolve before battle startup; absent content fails before simulation; a synthetic authored job changes a supported unit parameter without a simulator edit. Each derived field is documented as source-traced or an explicit prototype input.

### ZOD-019 — Trace the smallest CT and turn-completion scenario

**Type:** Spike · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-002

**Scope:** Study normal CT accumulation, ready-unit selection/ties, and turn completion for two ordinary units, with no speed-changing statuses or charged abilities.

**Acceptance criteria:** A source-backed state/command sequence states initial CT, speed, readiness thresholds, tie order, and CT consequences of move/action/wait; arithmetic and update order are recorded; special-case exclusions are explicit. Identify the engine scheduler seam and FFT-owned rules without designing every turn system.

### ZOD-020 — Implement the bounded turn cycle and wait command

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-008, ZOD-011, ZOD-019

**Scope:** Advance the two-unit scenario using the selected CT semantics and explicit turn completion.

**Acceptance criteria:** Ready-unit/tie vectors pass; inactive units cannot issue actions; waiting/ending a turn updates the documented fields and permits subsequent turns; event/state sequences replay identically. RNG is introduced only if the traced scenario needs it. Expanded status, charge, and unusual tie behavior remain separately tracked.

### ZOD-021 — Trace one ordinary physical attack end to end

**Type:** Spike · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-002, ZOD-003, ZOD-005

**Scope:** Select a precisely specified unit/weapon/target configuration and trace target legality, facing/range, hit checks, damage arithmetic, HP/KO changes, RNG draw order, and turn consequences. Identify every deliberately absent modifier.

**Acceptance criteria:** Expected hit/miss/damage/KO and invalid-target vectors cite source functions and commit; operand widths and rounding points are explicit; the definition fields needed by the scenario are identified. Break out prerequisite schema work if the minimal job/ability definitions are insufficient. Do not implement a general formula interpreter as part of the trace.

### ZOD-022 — Implement the selected attack and HP/KO transitions

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-013, ZOD-018, ZOD-020, ZOD-021

**Scope:** Implement only the source-traced action configuration and required definition fields.

**Acceptance criteria:** Hit, miss, boundary damage, KO, range/facing, and invalid-command cases have fixtures; HP, action availability, and RNG state advance in the documented order; rejected actions preserve state as specified; state/events support presentation without presentation code. Unsupported equipment/status/modifier combinations fail explicitly or are excluded by scenario validation.

### ZOD-023 — Complete a reproducible two-unit battle scenario

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-010, ZOD-022

**Scope:** Define a tiny battle objective and run a complete start-to-result sequence with the supported move/action/wait rules. Label an invented objective as a scenario rule unless original victory behavior has been traced.

**Acceptance criteria:** A documented command sequence reaches a terminal result; commands after completion follow an explicit rule; restart/replay produces identical ordered state/events/RNG; invalid commands do not corrupt the scenario. The scenario can be launched headlessly with synthetic content. Record a coverage statement so this milestone cannot be mistaken for complete FFT combat.

## Windows rendering, assets, and comparison workflow

### ZOD-024 — Select a Windows rendering/input stack using a bounded spike

**Type:** Spike · **Priority:** P2 · **Status:** Ready · **Depends on:** None

**Scope:** Compare a small set of managed-host options against windowing, texture upload, pixel sampling, indexed palettes/transparency, PS1 blend approximations, depth/order, camera transforms, input, diagnostics, and Windows deployment requirements. Clarify whether platform libraries use native components and how that fits the managed-game-logic requirement.

**Acceptance criteria:** An ADR records the recommended stack, tested SDK/runtime compatibility, license/deployment needs, limitations, and a minimal window/texture experiment if necessary to resolve a decision. Game simulation stays headless. Editor framework selection is a separate decision unless evidence supports sharing it.

### ZOD-025 — Create the Windows host with a synthetic battle view

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-024

**Scope:** Open a window, display a synthetic grid/unit, manage resize/exit, and separate presentation timing from simulation advancement using the selected stack.

**Acceptance criteria:** Documented Windows launch works; the host renders an immutable snapshot and has a predictable resource lifecycle; rendering frames does not mutate or advance simulation; no graphics/input packages enter the engine library. A synthetic scene requires no original assets.

### ZOD-026 — Establish an independent sprite/reference capture workflow

**Type:** Spike · **Priority:** P2 · **Status:** Ready · **Depends on:** None

**Scope:** Evaluate an executing original routine or controlled original/emulator observation for the current sprite proof. Define how to capture relevant decoded output or appearance with edition, palette, frame, camera, timing, and environment recorded. Align resulting metadata with ZOD-002 before integrating it into the fixture runner.

**Acceptance criteria:** Record one independent comparison if feasible, including input/output hashes or local capture references and its limits. If the oracle cannot be established, document the exact blocker and bounded fallback; do not relabel the existing source-transcribed hash as independent evidence. Media remains local. An emulator or reference harness is a development tool, never a game runtime dependency.

### ZOD-027 — Trace one sprite frame, palette, and animation selection path

**Type:** Spike · **Priority:** P2 · **Status:** Ready · **Depends on:** None

**Scope:** Starting from the existing `.SPR` proof and [sprite decoder note](sprite-attack-image-decoder-poc.md), select one ordinary unit pose and trace the minimum SHP/SEQ references, palette selection, piece transforms, transparency/blending, and timing needed to display it.

**Acceptance criteria:** A source map identifies exact files/records and one frame's composition; unknown ordering/timing behavior is listed; define the smallest parser/rendering input slice. Avoid assuming every `.SPR`, `.SHP`, or `.SEQ` uses the selected format or covers every unit/effect.

### ZOD-028 — Parse and prepare the selected sprite-frame slice

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-027

**Scope:** Implement the selected metadata parsers/converter and decoded render input for one pose or short sequence.

**Acceptance criteria:** Synthetic/truncated/out-of-range records have meaningful tests; frame pieces, palette references, transforms, and durations match the trace; local prepared output is repeatable and manifest-addressable; runtime reads prepared files without BIN/ISO access. Keep the broad animation catalog outside this ticket.

### ZOD-029 — Trace one battle map's geometry, terrain, and texture inputs

**Type:** Spike · **Priority:** P2 · **Status:** Ready · **Depends on:** None

**Scope:** Select one small battle map and trace its file selection, geometry/texture/palette data, tile heights/slopes/layers, coordinates, and minimum scene setup. Distinguish visual geometry from movement terrain.

**Acceptance criteria:** Record a file/structure/dependency map and the edition/decomp revision; identify one region suitable for a comparison fixture and the required converter scope; state unsupported weather, alternate layouts, animation, or effects. Do not assume map visuals alone determine traversability.

### ZOD-030 — Prepare and load the selected map slice

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-029

**Scope:** Parse/convert the traced map inputs into prepared geometry/textures and FFT-owned simulation terrain definitions.

**Acceptance criteria:** Synthetic format tests cover bounds and malformed offsets/counts; terrain coordinates/heights/layers match selected source records; conversion preserves provenance and is repeatable; prepared outputs load with the BIN unavailable. The source parser remains an offline tool, not part of simulation.

### ZOD-031 — Render one prepared map and unit pose

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-025, ZOD-028, ZOD-030

**Scope:** Compose the selected map and sprite using fixed camera/pose inputs; implement only the selected palette, sampling, blend, and ordering behavior.

**Acceptance criteria:** A fixed scene renders repeatably; terrain/unit placement agrees with simulation coordinates; a documented original/reference comparison records differences and confidence using ZOD-026 where available. Unsupported PS1 effects are explicitly listed. Runtime uses prepared files and no importer or reference C code.

### ZOD-032 — Make the bounded battle playable through input and UI

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-023, ZOD-031

**Scope:** Select a unit, show legal destinations/targets, issue supported move/attack/wait commands, and display active turn, HP, rejections, and terminal result.

**Acceptance criteria:** The M1 scenario can be completed in the Windows client; simulation validates every command; the UI consumes snapshots/events without writing battle state; the same commands produce the same headless result. Keep original menu fidelity and complete controller navigation in later work.

### ZOD-033 — Add deterministic visual comparison checkpoints

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-026, ZOD-031

**Scope:** Capture selected scenes at fixed state/presentation ticks and compare them with documented baseline/reference captures using appropriate regions and tolerances.

**Acceptance criteria:** Capture conditions are reproducible; reports distinguish geometry, palette, transparency/order, and timing discrepancies from simulation failures; thresholds are justified rather than masking visible defects. Synthetic renderer checks can run without game files; original media and comparisons remain local. Human inspection is recorded for effects automated metrics cannot establish.

### ZOD-034 — Add Windows CI and clean-machine launch validation

**Type:** Chore · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-025

**Scope:** Add a suitable Windows build/test path in CircleCI when the host introduces Windows requirements. Retain Linux coverage for platform-neutral projects and store useful test/build reports.

**Acceptance criteria:** Windows-targeted build/test failures are visible in CI; no original assets or interactive desktop are required for automated checks; manual clean-machine launch prerequisites are documented and checked. Headless code builds without the rendering backend. Build artifact creation is permitted; public publishing/version automation remains a separate decision.

## Complete PS1 baseline: decompose these as source evidence grows

### ZOD-035 — Add a versioned save/load proof for the bounded scenario

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-016, ZOD-023

**Scope:** Save and load explicit battle snapshot DTOs with schema/content-profile versions, stable definition IDs, unit state, active turn, and RNG state. Choose an explicit legal checkpoint if mid-action state is unsupported.

**Acceptance criteria:** Loading and continuing matches uninterrupted play; invalid/missing IDs, corrupt data, and unsupported versions fail clearly; save writes preserve the last valid file on failure; the format does not serialize the in-memory object graph. Test migration handling with synthetic versioned data. Original memory-card import is optional until separately scoped.

### ZOD-036 — Implement status, reaction, support, and charge interactions

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-023

**Scope:** Inventory baseline statuses, durations, turn triggers, reactions/counters, support modifiers, charged actions, and ordering interactions. Split by one traced mechanic or interaction set per implementation ticket.

**Acceptance criteria:** The baseline coverage matrix accounts for every required mechanic; representative combinations and known quirks have deterministic state/RNG/event regressions; timing and arithmetic are traced and compared against independent observations where available. Start with one status in a complete turn/action scenario before expanding the catalog.

### ZOD-037 — Complete baseline movement and path-selection behavior

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-010, ZOD-012

**Scope:** Inventory and implement remaining slope/elevation cases, horizontal jumps, water/ceilings, mounts/standing height, ally/enemy traversal, layer transitions, special movement modes/abilities, and path/tie ordering.

**Acceptance criteria:** Each supported mode has source-traced eligibility, reachable-set, destination, and selected-path fixtures; interactions are tested; independent comparisons cover selected difficult maps. No item is accepted merely because the current cost algorithm returns a plausible destination set.

### ZOD-038 — Complete combat formulas, targeting, rewards, and AI

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-022

**Scope:** Inventory action families, formula variants, range/area/line-of-sight rules, equipment/elements, hit/evade behavior, XP/JP/loot, victory/loss conditions, and required AI decisions. Split behavior families and decision rules into separate tickets, linked to ZOD-036/037 where needed.

**Acceptance criteria:** All baseline actions and required AI choices are mapped to implementation/fixtures or explicit unresolved entries; representative encounters validate state, RNG draw ordering, and rewards; mandatory enemy/ally turns can complete without player scripting. Record observed AI deviations separately from player-action correctness.

### ZOD-039 — Expand typed definitions and import to the baseline catalog

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-018

**Scope:** Add items/equipment, full jobs and abilities/skill sets, monsters/characters, statuses, formulas/parameters, encounters/ENTD, localized text, maps, sprites/animations, stores, and campaign references in source-driven increments.

**Acceptance criteria:** Every baseline record family is inventoried and accounted for; all required references/flags validate; importing supported records is repeatable; every meaningful source field is mapped, intentionally unused, or explicitly unresolved. Content-sized synthetic catalogs prove that original fixed table limits are not accidental runtime restrictions. Authoring and runtime share validation.

### ZOD-040 — Investigate event/script formats and community-tool interoperability

**Type:** Spike · **Priority:** P2 · **Status:** Ready · **Depends on:** None

**Scope:** Identify battle/story/world script families, exact relevant community editors, supported editions, opcode encodings, execution order, state dependencies, and the subset needed for one small event. Inspect tool licenses before proposing code reuse.

**Acceptance criteria:** Provide an opcode/control-flow map for one chosen event, a tool capability/interchange assessment with pinned references, and an interpreter/conversion recommendation. Separate bytecode decoding from semantic execution and specify unsupported opcodes. Produce child tickets for one bounded event interpreter, not a full scripting framework.

### ZOD-041 — Implement campaign, encounters, events, and optional content

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-035, ZOD-039, ZOD-040

**Scope:** World travel/time, encounters/battle setup, story and battle events, dialogue/triggers, party transitions, propositions and side content, and ending paths. Coordinate combat prerequisites with ZOD-036/038 as each encounter needs them.

**Acceptance criteria:** A coverage matrix accounts for every baseline campaign/optional path; the first child milestone completes one event-to-battle-to-world loop; complete acceptance requires new game through ending plus required side content, with scenario checks for flag/party/encounter transitions. Report unsupported scripts and avoid silently skipping them.

### ZOD-042 — Implement original game flow, menus, and progression UI

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-032, ZOD-039

**Scope:** Opening/title flow, formation, equipment, jobs/JP, shops, world navigation, battle menus, settings, help, and save/load screens. Identify required text/font/layout and input behavior for each screen.

**Acceptance criteria:** The baseline UI inventory is complete; each screen supports the required operations and navigation without direct state mutation; selected original screen/input comparisons and transition regressions pass. Start with the smallest party/job/equipment workflow needed by the first campaign loop.

### ZOD-043 — Establish audio decoding/playback and expand to baseline coverage

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-024

**Scope:** First investigate music/sample/sound-effect formats and playback semantics. Select a backend, prove one prepared sound/music path, then expand music, effects, loops, transitions, timing, volume, and any required streamed audio.

**Acceptance criteria:** A bounded sample plays from prepared disk data; source formats and timing assumptions are documented; audio events do not control simulation; required baseline audio is inventoried and comparison checkpoints cover selected loops/transitions. Parser tests use synthetic data and original recordings stay local.

### ZOD-044 — Finish asset preparation for remaining required formats

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-028, ZOD-030

**Scope:** Inventory unsupported map/effect/menu/world/font/movie/audio formats and the seven mixed XA/STR entries; resolve their sector/data semantics before building separate converters/streaming decoders. Prioritize by the next baseline scenario's dependencies.

**Acceptance criteria:** Every resource required by the baseline is accounted for with conversion/load status and provenance; mixed entries are no longer silently omitted; runtime can present required movie/streamed content from prepared files without the BIN. Synthetic bounds/malformed-input tests and local decode/comparison evidence cover each format family. An intentionally unused resource requires a documented reason.

### ZOD-045 — Build the baseline feature/content coverage matrix

**Type:** Inventory task · **Priority:** P2 · **Status:** Ready · **Depends on:** None

**Scope:** Convert the full-original-game target into an inventory by subsystem, content family, screen, campaign/optional encounter, asset family, and compatibility profile. Use existing evidence and explicitly label unknown entries rather than inventing counts.

**Acceptance criteria:** Entries identify source/evidence, implementation status, ticket owner/reference, automated scenario checks, manual checkpoints, and unresolved gaps; all major original-game areas have a place in the matrix; it can drive child tickets for the broad epics. Update it incrementally rather than waiting until final acceptance.

### ZOD-046 — Run original-game acceptance and regression review

**Type:** Acceptance epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-034, ZOD-036 through ZOD-045, ZOD-053

**Scope:** Execute the baseline matrix, campaign playthroughs, optional content, save/continue paths, presentation comparisons, asset completeness checks, and supported Windows launch validation.

**Acceptance criteria:** Every baseline entry is implemented and checked, intentionally unused with evidence, or explicitly unresolved; unresolved required behavior blocks a claim of complete baseline compatibility. New game through ending and mandatory optional-content routes are documented; deterministic scenarios and presentation checkpoints have reviewable results; runtime operates with the BIN and C checkout unavailable. Use independent original/emulator comparisons for critical behaviors and disclose remaining evidence limits.

## Authoring, extensions, and later goals

### ZOD-047 — Build the first job/ability inspection and editing UI

**Type:** Story · **Priority:** P2 · **Status:** Waiting · **Depends on:** ZOD-016, ZOD-018

**Scope:** Choose an editor host for a small search/select/edit/validate/export workflow over the established definitions. Start with the fields and linked ability references from the first content slice; choose the UI technology through a short decision if required.

**Acceptance criteria:** Inspect a local original definition, create an authored copy/add-on, change a supported field, receive field-specific validation, export, and use it in the bounded scenario; read-only baseline source and authored changes have clear provenance. Unsaved changes and failed export are handled predictably. The editor uses shared validators/exporters and never reimplements combat formulas or edits the BIN.

### ZOD-048 — Expand authoring to flags, formulas, text, sprites, animation, maps, and scripts

**Type:** Epic · **Priority:** P4 · **Status:** Needs breakdown · **Depends on:** ZOD-047

**Scope:** Add focused editing/preview workflows as the corresponding definitions and decoders mature. Named flags show verified meanings and unknown bits; formula parameters distinguish data from executable behavior; text has font/layout preview; sprite/animation/map/script editing uses established format and validation APIs.

**Acceptance criteria:** Each selected editor round-trips supported authored data, validates references, supports recoverable editing, and previews through the same runtime/decoder semantics where practical. Complete this incrementally by content category. A general visual editor for every domain is a future goal rather than a prerequisite for the original game.

### ZOD-049 — Prove add-on packages and compatibility-profile isolation

**Type:** Story · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-016, ZOD-023, ZOD-045

**Scope:** Establish package identity/dependencies, namespaced IDs, precedence/conflict rules, supported schema versions, and explicit profile selection. Use synthetic added jobs/items/abilities and one changed rule parameter after each relevant category exists.

**Acceptance criteria:** Original mode produces the same regression results with add-on files present but disabled; enabled add-ons add supported content without source edits; duplicate/conflicting IDs and missing dependencies fail clearly; saves/replays identify the profile/package versions they require. No arbitrary assembly loading is required. Expand the proof to new encounters/maps/campaigns as those systems become available.

### ZOD-050 — Decide whether constrained custom mechanics or scripting are needed

**Type:** Conditional spike · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-040, ZOD-049

**Scope:** Select a real mechanic/campaign need that validated definitions, supported typed effects, and the baseline script interpreter cannot express. Compare extending typed operations with a deterministic scripting/extension API.

**Acceptance criteria:** Either demonstrate the need and record execution limits, state access, determinism, API/version policy, diagnostics, trust, and replay implications, or close the ticket with evidence that current mechanisms suffice. General executable plugins require a separate design decision and are not implied by the C# authoring API.

### ZOD-051 — Select and inventory the exact Lion War target

**Type:** Spike · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-045

**Scope:** Choose the intended Lion War variant/revision with the owner and inventory its included WotL content, mod-specific adaptations, resources, scripts, and rule changes. Identify reference/provenance requirements and available local inputs.

**Acceptance criteria:** A pinned feature-by-feature scope distinguishes existing PS1 baseline behavior, added content, intentional mechanical differences, and unknowns; each item maps to engine/content/script/asset capability or a child ticket. Do not assume the original PS1 BIN contains later content or that every WotL feature exists in the selected mod.

### ZOD-052 — Integrate the selected Lion War content/profile

**Type:** Epic · **Priority:** P4 · **Status:** Needs breakdown · **Depends on:** ZOD-046, ZOD-049, ZOD-051

**Scope:** Add the agreed features using established import/content/compatibility boundaries, creating focused tickets for missing capabilities identified by ZOD-051.

**Acceptance criteria:** Every selected feature is represented and checked against its pinned target; Lion War is an explicit selectable profile/content set; baseline FFT regressions still pass; game data remains local and source/provenance is preserved. Any mechanic requiring ZOD-050 is gated by that design instead of bypassing the engine boundary.

### ZOD-053 — Extend save data to complete campaign and content-version handling

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-035, ZOD-041, ZOD-042

**Scope:** Extend the save proof to party/progression/equipment, campaign flags/time/world state, supported battle checkpoints, settings as appropriate, and required content/profile references. Define migration and backup handling as formats change.

**Acceptance criteria:** Save/continue covers the required campaign paths and does not lose flags, inventory, progression, or RNG state relevant to the supported checkpoint; incomplete/corrupt/incompatible saves fail with useful diagnostics and preserve recoverable data; migrations have synthetic regression fixtures. Interoperability with original memory-card saves remains a separately scoped optional feature.

### ZOD-054 — Decide local packaging, versioning, and optional publishing

**Type:** Deferred decision · **Priority:** P5 · **Status:** Waiting · **Depends on:** ZOD-034

**Scope:** Decide portable folder versus installer, runtime deployment mode, local build versioning, and whether any reusable packages need a private/public feed. Revisit the owner's shared CI tooling only when a concrete packaging need exists.

**Acceptance criteria:** Record the chosen local launch/update workflow and build metadata requirements; automate only selected delivery outputs; no game assets, credentials, or BIN are bundled accidentally. Public NuGet publishing and distribution are not current project requirements.

## Completed foundations carried forward

These are existing capabilities to preserve, not new tickets to rebuild:

- .NET 10 solution, console bootstrap, Core/Content/AssetTool projects, and xUnit projects.
- Immutable map/state models, stable prototype IDs, movement inputs, and deterministic command/state/event proof.
- Flat movement and a separate bounded terrain-cost calculation with regression coverage.
- Ordinary ISO file inventory/extraction and manifest/hash verification.
- Bounded unit sprite parsing/attack-image decode and local diagnostic preview.
- Linux CircleCI build/test/Sonar invocation and initial architecture/behavior notes.

Completion of those foundations does not establish full FFT equivalence or a reusable engine boundary. The source code, behavior notes, and milestone acceptance criteria define what has actually been demonstrated.
