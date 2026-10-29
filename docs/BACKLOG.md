# Zodiac / FFT backlog

This file owns **open work and its order**. [AGENTS.md](../AGENTS.md) owns project
constraints; [architecture](architecture/overview.md) owns design direction;
[the handoff](PROJECT_HANDOFF.md) records current state and last validation.

**Next bounded work: ZOD-187 (implementation).** Pure Throw Stone formula projection ZOD-186 is complete.
There are 33 open tickets below. The 154 completed tickets, prior checkpoints and
full original ticket scopes are in the [history index](history/README.md).
Summaries below retain current acceptance intent; linked original scopes/designs
provide detail. This file supersedes archived priorities and next-task advice.

## Working rules

- P1–P3 cover foundations and original-game delivery; P4 covers later extensions;
  P5 is optional delivery convenience. Dependencies alone do not activate work.
- **Ready:** bounded and prerequisites complete. **Needs breakdown:** choose a
  source-traced child before coding. **Waiting:** named prerequisites/decisions
  remain. **Deferred:** wait for the stated evidence trigger or owner request.
- Keep original FFT behavior as default; select custom variants explicitly. Use
  stable IDs, prepared files and deterministic simulation. Pin the read-only
  reference revision for new source study; never run C or read the BIN at runtime.
- Preserve meaningful behavioral coverage, not a fixed test count or source
  structure. Use the [test-maintenance policy](development/test-maintenance.md).
- Update a ticket's current scope/status in place. Move completed work to the
  history index with an evidence/commit link; do not append checkpoint narratives
  or old test counts here. Keep the handoff current rather than chronological.


## Next bounded milestone

### ZOD-187 — Project one hypothetical Throw Stone Move/ACT candidate

**Type:** Story · **Priority:** P3 · **Status:** Ready (recommended next) · **Depends on:** ZOD-186

Own one fresh [Throw Stone formula projection](behavior/throw-stone-ai-formula.md)
from the supplied ACT-before-Move or Move-before-ACT origin, then the existing
effect-free hypothetical clock. Certify movement on the original living baseline;
retain four formula draws plus real upkeep draws, current PA/HP and no knockback.
Use neutral names for genuinely shared simulation diagnostics, preserving Attack
behavior. Verify CT/refunds, zero accuracy, KO/corpse ordering, exact budgets,
atomic rejection and actor/order replay. No scoring, scan, selection, live
authority, general interface or host/editor activation.

## Existing validation gates

### ZOD-006 — Make SDK selection and dependency restoration reproducible

**Type:** Chore · **Priority:** P1 · **Status:** Implemented; Linux CI validation pending · **Depends on:** None

SDK 10.0.402, central package versions, lock files and Windows reproduction are implemented.
Confirm Linux CI restore/build/tests and the configured Sonar lane before closing the gate.
Keep one deliberate upgrade process; package drift must be prevented or detectable. [Build
policy](development/build-reproducibility.md).

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-006--make-sdk-selection-and-dependency-restoration-reproducible).

### ZOD-033 — Add deterministic visual comparison checkpoints

**Type:** Story · **Priority:** P2 · **Status:** Partial; broader fidelity/timing pending · **Depends on:** ZOD-026, ZOD-031

Synthetic capture and selected original banner/Ready/dark-screen comparisons exist. Complete
broader original fidelity/timing, capture-state/event alignment and representative human
review; record unobserved loading/entry, input boundaries/repeats, movie/latency and
scene-rendering gaps explicitly. Reports must distinguish simulation, geometry, palette,
blending/order and timing differences with justified thresholds. Keep original captures local.
[Comparison workflow](development/visual-comparison-checkpoints.md); past investigations and
approvals are in the archived scope.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-033--add-deterministic-visual-comparison-checkpoints).

### ZOD-034 — Add Windows CI and clean-machine launch validation

**Type:** Chore · **Priority:** P2 · **Status:** Implemented; remote CI and clean-machine validation pending · **Depends on:** ZOD-025

Windows/Linux CI configuration, local publishing and workstation launch checks exist. Record
actual remote job results and a clean Windows 11 x64 interactive launch without SDK/original
media. Preserve headless builds, useful failure reports and deployment prerequisites. Public
release/version automation remains separately scoped. [Launch
validation](development/windows-launch-validation.md).

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-034--add-windows-ci-and-clean-machine-launch-validation).

## Required original-game breadth — select bounded children

### ZOD-036 — Implement status, reaction, support, and charge interactions

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-023

Inventory statuses, durations, turn triggers, reactions/counters, support modifiers, charge
timing and interaction ordering. Select one source-traced status in a complete action/turn
scenario before expanding. Cover required combinations, quirks and deterministic
state/RNG/events in the baseline inventory; use independent observations where available.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-036--implement-status-reaction-support-and-charge-interactions).

### ZOD-037 — Complete baseline movement and path-selection behavior

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-010, ZOD-012

Complete elevation/slopes, horizontal jumps, water/ceilings, mounts/standing height,
occupancy/traversal, layer transitions, special movement and path/tie ordering. Each bounded
mode needs traced eligibility, reachability, destinations and selected paths, including
difficult-map comparisons and interactions.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-037--complete-baseline-movement-and-path-selection-behavior).

### ZOD-038 — Complete combat formulas, targeting, rewards, and AI

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-022

Complete formulas, targeting/range/area/line-of-sight, equipment/elements, rewards, objectives
and original AI through traced bounded children. Keep formulas readable C# and custom variants
explicit. The eventual AI decision interface must be replaceable, use explicit
state/definitions/RNG and return typed decisions validated by authoritative rules. Preserve
decision traces and compare implementations on identical inputs. Brief enemy presentation
pauses remain outside simulation/time/RNG. [Formula
direction](development/editable-combat-formulas.md).

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-038--complete-combat-formulas-targeting-rewards-and-ai).

### ZOD-039 — Expand typed definitions and import to the baseline catalog

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-018

Expand stable-ID definitions/imports across equipment, jobs/skillsets/abilities,
units/monsters/statuses, formulas, encounters/ENTD, text, maps/animation, stores and campaign
references. Account for every baseline field/reference or unresolved meaning. Authoring/runtime
share validation; synthetic catalogs must expose accidental original-table limits. Apply named
semantic flags as each family matures. Add armor, accessory and consumable definitions
alongside weapons; identify shared item metadata and family-specific data before selecting
their structure. Keep owned quantities, equipped instances and battle use in state.

Element mapping is one part of import expansion. Review the owner's remaining toggle groups
as they are defined, trace each known property to its original field/bit and preserve unknown
bits in adapters. Record custom-only toggles explicitly; decide defaults, validation and JSON
migration per family before implementing its converter. Do not guess mappings for unfinished
definitions or treat all toggle groups as elemental flags.

Bounded import candidate after the owner's definition refactor settles: extend the existing
selected-weapon importer/exporter to map the original eight element bits into immutable
`ElementToggles` and prepared JSON. Derive Neutral from an empty set; retain multi-element
combinations and initialize custom Gravity/Poison to false for original inputs. Pin the
reference mapping, version/migrate the existing numeric element schema, and verify neutral,
individual bits, combinations and JSON round trips. Preserve raw import provenance and
explicit unsupported-runtime diagnostics; richer content must not silently activate
unimplemented combat behavior. This does not reorder the AI milestones or activate broad catalog import.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-039--expand-typed-definitions-and-import-to-the-baseline-catalog).

### ZOD-041 — Implement campaign, encounters, events, and optional content

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-035, ZOD-039, ZOD-040

Complete world travel/time, encounters/setup, story/battle events, dialogue, party transitions,
propositions, side content and endings. First select one event-to-battle-to-world loop.
Acceptance ultimately covers new game through ending and required side paths, including
flag/party/encounter transitions; unsupported scripts must remain visible.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-041--implement-campaign-encounters-events-and-optional-content).

### ZOD-042 — Implement original game flow, menus, and progression UI

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-032, ZOD-039

Complete original opening/title, formation/equipment/jobs/JP, shops, world/battle menus, help
and save/load flows. Start with the party/job/equipment workflow needed by a campaign loop.
Existing synthetic world/slot UI does not complete original UI coverage. Add Settings as
display/audio options mature, with defaults, persistence, live/restart behavior and safe
recovery. [Later presentation decisions](development/later-presentation-decisions.md).

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-042--implement-original-game-flow-menus-and-progression-ui).

### ZOD-043 — Establish audio decoding/playback and expand to baseline coverage

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-024

Trace source audio formats and sequencing/playback, then prove one prepared sound/music path
before broadening coverage. Include pitch/envelopes, loops/transitions,
mixing/interpolation/reverb and streamed paths as required. Compare original recordings
separately from device latency/settings; audio must not control simulation. Original recordings
remain local.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-043--establish-audio-decodingplayback-and-expand-to-baseline-coverage).

### ZOD-044 — Finish asset preparation for remaining required formats

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-028, ZOD-030

Inventory and prepare remaining map/effect/menu/world/font/movie/audio resources, including
seven mixed XA/STR entries. Resolve sector/data semantics before converters or streaming
decoders. Every required resource needs conversion/load status and provenance, or an explicit
unused/unresolved reason. Runtime consumes prepared files without the BIN.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-044--finish-asset-preparation-for-remaining-required-formats).

### ZOD-046 — Run original-game acceptance and regression review

**Type:** Acceptance epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-034, ZOD-036 through ZOD-045, ZOD-053

Run the baseline coverage matrix, campaign/side-content playthroughs, save/continue,
presentation comparisons, asset-completeness and Windows launch acceptance. Required unresolved
behavior prevents a completeness claim. Demonstrate runtime with the BIN and C checkout
unavailable, and disclose limits of source-derived versus original/emulator observations.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-046--run-original-game-acceptance-and-regression-review).

### ZOD-053 — Extend save data to complete campaign and content-version handling

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-035, ZOD-041, ZOD-042

Extend persistence to party/progression/equipment, campaign flags/time/world state, settings
and required content/profile versions. Supported save/continue paths must preserve state,
inventory and relevant RNG, validate compatibility, recover safely and migrate with synthetic
regressions. Original memory-card interoperability is a separate optional decision; ZOD-106
remains independently deferred.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-053--extend-save-data-to-complete-campaign-and-content-version-handling).

### ZOD-061 — Complete native Windows keyboard, mouse and controller interaction

**Type:** Story · **Priority:** P3 · **Status:** Waiting · **Depends on:** ZOD-032, ZOD-042

Complete keyboard/mouse/controller navigation and rebinding across game flow. Specify focus,
hover, targeting, prompts, conflicts, device changes and binding persistence. Devices must
produce the same logical command replay without OS input/timing entering simulation. Keep
Windows manual checks separate from headless tests.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-061--complete-native-windows-keyboard-mouse-and-controller-interaction).

## Deferred until their evidence trigger

### ZOD-106 — Preserve boosted battle PA across checkpoints and replay

**Type:** Story · **Priority:** P3 · **Status:** Deferred · **Depends on:** ZOD-105

Revisit only for a concrete debugging/checkpoint requirement or an explicitly selected
battle-suspension feature. It is not a prerequisite for abilities or original campaign saving.
If selected, add a versioned ability-profile save for current battle PA; preserve old sword
schemas/world slots and resolve definitions/grants from trusted setup. Execute ACC-021..027 for
continuation, caps/choices, restart, semantic rejection and replay. Accumulate PA does not
persist beyond battle.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-106--preserve-boosted-battle-pa-across-checkpoints-and-replay).

### ZOD-134 — Migrate packed content flags to semantic boolean properties

**Type:** Refactor · **Priority:** P4 · **Status:** Deferred · **Depends on:** ZOD-014, ZOD-016

Revisit when a definition family is being extended or its verified flag mapping is ready.
Prefer named booleans for independent switches; keep packed original bytes in import/export
adapters and unknown bits opaque. Keep real numeric values/modes typed, preserve unusual
combinations/quirks, and decide package migration explicitly. Packed semantic storage needs
measured justification and readable accessors. New flags need no original bit assignment.
[Policy](architecture/overview.md#content-flag-representation).

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-134--migrate-packed-content-flags-to-semantic-boolean-properties).

### ZOD-142 — Investigate Lion War event instruction upgrade compatibility

**Type:** Conditional spike · **Priority:** P4 · **Status:** Deferred · **Depends on:** ZOD-040, ZOD-049, ZOD-051

Activate when the selected Lion War target requires its event extensions or the owner
explicitly selects this work. Owner reports Event Instructions Upgrade v1.377; verify
the exact patch, sources, dependencies and script/compiler dialect before promising
compatibility. Pin the inspected inputs and trace changed/new instructions and Event
Conditional Triggers, including scheduling, ordering, repeat/interruption behavior and RNG.

Specify explicit instruction profiles/versions, package requirements and unsupported-op
diagnostics. Investigate memory/address operations as semantic managed-state access;
original addresses must not become C# object pointers. Select one bounded compatibility
proof with original-profile isolation and state/save/replay implications before implementing
broader support. EVSP/TEST.EVT interoperability needs verified examples and remains an
offline authoring/import concern, with no required original tool at runtime. This ticket
does not activate a visual editor, general scripting engine or full TLW integration.

### ZOD-100 — Revisit an engine contract when substitution is needed

**Type:** Conditional spike · **Priority:** P3 · **Status:** Deferred · **Depends on:** ZOD-099

Activate only for a real second engine implementation, independent consumer or demonstrated
substitution need with understood semantics. A synthetic duplicate alone is insufficient. Study
the smallest useful boundary, value ownership, real-consumer proof and shared conformance cases
before selecting any API or assembly split. Preserve current behavior/RNG and ownership guards.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-100--revisit-an-engine-contract-when-substitution-is-needed).

### ZOD-101 — Revisit a framework contract when usage establishes a boundary

**Type:** Conditional spike · **Priority:** P3 · **Status:** Deferred · **Depends on:** ZOD-099

Activate only when a real alternate framework or independent application establishes a concrete
boundary. Trace consumer commands, observations, content/lifecycle, rejection ordering,
RNG/probes and persistence. Pin the managed baseline and specify minimal contracts,
profile/version rules and a replacement proof before implementation. No automatic interface
extraction or plugin loader is selected.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-101--revisit-a-framework-contract-when-usage-establishes-a-boundary).

### ZOD-047 — Build the first job/ability inspection and editing UI

**Type:** Story · **Priority:** P2 · **Status:** Implemented; further UI work and owner review deferred by owner · **Depends on:** ZOD-016, ZOD-018

The first editor and separate Jobs/Abilities revision exist; owner approval and further UI work
remain deferred. Resume only for a selected gameplay/content need or explicit owner request.
Use shared definitions/validation/export/runtime previews, preserve baseline/copy provenance
and recoverable edits, and never edit the BIN or duplicate formulas. [Workspace
guide](development/editor-workspaces.md); [FFTPatcher 0.497
reference](development/fftpatcher-desktop-reference.md).

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-047--build-the-first-jobability-inspection-and-editing-ui).

## Later authoring, extensions and presentation

### ZOD-048 — Expand authoring to flags, formulas, text, graphics, scripts, conditions, and audio

**Type:** Epic · **Priority:** P4 · **Status:** Needs breakdown · **Depends on:** ZOD-047

Add focused authoring/preview workflows as runtime/content needs them, rather than expanding UI
solely to resemble FFTPatcher. Cover flags, formulas, text/fonts, graphics/animation/maps,
scripts/events/conditions and music/audio incrementally with stable IDs, shared validation,
provenance and runtime/decoder previews. Formulas remain directly editable C#. Recoverable
editing and round-trip support precede breadth.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-048--expand-authoring-to-flags-formulas-text-graphics-scripts-conditions-and-audio).

### ZOD-049 — Prove add-on packages and compatibility-profile isolation

**Type:** Story · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-016, ZOD-023, ZOD-045

Prove namespaced packages, dependencies, precedence/conflicts, schema versions and explicit
profile selection with added content and a changed rule parameter. Disabled add-ons must leave
original results unchanged; enabled packages must work without source edits. Invalid
references/conflicts need deterministic diagnostics, and saves/replays identify required
versions. Arbitrary assembly loading is not required.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-049--prove-add-on-packages-and-compatibility-profile-isolation).

### ZOD-050 — Decide whether constrained custom mechanics or scripting are needed

**Type:** Conditional spike · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-040, ZOD-049

Select a real mechanic/campaign need that validated definitions, typed effects and the
supported interpreter cannot express. Compare new typed operations with constrained
deterministic scripting. Either justify the extension with
execution/state/version/trust/diagnostic/replay limits, or close the question with evidence
that current mechanisms suffice. Executable plugins require their own decision.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-050--decide-whether-constrained-custom-mechanics-or-scripting-are-needed).

### ZOD-051 — Select and inventory the exact Lion War target

**Type:** Spike · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-045

Agree the exact Lion War variant/revision and inventory its WotL-derived content, adaptations,
resources, scripts, rule changes, local inputs and provenance. Map each feature to baseline
behavior, explicit differences, missing capability or unknowns; do not assume the PS1 BIN
includes later content.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-051--select-and-inventory-the-exact-lion-war-target).

### ZOD-052 — Integrate the selected Lion War content/profile

**Type:** Epic · **Priority:** P4 · **Status:** Needs breakdown · **Depends on:** ZOD-046, ZOD-049, ZOD-051

Integrate only the features selected by ZOD-051 through established content/import/profile
boundaries. Each feature needs pinned-target evidence; original FFT remains the default with
unchanged regressions. Keep data local and create bounded missing-capability children.
Mechanics needing ZOD-050 remain gated by that decision.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-052--integrate-the-selected-lion-war-contentprofile).

### ZOD-079 — Specify later display, rendering and Settings options

**Type:** Deferred design task · **Priority:** P4 · **Status:** Waiting — owner requested much later · **Depends on:** ZOD-025, ZOD-033

Owner requested this much later. Revisit display/Settings design when selected presentation
work and evidence justify it. Preserve CRT-correct content proportions inside original or
widescreen viewports; keep wider composition, pixel aspect, optional CRT processing, rendering
backend and resolution choices distinct. Record defaults, transforms, settings persistence,
audio coordination and deployment/validation tradeoffs before choosing implementations.
[Decision notes](development/later-presentation-decisions.md).

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-079--specify-later-display-rendering-and-settings-options).

### ZOD-059 — Add a widescreen presentation mode

**Type:** Story · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-025, ZOD-031, ZOD-033

Add an explicit widescreen camera/viewport and adapted UI while preserving CRT-correct
text/sprite/environment proportions and the original reference profile. Show more horizontal
scene area; validate picking, resize and geometry without changing simulation. Fixed
scenes/cinematics and exact correction mappings need decisions. Coordinate mouse/keyboard
layout with ZOD-061; CRT processing remains separate.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-059--add-a-widescreen-presentation-mode).

### ZOD-060 — Add selectable game-view rendering resolution

**Type:** Story · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-031, ZOD-033

Provide selectable game-view internal rendering resolution, with 1920×1080 as an example
target. Distinguish scene rasterization from output scaling, artwork detail, aspect correction
and prerecorded movies. Validate supported geometry/UI/input transforms and performance;
settings must not change simulation. Preserve the original comparison profile.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-060--add-selectable-game-view-rendering-resolution).

### ZOD-062 — Design extensible menu entries and availability conditions

**Type:** Design task · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-040, ZOD-049

Design declarative menu contributions with typed action references and read-only availability
conditions, using a title-screen campaign entry as the bounded example. Specify stable IDs,
ordering/placement, localized labels, arguments, schemas and deterministic
conflict/cycle/missing-handler diagnostics. Separate new entries from new actions and gameplay
conditions from device/presentation availability. No general executable plugin system is
implied.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-062--design-extensible-menu-entries-and-availability-conditions).

### ZOD-063 — Support validated higher-detail graphic replacements

**Type:** Story · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-031, ZOD-033, ZOD-049

Support higher-detail replacement textures/sprites through explicit packages and logical asset
IDs, independently of output resolution. Define dimension/palette/frame/version rules,
deterministic resolution, sampling/alignment/transparency and missing/invalid fallback policy.
Keep original assets available and compare artwork changes without changing simulation. Movie
enhancement remains separately scoped.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-063--support-validated-higher-detail-graphic-replacements).

### ZOD-070 — Plan and implement selected original-game bug fixes

**Type:** Epic · **Priority:** P4 · **Status:** Needs breakdown · **Depends on:** ZOD-045, ZOD-049

Owner requested original-game bug corrections much later, after the affected behavior is
understood and tested. Collect candidates in the [bug catalog](KNOWN_BUGS.md); select none
implicitly. Each bounded correction needs an original reproducer, source evidence, explicit
optional profile semantics, original/corrected tests and RNG/order/save/replay implications.
Readable behavior-preserving refactors may proceed without selecting a quirk fix.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-070--plan-and-implement-selected-original-game-bug-fixes).

### ZOD-054 — Decide local packaging, versioning, and optional publishing

**Type:** Deferred decision · **Priority:** P5 · **Status:** Waiting · **Depends on:** ZOD-034

After Windows delivery validation, decide portable folder versus installer, local versioning
and any optional publishing/package feed. Automate only selected outputs and preserve
dependencies/notices. Never bundle game assets, credentials or the BIN. Public
distribution/version automation is not currently required.

[Detailed scope](history/2026-10-27/BACKLOG.md#zod-054--decide-local-packaging-versioning-and-optional-publishing).

## Delivery gates

- **M0/M1:** engine boundary and bounded deterministic battle proofs complete.
- **M2:** playable Windows slice exists; original comparison and remote/clean-machine gates remain.
- **M3:** first editor implemented; owner review and further UI work deferred.
- **M4:** complete original game remains open across the coverage matrix, including campaign/side content.
- **M5:** extensions/Lion War require explicit package isolation and a pinned target.

Use the [coverage inventory](coverage/README.md) for family-level gaps and evidence.
Completed foundation tickets do not establish full original-game compatibility.
