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

**Current focus (2026-10-23):** ZOD-023 completes the bounded M1 headless start-to-result scenario;
ZOD-025 adds the synthetic Windows view with live GPU/resize/close checks and explicit input/DPI limits.
ZOD-027 now traces the first ordinary Chocobo idle pose. ZOD-028 can implement that bounded
prepared slice; ZOD-029's map trace and ZOD-026's independent capture investigation remain
separate lanes. These unlock the prepared-asset renderer, not full M2 yet.
Independent asset/reference investigations can proceed in parallel when the owner selects their scope;
give each agent distinct files and acceptance criteria. M1 does not imply complete FFT combat or presentation.
The numbered queue below records the completed foundation sequence.

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

**Type:** Spike · **Priority:** P1 · **Status:** Complete · **Depends on:** None

**Result:** [Ownership audit and extraction recommendation](architecture/zod-001-type-ownership.md) (2026-10-22). Select the bounds-only flat cardinal range calculation with an FFT-owned facade; retain terrain semantics, profiles and transition policy in FFT. No extraction or project rename implemented. ZOD-007 still requires ZOD-002.

**Scope:** Classify every current `Fft.Core` type as FFT-owned, reusable candidate, or unresolved. Inspect its actual consumers in Content, tools, client, and tests. Evaluate one small extraction that performs useful work; use the existing map/movement transition as the concrete example.

**Acceptance criteria:** A committed ownership map cites the relevant files and dependencies; the proposed engine contract can be explained without FFT; FFT terrain semantics and compatibility policy remain game-owned; the smallest extraction and FFT adapter are specified with preserved behavior and unresolved issues. Do not declare a type generic solely because its name is generic. If no viable extraction is supported yet, identify the exact missing evidence instead of inventing an API.

### ZOD-002 — Define behavioral fixture provenance and comparison rules

**Type:** Design task · **Priority:** P1 · **Status:** Complete · **Depends on:** None

**Result:** [Behavioral fixture format v1](behavior/fixture-format.md) (2026-10-22) defines provenance, confidence, exact comparison rules and local capture metadata. Includes a synthetic accepted movement scenario and a source-traced coordinate-guard description with separately labeled managed expectations. The JSON examples have not been executed by a fixture runner; ZOD-011 implements that runner.

**Scope:** Define a small versioned scenario/expected-result format using the movement proof as an example. Record target edition, decomp SHA and function, evidence kind, preconditions, inputs, expected state/events, exclusions, and confidence.

**Acceptance criteria:** One synthetic movement fixture and a source-traced fixture description demonstrate the format; comparison rules specify stable IDs, ordered events, integer values, and explicit state fields; unresolved expectations cannot appear as verified results. Define where local captures belong and how committed metadata references them without including game media. Do not use .NET object hash codes as persistent replay fingerprints.

### ZOD-003 — Trace a minimal job and ability definition slice

**Type:** Spike · **Priority:** P1 · **Status:** Complete · **Depends on:** None

**Result:** [Job/ability slice design](behavior/job-ability-slice.md) maps Squire's candidate records, widths, flags, consumers and definition/state boundary, with a synthetic metadata sample. Exact retail row values and membership remain an explicit verified-import gate; no catalog or ability execution is claimed. FFTPatcher was not consulted or reused.

**Scope:** Select one baseline job and only the ability/skill-set fields needed to represent it. Trace original records, field encodings, references, consumers, and meaningful flags. Compare selected FFTPatcher field names/workflow with the PS1 source, verifying the exact tool revision and edition support if consulted.

**Acceptance criteria:** A field-by-field map identifies source evidence, original indices, proposed stable IDs, signedness/ranges, flag meanings, and unknown values; `JobDefinition` is separated from a unit's job level, JP, HP, and equipment state; the minimum next schema and synthetic sample are specified. Record the community-tool reference/reuse decision and preserve unknown source bits at the import boundary. This ticket produces a design, not a full job catalog.

### ZOD-004 — Trace one adjacent-step height/slope eligibility rule

**Type:** Spike · **Priority:** P1 · **Status:** Complete · **Depends on:** None

**Result:** [Adjacent-step geometry trace](behavior/adjacent-step-geometry.md) specifies the dry non-steep facing-edge gate, doubled capped Jump, source-derived threshold/reverse/slope vectors, arithmetic envelope and exclusions. Static source evidence only; no oracle or original observation. ZOD-010 still depends on ZOD-012.

**Scope:** Extend [movement-inputs.md](behavior/movement-inputs.md) for one ordinary same-layer cardinal step. Trace source callers, height units, slope orientation, effective standing height, comparison widths, and the exact unit/profile assumptions.

**Acceptance criteria:** Select a bounded rule with expected accept/reject vectors at the threshold, above it, and in both directions; identify whether slope affects this particular rule and how; explicitly exclude horizontal jump spans, water, mounts, occupied tiles, and special movement modes unless the selected rule requires one. Record source commit and remaining ambiguity before implementing ZOD-010.

### ZOD-005 — Trace FFT RNG state, arithmetic, and call ordering

**Type:** Spike · **Priority:** P1 · **Status:** Complete for pinned US v3.0 BIOS · **Depends on:** None

**Findings:** [Initial RNG trace](behavior/rng-state-and-ordering.md) pins seed sites, bounded range arithmetic and formula-01 draw ordering. [Supplied BIOS verification](behavior/bios-rng-verification.md) resolves its missing state/update evidence through independent static analysis and actual ROM instruction execution: eight seeds, 64 observed state/output pairs, multiply/add wrap boundaries and reseeding. BIOS and raw captures stay ignored; only metadata/vectors and an optional verification script are committed. No whole-game or other-BIOS equivalence is claimed; ZOD-013 can implement the now-evidenced generator.

**Scope:** Find the RNG routines used by the proposed physical attack and record state width, update arithmetic, range conversion, seeding, and draw ordering, including failed/rejected actions where relevant.

**Acceptance criteria:** Provide source-derived vectors including overflow/boundary values; identify which parts can be a neutral engine state contract and which are PS1 compatibility behavior; document a viable independent verification method or its blocker. Do not substitute `System.Random` without evidence of equivalence.

### ZOD-006 — Make SDK selection and dependency restoration reproducible

**Type:** Chore · **Priority:** P1 · **Status:** Implemented; Linux CI validation pending · **Depends on:** None

**Result:** [SDK and dependency policy](development/build-reproducibility.md) preserves Antigravity's central versions and lock files, pins SDK 10.0.402 without roll-forward, defaults to locked restore, and aligns CircleCI. Windows locked restore and Release build passed; all 155 tests passed on 2026-10-22. Linux CI and remote Sonar have not run for these changes; confirm them before closing the acceptance gate.

**Scope:** Inspect the installed SDK, project package versions, and CircleCI image. Choose an explicit SDK selection/roll-forward policy and a package restoration strategy appropriate to this solution.

**Acceptance criteria:** A clean Windows checkout and Linux CI can restore/build/test using documented versions; deliberate upgrades have one documented process; package drift is either prevented or explicitly detectable. No publishing or version-number automation is required by this ticket.

### ZOD-007 — Extract the first useful Zodiac engine slice

**Type:** Story · **Priority:** P1 · **Status:** Complete · **Depends on:** ZOD-001, ZOD-002

**Result:** [Engine slice](architecture/zod-007-engine-slice.md) adds independent bounds/position contracts and flat cardinal range with 21 engine tests. Coordinator validated locked restore, zero-warning Release build and all 155 tests. FFT still uses its existing calculation; adapter integration is ZOD-008.

**Scope:** Implement exactly the extraction justified by ZOD-001 in `Zodiac.Engine`, with an engine test project. Prefer the smallest coherent functionality over a general framework of empty interfaces.

**Acceptance criteria:** The engine slice runs on synthetic data, has meaningful independent tests, and references no FFT assemblies, definitions, assets, or formats; deterministic behavior and any ordering contract are explicit; no platform/file/input dependencies enter simulation. Update solution/CI references only as required. Keep behavior changes out of the move and retain FFT-specific types in their current home until their own migration is justified.

### ZOD-008 — Adapt the existing FFT transition to the engine slice

**Type:** Story · **Priority:** P1 · **Status:** Complete · **Depends on:** ZOD-007

**Result:** [FFT adapter integration](architecture/zod-008-fft-adapter.md) routes the existing FFT facade through Zodiac while retaining flat prototype behavior. Locked restore, Release build and all 162 tests passed on Windows; no project rename or terrain-policy change.

**Scope:** Connect the current FFT battle proof to the new engine functionality while preserving the documented prototype movement behavior. This is the first actual game-to-engine integration.

**Acceptance criteria:** Existing accepted/rejected commands, input immutability, resulting state, and event ordering remain covered; FFT selects/provides its policy without FFT branches in the engine; the reference graph is acyclic and one-way. Record which current files remain a prototype and any project names migrated as part of this bounded move.

### ZOD-009 — Prove engine independence with a neutral sample and dependency check

**Type:** Story · **Priority:** P1 · **Status:** Complete · **Depends on:** ZOD-008

**Result:** [Relay neutral scenario](../samples/Relay/README.md) compiles in engine-only tests and completes an invented delivery. A separate integration test varies FFT Move without affecting its result. The evaluated project-reference guard rejects a deliberate FFT reference and runs in the existing CI build. Windows locked restore, Release build and all 164 tests passed; remote CI remains unverified.

**Scope:** Add one tiny synthetic tactical scenario using the extracted engine functionality and no FFT types. Add a focused architectural check for forbidden engine-to-FFT project references.

**Acceptance criteria:** The sample executes meaningful simulation behavior with invented content; changing an FFT profile does not change its result; CI detects an introduced engine-to-game dependency. Keep the sample small and avoid creating a second full game or plugin system.

## First deterministic battle and typed content

### ZOD-010 — Implement the selected adjacent-step geometry rule

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-004, ZOD-012

**Result:** [Bounded adjacent-step policy](behavior/adjacent-step-implementation.md) adds explicit dry, non-steep facing-edge geometry with capped Jump and conservative supported-domain validation. Source-derived bidirectional vectors, combined cost/blocker cases and synthetic accepted/rejected replay fixtures pass. Existing policies are unchanged. Coordinator verified locked restore, zero-warning Release build and all 281 tests after parallel package-prototype integration.

**Scope:** Implement only the eligibility rule selected by ZOD-004 through the established movement boundary.

**Acceptance criteria:** Source-derived threshold/direction/slope vectors pass; tests combine geometry with entry cost and blockers; rejected moves preserve state; the policy's unsupported mechanics remain explicit. A geometry change does not silently replace the original compatibility target with a generic rule.

### ZOD-011 — Add a deterministic scenario fixture runner

**Type:** Story · **Priority:** P1 · **Status:** Complete · **Depends on:** ZOD-002, ZOD-008

**Result:** [Movement fixture runner](behavior/fixture-runner.md) executes accepted/rejected sequential prototype scenarios, validates v1 metadata, retains provenance and diagnoses state/event mismatches. Source descriptions remain not executed. Windows locked restore, Release build and all 182 tests passed; no gameplay change.

**Scope:** Execute the versioned fixture inputs through the current FFT transition, compare explicit state fields and ordered events, and report the first meaningful mismatch.

**Acceptance criteria:** Accepted and rejected movement scenarios replay deterministically; deliberate state/event mismatches produce useful diagnostics; fixture metadata retains source/evidence provenance; tests require no BIN, emulator, or network. Extend the runner incrementally as new commands and state fields are introduced.

### ZOD-012 — Integrate the bounded terrain-cost policy explicitly

**Type:** Story · **Priority:** P1 · **Status:** Complete · **Depends on:** ZOD-008, ZOD-011

**Result:** [Explicit terrain policy](behavior/terrain-policy-integration.md) integrates resolved entry costs, blockers, stopping flags and deliberate occupancy traversal rules. Flat prototype behavior is retained and both policies have replay fixtures. Locked restore, zero-warning Release build and all 227 solution tests passed on Windows after parallel ZOD-014 integration. No full FFT movement equivalence is claimed.

**Scope:** Allow the battle transition to use the effective movement profile and existing terrain-cost policy through the established engine/game boundary. Preserve an explicitly selected flat prototype mode where needed for existing tests.

**Acceptance criteria:** Commands respect terrain entry costs, blocked surfaces, destination flags, occupied destinations, and available movement under the documented subset; missing costs fail deliberately; accepted/rejected transitions have fixtures. Specify traversal through occupied tiles rather than accidentally inheriting it from range generation. Preserve separate compatibility/prototype labels; do not claim full FFT movement.

### ZOD-013 — Implement explicit deterministic RNG state

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-005, ZOD-007, ZOD-011

**Result:** [Explicit deterministic RNG](behavior/deterministic-rng.md) adds a neutral explicit-state
transition contract, FFT-owned verified BIOS generator, ordered RNG replay diagnostics and optional battle
RNG snapshots. All 64 recorded BIOS vectors pass; accepted/rejected movement preserves state and consumes
zero draws under each current prototype policy. Locked restore, Release build (zero warnings/errors) and
all 334 solution tests passed on 2026-10-22. Combat consumption ordering remains with its action slice.

**Scope:** Introduce the neutral state/transition seam and the source-traced FFT RNG implementation justified by ZOD-005.

**Acceptance criteria:** Recorded vectors pass with explicit integer/overflow semantics; a transition returns updated RNG state; replay compares it; draw counts/order are testable and no wall-clock or global RNG enters simulation. Rejected commands consume draws only if source evidence requires it for the selected behavior.

### ZOD-014 — Implement minimal typed job and ability definitions

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-003

**Result:** [Typed metadata definitions](architecture/zod-014-typed-definitions.md) add immutable FFT-owned IDs/definitions and a validated catalog with 32 new tests, including a graph beyond original table capacities. Retail row import, execution effects and package format remain separate. Coordinator verified all 227 solution tests after integration.

**Scope:** Create FFT-owned immutable definitions and typed IDs for the selected job/ability slice, including references and named flags justified by source. Use the agreed `Fft` ownership convention when a new project boundary is warranted.

**Acceptance criteria:** Synthetic definitions preserve field values and structural invariants; invalid/duplicate IDs and unresolved references have clear diagnostics; unit progression/runtime state remains separate; collections accept a synthetic catalog beyond original table capacities. Original numeric indices are import metadata, not the public storage or identity limit. Full catalogs and every FFTPatcher field are outside this slice.

### ZOD-015 — Decide and prototype the editable content package format

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-014

**Result:** [Content package decision/prototype](architecture/zod-015-content-package.md) selects strict loose JSON, round-trips synthetic typed metadata, preserves all primary flag bytes, and specifies explicit baseline/add-on composition and deterministic output. Test-only prototype has 22 passing cases; production loader/export work remains ZOD-016. Full integrated solution passes 281 tests.

**Scope:** Compare simple file-based representations for the minimal definitions. Recommend a format, package layout, schema version policy, stable ID/reference convention, and separation from the existing asset manifest and future save data.

**Acceptance criteria:** A synthetic job/ability package round-trips through a small prototype; malformed data, unknown versions, unknown fields/flags, duplicate IDs, and unresolved references have documented handling; deterministic output and baseline/add-on isolation are specified. Prefer a simple choice supported by the evidence. No SQL service or archive format is required to prove this workflow.

### ZOD-016 — Build the shared content validator and C# authoring export

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-015

**Result:** [Production content package API](architecture/zod-016-content-packages.md) shares typed validation across C# and JSON, reports package/record/field context, exports deterministic LF JSON through a validated sibling-file replacement, and loads runtime metadata without authoring code. `validate-content` returns 0/1/2 for valid/content-or-file-error/usage. Windows locked restore, zero-warning Release build and all 309 tests passed; actual CLI checks outside the repository confirmed success/failure. Only synthetic example data is committed with this work; Linux CI remains unverified locally.

**Scope:** Let a trusted local authoring program construct the selected C# definitions and export the same versioned data that a hand-edited package uses. Expose validation for both CLI and later editor consumers.

**Acceptance criteria:** C#-authored and file-authored equivalent data produce equivalent definitions; diagnostics identify package, record, and field; a validation command returns a nonzero exit on failure; repeated exports are deterministic; invalid input does not overwrite a valid package. Runtime consumes exported data without loading authoring code. Commit only synthetic examples.

### ZOD-017 — Import and validate the selected original job/ability records

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-003, ZOD-016

**Result:** [Selected original metadata import](behavior/original-metadata-import.md) verifies the pinned
SCUS-94221 executable, resolves Squire's actual skillset and imports one job, one 22-slot skillset and ten
referenced abilities. All 22 raw source records and unknown fields remain in ignored provenance. Two local
imports were byte-identical and passed shared validation; synthetic tests require no game files. Windows
locked restore, Release build (zero warnings/errors) and all 379 solution tests passed on 2026-10-22.

**Scope:** Read only the selected source records from local prepared/extracted files and convert them to the new definitions. Identify any additional container or executable-table parsing needed before implementation.

**Acceptance criteria:** Imported fields and original-index mappings match the traced source records; unknown bits/data are reported and preserved in import metadata; output provenance includes edition, input hashes, and converter version; repeated import is deterministic. Generated records stay in `scratch/`; synthetic parser tests run without game files. Missing or malformed input produces diagnostics rather than guessed defaults.

### ZOD-018 — Resolve content definitions into bounded battle units

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-012, ZOD-017

**Result:** [Base-job setup](behavior/base-job-battle-setup.md) resolves stable definition references before
startup and creates independent movement snapshots from shared immutable definitions. Base Move/Jump map
directly; body height/costs remain explicit prototype inputs, with stepping stones and effective-stat
derivation excluded. Authored Move changes reachability through the unchanged simulator. Integrated Windows
locked restore, zero-warning Release build and all 464 tests passed on 2026-10-22.

**Scope:** Build a unit for the selected scenario from typed definitions and explicit runtime inputs. Source-trace any calculation needed to derive its effective movement/combat values; retain explicitly supplied values where the calculation is not yet implemented.

**Acceptance criteria:** Two units can share a definition while retaining independent state; stable IDs resolve before battle startup; absent content fails before simulation; a synthetic authored job changes a supported unit parameter without a simulator edit. Each derived field is documented as source-traced or an explicit prototype input.

### ZOD-019 — Trace the smallest CT and turn-completion scenario

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-002

**Result:** [CT and turn-completion trace](behavior/ct-turn-cycle.md) pins tick caps, CT modulo, largest-CT/
slot-tie selection, Move/Act/Wait markers and completion refunds. Includes a checkable two-unit sequence and
the unconditional upkeep RNG draw before readiness delivery. Independently reviewed static source evidence;
no original-game/executable turn oracle or implementation is claimed.

**Scope:** Study normal CT accumulation, ready-unit selection/ties, and turn completion for two ordinary units, with no speed-changing statuses or charged abilities.

**Acceptance criteria:** A source-backed state/command sequence states initial CT, speed, readiness thresholds, tie order, and CT consequences of move/action/wait; arithmetic and update order are recorded; special-case exclusions are explicit. Identify the engine scheduler seam and FFT-owned rules without designing every turn system.

### ZOD-020 — Implement the bounded turn cycle and wait command

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-008, ZOD-011, ZOD-013, ZOD-019

**Result:** [Bounded turn implementation](behavior/ct-turn-implementation.md) implements explicit CT/Speed/
tie keys, selection, choice consumption, completion and one upkeep RNG draw per new readiness. A separate
movement wrapper enforces active turns and restores movement on later turns while preserving movement-only
APIs/fixtures. Source boundary vectors and repeated state/event/RNG scenarios pass. Integrated Windows
locked restore, zero-warning Release build and all 464 tests passed on 2026-10-22. ConfirmAct is a synthetic
choice marker; attack effects remain ZOD-022.

**Scope:** Advance the two-unit scenario using the selected CT semantics and explicit turn completion.

**Acceptance criteria:** Ready-unit/tie vectors pass; inactive units cannot issue actions; waiting/ending a turn updates the documented fields and permits subsequent turns; event/state sequences replay identically, including the source-traced upkeep RNG draw at new readiness selection. Expanded status, charge, and unusual tie behavior remain separately tracked.

### ZOD-021 — Trace one ordinary physical attack end to end

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-002, ZOD-003, ZOD-005

**Result:** [Ordinary physical attack trace](behavior/ordinary-physical-attack.md) specifies synthetic sword
inputs, targeting/facing/height, sequential evade and critical/proc draws, damage widths, HP/KO/Critical and
turn consequences. Seeded hit/miss/critical and boundary vectors were independently reviewed against pinned
source. No executing FFT oracle or verified retail sword row is claimed. Prerequisites are ZOD-055/056;
retail sword verification is ZOD-057.

**Scope:** Select a precisely specified unit/weapon/target configuration and trace target legality, facing/range, hit checks, damage arithmetic, HP/KO changes, RNG draw order, and turn consequences. Identify every deliberately absent modifier.

**Acceptance criteria:** Expected hit/miss/damage/KO and invalid-target vectors cite source functions and commit; operand widths and rounding points are explicit; the definition fields needed by the scenario are identified. Break out prerequisite schema work if the minimal job/ability definitions are insufficient. Do not implement a general formula interpreter as part of the trace.

### ZOD-022 — Implement the selected attack and HP/KO transitions

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-013, ZOD-018, ZOD-020, ZOD-021, ZOD-055, ZOD-056

**Result:** [Bounded sword implementation](behavior/bounded-sword-implementation.md) adds pure atomic
unit attacks, ordered evade/critical/proc draws, facing/action/turn effects, HP/Critical/Dead and byte-wrapped
KO counts. Explicit content projection supports both sword schema versions. Executable fixture v3 supplies
weapon parameters and compares complete ordered state/events; v1/v2 behavior remains unchanged. Nine
source-derived/synthetic replays cover hits, misses, criticals, KO, facing, PA-zero/damage bounds, range
rejection and Move/Attack completion. In-bounds knockback and post-KO scheduling remain explicitly unsupported;
no retail combat equivalence is claimed.
Windows locked restore, zero-warning Release build and all 618 solution tests passed on 2026-10-23.

**Reference checkpoint:** [Pre-implementation refresh](behavior/reference-refresh-2026-10-22.md)
rechecks the bounded chain at `11464943bbdbd2a0af022d90cb7f10455cf911df`. New implementation
evidence should use that revision/current symbols; older traces and fixtures retain their
actual original source revisions. No vector change was identified by the targeted review.

**Scope:** Implement only the source-traced action configuration and required definition fields.

**Acceptance criteria:** Hit, miss, boundary damage, KO, range/facing, and invalid-command cases have fixtures; HP, action availability, and RNG state advance in the documented order; rejected actions preserve state as specified; state/events support presentation without presentation code. Unsupported equipment/status/modifier combinations fail explicitly or are excluded by scenario validation.

### ZOD-055 — Define the bounded executable sword content slice

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-014, ZOD-021

**Result:** [Sword definitions and package](architecture/zod-055-sword-content.md) add immutable constrained
weapon/effect content and a separate versioned sword layer with shared strict validation and canonical
round trips. Metadata v1 remains unchanged. Unsupported fields have package/record/field diagnostics;
retail values and attack resolution remain separate. Integrated Windows locked restore, zero-warning
Release build and all 464 tests passed on 2026-10-22.

**Scope:** Add the minimum FFT-owned immutable sword/attack definition fields selected by ZOD-021 and shared
package validation. Keep learning-only abilities distinct from supported executable content.

**Acceptance criteria:** Weapon type, power, formula, targeting flags, evade/element and empty added-effect
configuration can represent the selected synthetic sword without simulator constants or guessed defaults.
Unsupported configurations fail explicitly; authored packages round-trip with field-specific diagnostics.
No retail sword values are asserted until ZOD-057 verifies them.

### ZOD-056 — Extend battle snapshots and fixtures for the bounded attack

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-013, ZOD-018, ZOD-020, ZOD-021

**Result:** [Attack snapshots and fixture v2](behavior/attack-snapshot-fixtures-v2.md) add explicit combat
state, startup equipment/domain validation and exact ordered replay comparisons across existing movement/
turn transitions. V1 semantics remain unchanged. A source-derived KO description validates but reports
not-executed; no attack resolution or death scheduling is claimed. Windows locked restore, zero-warning
Release build and all 498 solution tests passed on 2026-10-22.

**Scope:** Add explicit attack runtime inputs/state and a versioned fixture contract for the selected scenario,
using ZOD-021's field inventory. Keep immutable definitions separate from HP, facing and current turn state.

**Acceptance criteria:** Snapshots represent required HP/maxHP, combat stats, facing, supported statuses/KO,
equipment references, action availability and RNG state/count. Replay compares those fields and ordered events;
existing movement fixtures retain their documented semantics. Invalid or unsupported setup fails before
simulation. This ticket supplies contracts/validation; ZOD-022 implements attack resolution.

### ZOD-057 — Verify and import the selected retail sword and attack records

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-017, ZOD-021, ZOD-055

**Result:** [Verified retail sword import](behavior/retail-sword-import.md) traces and imports seven selected
item/weapon/proc/attribute/ability/menu records with exact input identity and raw provenance. Two imports were
byte-identical; direct byte checks found no discrepancies. Retail power4, evade5, flags0x8e and empty proc
type0 differ from the synthetic profile. That milestone's export was metadata-only; ZOD-058 adds explicit
schema 2 representation separately. Existing Squire output is unchanged. Windows locked restore, zero-warning Release build
and all 519 solution tests passed on 2026-10-22.

**Scope:** Trace and import the selected original sword/item and attack configuration from verified prepared
files; resolve the candidate row identified by ZOD-021 instead of substituting familiar values.

**Acceptance criteria:** Every required semantic field maps to a pinned layout and exact original record;
unknown bytes remain in ignored import provenance with edition/input hashes and converter version.
Repeated imports are deterministic; malformed/unsupported inputs have diagnostics and synthetic parser tests.
Record any mismatch with ZOD-021's deliberately synthetic configuration before claiming retail equivalence.

### ZOD-058 — Represent the verified retail sword differences explicitly

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-055, ZOD-056, ZOD-057

**Result:** [Retail sword representation and migration](behavior/retail-sword-representation.md)
add explicit schema 2 capability flags and empty proc modes, preserve schema 1 behavior, and trace raw
versus effective weapon evade. Opt-in verified sidecar export preserves retail values, requires selected
metadata, and rejects unsupported combinations. Two exports are byte-identical; raw records and existing
Squire output are unchanged. No attacks or retail combat equivalence are claimed.
Windows locked restore, zero-warning Release build and all 553 solution tests passed on 2026-10-22.

**Scope:** Extend the bounded content/setup contract to represent the verified Broad Sword without replacing
its power, evade, capability flags or empty proc mode. Decide an explicit package-version/migration policy.

**Acceptance criteria:** Capability bits are distinct from targeting/active support effects; empty type0
status data round-trips without becoming type128; weapon evade has source-traced effective-stat/evasion
semantics before runtime support is claimed. Imported data can be validated/exported faithfully, unsupported
combinations fail explicitly, and existing synthetic profile/fixtures retain their versioned behavior.
No retail combat equivalence is claimed without executing ZOD-022 and checking appropriate vectors.

### ZOD-023 — Complete a reproducible two-unit battle scenario

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-010, ZOD-022

**Result:** [Two-unit headless battle](behavior/two-unit-battle-scenario.md) adds an immutable authored
definition, pure command/result lifecycle, explicit first-KO terminal rule and restart via the original setup.
The synthetic content/script composes adjacent-step movement, ordinary turns, both units' attacks and Wait.
The client launches it with `--scenario two-unit-battle`. Two full CLI transcripts match, ending at tick 20
with first-unit victory, RNG word 2473755763 and 22 draws. Tests compare full replay snapshots/events,
invalid-command recovery, terminal rejection, setup preflight and either winner. This satisfies bounded M1;
no original victory logic, AI, death scheduling or complete FFT compatibility is claimed.
Windows locked restore, zero-warning Release build and all 632 solution tests passed on 2026-10-23.

**Scope:** Define a tiny battle objective and run a complete start-to-result sequence with the supported move/action/wait rules. Label an invented objective as a scenario rule unless original victory behavior has been traced.

**Acceptance criteria:** A documented command sequence reaches a terminal result; commands after completion follow an explicit rule; restart/replay produces identical ordered state/events/RNG; invalid commands do not corrupt the scenario. The scenario can be launched headlessly with synthetic content. Record a coverage statement so this milestone cannot be mistaken for complete FFT combat.

## Windows rendering, assets, and comparison workflow

### ZOD-024 — Select a Windows rendering/input stack using a bounded spike

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** None

**Result:** [ADR 0005](adr/0005-windows-rendering-and-input-stack.md) recommends MonoGame DesktopGL
3.8.6 after comparing WindowsDX, FNA and Silk.NET. The isolated locked probe passes with SDK 10.0.402,
.NET 10.0.13 and Windows x64, including SDL native loading/version query. No production dependency added.
Window/GPU/texture execution, controllers, resize and DPI remain explicit ZOD-025 manual gates;
no presentation compatibility is claimed from the no-window probe.

**Scope:** Compare a small set of managed-host options against windowing, texture upload, pixel sampling, indexed palettes/transparency, PS1 blend approximations, depth/order, camera transforms, input, diagnostics, and Windows deployment requirements. Clarify whether platform libraries use native components and how that fits the managed-game-logic requirement.

**Acceptance criteria:** An ADR records the recommended stack, tested SDK/runtime compatibility, license/deployment needs, limitations, and a minimal window/texture experiment if necessary to resolve a decision. Game simulation stays headless. Editor framework selection is a separate decision unless evidence supports sharing it.

**Later-goal inputs:** Assess support for widescreen/resize/high-DPI viewports, selectable internal resolution,
replacement texture handling, keyboard/mouse and controllers. These capabilities inform stack selection;
ZOD-059/060/061 own the later feature work and do not expand this spike into a full implementation.

### ZOD-025 — Create the Windows host with a synthetic battle view

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-024

**Result:** [Windows launch, ownership and validation notes](development/windows-synthetic-host.md)
(2026-10-23). MonoGame DesktopGL 3.8.6 is confined to Client. An immutable two-unit snapshot
renders with authored textures/font, point sampling, explicit transparency/order, centered resize
and disposable/resettable graphics resources. Live Windows GPU readback and native close passed;
rendering leaves CT/RNG/state unchanged. Physical Escape/controller, mixed-DPI and forced-device-reset
checks remain explicitly unverified. Full solution: 656 passed, including 24 headless layout cases.

**Scope:** Open a window, display a synthetic grid/unit, manage resize/exit, and separate presentation timing from simulation advancement using the selected stack.

**Acceptance criteria:** Documented Windows launch works; the host renders an immutable snapshot and has a predictable resource lifecycle; rendering frames does not mutate or advance simulation; no graphics/input packages enter the engine library. A synthetic scene requires no original assets.

### ZOD-026 — Establish an independent sprite/reference capture workflow

**Type:** Spike · **Priority:** P2 · **Status:** Ready · **Depends on:** None

**Scope:** Evaluate an executing original routine or controlled original/emulator observation for the current sprite proof. Define how to capture relevant decoded output or appearance with edition, palette, frame, camera, timing, and environment recorded. Align resulting metadata with ZOD-002 before integrating it into the fixture runner.

**Acceptance criteria:** Record one independent comparison if feasible, including input/output hashes or local capture references and its limits. If the oracle cannot be established, document the exact blocker and bounded fallback; do not relabel the existing source-transcribed hash as independent evidence. Media remains local. An emulator or reference harness is a development tool, never a game runtime dependency.

### ZOD-027 — Trace one sprite frame, palette, and animation selection path

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** None

**Result:** [Chocobo idle-pose source map and preparation scope](architecture/zod-027-chocobo-idle-pose.md)
(2026-10-23), pinned to reference `11464943bbdbd2a0af022d90cb7f10455cf911df`.
Select sheet 134, primary SHP frame 9, idle SEQ 6 and explicit palette bank 0.
Records, six-piece composition, ordinary-sheet nibble packing, reverse piece draw order,
palette/STP handling and source wait units are documented and checked against local file metadata.
No parser/runtime changes or original-game rendering comparison; camera, timing and GPU limits remain explicit.

**Scope:** Starting from the existing `.SPR` proof and [sprite decoder note](sprite-attack-image-decoder-poc.md), select one ordinary unit pose and trace the minimum SHP/SEQ references, palette selection, piece transforms, transparency/blending, and timing needed to display it.

**Acceptance criteria:** A source map identifies exact files/records and one frame's composition; unknown ordering/timing behavior is listed; define the smallest parser/rendering input slice. Avoid assuming every `.SPR`, `.SHP`, or `.SEQ` uses the selected format or covers every unit/effect.

### ZOD-028 — Parse and prepare the selected sprite-frame slice

**Type:** Story · **Priority:** P2 · **Status:** Ready · **Depends on:** ZOD-027

**Selected slice:** Follow ZOD-027's [bounded implementation recommendation](architecture/zod-027-chocobo-idle-pose.md#smallest-zod-028-implementation):
ordinary Chocobo primary frame 9 with bank 0; optional sequence 6 idle loop only.
Keep attack/EVTCHR/SP2, special part splitting and general animation interpretation outside this first converter.

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

### ZOD-048 — Expand authoring to flags, formulas, text, graphics, scripts, conditions, and audio

**Type:** Epic · **Priority:** P4 · **Status:** Needs breakdown · **Depends on:** ZOD-047

**Scope:** Add focused editing/preview workflows as the corresponding definitions and decoders mature. Named flags show verified meanings and unknown bits; formula parameters distinguish data from executable behavior; text has font/layout preview; sprite/animation/map/script editing uses established format and validation APIs.

**Owner's content-management goals:** Add, modify and organize music, graphics, scripted game events and
conditions as supported formats mature. Include logical-ID/reference inspection, asset association, shared
validation, package provenance and appropriate previews. Coordinate audio with ZOD-043, event interpretation
with ZOD-040 and package management with ZOD-049. Menu/action/condition declarations are scoped by ZOD-062.

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

### ZOD-059 — Add a widescreen presentation mode

**Type:** Story · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-025, ZOD-031, ZOD-033

**Scope:** Add an explicitly selected widescreen viewport/camera and menu/HUD layout mode while retaining
the original presentation/reference profile. Decide how battle scenes, fixed layouts and cinematics fit.

**Acceptance criteria:** Supported aspect ratios render with correct geometry, picking and UI placement;
resize does not stretch the scene or alter simulation coordinates/ranges. Original-profile comparisons
remain available. The same logical commands and seed produce identical gameplay outcomes across modes;
framing and unsupported scenes have documented decisions and manual checks.

### ZOD-060 — Add selectable game-view rendering resolution

**Type:** Story · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-031, ZOD-033

**Scope:** Implement selectable internal rendering resolution for the game view, with 1920 x 1080 as an
example landscape target. The owner means rendered gameplay, not enhanced prerecorded movies. Distinguish
scene rasterization, output/window scaling, original texture/sprite detail and aspect ratio.

**Acceptance criteria:** Supported scene geometry renders at the chosen internal resolution rather than
only scaling a completed low-resolution frame. Scene alignment, sampling/transparency, picking and UI
readability remain correct; original assets and presentation profile remain supported. Rendering resolution
can vary independently of aspect-ratio mode, with explicit viewport/scaling behavior. Original sprites and
textures retain their source detail unless separate replacements are selected. Simulation results remain
unchanged. No replacement asset pack or movie enhancement is required by this ticket.

### ZOD-061 — Complete native Windows keyboard, mouse and controller interaction

**Type:** Story · **Priority:** P3 · **Status:** Waiting · **Depends on:** ZOD-032, ZOD-042

**Scope:** Extend the initial battle input proof into consistent keyboard/mouse and controller navigation
across supported game screens, using device-independent application actions and configurable bindings.

**Acceptance criteria:** Battle and menu workflows are operable through the supported devices; mouse
picking respects camera/viewport transforms, and controller navigation has predictable focus. Binding
conflicts, device changes, prompts and persistence have explicit behavior. Different devices can generate
the same logical command replay without device APIs, polling timing or input state entering simulation.
Record Windows manual checks separately from headless tests.

### ZOD-062 — Design extensible menu entries and availability conditions

**Type:** Design task · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-040, ZOD-049

**Scope:** Specify declarative menus/contributions, typed action references and supported read-only
availability conditions. Use adding a title-screen campaign option as the bounded example; align with the
framework's event/condition semantics without introducing a general-purpose executable plugin system.

**Acceptance criteria:** A synthetic package contributes an entry targeting an existing typed action;
stable IDs, placement/ordering, localized labels, action arguments and condition references are specified.
Duplicate entries, missing targets/handlers and conflicting/cyclic placement have deterministic diagnostics.
Original content/profile behavior remains explicit. Distinguish adding an entry from implementing a new
action, and deterministic gameplay conditions from presentation/device availability. Record schema/version
and validation rules before creating implementation child tickets.

### ZOD-063 — Support validated higher-detail graphic replacements

**Type:** Story · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-031, ZOD-033, ZOD-049

**Scope:** Add higher-detail texture/sprite replacements through selected content packages, independently
of game-view rendering resolution. Use existing logical asset identities and shared authoring validation.

**Acceptance criteria:** Replacement assets resolve deterministically with explicit dimension/palette/frame
compatibility rules and package/version identity. Sampling, frame alignment and transparency preserve the
supported scene semantics. Invalid/missing replacements have diagnostics/fallback policy; original assets
remain available. Visual comparisons and manual checks distinguish new artwork from higher output
resolution. Simulation results remain unchanged; movie enhancement remains separately scoped.

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
