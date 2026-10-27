# Zodiac / FFT development backlog

**Created:** 2026-10-22

**Scope:** Reusable Zodiac engine, PS1-compatible FFT game, asset preparation, content authoring, validation, and development infrastructure.

**Tracker:** Repository-local planning document. These IDs are not tickets created in Jira or another service.

This is the ordered work breakdown for the goals in [PROJECT_PLAN.md](../PROJECT_PLAN.md). The [architecture overview](architecture/overview.md) owns architectural intent; [ADR 0004](adr/0004-zodiac-engine-and-fft-game-boundary.md) records the engine/game boundary. The [handoff](PROJECT_HANDOFF.md) preserves earlier implementation and research context. This backlog owns future work order and ticket dependencies; its order supersedes the handoff's older next-task recommendations.

## Starting evidence and limits

Repository inspection confirms the following starting point:

- `Zodiac.Framework` has immutable map/battle models, a typed command transition proof, and two bounded movement policies. [BattleSimulator](../src/Zodiac.Framework/BattleSimulator.cs) still calls `FlatGridMovementRange`; [TerrainAwareMovementRange](../src/Zodiac.Framework/TerrainAwareMovementRange.cs) is a separate proof and omits height, slopes, jumps, occupancy, water, movement modes, and layer transitions.
- `Zodiac.Content` loads hash-verified files through a [prepared asset manifest](../src/Zodiac.Content/PreparedAssetManifest.cs). AssetTool and its CLI support ordinary-file extraction and a bounded unit sprite decode. This is not yet a general game-definition package format or full asset decoder.
- There is no `Tessera.Engine` project, typed job/ability/item catalog, rendering backend, playable battle loop, campaign runtime, save system, or editor.
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

**Deferred** means the idea is retained but must not start until its stated
evidence trigger is met and the scope is reassessed; completed prerequisites
alone do not make it Ready.

IDs stay stable when priorities change. No calendar estimates or story points are assigned: the source investigations and first implementations should establish a basis for estimating individual tasks. Dependencies below are planning assumptions to refine during the relevant spike. Within a priority, use the recommended queue and dependency order; a high-numbered Ready investigation can proceed alongside an earlier one when it does not edit the same files.

For every implementation ticket: preserve the existing test suite, add meaningful regression coverage for new behavior, update its behavior/architecture note, and run the relevant build/tests. Record evidence as source-derived, executable-reference, emulator/original observation, or synthetic expectation. Source-derived expectations alone must not be called proven original-game equivalence. Maintain the managed runtime boundary, prepared-files-only runtime, immutable definitions/state separation, and original-mode isolation throughout.

## Recommended next queue

**Current completion (2026-10-27): ZOD-122 is complete**, the
[confirmed voluntary Move execution contract](architecture/zod-122-voluntary-move-contract.md).
The proposed additive MoveAlongPath method rebuilds the current certified path,
publishes real path-facing before Move/automatic completion, and binds receipts
to the fully settled snapshot. It preserves preview placement and legacy APIs,
rejects post-KO movement, and explicitly excludes post-move benefits/traps/pickups/
charge cancellation and scripted effects. Existing pending facing-only Fixed/Keep
completion and the authored first-KO terminal host remain separate boundaries.
Fresh clean read-only reference remains 11464943bbdbd2a0af022d90cb7f10455cf911df.
**396 Python tests pass** (18 new). Sixty vectors distinguish 26 source-derived
descriptions from 34 managed proposals; all 18 VME production cases remain
unexecuted. Two audits match 13 files / 33 functions, 82 historical artifacts and
24 managed baseline files. No C# changes/.NET rerun; last implementation validation
is 3,567 .NET / 378 Python. See [evidence](evidence/ai/voluntary-move-contract-v1.json).
**Recommended next: ZOD-123**, implement this additive confirmed path Move boundary
and execute VME-001..018. Full AI/candidate-clock/interface/general terrain remain
future work. Owner editor drafts stay excluded; editor UI and ZOD-106 stay deferred.

### ZOD-121 implementation checkpoint (historical)

**Current completion (2026-10-26): ZOD-121 is complete**, the opt-in
[certified flat ordinary movement query](behavior/flat-ordinary-movement.md).
It returns ordered physical destinations/residuals, AI origins including stay,
whole selected reverse-distance rows and optional exact adjacent path/packed
steps/final heading. Explicit stable-opponent/ordinary-ground certification,
source bytes, cost1 and whole-state geometry are checked. Any living actor can
query any phase; moved flags filter AI origins only. No command/clock/facing/RNG
mutation or legacy activation. Fresh clean decomp remains
11464943bbdbd2a0af022d90cb7f10455cf911df. Locked restore/zero-warning Release
build and **3,567 .NET / 378 Python tests pass** (152 new managed). All 18 FMP
cases and 24 source IDs execute in new evidence; the historical fixture remains
unchanged. Before/final audits match 15 files / 42 functions and two prepared
metadata records. Seventy-eight legacy/source artifacts are fingerprinted
unchanged. The owner's three untracked Common classes remain outside this change.
See [implementation evidence](evidence/ai/flat-movement-implementation-v1.json).
**Recommended next: ZOD-122**, specify an opt-in voluntary Move execution adapter,
including real heading before completion receipts and post-KO admission. Query
outputs currently grant no movement authority. General AI/search/candidate clock/
decision RNG/interface remain future work; editor UI and ZOD-106 stay deferred.

### ZOD-120 source/design checkpoint (historical)

**Current completion (2026-10-26): ZOD-120 is complete**, the
[flat ordinary AI movement/path contract](architecture/zod-120-flat-ai-movement-contract.md).
Living enemies block ordinary flat traversal, including horizontal jumps; legal
detours can exceed Manhattan distance. AI origins add stay, reverse target
distances clear unit occupancy, and exact path ties carry the previous backward
direction (initially East). Executed walking settles its final step heading;
AI simulation placement preserves facing. Existing managed movement APIs remain
unchanged. Fresh clean reference is 11464943bbdbd2a0af022d90cb7f10455cf911df.
**378 Python tests pass** (19 new), with 48 source-derived vectors / five families.
Two matching audits check 15 files / 42 functions, 68 preserved files, 21 managed
baseline files and two prepared metadata records. No C#/C/original execution or
.NET rerun; last implementation validation remains 3,415 .NET / 359 Python.
See [evidence](evidence/ai/flat-movement-contract-v1.json).
**Recommended next: ZOD-121**, a pure opt-in certified flat movement/range/path
query. All 18 FMP production cases remain unexecuted in the source artifact.
Execution rotation/receipt integration, candidate clock, full AI search/roster/
decision RNG/interface remain separate work. Editor and ZOD-106 stay deferred.

### ZOD-119 implementation checkpoint (historical)

**Current completion (2026-10-26): ZOD-119 is complete**, the opt-in
[bounded facing wrapper and closed-enemy chooser](behavior/turn-facing-implementation.md).
Explicit Wait applies one CT refund and facing atomically. Automatically ended
Move+Act turns issue a receipt after full combat/movement settlement, block
further commands and finalize facing without repeating CT/damage/RNG. Receipts
bind actor, revision, exact settled snapshot and lineage; same-facing completion
consumes authorization on the authoritative immutable state chain.
Fresh clean read-only reference remains 11464943bbdbd2a0af022d90cb7f10455cf911df.
Locked restore/zero-warning Release build and **3,415 .NET / 359 Python tests**
pass (144 new managed). All 18 FTF cases and 20 admitted source vector IDs execute.
Repeated source/history and legacy artifact audits match; four managed headless
runs preserve 14 sword-save and four Basic Skill files. Existing APIs/fixtures/
scenario/save/native paths remain unchanged. See [evidence](evidence/ai/turn-facing-implementation-v1.json).
**Recommended next: ZOD-120**, the bounded original flat AI movement/path
composition contract. Current Move still has prototype legality and no original
path heading. Full AI/search/roster/candidate-clock/decision RNG/interface remain
separate work; editor and ZOD-106 stay deferred.

### ZOD-118 source/design checkpoint (historical)

**Current completion (2026-10-26): ZOD-118 is complete**, the
[end-of-turn facing/completion contract](architecture/zod-118-turn-facing-contract.md).
Clock completion and facing publication are separate in the original, including
automatic Move+Act completion. The proposal adds an opt-in immutable wrapper with
an attributable pending-facing receipt, a barrier before the next command, atomic
WaitWithFacing and facing-only completion after an ended turn. Existing managed
Move does not publish original path heading; that limit is explicit.
Fresh clean read-only reference remains 11464943bbdbd2a0af022d90cb7f10455cf911df.
Seventy offline descriptions cover five families; 18 FTF production cases remain
unexecuted. Source outcomes, unsafe chooser admission limits and managed design
proposals are classified separately. **359 Python tests pass** (17 new);
repeated source/history audits are recorded in
[evidence](evidence/ai/turn-facing-contract-v1.json). No production C# or .NET
rerun; last full implementation validation remains 3,271 .NET / 342 Python.
**Recommended next: ZOD-119**, implement the opt-in bounded facing wrapper and
closed-enemy chooser. Full AI/roster/search/candidate clock/decision RNG and
original path/facing composition remain separate. Editor and ZOD-106 stay deferred.

### ZOD-117 implementation checkpoint (historical)

**Current completion (2026-10-26): ZOD-117 is complete**, a pure
[ordinary-sword AI formula query](behavior/sword-ai-formula-projection.md).
It returns normal damage, separate accuracy, projected HP and advanced RNG;
certain evasion is a supported zero-accuracy outcome after four draws.
Execution Attack and all seven managed baseline files remain byte-identical.
Fresh clean read-only reference remains 11464943bbdbd2a0af022d90cb7f10455cf911df.
Locked restore/zero-warning Release build, **3,271 .NET / 342 Python tests** pass
(227 new managed). Seven unchanged source vectors execute; repeated source/history
and legacy artifact audits match. Four managed headless runs preserve 14 sword-save
and four Basic Skill files. See [evidence](evidence/ai/sword-formula-implementation-v1.json).
The full AI/interface and all 20 AIC production cases remain unimplemented;
this is a formula-level draw budget. **Recommended next: ZOD-118**, the bounded
end-of-turn facing transition contract. Search/session/roster/candidate-clock and
movement composition remain separate dependencies. Editor work and ZOD-106 remain
deferred.

### ZOD-116 source/interface checkpoint (historical)

**Current completion (2026-10-26): ZOD-116 is complete**, a
[source-stage/helper map and proposed replaceable C# contract](architecture/zod-116-ai-decision-contract.md).
The original AI projects candidates, ranks them, consumes decision RNG and emits
one command at a time; source evidence distinguishes execution formulas from AI
projections, local tie rules, conditional randomization, post-Act replanning and
late Wait facing. No AI/interface implementation or whole-encounter parity claim.
Fifty helper/stage vectors cover eight families; 20 AIC production cases remain
unexecuted. Fresh clean read-only reference remains
11464943bbdbd2a0af022d90cb7f10455cf911df. Final offline audits repeat across
16 files/71 functions, verified Attack metadata, 37 historical and seven managed
baseline files. **342 Python tests pass** (13 new); no .NET rerun.
Last full implementation result remains 3,044 .NET / 329 Python.
**Recommended next: ZOD-117**, a pure formula-level ordinary-sword AI projection.
Roster/policy/session RNG progress, original search/movement composition and
end-of-turn facing remain required before a complete selector. Editor work and
ZOD-106 battle checkpoints remain deferred. See [evidence](evidence/ai/decision-contract-v1.json).

### ZOD-115 implementation checkpoint (historical)

**Current completion (2026-10-26): ZOD-115 is complete.** The
[explicit flat sword profile](behavior/flat-sword-knockback-implementation.md)
admits interior attacks on certified open dry flat ground with exactly two units.
Sword criticals push one tile when nonlethal and in bounds; both settled facings
and target choices/CT remain. One damage core preserves the default edge-only API,
four/six/seven draws and historical fixture/save/scenario behavior.
Locked restore and zero-warning Release build pass; **3,044 .NET / 329 Python**
tests pass (240 new managed). All 35 description vectors and SKB-001..020 execute.
Two source/history audits and two legacy artifact audits match. Four managed
headless CLI processes repeat 14 sword-save files and four Basic Skill files
against historical outputs with exact state/events/RNG and continuations.
No native/original launch, runtime C, content/native/save activation or editor work.
See [evidence](evidence/combat/sword-knockback-implementation-v1.json).
**Recommended next: ZOD-116**, the bounded original-AI Move/Attack/Wait source and
replaceable-interface contract. Record missing inputs/rules before translation;
do not substitute an invented heuristic. ZOD-106 and editor work remain deferred.

### ZOD-114 source-contract checkpoint (historical)

**Current completion (2026-10-26): ZOD-114 is complete**, a
[flat sword knockback source contract](architecture/zod-114-flat-sword-knockback.md).
Fresh clean read-only reference 11464943bbdbd2a0af022d90cb7f10455cf911df confirms
that ordinary sword criticals directly attempt displacement, without Brave rolls.
Miss/normal/critical retain four/six/seven draws; lethal damage cancels a pending
push and zero damage can still push. Settled facings and target choices/CT remain.
Two identical offline audits verify 17 reference files/41 functions, 35 vectors
and 42 preserved files; **329 Python tests pass** (12 new). No production change
or original/native launch; last full .NET result remains 2,804 from ZOD-113.
All SKB-001..020 production implementation cases remain unexecuted. See
[evidence](evidence/combat/sword-knockback-contract-v1.json).
**Recommended next: ZOD-115**, the strict opt-in pure flat-sword extension.
Keep the old edge-only entry point and history. Bounded original-AI
Move/Attack/Wait source/interface mapping follows validated attack support;
editor work and ZOD-106 battle checkpoints remain deferred.

### ZOD-113 implementation checkpoint (historical)

**Current completion (2026-10-26): ZOD-113 is complete.** The pure sword command
now retains the living actor's incoming settled facing. Current-action damage,
events, RNG and guards stay intact; later facing-based evade now follows the
corrected contract. Nine new-policy fixtures execute; nine old v3 fixtures remain
byte-identical and validate as superseded history. The supersession manifest
identifies all replacements without normalizing expected state in the runner.
Locked isolated restore/build pass with zero warnings; **2,804 .NET / 317 Python**
tests pass (81 new managed, six Python). All SFC-001..020 pass, including 32
cardinal vectors, draw budgets, action orders, corrected evasion/prefix replay,
13 existing save boundaries and six new fresh-definition save continuations.
Fourteen sword headless files match a retained pre-correction build; four Basic
Skill files match historical ZOD-110 output. Both repeat exactly; two independent
audits match and preserve historical content/captures. No new native/original
execution, format, profile version or editor work. ZOD-106 remains deferred.
See [implementation](behavior/sword-settled-facing-implementation.md) and
[evidence](evidence/combat/sword-facing-implementation-v1.json).
**Recommended next: ZOD-114**, a bounded source contract for flat sword critical
knockback, investigating removal of the current edge-only sword restriction.

### ZOD-112 source-contract checkpoint (historical)

**Current completion (2026-10-26): ZOD-112 is complete.** The bounded sword
[source contract](architecture/zod-112-sword-settled-facing.md) confirms temporary
actor turning followed by living-unit restoration at the settled boundary. Fresh
clean read-only decomp remains 11464943bbdbd2a0af022d90cb7f10455cf911df. Two final
offline audits repeat exactly across 12 source files/29 functions, 32 cardinal
vectors, boundary/restore branches and two subsequent-evasion continuations.
Nine historical sword fixtures, two saves, seven combat evidence files and all
inspected managed code are unchanged. Eight fixtures explicitly contain the old
facing gap; preserve them as superseded historical evidence when implementing.
**Next: ZOD-113 is Ready**, the pure correction and explicit fixture-policy
migration. All 20 implementation cases remain unexecuted. No production/native
change or test-suite rerun; last implementation result is 2,723 .NET / 311 Python.
Existing save fields suffice; no new save format or battle-checkpoint feature.
Editor and ZOD-106 remain deferred. See [evidence](evidence/combat/sword-facing-contract-v1.json).

### ZOD-111 owner approval checkpoint (historical)

**Owner approval (2026-10-26): ZOD-111 is complete.** After receiving the launch
guide and capture gallery, the owner replied, "This looks good." This closes
TST-034 by owner acceptance; individual physical-input steps were not separately
reported or agent-observed. Implementation/captures are unchanged; last validation
remains **2,723 .NET / 311 Python** tests, not rerun for this documentation update.
**Recommended next: ZOD-112**, a bounded source contract for the known sword
settled-facing discrepancy before further combat expansion. Editor and ZOD-106
remain deferred.

### ZOD-111 implementation checkpoint (historical)

**Implementation checkpoint (2026-10-26): ZOD-111 awaited owner review.** Prepared
Basic Skill Windows controls, range/availability, select/confirm/cancel and compact
map view pass TST-029..033. All **2,723 .NET / 311 Python** tests pass; zero-warning
isolated Release build. Exactly two native checks pass 121 inputs each with exact
headless state/events/RNG, two journal replays, resize/focus baselining, KO/restart.
Four prepared and 29 native files repeat; independent audits and image inspection
pass. **Next: TST-034 visual/physical approval**, see the
[launch/review guide](development/basic-skill-battle-controls.md). Editor and
ZOD-106 remain deferred. Do not infer owner approval from programmed checks.

### ZOD-110 completion checkpoint (historical)

**Current completion (2026-10-26): ZOD-110 is complete.** Strict targeted-effect
content/projection and verified Throw Stone import preserve historical metadata
IDs and bytes. The combined authored Basic Skill scenario proves PA 5→6, ranged
damage 6/12, knockback, first KO, restart and all terminal guards. Both two-file
imports and four-file headless outputs repeat exactly across 46 commands and 47
fresh-definition continuations. Independent audit checks six selected/22 historical
records, six preserved old files, every ranged damage/push/RNG delta and final RNG.
All **2,700 .NET / 305 Python tests** pass (92 new managed, six Python).
See the [guide](development/basic-skill-content.md) and
[evidence](evidence/combat/throw-stone-content-v1.json).
**Next: ZOD-111 is Ready**, prepared Windows target controls and owner review.
Ability-save/restore guards cover both profiles before file I/O; ZOD-106 and
editor work remain deferred. No new native/original execution or save format.

**Implementation checkpoint (2026-10-26): ZOD-109 is complete.** Pure Throw Stone now has
an explicit targeted profile/grants/command, named arithmetic, physical evasion,
current-PA damage and same-height one-tile knockback with exact seven/four draws.
It preserves settled facings and the target's voluntary choices, handles
edge/lethal suppression and rejects invalid/exhausted actions atomically.
All **2,608 .NET / 299 Python tests** pass (149 new managed cases), covering
TST-001..020 and prior profiles. See the
[behavior/API guide](behavior/throw-stone-implementation.md) and
[executed evidence](evidence/combat/throw-stone-implementation-v1.json).
**Next: ZOD-110 is Ready**, content/import and combined authored Basic Skill
scenario; ZOD-111 Windows controls waits for it. No new native launch, original
execution, content schema, save format or UI change. ZOD-106 and editor work
remain deferred.

**Owner approval (2026-10-26): ZOD-107 is complete.** After receiving the launch
guide and gallery, the owner replied, "Looks good. Let's keep going." This closes
ACC-032 by owner approval; individual physical-input steps were not separately
reported or agent-observed. The validated implementation/captures are unchanged.
The next bounded source investigation, ZOD-108, is now complete below. Keep
ZOD-106 and editor work deferred.

**Next gameplay lane: Throw Stone under ZOD-038. ZOD-108 is complete.**
The [source/data contract](architecture/zod-108-throw-stone-action.md) verifies
Squire/Basic Skill slot 2, ability 148, range 4 and formula 0x37. On a clear flat
two-unit scene it combines physical evasion, PA-or-double-PA damage and a
Brave-based one-tile knockback attempt: seven draws on a hit, four on a miss.
Two offline reports repeat exactly, rechecking 22 imported records, six selected
rows and 24 pinned source files. This is source-derived evidence, not original
execution or a managed action. The [34 acceptance cases](evidence/combat/throw-stone-contract-v1.json)
were all unexecuted at that contract checkpoint. The then-selected next task was
ZOD-109, the pure targeted-action proof;
then ZOD-110 content/import/scenario and ZOD-111 prepared Windows controls.
Battle checkpoints, editor polish and other documented deferrals stay deferred.
The trace also identifies the old authored sword's persistent attack facing as a
settled-state fidelity gap; record it under ZOD-038 without silently changing
legacy fixtures. Last implementation tests remain 2,459 .NET / 299 Python,
not rerun for this source/documentation milestone.

**Implementation checkpoint (2026-10-26): ZOD-107 implemented.** A
separately prepared authored Windows battle exposes C / Accumulate, current PA,
availability and success/cap feedback. All **2,459 .NET / 299 Python tests** pass.
Two finite native checks match exactly across 560 inputs and 15 captures each,
including legal repeat uses to PA99, zero resolution RNG draws, in-memory replay,
resize, first KO and restart to PA5. See the
[guide/review steps](development/accumulate-battle-controls.md) and
[evidence](evidence/combat/accumulate-window-v1.json).
The owner subsequently approved the bounded review above. Original-game
presentation equivalence remains unestablished.

**Owner sequencing (2026-10-26): proceed with ZOD-107; defer ZOD-106.** Battle
checkpoint persistence is a development proof, not a requirement for using
Accumulate or for original campaign saves. Retain the ability-profile save guard.
ZOD-106 waits for a concrete debugging/checkpoint need or explicitly selected
battle suspension. ZOD-107 uses in-memory command/replay validation and the
[pre-execution native plan](development/accumulate-battle-controls.md); its
save-continuation clause is deferred with ZOD-106. Editor work and other documented
deferrals remain in effect.

**Latest completion (2026-10-26): ZOD-105 is complete.** The bounded Accumulate
content/action/scenario proof adds an explicit immediate self PA+1 companion,
validated learned grants and pure atomic resolution. PA caps at 99; a capped use
still consumes Act. Later sword damage uses boosted PA with unchanged draw order.
All **2,438 .NET / 293 Python tests** pass (89 new managed cases); ACC-001..020
have executed coverage. Two verified imports repeat exactly; independent audit
checks six source records and four unchanged historical import files. Existing
sword-only fixtures, journals and saves pass. Ability-enabled save/restore is
explicitly rejected until its current PA can be preserved. See the
[behavior/workflow](behavior/accumulate-implementation.md) and
[evidence](evidence/combat/accumulate-implementation-v1.json).
The original next-task recommendation (ZOD-106 before ZOD-107) is superseded by
the owner sequencing above.
No Windows/editor/Engine production or package-lock changes; original execution,
visual approval and broader combat fidelity remain separate. Deferred work stays deferred.

**Selected gameplay milestone (2026-10-26): Accumulate under ZOD-038.**
**ZOD-104 is complete** as a bounded source/data investigation and contract:
verified generic Squire/Basic Skill membership, immediate self PA+1, cap 99,
Act consumption and zero resolution draws in the closed supported profile.
Two final offline reports repeat exactly, comparing 22 historical imported
records plus menu/status rows and fingerprinting 17 source files. This is not
original-game execution or a managed ability implementation. The
[contract](architecture/zod-104-accumulate-action.md) and
[32 unexecuted acceptance cases](evidence/combat/accumulate-contract-v1.json)
identify current PA persistence as a prerequisite: existing battle saves omit PA.
**Next: ZOD-105**, the pure content/action/scenario proof; then **ZOD-106** for
save/replay continuation and **ZOD-107** for the existing Windows battle controls.
Last implementation validation remains **2,349 .NET / 293 Python tests**, not
rerun for this investigation. Editor work and other documented deferrals remain.

**Owner direction (2026-10-26): limit further editor work.** A brief live
inspection of the owner's loaded FFTPatcher v0.497.0 PSX session confirms useful
Jobs / Skill Sets / Abilities separation, grouped fields and relationship links;
see the [desktop reference](development/fftpatcher-desktop-reference.md). Further
editor UI polish and owner review are deferred until a concrete gameplay/content
milestone needs that capability or the owner requests it. ZOD-047 is implemented
but unapproved; M3 remains open. This review does not block unrelated gameplay
development. **Next:** select a bounded gameplay/source/data milestone from the
remaining backlog; do not automatically start another editor revision or broad
ZOD-048. Existing deferred work stays deferred.

**Latest revision (2026-10-26): ZOD-047 separates Jobs and Abilities.** The owner
found the first UI rough and requested FFTPatcher-inspired separation; no visual
or interactive approval was given. The revised Windows editor has independent
search/list pages, job-reference navigation, routed field validation, a separate
movement-preview page and shared package actions/details. All **2,349 .NET / 293
Python tests** pass; two final native runs repeat **18 files**, with 16 images
and 22 checks each. One retained discovery run prompted numeric/flag polish.
Review `scratch/editor-workspaces-047-review/review.html` and use the
[current launch/manual guide](development/editor-workspaces.md). ZOD-047/M3 still
await owner approval. This keeps the existing copied-package scope; broader
ZOD-048 and deferred work remain separate. The previous editor process and any
owner edits were preserved; this revision has a separate validated build folder.

**Latest implementation (2026-10-26): ZOD-047 is implemented; owner review pending.**
A separate Windows Forms editor searches/inspects prepared baseline jobs and
linked abilities, copies one dependency graph into an authored namespace, edits
Move/Jump/JP, shares package validation/export and previews existing movement.
All **2,346 .NET / 291 Python tests** pass. One discovery run prompted a label
layout correction; two final finite native runs repeat **11 files**, including
nine native control images each. Source metadata/hash, reference order and
failed-export preservation pass independent audit. Review
`scratch/editor-047-review/review.html`, then use the
[launch/manual guide](development/job-ability-editor.md). Physical controls,
native dialogs and owner visual/manual approval remain pending. Broader ZOD-048
authoring and all documented deferred work stay deferred; do not mark M3 complete
before this bounded review closes.

**Latest completion (2026-10-26): ZOD-069 is complete.** Native Save/Load adds two slot pages, validated
primary/backup details, Cancel-first overwrite/discard/recovery, success/error
and explicit refresh states. All **2,317 .NET / 287 Python tests** pass.
Two finite native runs execute 140 samples/22 captures each, with complete
CPU/GPU/replay matches and **51 exact repeated UI/storage files**. The owner
approved `scratch/world-slots-069-review/review.html`: "This appears to be fine."
After the [launch/manual guide](development/world-save-load-ui.md), the owner
separately confirmed the interactive slot check: "It's good." Record this as
owner-reported manual observation. No implementation suites or agent-run graphics
checks were repeated for approval. Prior navigation approval is retained separately;
original campaign/save availability remains open. **Next:** select the next
bounded milestone from the remaining backlog, breaking down an epic if needed;
no follow-on implementation ticket has been selected. Deferred work stays deferred.

**Latest completion (2026-10-26): ZOD-103 is complete.** The owner confirmed the
manual keyboard/mouse/controller, resize, native close and focus checklist:
"It's good." The authored Windows world menu passes two
finite native runs, 51 samples/13 images each, with 16 exact repeated files and
full CPU/GPU/headless matches. All **2,300 .NET / 284 Python tests** pass.
The owner approved `scratch/world-menu-103-review/review.html`: "Looks good."
**Next: ZOD-069 is Ready** for native Save/Load slot navigation through the
existing checkpoint service. [Launch/check guide](development/windows-world-navigation.md).
No implementation suites or agent-run graphics checks were repeated for the
owner's manual report. Broader device support and original UI fidelity remain open.

**Latest completion (2026-10-26): ZOD-102 is complete.** The authored world-menu
application model proves Home/New/Resume, read-only Party, existing travel,
parent focus and safe New/Exit confirmation without storage operations. All
2,285 managed tests pass (18 new). **Next: ZOD-103**, the bounded Windows host
and device/visual checks. ZOD-069 remains Waiting on that child, rather than the
entire ZOD-042 epic. Original UI fidelity and campaign operations remain open.

**Latest completion (2026-10-26): ZOD-068 is complete.** The synthetic idle-world
Save/Load service validates fresh-definition restore/continuation, migration,
slots, stale/concurrent writers, safe publishing and explicit recovery. All
2,267 .NET / 281 Python tests pass; repeated headless checks match 14 files.
ZOD-069 still waits for the ZOD-103 navigation host before adding slot UI;
original campaign/RNG/card support remains separate.

**Authored scene completion (2026-10-26):** **ZOD-098 is complete**. The owner approved
`scratch/dark-screen-intro-098-review/review.html`: "Looks good to me."
This closes the authored composition/battle-switch review, not original startup
or duration equivalence. The implementation validation recorded below is unchanged.

**Owner-directed architecture interlude (2026-10-25):** **ZOD-099 is complete**.
Engine and framework replacement are now explicit goals;
[ADR 0006](adr/0006-replaceable-engine-and-framework-contracts.md) records the
replacement goal and future proof requirements. The
[audit](architecture/zod-099-replacement-boundary-audit.md) records current coupling.
Owner clarification defers ZOD-100/101: it is too early to select public contracts,
assembly splits or replacement proofs without concrete usage. They are not in
the active queue and do not block prepared introduction work.

**Current focus (2026-10-23):** ZOD-023 completes the bounded M1 headless start-to-result scenario;
ZOD-025 adds the synthetic Windows view with live GPU/resize/close checks and explicit input/DPI limits.
ZOD-027 traces the first ordinary Chocobo idle pose; ZOD-028 prepares and loads its static
render input with verified local files. ZOD-029 selects MAP003's frozen day/dry base scene
and traces its geometry, texture/palette and terrain inputs. ZOD-030 now prepares and
loads that slice, retaining native terrain heights and both layers. ZOD-031 renders
the prepared map and pose together in a fixed inspection scene with declared lighting/depth
approximations. ZOD-032 now makes the supported synthetic M1 battle playable with
selection, legal highlights, Move/Attack/Wait, manual next-turn and restart controls.
The static MAP003 scene does not expand that battle's supported terrain/combat domain.
ZOD-034 implements independent Windows CI, reports and self-contained folder checks;
remote CI execution and a clean interactive machine remain validation gates.
ZOD-035 completes the bounded save/load proof with explicit DTOs, active-turn/RNG
restoration, synthetic migration and safe replacement. ZOD-026 now independently
confirms the Chocobo attack decoder digest by executing verified original MIPS
instructions in the optional developer harness. Original rendered scene captures
remain a separate gate for ZOD-033. ZOD-040 now maps the script families and
selects a bounded conditional event; ZOD-045 adds the [coverage inventory](coverage/README.md).
ZOD-064 now prepares/loads that synthetic event with strict capability validation;
locked restore, zero-warning Release build and all 1,012 tests pass. ZOD-067 now
traces campaign save families, checkpoints and the two unsaved RNG lifecycles.
ZOD-065 now executes the bounded conditional event with immutable instruction
state/traces, replay, explicit arithmetic limits and atomic rejection on failures.
Locked restore, zero-warning Release build and all 1,057 tests pass. ZOD-066 now
traces verified common BattleStart event 401, its fallback activation and EventEnd
return to battle. Original observations and its required runtime capabilities are
still absent. ZOD-071 now adds a separate timed synthetic profile with explicit
scheduler ticks, immutable waits, exact resume ordering and atomic failures.
Locked restore, zero-warning Release build and all 1,105 tests pass. ZOD-072 now
selects a source-traced scene task ledger/barrier contract. ZOD-073 implements its
pure ledger/single-consumer proof with all 17 specification regressions plus 40
boundary cases; locked restore, zero-warning Release build and all 1,162 tests pass.
ZOD-074 now integrates E5 in a separate BATTLE synthetic profile with atomic
ledger/VM state and shared ticks; all 1,230 tests pass (68 new). Next event step:
ZOD-075 selects condition-8 banner progress after asset readiness, with exact
reveal/hold/hide/exit-yield and Circle boundaries plus 24 unexecuted specification
cases. ZOD-076 now implements the pure logical banner driver/atomic scene adapter,
with all 24 contract regressions and 47 additional boundary cases. Locked restore,
zero-warning Release build and all 1,302 tests pass (72 new including an opcode-78
rejection case). ZOD-077 now traces verified banner-0 pixels, palette bank 4,
six shaded pieces, UV wrap/screen origin and the preloaded readiness boundary.
ZOD-078 now prepares and strictly loads that six-piece artwork, with a pure
accepted-snapshot projection and RGB8 CPU preview. All 20 authored expectations
have regressions; locked restore, zero-warning Release build and all 1,388 tests
pass (86 new). Two preparations and two finite Windows GPU capture sets match;
prepared-only loading uses no original input/reference paths. Original loading
latency, opcode-78 admission and visual equivalence remain separate. The next
scene capability needs a separately selected source-traced milestone.
ZOD-080 now selects [bounded opcode-78 admission and update order](architecture/zod-080-banner-opcode-admission.md).
Retained original states show that the parent observes banner termination on the
following update. The existing synthetic effect-first adapter stays unchanged.
Next implement **ZOD-081's ordered Core transaction**, then **ZOD-082's separate
authored preloaded profile**. The 28 new contract expectations are specification-only;
no opcode is admitted yet. ZOD-079 remains deferred.
ZOD-068's synthetic world-checkpoint service is complete; ZOD-069's native
Save/Load UI still waits on minimal client navigation. Original campaign support remains open.
M2 is not complete yet.
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

**Scope:** Classify every current `Zodiac.Framework` type as FFT-owned, reusable candidate, or unresolved. Inspect its actual consumers in Content, tools, client, and tests. Evaluate one small extraction that performs useful work; use the existing map/movement transition as the concrete example.

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

**Scope:** Implement exactly the extraction justified by ZOD-001 in `Tessera.Engine`, with an engine test project. Prefer the smallest coherent functionality over a general framework of empty interfaces.

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

**Type:** Spike · **Priority:** P2 · **Status:** Complete (bounded executable decoder oracle) · **Depends on:** None

**Result:** [Original-instruction sprite capture](development/sprite-executable-reference.md)
(2026-10-23) executes the verified original decoder/helper with Unicorn 2.1.4,
confirming the existing 25,600-byte Chocobo attack-image digest. Two captures/reports
match; wrong-hash and existing-output checks pass; managed CLI verification passes.
Original input/code/output remain under scratch with committed provenance only.
No full game was booted and no palette/frame/camera/timing/visual equivalence is
claimed. This completes the ticket's executable-reference alternative; ZOD-033
still needs matched original rendered captures for original-scene comparison.
No .NET runtime changes; the last full suite remains 1,388 passing tests.

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

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-027

**Result:** [Preparation command, prepared format and validation](development/prepared-sprite-pose.md)
(2026-10-23). Bounded SHP/SEQ readers and ordinary-sheet decoding prepare Chocobo primary frame 9,
bank 0, wait 6 as a static six-piece pose plus RGBA atlas. Source inputs are length/hash verified;
fresh scratch exports repeat byte-for-byte. Content/Client load only manifest-verified prepared files.
No animation playback or prepared scene rendering yet. Locked restore, Release build and all 720 tests
pass, including 64 new synthetic parser/contract/CLI cases. Source evidence remains pinned to
`11464943bbdbd2a0af022d90cb7f10455cf911df`; no independent original-rendering equivalence is claimed.

**Selected slice:** Follow ZOD-027's [bounded implementation recommendation](architecture/zod-027-chocobo-idle-pose.md#smallest-zod-028-implementation):
ordinary Chocobo primary frame 9 with bank 0; optional sequence 6 idle loop only.
Keep attack/EVTCHR/SP2, special part splitting and general animation interpretation outside this first converter.

**Scope:** Implement the selected metadata parsers/converter and decoded render input for one pose or short sequence.

**Acceptance criteria:** Synthetic/truncated/out-of-range records have meaningful tests; frame pieces, palette references, transforms, and durations match the trace; local prepared output is repeatable and manifest-addressable; runtime reads prepared files without BIN/ISO access. Keep the broad animation catalog outside this ticket.

### ZOD-029 — Trace one battle map's geometry, terrain, and texture inputs

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** None

**Result:** [MAP003 source map, alignment checkpoint and converter scope](architecture/zod-029-map003-source-map.md)
(2026-10-23), reference `11464943bbdbd2a0af022d90cb7f10455cf911df`. Select day/dry,
default arrangement, frozen presentation. Verified GNS selects MAP003.8 texture and MAP003.9
primary mesh; terrain is 9x13 in two fixed-stride source layers. A four-cell rug checkpoint
matches terrain height to exact quad positions/materials/normals/flags. State/animation, camera,
lighting and original-rendering limits are explicit. No parser/renderer implementation or new test run.

**Scope:** Select one small battle map and trace its file selection, geometry/texture/palette data, tile heights/slopes/layers, coordinates, and minimum scene setup. Distinguish visual geometry from movement terrain.

**Acceptance criteria:** Record a file/structure/dependency map and the edition/decomp revision; identify one region suitable for a comparison fixture and the required converter scope; state unsupported weather, alternate layouts, animation, or effects. Do not assume map visuals alone determine traversability.

### ZOD-030 — Prepare and load the selected map slice

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-029

**Result:** [Prepared MAP003 pipeline and validation](development/prepared-map-slice.md)
(2026-10-23). Bounded offline GNS/mesh/indexed-texture preparation publishes a hash-verified
three-file package. Content loads immutable geometry/materials/palettes, frozen presentation
metadata and both dense terrain layers; Core receives normalized terrain with native heights.
Client's `--map` check reads prepared files only. Two verified local conversions have identical
hashes and match the four-cell source checkpoint. Locked restore, zero-warning Release build
and all 796 tests pass (76 new cases). No prepared scene rendering or combat-domain expansion.

**Selected slice:** Follow ZOD-029's [preparation recommendation](architecture/zod-029-map003-source-map.md#minimum-zod-030-preparation-and-zod-031-scene-setup):
MAP003 base day/dry geometry, per-polygon palette/page references, frozen presentation metadata,
and both valid terrain layers. Preserve native heights; current M1 combat's height-10 restriction
must not cause source terrain to be rewritten. Alternate states and animation playback are deferred.

**Scope:** Parse/convert the traced map inputs into prepared geometry/textures and FFT-owned simulation terrain definitions.

**Acceptance criteria:** Synthetic format tests cover bounds and malformed offsets/counts; terrain coordinates/heights/layers match selected source records; conversion preserves provenance and is repeatable; prepared outputs load with the BIN unavailable. The source parser remains an offline tool, not part of simulation.

### ZOD-031 — Render one prepared map and unit pose

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-025, ZOD-028, ZOD-030

**Result:** [Fixed prepared scene, source comparison and limits](development/prepared-scene.md)
(2026-10-23). MAP003 and the held Chocobo pose render from prepared files with native
terrain placement, per-polygon palettes, point cutout, a fixed authored camera and modern
depth. Yaw visibility, coordinate handedness, strip coverage and sprite offsets/order have
source-derived/synthetic checks. Neutral lighting and original-ordering limits are explicit;
independent original/emulator comparison remains unavailable pending ZOD-026/033.
Two Windows GPU runs produce identical images and unchanged input hashes across 120 frames.
Locked restore, zero-warning Release build and all 833 tests pass (37 new cases).

**Scope:** Compose the selected map and sprite using fixed camera/pose inputs; implement only the selected palette, sampling, blend, and ordering behavior.

**Acceptance criteria:** A fixed scene renders repeatably; terrain/unit placement agrees with simulation coordinates; a documented original/reference comparison records differences and confidence using ZOD-026 where available. Unsupported PS1 effects are explicitly listed. Runtime uses prepared files and no importer or reference C code.

### ZOD-032 — Make the bounded battle playable through input and UI

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-023, ZOD-031

**Result:** [Playable two-unit battle, controls and evidence](development/playable-battle.md)
(2026-10-23). `--battle` supports mouse/keyboard selection, legal move/target highlights,
Move/Attack/Wait/Advance, HP/turn/choice/rejection/outcome display and restart. Client
publishes only accepted Core scenario results; discarded pure probes supply highlights.
Programmed Windows UI input, including a mid-battle resize and two rejected attempts,
matches both session-journal replay and the original twelve-command headless script.
Two final runs have identical checkpoint images and simulation receipts. Locked restore,
zero-warning Release build and all 868 tests pass (35 new cases). Physical-device timing,
controllers, original menu fidelity and prepared MAP003 combat integration remain separate.

**Scope:** Select a unit, show legal destinations/targets, issue supported move/attack/wait commands, and display active turn, HP, rejections, and terminal result.

**Acceptance criteria:** The M1 scenario can be completed in the Windows client; simulation validates every command; the UI consumes snapshots/events without writing battle state; the same commands produce the same headless result. Keep original menu fidelity and complete controller navigation in later work.

### ZOD-033 — Add deterministic visual comparison checkpoints

**Type:** Story · **Priority:** P2 · **Status:** Partial — synthetic comparison and bounded original banner diagnostic implemented; fidelity/timing validation pending · **Depends on:** ZOD-026, ZOD-031

**Result (2026-10-24):** [Visual comparison workflow](development/visual-comparison-checkpoints.md)
adds optional headless authored/same-environment PNG comparisons, strict capture/
checkpoint bindings, regional RGBA metrics, explicit zero-tolerance synthetic
plans and a finite Windows receipt adapter. All 22 Python regressions pass,
including deliberately different pixels/ticks/state/environment, malformed inputs,
wrong hashes and output preservation. Two fresh 120-frame synthetic GPU captures
and two reports match exactly. The adapter's simulation binding covers the explicit
turn projection; wider state remains a future capture contract. No .NET source,
package graph, simulation/event/save behavior or original assets changed; the
earlier full managed validation remains 1,388 tests. A [private RetroArch/Beetle PSX
profile](development/retroarch-reference-capture.md) is now prepared and boot-checked:
two raw introductory captures match, installed settings are preserved, and all 30
Python tests pass (8 new). The owner subsequently captured the target banner and a
state with a matching thumbnail; two four-frame restores produce identical banner
scene images. [Checkpoint metadata](evidence/visual/original-banner-state-v1.json)
records this bounded observation. The [first observed banner comparison](development/observed-general-banner-comparison.md)
now declares the measured border mapping and a full-visible artwork anchor, with
separate raw/background-sensitive and managed-background-invariant diagnostics.
Two reports/review pages match; all 52 Python tests pass (22 new). Human inspection
finds consistent apparent placement; strict equality fails at 1,460/1,577 sampled
core pixels, with the original equal or darker by up to 16 RGB levels. No threshold
was relaxed or runtime changed. Exact original/managed timing/context alignment,
native draw-state provenance, justified fidelity thresholds and broader comparative
human review remain required before completion. The subsequent
[color precision study](architecture/general-banner-native-color-study.md) reads
the frozen GPU's dtd=0 and displayed VRAM, then exactly matches all 1,577 selected
samples with source-informed five-bit gradient/material/scanout arithmetic.
Two reports match; all 66 Python tests pass (14 new). No runtime policy changed;
the installed DLL's source revision and the banner-time draw-mode writer remain
unverified. Select a bounded precision implementation with explicit state before
changing the modern preview; timing and broader fidelity remain open.

**Five-bit follow-up:** The [bounded Client profile](development/five-bit-general-banner.md)
now supplies explicit per-draw dither/origin state, with opt-in CLI selection and
unchanged RGB8 default output. Frozen RAM chain reconstruction identifies the
REQUIRE dark-screen mode immediately before all six banner packets. Two native
GPU capture sets repeat exactly and match all 1,577 original foreground samples
at zero tolerance; dither-on retains separate state/color differences. Fresh
locked restore, zero-warning Release build, 1,418 .NET tests (30 new) and 81 Python
tests (15 new) pass. Reveal/hide timing, original background-sensitive blending,
general rasterization and full scene/event integration remain open.

**Continuation follow-up:** [Original saved-task observations](development/original-banner-continuation.md)
identify the existing full-visible state as reveal iteration 18, then reproduce
late reveal, sampled hold boundaries, all ten hide iterations and natural exit.
Two 17-checkpoint no-input capture sets repeat PNGs, exit states and submitted
geometry. Eighteen metadata-only managed replay cases match phase/counters/crops;
1,436 .NET and 92 Python tests pass with a zero-warning Release build. The observed
next task's slot reuse rejects. Full reveal onset/loading, physical Circle input, event identity,
pixel timing and background-sensitive blend/coverage comparisons remain open;
ZOD-033 remains partial. No runtime policy or original opcode admission changes.
The owner accepted the continuation gallery's visual appearance on 2026-10-24;
this does not close the remaining timing/input/rendering gaps.

**Background blend follow-up:** The [controlled background comparison](development/general-banner-background-blending.md)
now uses a scratch-only palette-transparent state copy at the full-visible anchor.
All 28 non-GPU subsystems/GPU registers and all 58,312 outside-footprint pixels
remain unchanged. Actual C# composition over the recovered background matches all
3,128 affected pixels, including 1,551 previously excluded background-sensitive
samples, at zero tolerance. Two capture sets repeat; all 16 default checker GPU
capture files keep historical bytes. Locked restores, zero-warning builds, 1,450
.NET tests (14 new) and 104 Python tests (12 new) pass. The owner confirmed the
original and C# views look identical on 2026-10-24. Partial reveal/hide pixels and framebuffer correspondence,
early reveal/loading/input, event identity and general scene rendering remain open.
The altered-state control is explicitly separate from unmodified retail evidence;
ZOD-033 remains partial. No automatic commit.

**Displayed hide follow-up:** [Source-bound hide comparison](development/displayed-banner-hide.md)
now identifies the pre-submission VSync stack and two-buffer order, binds each
displayed surface to a hash-bound earlier submission, and matches entire raw PNG
crops to displayed VRAM. Actual C# composition matches all 6,917 affected samples
across four visible hide anchors plus the fully hidden background-preservation
frame. The controlled probes retain all 28 non-GPU subsystem/GPU-register states;
two runs repeat. No crop/lag fitting or general frame-lag claim. Locked restores,
zero-warning builds, 1,450 .NET and 116 Python tests (12 new) pass. The owner
approved the displayed hide gallery on 2026-10-24. Early reveal/loading, physical Circle input,
event identity and broader scene rendering remain open; ZOD-033 stays partial.

**Earlier anchor received:** The owner's [new pre-banner state](evidence/visual/original-pre-banner-anchor-v1.json)
is frozen separately and advances without input to the selected reveal, hold and
hide task. Two sampled hold restores repeat PNG and exit-state bytes. Initial
overlay/partial-packet guards remain closed; next trace early reveal/readiness
before extending accepted observations. No new pixel, loading-duration, physical
input or event-identity claim; the original reference files remain ignored.

**Early reveal/readiness follow-up:** The [bounded diagnostic](development/early-banner-reveal.md)
now classifies four loader yields and the first reveal yield, explains the earlier
three-piece packet set, and matches sixteen original counter/crop observations
with managed replay. Actual C# composition matches nine of eleven displayed
frames; reveal 4/13 retain 21/14 blue-channel pixel differences of +8 original
minus C#. Source/probe isolation, scanout, geometry and C#/Python model checks
pass; two runs repeat 107 selected artifacts. No tolerance or runtime renderer
change. Locked restores, zero-warning builds, 1,466 .NET and 130 Python tests pass.
Next trace color interpolation/triangle initialization at the two 24-row reveals
before a renderer correction. The owner visually approved the diagnostic gallery
on 2026-10-24; the two measured pixel differences, physical Circle behavior and
event identity remain open. ZOD-033 stays partial.

**Triangle color follow-up:** The [source-backed correction](development/banner-triangle-colors.md)
now seeds the two rectangular triangles at their distinct core vertices. Actual
C# matches all eleven archived original reveal frames at zero tolerance; only
the prior 21/14 blue-channel pixels change. Two corrected runs repeat 57 artifacts.
Full-visible and hide outputs remain byte-identical, fifteen prior five-bit GPU
PNGs remain unchanged, and all sixteen RGB8 files remain unchanged. Five-bit
receipt policy text changes explicitly. Locked restores, zero-warning builds,
1,486 .NET and 135 Python tests pass. The owner visually approved the corrected
gallery on 2026-10-24; historical diagnostic approval remains separate. Next select an original
Circle-input validation method; physical input, event identity and broader
integration/rendering remain open. ZOD-033 stays partial.

**Physical reveal Circle follow-up:** A [paired original observation](development/original-banner-circle-input.md)
now verifies a USB-controller Circle press from neutral reveal count 5. The
source-bound input/phase witness distinguishes the shared natural/skip exit PC:
the task retains 5/0/0 progress, takes one active exit yield and clears on the next
source update. Two no-input continuations repeat states/PNGs; two inspection
reports repeat. Three metadata-only C# replay cases match the existing driver;
eight authored Python checks reject invalid witnesses. Locked restore,
zero-warning build, 1,489 .NET and 143 Python tests pass. No runtime change.
A neutral hold checkpoint is prepared in private slot 1 for the next physical
case. Hold/hide input, BSV playback, USB/display latency, original event identity
and broader integration/rendering remain open. ZOD-033 stays partial.

**Physical hold Circle follow-up:** The same [source-bound lane](development/original-banner-circle-input.md#physical-hold-result)
now verifies the owner's hold-phase press from a frozen neutral Holding count-1
state. Source frames 9000/9001/9002 map to logical ticks 21/22/23, retaining 20/1/0
through the extra active yield and task clear. Two three-frontend-frame restores
repeat state/PNG bytes and both inspector reports match. A later four-frame sample
is correctly rejected after original slot reuse. Six combined C# checkpoints and
ten authored Python witness checks pass; zero-warning Release build, all 1,492
.NET tests and 145 Python tests pass. No runtime changes. Next select a separate
physical hide-input or input-movie case; latency, event identity and broader
integration/rendering remain open. ZOD-033 stays partial.

**Physical hide Circle follow-up:** The [paired hide observation](development/original-banner-circle-input.md#physical-hide-result)
now verifies neutral input -> Circle-only over source frames 6435/6436. The same
active task advances Hiding count 1 -> 2, alternates buffers and queues six exact
8-row crops; no skip yield occurs. Both inspector reports match. Two additional
metadata-only C# checkpoints match the unchanged driver, and seven authored
Python hide-witness checks pass. Zero-warning Release build, 1,494 .NET and 152
Python tests pass. No runtime change or emulator launch for this pair inspection.
Each phase now has one bounded owner-operated press observation; later natural
completion after this hide press, input boundaries/repeats, BSV playback, latency,
event identity and wider integration remain open. Review/commit this evidence,
then select the next independent gate. ZOD-033 stays partial.

**Captured event identity follow-up:** The [saved-state verifier](development/original-banner-event-identity.md)
identifies event **4** in all eight selected states through current/cache IDs,
unique full archive payload match, text relocation and the saved interpreter
context. The condition-8/hold-60 command is at offset 804, followed by the task-56
wait at 807; active worker arguments agree. The earliest state is already waiting
inside event 4 at offset 731. Scenario-active and packed gate bits support this
scenario path; event 401's fallback is not observed. Two reports repeat; all 163
Python tests pass (11 new). No emulator, instruction execution, managed changes
or .NET rerun; last full .NET validation remains 1,494. Next trace/select bounded
opcode-78 scene admission with the preloaded driver; event entry, original
loading duration and complete playback remain separate gates. ZOD-033 stays partial.

**Scope:** Capture selected scenes at fixed state/presentation ticks and compare them with documented baseline/reference captures using appropriate regions and tolerances.

**Acceptance criteria:** Capture conditions are reproducible; reports distinguish geometry, palette, transparency/order, and timing discrepancies from simulation failures; thresholds are justified rather than masking visible defects. Synthetic renderer checks can run without game files; original media and comparisons remain local. Human inspection is recorded for effects automated metrics cannot establish.

### ZOD-034 — Add Windows CI and clean-machine launch validation

**Type:** Chore · **Priority:** P2 · **Status:** Implemented; remote CI and clean-machine validation pending · **Depends on:** ZOD-025

**Scope:** Add a suitable Windows build/test path in CircleCI when the host introduces Windows requirements. Retain Linux coverage for platform-neutral projects and store useful test/build reports.

**Acceptance criteria:** Windows-targeted build/test failures are visible in CI; no original assets or interactive desktop are required for automated checks; manual clean-machine launch prerequisites are documented and checked. Headless code builds without the rendering backend. Build artifact creation is permitted; public publishing/version automation remains a separate decision.

**Implementation/evidence (2026-10-23):** [Windows launch guide](development/windows-launch-validation.md)
records the independent CircleCI Windows machine job, retained Linux/Sonar lane,
TRX/JUnit reports, separate locked win-x64 publish graph and private folder bundle
with native dependencies/notices. Local Windows PowerShell 5.1 publishing and headless
launch passed; portable locked restore, zero-warning Release build and all 868 tests
passed. Core/Engine built without Client/MonoGame. The published synthetic GPU check
matched the headless script/replay after resize. These are workstation observations;
remote CI and a clean Windows 11 x64 interactive launch are still required before
closing this ticket. No original assets are needed or bundled, and no public release
or automatic versioning is introduced.

## Complete PS1 baseline: decompose these as source evidence grows

### ZOD-035 — Add a versioned save/load proof for the bounded scenario

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-016, ZOD-023

**Scope:** Save and load explicit battle snapshot DTOs with schema/content-profile versions, stable definition IDs, unit state, active turn, and RNG state. Choose an explicit legal checkpoint if mid-action state is unsupported.

**Acceptance criteria:** Loading and continuing matches uninterrupted play; invalid/missing IDs, corrupt data, and unsupported versions fail clearly; save writes preserve the last valid file on failure; the format does not serialize the in-memory object graph. Test migration handling with synthetic versioned data. Original memory-card import is optional until separately scoped.

**Evidence (2026-10-23):** [Save contract/proof](development/two-unit-battle-saves.md)
records schema/profile/content versions, stable ID resolution, exact active-turn/RNG
restoration and strict bounded JSON/checksum validation. Synthetic schema-1/2
fixtures exercise migration without rewriting the source. Flushed sibling writes
preserve the previous file on validation or promotion failure, including a tested
Windows deletion-sharing lock. The headless `--save-check` compares all remaining
state/events at thirteen checkpoints with freshly composed definitions. Locked
restore, zero-warning Release build and all 954 tests passed (86 new cases).
Campaign persistence, UI save slots and original memory-card import remain separate.

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

**Owner direction (2026-10-24):** Keep weapon/ability formulas readable and editable
as ordinary named C# code. Before expanding combat, consider extracting the bounded
sword calculation into typed, independently testable arithmetic while preserving
RNG/modifier order and atomic application. Distinguish data tuning from algorithm
changes; preserve original behavior beside explicitly selected custom variants.
See [editable combat formulas](development/editable-combat-formulas.md) for current
limits, source-edit workflow and refactoring criteria. This does not start a broad
combat rewrite or change the active banner queue.

**Acceptance criteria:** All baseline actions and required AI choices are mapped to implementation/fixtures or explicit unresolved entries; representative encounters validate state, RNG draw ordering, and rewards; mandatory enemy/ally turns can complete without player scripting. Record observed AI deviations separately from player-action correctness.

**Owner AI pacing preference (2026-10-26):** Use brief presentation pauses between
enemy movement and action in the proposed first AI skirmish. Pauses must not feed
wall-clock time into deterministic decisions, battle state or RNG. The current
managed battle has no AI opponent. Read-only decomp at
11464943bbdbd2a0af022d90cb7f10455cf911df contains battle/ai/turn.c (command selection),
evaluation.c (candidate evaluation/simulation) and abilities.c (usable abilities).
Translate a separately traced bounded subset into C#; do not execute C at runtime
or claim the entire original AI is implemented. This records future direction;
ZOD-114/115's sword work and ZOD-116's AI mapping are complete; ZOD-117 is next.

**Owner AI extensibility direction (2026-10-26):** Translate the original decision
algorithm into readable, source-traced C# behind a replaceable typed decision
interface in Zodiac Framework. The original SCUS-94221 implementation remains the
compatibility default; refined or alternative implementations are explicitly
selected variants. This is a code contract, not a new editor UI requirement.
The interface should receive explicit battle/definition inputs and return typed
commands plus any explicit decision-state/RNG transition, without directly
mutating live battle state. Existing authoritative rules validate and execute
commands for every implementation. Trace original candidate simulation, RNG,
ordering and resumable decision state before fixing the interface shape; do not
assume one stateless call or a complete turn plan matches the source. Keep useful
decision traces and deterministic scenario fixtures so implementations can be
compared on identical inputs. No production interface, AI implementation, plugin
loader or editor work is introduced by this direction; ZOD-117 remains next.

### ZOD-104 — Trace and specify the first executable job ability

**Type:** Spike · **Priority:** P3 · **Status:** Complete (source/data contract; implementation unexecuted) · **Depends on:** ZOD-017, ZOD-022, ZOD-023

**Result (2026-10-26):** The [Accumulate contract](architecture/zod-104-accumulate-action.md)
pins clean decomp `11464943bbdbd2a0af022d90cb7f10455cf911df` and verifies the
prepared SCUS executable's exact hash/header. Rechecked 22 imported records and
the default-menu/empty-status rows, then traced learned-slot selection, automatic
self targeting, immediate preparation, formula 0x36, byte/signed arithmetic,
base/equipment/effective PA, cap/no-change behavior and ordinary turn/RNG order.
Two final offline reports match; 17 source files are fingerprinted. The
[evidence](evidence/combat/accumulate-contract-v1.json) assigns 32 explicitly
unexecuted cases to ZOD-105/106/107. No production code, schema, editor or native
launch changes; no implementation suites rerun or original execution claimed.

**Scope/acceptance:** Select one ability, document its traced behavior and supported
domain, identify content/state/save/UI prerequisites, and define independently
checkable implementation children. Original rewards, learning, equipment bonuses,
AI and presentation equivalence remain outside the selected proof.

### ZOD-105 — Prove the pure Accumulate content/action scenario

**Type:** Story · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-104

**Result (2026-10-26):** [Implementation/workflow](behavior/accumulate-implementation.md)
and [executed evidence](evidence/combat/accumulate-implementation-v1.json) record
the closed effect companion, grant/reference validation, pure PA mutation, shared
turn/sword continuation and explicit unsupported-save guards. All **2,438 .NET /
293 Python tests** pass (89 new .NET); ACC-001..020 are covered. Two actual imports
match, all six raw source records independently compare, and four prior metadata/
sword files are unchanged. No native UI/original execution or broad ability claim.

**Scope:** Implement the closed immediate self PA+1 effect, validated companion
content/projection and explicit unit grants in a separately identified authored
scenario. Keep existing learning metadata and sword-only profiles intact. Current
battle PA changes immutably and feeds subsequent sword damage. Enforce the
reachable-PA-99 sword envelope before execution; no broad formula pipeline or
editor changes. Existing save entry points must explicitly reject unsupported
ability-enabled states until ZOD-106, including no-change-at-cap states.

**Acceptance criteria:** ACC-001..020 pass with meaningful arithmetic, atomicity,
turn, draw-count, grant/reference, repeat-use, preview and regression coverage.
Original import validates the selected secondary/menu/status contract and retains
raw provenance only under scratch; unsupported fields fail explicitly. Content
round-trips deterministically. Existing sword fixtures/journals retain exact
outcomes/RNG. Update behavior evidence and run appropriate locked build/suites.

### ZOD-106 — Preserve boosted battle PA across checkpoints and replay

**Type:** Story · **Priority:** P3 · **Status:** Deferred · **Depends on:** ZOD-105

**Trigger (owner sequencing, 2026-10-26):** Revisit when a concrete debugging/
checkpoint need or an explicitly selected battle-suspension feature requires
dynamic-PA persistence. Not a prerequisite for ability gameplay or original
campaign saving. Keep the unsupported-profile guard until then.

**Scope:** Add current PA to a new explicit ability-profile battle-save schema
and permit only validated dynamic PA restoration. Preserve old sword-profile
schema-2 writing and schema-1/2 reading; legacy documents do not select the new
profile. Definitions/grants resolve from trusted prepared setup, never the save.
Keep existing world checkpoint and slot formats unchanged.

**Acceptance criteria:** ACC-021..027 pass: fresh-definition restored continuation
matches uninterrupted state/events/damage/RNG, cap and choice flags persist,
restart resets PA, legacy behavior is preserved, and malformed/semantically invalid
or mismatched-profile saves fail without changing state/files. Use rehashed semantic
negatives, not only hash-mismatch checks. Repeat accepted/rejected command journals;
run appropriate build/suites and retain current save regression evidence.

### ZOD-107 — Use Accumulate through the existing Windows battle controls

**Type:** Story · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-105

**Owner sequencing (2026-10-26):** ZOD-106 is deferred. Replace this slice's
save-continuation check with in-memory command/headless/replay equality, preserving
the save guard. Follow the [bounded native plan](development/accumulate-battle-controls.md)
before launching; retain the owner visual/manual gate separately.

**Scope:** A separate prepared ability-enabled battle launch exposes the granted
self action, current PA, action availability and success/cap feedback through
shared simulation commands. Retain existing battle controls and introduction,
world and editor behavior. No editor expansion or original animation port.

**Acceptance criteria:** ACC-028..032 pass with a finite native plan written before
execution, repeated state/journal/headless-continuation/capture checks and appropriate
implementation suites. Owner visual/physical review remains separate from
programmed controls. Report authored presentation and original fidelity limits.

**Result (2026-10-26):** ACC-028..031 have executed automated coverage under the
revised sequencing; ACC-032 was subsequently approved by the owner ("Looks good.
Let's keep going."). No agent-observed physical-input claim is added. Both 560-input native runs
and prepared bundles repeat byte-for-byte. Independent audit checks all ability
state deltas, RNG preservation, atomic rejections and 15 captures. Locked Release
build has zero warnings; 2,459 .NET / 299 Python tests pass. Review instructions and
limits are in the [guide](development/accumulate-battle-controls.md).

### ZOD-108 — Trace Throw Stone targeting, damage and flat knockback

**Type:** Spike · **Priority:** P3 · **Status:** Complete (source/data contract) · **Depends on:** ZOD-105, ZOD-107

**Result (2026-10-26):** The [contract](architecture/zod-108-throw-stone-action.md)
pins clean read-only decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, re-verifies
the prepared SCUS executable and identifies six selected records, direct
projectile targeting, physical evasion, formula arithmetic, seven/four draw order,
Brave comparison, dominant-axis push with Y ties, lethal cancellation and settled
facing restoration. Two offline audits repeat exactly; 22 prior imported records
and 24 source identities are checked. The [evidence](evidence/combat/throw-stone-contract-v1.json)
assigns 34 explicitly unexecuted cases to ZOD-109/110/111. No managed action,
schema, new suite run, native launch or original execution is claimed.

**Scope/acceptance:** Select a bounded targeted Basic Skill action and trace its
source/callers/data/widths/order; define a clear flat two-unit domain and explicit
unsupported projectile/terrain/status behavior. Preserve old profiles and record
found fidelity gaps. Separate pure rules, prepared content and native review.

### ZOD-109 — Prove pure Throw Stone damage and flat knockback

**Type:** Story · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-108

**Result (2026-10-26):** [Behavior/evidence](behavior/throw-stone-implementation.md)
records the closed pure profile, exact damage/Brave/facing/range rules and atomic
HP/knockback publication. All 2,608 .NET / 299 Python tests pass, adding 149 managed
cases for TST-001..020. Existing sword/self-action/content/save/UI paths are unchanged.
Content projection/scenario composition remains ZOD-110; no native check or
independent original execution is claimed.

**Scope:** Add ordinary named C# arithmetic, a closed targeted-action profile,
explicit grants and a separate actor/ability/target command over immutable
combat state. Admit only the traced open dry flat two-unit domain. Resolve
physical evade, current-PA damage and same-height one-tile knockback with exact
conditional RNG, preserving incoming settled facings and target voluntary choices.
Retain self-action and sword behavior. No content import, save, Engine or UI work.

**Acceptance criteria:** TST-001..020 pass, including range/facing ties, arithmetic
boundaries, miss/hit draw order, Brave strict comparison, in-bounds/off-map push,
lethal cancellation, Act/Move ordering, RNG exhaustion, replay and atomic rejects.
Unsupported setups reject before publication. Record exact event order and source
limits; run meaningful regressions and appropriate full implementation suites.

### ZOD-110 — Prepare Throw Stone content and a combined Basic Skill scenario

**Type:** Story · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-109

**Result (2026-10-26):** TST-021..028 pass with strict companion/grant validation,
six-record verified import, canonical metadata matching, combined action routing,
first-KO/restart/continuation and both-profile save-preflight rejection.
Two actual imports and headless checks repeat byte-for-byte; independent audit
rechecks source/mappings/historical bytes and damage/knockback/RNG.
All 2,700 .NET / 305 Python tests pass. See the
[workflow/evidence](development/basic-skill-content.md); native controls remain ZOD-111.

**Scope:** Add a separate closed targeted-effect companion and validated
metadata/grant projection. Offline import verifies selected original records and
preserves raw provenance under scratch. Compose an authored two-unit scenario
that explicitly grants Accumulate and Throw Stone, with unchanged old profiles.
Keep the old sword domain guards and ability-save rejection; ZOD-106 is deferred.

**Acceptance criteria:** TST-021..028 pass: strict parsing/reference/semantic
negatives, edition/hash/record checks, repeated imports and historical metadata
preservation, boosted ranged damage, first KO, restart and headless continuation.
Unsupported ability saves reject before file I/O, including initial states.
Run appropriate suites and independent import/evidence audit.

### ZOD-111 — Use Throw Stone through prepared Windows target controls

**Type:** Story · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-110

TST-029..033 pass; TST-034 is owner-approved ("This looks good.", 2026-10-26).
Individual physical-input steps were not separately reported or agent-observed.
No implementation, captures or test results changed for approval. See the
[native plan/results/review guide](development/basic-skill-battle-controls.md) and
[evidence](evidence/combat/throw-stone-window-v1.json). Programmed focus checks and
agent image inspection do not establish physical-input or owner approval.

**Scope:** Expose the validated targeted action through the existing Windows
battle router with select/confirm/cancel, range and availability feedback, and
HP/PA/position outcomes. No editor expansion or original projectile animation port.

**Acceptance criteria:** TST-029..034 pass. Write a finite native plan before
execution, compare every routed state/event/RNG with headless execution and replay,
repeat prepared/capture outputs, inspect GPU images, and preserve render/resize/
focus atomicity. Owner visual/physical-input approval remains a separate gate.
Battle-save persistence is not a prerequisite.

### ZOD-112 — Specify the bounded sword's settled facing lifecycle

**Type:** Spike · **Priority:** P3 · **Status:** Complete (source/fixture contract; correction unimplemented) · **Depends on:** ZOD-111

**Result (2026-10-26):** [Pinned trace and migration policy](architecture/zod-112-sword-settled-facing.md)
and [evidence](evidence/combat/sword-facing-contract-v1.json) identify the living
actor's incoming facing as the settled result for the closed sword profile.
32 cardinal vectors and two continuation examples calculate the later hit/evade
and RNG difference independently. Preserve old fixtures/evidence; select a new
test policy ID for replacement executable fixtures. Existing sword save fields
already store facing; direction-aligned built-in saves are expected unchanged
and must be revalidated. No managed correction or new original/native execution.

**Scope:** Recheck the ZOD-108 finding against fresh pinned read-only decomp HEAD:
trace sword preparation, temporary actor turning, hit/miss/KO resolution and the
return-to-control restoration boundary. Compare the managed sword's persistent
actor turn and its effect on subsequent facing-based evasion. Keep temporary
animation facing separate from settled battle state. This is a source/fixture
contract; do not silently change legacy behavior or rewrite historical evidence.

**Acceptance criteria:** Record source callers, ordering, widths and relevant
living/dead actor/target branches within the supported sword profile. Specify
independently checkable initial-facing, hit/miss, target-KO, action-order and
subsequent-evasion vectors, including state/events/RNG preservation and replay.
Identify affected current tests/save continuations and recommend one bounded
implementation ticket with explicit fixture-migration policy. No broader sword
range/critical/knockback expansion, rewards/animation port, new native UI work,
editor work or battle-save feature. Source-derived expectations remain distinct
from original-game execution.

### ZOD-113 — Correct settled sword facing and migrate executable fixtures

**Type:** Story · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-112

**Result (2026-10-26):** [Correction/migration guide](behavior/sword-settled-facing-implementation.md)
and [evidence](evidence/combat/sword-facing-implementation-v1.json) execute all
20 SFC gates. No old fixture/save/evidence rewrite or legacy-bug runtime mode.
Both save and Basic Skill headless outputs remain byte-identical to prior builds;
the intentionally misaligned continuation now matches corrected facing/evade/RNG.

**Scope:** Implement the [ZOD-112 contract](architecture/zod-112-sword-settled-facing.md)
within the current closed pure sword API. Publish the living actor's incoming
settled facing; retain target facing, rejection guards, same-action damage/RNG
and turn/event ordering. New explicit test policy ID selects replacement
executable fixtures; old v3 fixtures become validated superseded history without
normalizing assertions or retaining a second buggy production algorithm.

**Acceptance criteria:** SFC-001..020 pass with full state/events/RNG, all four
directions/incoming facings, hit/miss/zero/critical/KO, action orders, atomic
rejection/exhaustion, both actor IDs, pure probes and corrected multi-action replay.
Verify the exact subsequent-evasion vectors, all 13 existing sword-save boundaries
and six new reachable misaligned-facing save/replay boundaries using the existing
format and fresh definitions. Preserve nine historical fixture files, two save
files and historical combat evidence byte for byte, with explicit supersession
mapping and new-policy strict parser/mismatch checks. Run locked isolated build,
full .NET/Python suites and repeated headless scenarios. No editor, transient
animation state, broader sword rules or ability-save support. New native work is
not required if presentation is unchanged; historical captures remain historical.

### ZOD-114 — Specify flat sword critical knockback and the geometry gate

**Type:** Spike · **Priority:** P3 · **Status:** Complete (source/description contract; production unimplemented) · **Depends on:** ZOD-113

**Result (2026-10-26):** [Source and bounded recommendation](architecture/zod-114-flat-sword-knockback.md)
and [evidence](evidence/combat/sword-knockback-contract-v1.json) pin clean read-only
11464943bbdbd2a0af022d90cb7f10455cf911df. Critical directly attempts a push;
no Brave rolls. Exact four/six/seven draws, destination/lethal cancellation,
zero-damage push and settled choice/facing effects are specified. Two identical
offline audits verify 17 files/41 functions, 35 arithmetic vectors and 42 preserved
files. All 329 Python tests pass (12 new); no .NET rerun or runtime/native change.
SKB-001..020 remain unexecuted. The current geometry validator is insufficient
to remove its edge-only guard globally: ZOD-115 must add strict explicit opt-in.

**Scope:** Trace the next closed ordinary sword extension against fresh pinned
read-only decomp: critical-triggered knockback on an open dry flat two-unit map.
Determine exact eligibility, conditional RNG ordering, Brave comparisons (if any),
destination/occupancy/off-map checks, lethal suppression and settled position,
facing and choice effects. Compare with existing Throw Stone flat displacement
without assuming the same roll or validation order. Identify which part of the
current off-map-beyond-target sword guard can safely be relaxed.

**Acceptance criteria:** Produce source/data findings, explicit unknowns, arithmetic
and boundary vectors, an unexecuted implementation-case inventory and one bounded
implementation recommendation. Preserve settled-facing correction and historical
evidence. No guard relaxation or gameplay change in this spike; no falls, slopes,
water, additional units, reactions, animations, rewards, editor or save feature.

### ZOD-115 — Implement the opt-in flat sword critical knockback extension

**Type:** Feature · **Priority:** P3 · **Status:** Complete (pure explicit profile; integration unactivated) · **Depends on:** ZOD-114

**Result (2026-10-26):** [API/behavior/reproduction](behavior/flat-sword-knockback-implementation.md)
and [evidence](evidence/combat/sword-knockback-implementation-v1.json) record the
strict FlatSwordBattleProfile overload and settled SwordKnockbackEvent. One shared
damage core retains the old default/guard. All SKB-001..020 and 35 historical
description vectors execute without rewriting stored expectations. Fresh clean
reference 11464943bbdbd2a0af022d90cb7f10455cf911df matches all 17 files/41 functions.
Locked restore/zero-warning Release build and **3,044 .NET / 329 Python** tests pass
(240 new managed). Repeated historical save/Basic Skill output and source/history
audits agree; no presentation, original execution, content/scenario/save activation,
format, dependency or editor change. ZOD-116 owns the next AI source/interface spike.

**Scope:** Implement SKB-001..020 in the pure Zodiac Framework boundary using the
ZOD-114 source contract, rechecking reference HEAD before translation. Add an
explicit ordinary-ground, two-unit, open flat lower-layer profile; preserve the
old edge-only entry point/default. Retain the existing sword damage, signed-16
gate, four/six/seven draws, settled facings and atomic rejection behavior. Publish
one-tile nonlethal critical displacement without spending the target's choices.
Avoid a second damage algorithm and do not reuse Throw Stone's Brave contest.

**Acceptance criteria:** Execute the source/boundary vectors, every draw-budget
branch, exact state/event/RNG invariants, both action orders, later range and
accepted/rejected replay. Prove profile rejection before mutation for unsupported
terrain, units and inputs. Preserve historical fixtures/evidence/saves and legacy
headless results; run locked build and appropriate full suites. Specify the
forced-position event and strict profile semantics in a behavior note. No content,
native/scenario/save activation, falls, slopes, water, extra units, reactions,
rewards, editor or AI implementation in this milestone. After this proof, select
the bounded original-AI Move/Attack/Wait source/interface mapping; content/native
integration must receive its own finite scope before activation.

### ZOD-116 — Map bounded original AI decisions and the replaceable C# interface

**Type:** Spike · **Priority:** P3 · **Status:** Complete (stage/helper map and proposed interface; production unimplemented) · **Depends on:** ZOD-115

**Result (2026-10-26):** [Pinned algorithm map and contract sketch](architecture/zod-116-ai-decision-contract.md)
and [evidence](evidence/ai/decision-contract-v1.json) distinguish AI formula
projection, state scoring, probability/scan/tie ordering, cached command and
post-Act replanning, suspended state, decision RNG and late Wait facing. Fifty
offline helper/stage vectors and 20 unexecuted production cases are specified.
Two final audits recheck 16 reference files/71 functions and the verified Attack
metadata record, preserving 37 historical and seven managed baseline files.
All **342 Python tests pass** (13 new); no production/.NET test rerun.
No C# interface/AI, live rule, native/editor/save activation or original execution.
ZOD-117 is the bounded projection prerequisite; full search and facing remain gaps.

**Scope:** Trace fresh pinned read-only decomp for original Move/ordinary-sword
Attack/EndTurn decisions in a flat two-unit encounter. Start from battle/ai/turn.c,
evaluation.c and abilities.c; follow required callers/data rather than replacing
the original with an approach-and-attack heuristic. Map policy/team/unit inputs,
ability and tile enumeration, Act-before-Move versus Move-before-Act, scoring,
comparison/ties, candidate simulation, preview versus execution RNG, selected
command lifecycle and suspended work. Include Wait-facing decisions and identify
any mismatch with current managed Wait capabilities.

**Acceptance criteria:** Produce pinned source findings, decision-stage/data and
width/order maps, explicit unknowns and missing runtime inputs/rules, source-derived
scenario vectors and an unexecuted implementation inventory. Recommend a typed
replaceable C# decision contract in Zodiac Framework with explicit input, decision
state, command and RNG transitions. Keep original behavior the default and custom
policies explicitly selected; all commands use authoritative battle validation.
Specify a bounded next implementation only when its dependencies are supported;
otherwise split the concrete prerequisite without inventing behavior. No production
AI/interface or native/content/save/editor activation in this spike. Brief enemy
movement/action pauses remain presentation-only; original work scheduling must be
traced separately from deliberate host pacing. ZOD-106 remains deferred.

### ZOD-117 — Implement the bounded ordinary-sword AI formula projection

**Type:** Feature · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-116

**Scope:** Recheck fresh pinned source/callers and implement a pure formula-level
AI projection in Zodiac Framework for the existing closed ordinary sword domain.
Keep execution Attack APIs unchanged. Return typed projected normal damage, HP,
hit percentage and explicit advanced RNG; four physical evade draws still occur,
while fixed midpoint critical and skipped execution proc add none. Use current
stats and shared arithmetic only where both execution modes actually agree.
This is neither expected-value damage nor a discarded seeded execution attack.

**Acceptance criteria:** Execute the seven ZOD-116 projection vectors plus raw/HP/
facing/zero/cap/signed-envelope boundaries. Verify exact four-draw budgets and
atomic rejection/exhaustion, input immutability and repeated probes, both actor
IDs/orders and current stats. Certain evade yields a zero-accuracy/nonvaluable
projection after its four draws; distinguish it from unsupported setup and do
not erase decision RNG when the future caller rejects that candidate. Preserve
all existing execution/fixture/save/headless regressions and run locked build
and relevant full suites. Document where the formula result ends: candidate
clock/upkeep, scoring/search enumeration, roster/policy, interface/session runner,
Wait-facing and scenario/content/native/save/editor activation remain separate.
No claim of a complete AI or whole-decision draw budget. ZOD-106 remains deferred.

**Evidence (2026-10-26):** [API/source/reproduction](behavior/sword-ai-formula-projection.md)
and [implementation evidence](evidence/ai/sword-formula-implementation-v1.json).
227 new managed cases execute the seven source vectors, directional/raw/HP/PA/
signed-envelope boundaries, exact budgets, atomic rejection, current battle PA,
both action orders and pure repeated probes. All 3,271 .NET / 342 Python tests pass;
execution, historical evidence, save/headless/prepared/native artifacts remain.
Full AI/interface/search and candidate-clock are outside this completed scope.

### ZOD-118 — Specify bounded end-of-turn facing and completion lifecycle

**Type:** Spike · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-116, ZOD-117

**Scope:** Freshly trace original Wait direction selection/application and the
managed completion boundary. Specify a narrow immutable facing transition and
its authorization/lifecycle for explicit Wait and automatic Move+Act completion.
Separate source-chosen facing from presentation pauses and from a future selector.
Record how a completed turn is identified without accepting a stale unit ID,
changing CT twice or consuming decision RNG again. No broad turn-system rewrite.

**Acceptance criteria:** Pin current read-only decomp revision and callers/data/
ordering. Describe independently checkable cardinal/diagonal/hint/viability and
post-movement/push cases, explicit versus automatic completion, both choice orders,
unused-choice CT refunds, repeated/stale/mismatched completion and unsupported
coincident/no-enemy chooser cases. Distinguish pure facing choice from authoritative
application, including ended turns with no active actor; specify the minimum
receipt/state needed before production code. Preserve existing Wait/Attack/Move/
replay/save behavior and document compatibility limits. Produce one bounded
implementation ticket; no production AI/interface/search, save format, scenario/
native/editor activation, midbattle save work or timing-dependent simulation.

**Evidence (2026-10-26):** [Contract/reproduction](architecture/zod-118-turn-facing-contract.md),
[source fingerprints](evidence/ai/turn-facing-contract-v1.json) and
[70 description vectors / 18 unexecuted production cases](fixtures/behavior/turn-facing-contract-v1.json).
Fresh pinned source confirms the late facing path, source repeat-turn_end no-op,
enum mapping, viability/target order, overlapping/no-enemy stack-byte uncertainty
and the existing managed movement-facing gap. The receipt/lifecycle is a managed
proposal, with no production types, runtime behavior or save format introduced.

### ZOD-119 — Implement bounded turn-facing completion and closed-enemy choice

**Type:** Feature · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-118

**Scope:** Freshly recheck ZOD-118 source/callers and implement its opt-in immutable
Zodiac facing wrapper over the existing strict two-unit flat sword profile.
Start from validated fresh AddCt setup; no arbitrary snapshot/receipt attachment.
Delegate existing Advance/Move/Attack, capture automatic completion only after
the full command settles, and block subsequent commands until facing completes.
Implement typed Fixed/KeepCurrent/ChooseClosedEnemy choices, explicit atomic
WaitWithFacing and receipt-authorized facing-only finalization. A chooser enemy
must be explicitly certified; no team inference or new general AI roster.

**Acceptance criteria:** Execute FTF-001..018 and admitted source direction/hint
vectors. Cover both choice orders and actors/collection orders, actual displaced
target coordinates, current battle stats and strict geometry/coordinate domain.
Verify single CT refund/event, no repeated combat or RNG, same-facing receipt
consumption, pending command barriers, missing/wrong/stale/cross-branch receipt
rejection, revision exhaustion, zero facing draws at exhausted RNG and immutable
prefix/suffix replay. Keep wrapper construction and receipt issuance controlled;
retrying the returned authoritative state differs from replaying an old immutable
input. Preserve all legacy APIs/fixtures/save/headless behavior and run locked
build/full suites. KeepCurrent means supplied managed settled facing; do not
invent a movement path heading or claim original path/search parity. General
multi-unit terrain/status/imminence helpers remain outside this profile. No
scenario/native/content/save/editor activation, production AI interface/session
RNG runner or midbattle-save work. Editor and ZOD-106 remain deferred.

**Evidence (2026-10-26):** [API/reproduction](behavior/turn-facing-implementation.md)
and [implementation mapping](evidence/ai/turn-facing-implementation-v1.json).
All 18 FTF cases and 20 admitted source IDs execute in 144 new managed cases;
3,415 .NET / 359 Python tests pass. Both completion paths, stale/cross-branch/
earlier-turn receipts, exact settlement, pending barriers, CT/RNG/revision limits,
current fields and immutable replay are covered. Legacy APIs and old evidence
remain; no scenario/content/native/save activation or original path parity claim.

### ZOD-120 — Specify flat ordinary AI movement and path composition

**Type:** Spike · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-119

**Scope:** Freshly source-trace the original movement, target-distance and selected
path behavior needed by two ordinary opposing units on certified open dry flat
lower ground. Follow callers from AI reachable/origin/destination enumeration
through confirmed movement and settled heading. Compare existing managed movement
prototypes against this precise domain before using them in an original selector.
Keep general terrain/status/mount movement breadth outside this spike.

**Acceptance criteria:** Pin the read-only revision, functions/data/widths/order
and any random draws. Describe independently checkable reachable/stopping sets,
enemy traversal/occupied destinations and detours, stay-at-origin candidates versus
actual Move commands, target-distance propagation, scan/path tie ordering, both
choice orders and paths from post-push coordinates. Include exact selected paths/
final headings where source establishes them; record uncertainties and mismatches
rather than infer heading from net displacement. Identify reusable managed rules
and missing contracts, then produce one bounded implementation ticket. No broad
movement rewrite, full AI/interface/search activation, new save format, native/
editor work or midbattle saves. Preserve all existing evidence; ZOD-106 and editor
redesign remain deferred.

**Evidence (2026-10-26):** [Source/design contract](architecture/zod-120-flat-ai-movement-contract.md),
[48 description vectors](fixtures/behavior/flat-ai-movement-contract-v1.json) and
[pinned evidence](evidence/ai/flat-movement-contract-v1.json). Nineteen new Python
tests cover literal selected paths/packed steps/headings, independent all-pairs
graph distances, occupancy/detours, stay/choice flags, reverse rows, ties and
post-push coordinates. All 378 Python tests pass. Repeated audits preserve
68 historical and 21 managed baseline files and match 15 source files / 42
functions plus two prepared metadata records at the clean pinned revision.
Source descriptions do not establish executable original parity. No production
movement, legacy activation, new C#/native/headless/original execution or .NET
rerun. The bounded implementation follow-up is ZOD-121.

### ZOD-121 — Implement the certified flat ordinary movement query

**Type:** Feature · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-120

**Scope:** Add a pure opt-in query for two living ordinary unmounted stable
opponents on explicitly certified dry open flat lower ground with effective
cost 1. Bound each dimension to 1..16 and source Move/Jump to bytes, applying
caps 124/7. Query either actor from current settled coordinates. Return physical
destinations/residuals, AI origins including stay, reverse target-distance rows,
and an optional original selected adjacent path/packed steps/final execution
heading. A moved flag filters AI origins only; command/phase authority is separate.

**Acceptance criteria:** Execute FMP-001..018 in new implementation evidence
without rewriting the historical ZOD-120 unexecuted inventory. Validate geometry,
body/movement traits, explicit stable enemy relation and cost certification;
reject missing costs/unsupported profiles rather than invent default semantics.
Use typed unsupported/invalid-destination outcomes without partial publication.
Check source byte caps, Move0, enemy traversal and detours/disconnected corridors,
stay versus physical destinations, both actor identities, post-push current
occupancy, whole selected reverse rows, deterministic y/x enumeration and exact
backtrace ties/packed direction names. Compare actual managed prototype membership
and exercise independent shortest-distance and literal path vectors. Queries are
immutable/repeatable and consume no command/clock/RNG progress, including at an
exhausted supplied RNG count. Return execution heading as data only; never rotate
the battle or preview. Run locked restore, Release build and full .NET/Python
tests while preserving legacy movement/facing APIs and historical evidence.

No automatic movement policy selection, Move execution/facing-receipt adapter,
scenario/content/save/native/editor activation, candidate clock, complete AI
selector/search/roster/interface or general terrain/status/mount breadth. Editor
redesign and ZOD-106 remain deferred. A later execution adapter must distinguish
real path-facing publication from preview placement and settle facing before
minting completion receipts.

**Evidence (2026-10-26):** [API/reproduction](behavior/flat-ordinary-movement.md)
and [completed FMP mapping](evidence/ai/flat-movement-implementation-v1.json).
All 18 FMP cases pass; 96 managed cases execute all 24 admitted route IDs for
both actors/collection orders. Independent all-pairs distances and exhaustive
shortest-path ranking, real cost/adjacent/default prototype comparisons, actual
sword push/current coordinates, profile rejection, exhausted RNG and preserved
legacy facing cover the bounded query. Locked restore, zero-warning Release build
and 3,567 .NET / 378 Python tests pass (152 new managed). No original/C/native
execution, command activation, save or editor change. Seventy-eight historical
artifacts and all prior APIs remain unchanged; owner editor drafts are excluded.

### ZOD-122 — Specify the bounded voluntary Move execution adapter

**Type:** Spike · **Priority:** P3 · **Status:** Complete · **Depends on:** ZOD-121, ZOD-119

**Scope:** Freshly trace the original selected voluntary path through movement
confirmation, settled tile/facing, Move choice consumption, automatic turn end
and WaitDirection. Design one explicit opt-in adapter over the new certified
query and existing immutable facing boundary. Decide supported admission after
an action, push or KO without silently expanding the two-living-opponent query.

**Acceptance criteria:** Pin read-only revision, relevant source/callers/fields,
publication order and any RNG. Specify current-state path rebuilding, active
actor/consumed-choice/destination checks, atomic rejection, both Move/Act orders,
path heading publication before completion receipts, receipt/snapshot lineage,
and repeatable replay. Preserve original AI preview's coordinate-only placement
and distinguish it from real movement. State explicit post-KO and profile limits,
using the existing first-KO policy where applicable rather than inventing corpse
traversal or broad death scheduling. Define independently checkable source vectors
and a production implementation inventory, then one bounded implementation ticket.
Keep all existing APIs/fixtures unchanged during the spike. No full path animation,
selector/candidate clock/interface/search/RNG runner, general terrain/status/mount
breadth, save/native/editor activation or ZOD-106 work.

**Evidence (2026-10-27):** [Contract/API/reproduction](architecture/zod-122-voluntary-move-contract.md),
[60 classified vectors](fixtures/behavior/voluntary-move-contract-v1.json) and
[pinned evidence](evidence/ai/voluntary-move-contract-v1.json). Eighteen new Python
tests cover source confirmation/undo/heading/CT rules and normalized adapter/receipt
proposals; all 396 Python tests pass. Two audits match 13 files / 33 functions,
82 preserved artifacts and 24 managed baseline files. No production adapter or
original-game equivalence is claimed; VME cases remain unexecuted. Existing
query/facing/runtime code and editor drafts are unchanged. ZOD-123 is the bounded
implementation follow-up.

### ZOD-123 — Implement the confirmed flat path Move boundary

**Type:** Feature · **Priority:** P3 · **Status:** Ready (recommended next) · **Depends on:** ZOD-122

**Scope:** Add FacingTurnBattle.MoveAlongPath, a new typed PathMoveResult and a
VoluntaryPathCompletedEvent, using the current certified query and existing
TurnCycle/receipt lifecycle. It represents an already confirmed ordinary Move;
no tentative/cancel UI or caller-supplied path authorization. Keep existing Move
APIs, policy values and legacy behavior unchanged.

**Acceptance criteria:** Execute VME-001..018 in new implementation evidence,
leaving the source fixture historical. Preserve pending/revision/actor/choice/
profile/path validation precedence with exact input-state rejection, no events
or partial path. Require current strict profile/cost1 and no unrepresented
post-move effects. Publish actor position and selected real path heading before
Move consumption and any automatic completion; bind receipt only to the fully
settled snapshot/revision/lineage. Use one revision increment and zero RNG draws;
retain unrelated fields/ticks. Test both actors/collection orders, both Move/Act
orders, exact event/path/heading values, actual pushes/current occupancy, bad
destinations, unsupported costs/geometry, pending barriers, counter limits,
stale/cross-run/branch/earlier-turn receipts and immutable replay. FinishFacing
must KeepCurrent at the new path heading without repeating Move/CT/RNG; same-face
completion consumes once. Post-KO path Move rejects without expanding the query
or bypassing first-KO host; existing pending Fixed/Keep completion remains valid.
Run locked restore, zero-warning Release build and full .NET/Python suites,
preserving existing APIs/tests/historical evidence and owner editor work.

No activation in content/scenario/save/native/editor paths, full path animation/
cancel flow, general post-move effects or corpse/death movement, AI preview
mutation, candidate clock/search/roster/interface/decision RNG runner. Editor UI
redesign and ZOD-106 remain deferred.

### ZOD-039 — Expand typed definitions and import to the baseline catalog

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-018

**Scope:** Add items/equipment, full jobs and abilities/skill sets, monsters/characters, statuses, formulas/parameters, encounters/ENTD, localized text, maps, sprites/animations, stores, and campaign references in source-driven increments.

**Acceptance criteria:** Every baseline record family is inventoried and accounted for; all required references/flags validate; importing supported records is repeatable; every meaningful source field is mapped, intentionally unused, or explicitly unresolved. Content-sized synthetic catalogs prove that original fixed table limits are not accidental runtime restrictions. Authoring and runtime share validation.

### ZOD-040 — Investigate event/script formats and community-tool interoperability

**Type:** Spike · **Priority:** P2 · **Status:** Complete for the selected synthetic event; retail/tool round-trips pending · **Depends on:** None

**Scope:** Identify battle/story/world script families, exact relevant community editors, supported editions, opcode encodings, execution order, state dependencies, and the subset needed for one small event. Inspect tool licenses before proposing code reuse.

**Acceptance criteria:** Provide an opcode/control-flow map for one chosen event, a tool capability/interchange assessment with pinned references, and an interpreter/conversion recommendation. Separate bytecode decoding from semantic execution and specify unsupported opcodes. Produce child tickets for one bounded event interpreter, not a full scripting framework.

**Evidence (2026-10-23):** [Script investigation](architecture/zod-040-event-script-investigation.md)
distinguishes scenario bytecode, battle conditions, location scripts and presentation
streams at reference `11464943bbdbd2a0af022d90cb7f10455cf911df`. A fifteen-record
authored event maps exact offsets, operand lengths and true/false paths; a local
source-derived check verifies inputs 0/1/2. Tool assessment pins EntryEdit/FFTPatcher,
event syntax support and libFFT source/licenses; Juravis/EasyVent source/edition
uncertainties and binary round-trips remain explicit. No original event/script/media
is committed or executed. The bounded decoder/executor/lifecycle follow-ups are
ZOD-064/065/066. Research completion does not establish original event compatibility.

### ZOD-041 — Implement campaign, encounters, events, and optional content

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-035, ZOD-039, ZOD-040

**Scope:** World travel/time, encounters/battle setup, story and battle events, dialogue/triggers, party transitions, propositions and side content, and ending paths. Coordinate combat prerequisites with ZOD-036/038 as each encounter needs them.

**Acceptance criteria:** A coverage matrix accounts for every baseline campaign/optional path; the first child milestone completes one event-to-battle-to-world loop; complete acceptance requires new game through ending plus required side content, with scenario checks for flag/party/encounter transitions. Report unsupported scripts and avoid silently skipping them.

### ZOD-042 — Implement original game flow, menus, and progression UI

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-032, ZOD-039

**Scope:** Opening/title flow, formation, equipment, jobs/JP, shops, world navigation, battle menus, settings, help, and save/load screens. Identify required text/font/layout and input behavior for each screen.

**First bounded children (2026-10-26):** ZOD-102 proves the authored world-menu
application model; ZOD-103 hosts it on Windows before ZOD-069 adds slot screens.
These synthetic prerequisites do not require the full ZOD-039 catalog and do not
complete this epic's original UI inventory, party/job/equipment workflow or fidelity.

**Acceptance criteria:** The baseline UI inventory is complete; each screen supports the required operations and navigation without direct state mutation; selected original screen/input comparisons and transition regressions pass. Start with the smallest party/job/equipment workflow needed by the first campaign loop.

**Later owner direction (2026-10-24):** Provide an in-game Settings menu as display,
CRT, supported renderer/resolution and audio options grow. Decide defaults,
persistence, live/restart behavior and safe display recovery when those options
are scoped. Coordinate ZOD-079 and the [later presentation notes](development/later-presentation-decisions.md);
this does not advance Settings ahead of the current campaign/UI prerequisites.

### ZOD-043 — Establish audio decoding/playback and expand to baseline coverage

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-024

**Scope:** First investigate music/sample/sound-effect formats and playback semantics. Select a backend, prove one prepared sound/music path, then expand music, effects, loops, transitions, timing, volume, and any required streamed audio.

**Acceptance criteria:** A bounded sample plays from prepared disk data; source formats and timing assumptions are documented; audio events do not control simulation; required baseline audio is inventoried and comparison checkpoints cover selected loops/transitions. Parser tests use synthetic data and original recordings stay local.

**Later owner direction (2026-10-24):** Discuss accurate music and sound-effect
reproduction, including source-backed sequencing/timing, pitch/envelopes, loops,
mixing, interpolation/reverb and applicable streamed paths. Define original
recording comparisons separately from device latency and Settings controls; see
the [later presentation notes](development/later-presentation-decisions.md).

### ZOD-044 — Finish asset preparation for remaining required formats

**Type:** Epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-028, ZOD-030

**Scope:** Inventory unsupported map/effect/menu/world/font/movie/audio formats and the seven mixed XA/STR entries; resolve their sector/data semantics before building separate converters/streaming decoders. Prioritize by the next baseline scenario's dependencies.

**Acceptance criteria:** Every resource required by the baseline is accounted for with conversion/load status and provenance; mixed entries are no longer silently omitted; runtime can present required movie/streamed content from prepared files without the BIN. Synthetic bounds/malformed-input tests and local decode/comparison evidence cover each format family. An intentionally unused resource requires a documented reason.

### ZOD-045 — Build the baseline feature/content coverage matrix

**Type:** Inventory task · **Priority:** P2 · **Status:** Complete initial family-level inventory; deeper catalogs remain open · **Depends on:** None

**Scope:** Convert the full-original-game target into an inventory by subsystem, content family, screen, campaign/optional encounter, asset family, and compatibility profile. Use existing evidence and explicitly label unknown entries rather than inventing counts.

**Acceptance criteria:** Entries identify source/evidence, implementation status, ticket owner/reference, automated scenario checks, manual checkpoints, and unresolved gaps; all major original-game areas have a place in the matrix; it can drive child tickets for the broad epics. Update it incrementally rather than waiting until final acceptance.

**Evidence (2026-10-23):** [Coverage guide](coverage/README.md),
[readable entries](coverage/baseline-coverage.md) and authoritative
[JSON](coverage/baseline-coverage.json) define 58 stable planning entries across
nine domains. Every entry names its ticket/component, scoped implementation and
evidence, pinned source entry points, existing bounded checks or their absence,
manual checkpoint, unresolved gaps and next action. No original record/encounter
count or completion percentage is inferred. Sound-novel/music/test reachability
has an explicit US-edition eligibility audit; Windows Save/Load and custom profiles
are distinct scopes. The standard-library checker validates links/tickets/IDs and
the generated view; optional reference validation verifies HEAD/source paths.
No .NET runtime, package, asset or C reference change is made by this inventory.
The last recorded 954-test baseline is retained as historical evidence, not rerun.


### ZOD-046 — Run original-game acceptance and regression review

**Type:** Acceptance epic · **Priority:** P3 · **Status:** Needs breakdown · **Depends on:** ZOD-034, ZOD-036 through ZOD-045, ZOD-053

**Scope:** Execute the baseline matrix, campaign playthroughs, optional content, save/continue paths, presentation comparisons, asset completeness checks, and supported Windows launch validation.

**Acceptance criteria:** Every baseline entry is implemented and checked, intentionally unused with evidence, or explicitly unresolved; unresolved required behavior blocks a claim of complete baseline compatibility. New game through ending and mandatory optional-content routes are documented; deterministic scenarios and presentation checkpoints have reviewable results; runtime operates with the BIN and C checkout unavailable. Use independent original/emulator comparisons for critical behaviors and disclose remaining evidence limits.

## Bounded children selected by ZOD-040 and ZOD-045

### ZOD-064 — Decode one bounded PS1 scenario block into prepared event data

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-040

**Result (2026-10-23):** [Prepared conditional event](development/prepared-scenario-event.md)
implements raw/processed block inspection, the independently authored fixture,
closed capability/CFG validation, exact forward targets and strict hash-verified
prepared loading. Inspection retains unsupported records separately. No interpreter,
retail archive import or new CLI command is implemented. Locked restore, zero-warning
Release build and all 1,012 tests passed; source-derived/synthetic evidence only.

**Scope:** Implement offline structural parsing for the authored conditional event
selected by ZOD-040. Distinguish raw/processed block headers, preserve block-relative
offsets and scene/dialect identity, and produce a validated prepared representation
for the selected opcode subset. Keep raw import in AssetTool and prepared validation
in Content; no C runtime, scene execution or general scripting framework.

**Acceptance criteria:** Synthetic blocks cover text offsets, bounded lengths,
truncation, unsupported bytes and exact first forward-label resolution. D0 may
target D2/D4; D1 targets D2 only; jumps land after markers; DB stops search but is
not executable in this profile. Invalid/unsupported programs fail before emitting
an executable definition. Preserve inspection data separately from executable
capability validation. Prepared loading needs no original block/BIN/reference code.
No original event dumps are committed. An optional EntryEdit round-trip needs its
exact binary/configuration pinned and is not a runtime dependency.

### ZOD-065 — Execute the bounded conditional event deterministically

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-064, ZOD-035

**Result (2026-10-23):** [Conditional-event execution](behavior/conditional-event-execution.md)
adds a Core-owned immutable program/state and pure Step/Run transitions, projected
from validated prepared Content records. Both branches match exact dirty-variable
traces and replay; D0/D1/D4 landing behavior, arithmetic/budget rollback and invalid
definitions are covered. Variables are restricted to 0..65,535; no overflow
semantics, EventEnd, scene lifecycle or save integration is claimed. Locked restore,
zero-warning Release build and all 1,057 tests passed (45 new cases).

**Scope:** Add a pure FFT-owned transition over the selected prepared event, explicit
instruction position, variables 0..3 and terminal state. Support only the documented
F2/BE/B0/B1/A2/D0/D1/D2/D4 capability set and bounded arithmetic domain. Keep DB as
a structural sentinel; original EventEnd, waits, RNG, dialogue and concurrent tasks
remain unsupported. ZOD-035's save schema is unchanged. Project validated Content
records into a Core-owned execution definition; do not add a Core-to-Content reference.

**Acceptance criteria:** True/false and dirty-initial-variable cases match exact
instruction/variable traces; input snapshots remain unchanged, replay is identical,
and D4 landing differs from sequential exit. Unsupported/malformed programs and
arithmetic-domain/budget failures report clear diagnostics without silently ending
the event or applying partial state. Tests need no assets, clock, UI, C or network.
FFT opcodes/flags stay out of Zodiac.

### ZOD-066 — Trace one real event's activation and completion prerequisites

**Type:** Spike · **Priority:** P2 · **Status:** Complete for selected source trace; original observation pending · **Depends on:** ZOD-064, ZOD-065

**Result (2026-10-23):** [BattleStart investigation](architecture/zod-066-battle-start-event.md)
selects event 401 (`0x191`) from a reverified BIN/archive, with pinned block hash
and summarized evidence metadata. Python and the managed inspector agree on 31
records/13 opcodes. The menu fallback entry path, concurrent camera/fade/banner
work and EventEnd return to normal battle are traced. No matched original capture
or event-entry state was available; no semantic playback or runtime change.
ZOD-071 selects one non-tutorial explicit-tick F1 wait proof; other capabilities
remain required before original playback. The 1,057-test baseline was not rerun.

**Scope:** Select one small original event from a verified local TEST.EVT import.
Record numeric event ID/hash, the condition/entry path, every used opcode and the
completion/next-scene path. Distinguish EventEnd lifecycle from thread exit and
name required state/presentation capabilities before translating another operation.

**Acceptance criteria:** Source/caller/data and control-flow notes are pinned to the
actual reference SHA. Original bytes/dumps remain ignored; committed metadata
identifies local inputs/captures without containing the original script. Record
an independent original/emulator observation if available, or its exact blocker.
Propose one bounded next semantic addition and fixtures; no silent instruction
skipping or bulk dispatcher port. This trace does not claim the campaign works.

### ZOD-067 — Trace campaign save fields, lifetimes and legal checkpoints

**Type:** Spike · **Priority:** P2 · **Status:** Complete for field-family and entry-path trace; retail checkpoint catalog pending · **Depends on:** ZOD-035, ZOD-045

**Result (2026-10-23):** [Campaign save investigation](architecture/zod-067-campaign-save-state.md)
maps logical fields, writers, lifetime/ownership, restore order, unknowns and
title/world/story save entry paths. Neither RNG generator is stored in the original
image; live BIOS callbacks and scene/menu reseeding require an explicit native
policy. Original/emulator observations and complete retail checkpoint enumeration
remain open. No campaign runtime, UI or original-card importer added. Follow-ups:
ZOD-068 for a synthetic checkpoint service, then ZOD-069 for native UI.

**Scope:** Inventory logical state read/written by original CARD/WORLD save paths
and resident setup: party/progression/equipment, inventory/poached stock, world/
script flags, calendar, side-content progress, settings and summary metadata.
Trace original save availability and distinguish persistent data from transient
battle/UI state. Recommend a bounded campaign DTO and Windows Save/Load workflow.

**Acceptance criteria:** Each selected logical field has source/caller, lifetime,
owner, restore order and unknowns; unsupported fields/checkpoints are explicit.
Determine which RNG streams are saved, reconstructed or reset on load rather than
assuming that the bounded battle proof defines original campaign load behavior.
Original save availability is recorded independently of proposed Windows slot/path,
summary/overwrite/recovery behavior. Propose the first campaign persistence/UI
implementation child with dependencies and synthetic migration/failure checks.
No packed memory-card image import, new UI or campaign runtime is implemented;
original-card interoperability remains optional. Can run alongside ZOD-064.

### ZOD-068 — Prove a synthetic world-checkpoint Save/Load service

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-035, ZOD-067

**Result (2026-10-26):** [Synthetic checkpoint proof](development/synthetic-world-checkpoint.md)
adds an immutable authored world state and one deterministic travel command,
strict schema-1/2 save migration and summary validation, ten safe slots,
content-revision/lock protection, flushed atomic publishing, retained backups and
confirmed candidate-only load/recovery. No user-directory writes or new UI.
Locked restore, zero-warning Release build, **2,267 .NET / 281 Python tests** pass
(94/3 new). Fault injection and real Windows sharing failure preserve state/files;
two headless CLI checks repeat all **14 files**, independently audited against
both invented fixtures. Original campaign/checkpoints/RNG/card support remains
open. ZOD-069 still waits for the minimal ZOD-042 navigation flow.

**Scope:** Define one small authored campaign profile and Core-owned immutable idle
world checkpoint: stable route/location IDs, party/equipment, inventory/funds,
one date/progress flag and elapsed duration. Use Content DTOs/bindings/migration and
an application service for slots and safe storage. Establish the few required
job/item/location definitions within this child rather than waiting for all of
ZOD-039/041. Keep it independent of event decoding/execution. No RNG operations,
encounters, side-content, battle/script suspension or original-card import.

**Acceptance criteria:** Freshly composed definitions restore identical supported
state and the next synthetic command's state/events. Cross-field IDs, duplicates,
limits, checkpoint/route and summary mismatches reject before publication. Commit
invented predecessor/current schema fixtures; test known migration and reject
future versions, wrong profiles, checksum errors and malformed/oversized input.
Specify per-user location, safe slot IDs, catalog metadata and stale/concurrent
writer behavior. Validate serialize/flush/promotion/access failures preserve the
live session and previous file; canceled/failed load preserves state, with explicit
validated recovery. No hidden RNG seed/draw or wall-clock dependency enters Core.
RNG capability is unsupported until a named policy and replayable inputs exist.
This is a synthetic campaign proof, not completion of ZOD-041/053.

### ZOD-102 — Prove authored world-menu application navigation

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-068

**Parent:** ZOD-042. [Selected contract](development/synthetic-world-navigation.md)
records original title/world/card input context and the separate authored flow.

**Result (2026-10-26):** Closed client-owned model and 18 new transition tests;
locked restore, zero-warning Release build and **2,285 managed tests pass**.
Complete travel state/events match the existing command; repeated scripts match
all views/results and leave save directories absent. [Executed evidence](evidence/navigation/synthetic-world-navigation-v1.json).
Native presentation/device checks remain ZOD-103; no original UI claim.

**Scope:** A closed client-owned Home/World/read-only Party/New-confirm/Exit-confirm
model with immutable views, enabled focus, parent restoration and typed actions.
Delegate synthetic New/travel to authored content and the checkpoint service.
No native window, storage operations, original UI fidelity or general menu API.

**Acceptance criteria:** Disabled/off-screen actions cannot mutate the
checkpoint; modal cancellation/defaults preserve it; explicit New replaces only
after confirmation. Travel state/events match the existing pure command,
including rejection. Repeated logical scripts match; navigation actions perform
no filesystem operations and consume no clock/RNG/device/graphics input. Record
native/device checks as unexecuted.

### ZOD-103 — Host authored world-menu navigation on Windows

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-102

**Result (2026-10-26):** [Windows host](development/windows-world-navigation.md)
adds explicit world-menu launch/check routes, authored panels/font, shared
keyboard/mouse/controller edges, focus/connection baselines, close confirmation
and resize-aware clicks. Two native processes finish the finite 51-sample plan
and match 13 CPU/GPU images each; all 16 repeated files match. Complete action/
event/view replay and independent state/PNG audit pass. Zero-warning Release
build and **2,300 .NET / 284 Python tests** pass (15/3 new). Programmed focus/close
and controller samples do not prove physical input. The owner approved the gallery
on 2026-10-26: "Looks good." After receiving the five-step manual checklist, the
owner confirmed "It's good." This completes the selected keyboard/mouse/controller,
resize, close/cancel and focus checks as owner-reported observations. No measured
latency, device identifiers, mixed-monitor DPI or forced-device-loss claim.
No implementation suites or agent-run graphics checks were repeated, and reviewed
artifacts are unchanged. [Executed evidence](evidence/navigation/windows-world-navigation-v1.json).

**Parent:** ZOD-042. **Scope:** Bind the ZOD-102 model to one explicitly selected
Windows host with synthetic labels/panels, keyboard/mouse/controller action
edges, safe focus/reactivation and resize-aware hit tests. Existing intro/battle
commands remain separate. No slot screens, original title assets or new renderer.

**Acceptance criteria:** A finite predeclared native plan exercises New, travel,
read-only Party, Home/Resume, cancel/accept New and Exit, modal isolation, held
input/focus transitions and resize. Capture model/view evidence and verify the
same logical commands headlessly. Physical controller/keyboard/mouse and visual
review remain separately reported gates. Do not create real user save files.
Only this bounded navigation prerequisite is required by ZOD-069; the full
ZOD-042 epic and original party/job/equipment workflows stay open.

### ZOD-069 — Add native Windows Save/Load slot navigation

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-068, ZOD-103

**Result (2026-10-26):** [Slot UI](development/world-save-load-ui.md) delegates
catalog/save/load/recovery to the existing service, freezes selected revisions,
preserves sessions on cancel/failure and requires refresh before retrying.
Two five-slot pages expose only validated summaries and available operations;
recovery preserves damaged primary bytes and supports saving to a new slot.
Locked restore, zero-warning Release build and **2,317 .NET / 287 Python tests**
pass (17/3 new). Exactly two native checks each complete 140 samples, 22 full
CPU/GPU captures, six successful and three rejected storage operations. Complete
headless replay and independent state/storage/PNG audit pass; 51 files repeat,
and 32 prior navigation artifacts are unchanged. Existing-output/syntax gates
reject before window construction. No actual user save folder was written by
validation. The owner approved the gallery on 2026-10-26: "This appears to be fine."
The owner separately confirmed the interactive slot check after launch/check
instructions: "It's good." Record the bounded workflow as passed by owner report;
do not infer measured latency or exact device/mapping fidelity. No implementation
suites or agent-run graphics checks were repeated and reviewed artifacts are
unchanged. [Executed evidence](evidence/navigation/world-save-load-ui-v1.json).

**Scope:** After ZOD-068 and a minimal client screen/navigation flow under ZOD-042,
add a slot list/details, Save/Load actions, overwrite/discard confirmation and
error/recovery states. Use the tested service and published checkpoint availability.
The UI does not create campaign state or replicate persistence validation.

**Acceptance criteria:** Supported synthetic checkpoints save/continue through the
UI; invalid/corrupt/incompatible slots show useful status and preserve recoverable
data. Cancel, stale overwrite, access errors and load failure preserve the session.
Record keyboard/mouse/controller and Windows manual checks separately from pure
state tests. Keep original save availability distinct from any future save-anywhere
feature; no PS1 card formatting/browser emulation is required.

### ZOD-071 — Prove one deterministic non-tutorial event frame wait

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-065, ZOD-066

**Result (2026-10-23):** [Explicit-tick wait proof](behavior/timed-event-waits.md)
adds a distinct non-tutorial speed-one timed profile, signed-halfword F1 validation,
immutable pending waits and RunUntilBlocked/AdvanceTicks. Exact first/final tick,
zero/sequential/max waits, replay and per-call rollback are covered; original
conditional behavior and battle saves remain unchanged. Locked restore, zero-warning
Release build and all 1,105 tests passed (48 new). No E5, camera/banner workers,
EventEnd, original playback or renderer timing. ZOD-072 defines the next task boundary.

**Scope:** Add F1 wait semantics to a separately identified timed synthetic profile,
using immutable pending-wait state and explicit caller-supplied scheduler ticks.
Start with non-tutorial speed 1 and counts 0..32,767; preserve signed-halfword
encoding and document/reject negative counts as outside the proof domain. Trace
scheduler entry/resume ordering before implementing. Keep the synchronous
conditional profile, battle save schema and one-way Content-to-Core dependency.
No wall-clock sleeps, renderer timing, E5 task barriers, camera workers, event
speed changes, EventEnd, original export or full PS1 cooperative scheduler.

**Acceptance criteria:** Independently authored fixtures cover zero, one, 60 and
maximum counts, sequential waits, arithmetic before/after waiting, unchanged inputs,
identical tick replay, suspension versus exit, post-exit rejection, negative/
truncated/unsupported input and budget failures. Define the exact first consumed
tick and resumption boundary, with separate instruction/tick limits and atomic
rejection per call. Reject absent capabilities before applying any state; do not
silently skip original instructions. Existing conditional event regressions remain
unchanged. This does not enable original BattleStart: ZOD-066 lists the remaining
camera, task, banner, mutable-operand and EventEnd prerequisites.

### ZOD-072 — Define deterministic event task lifetimes and barrier contract

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-066, ZOD-071

**Result (2026-10-23):** [Scene task/barrier contract](architecture/zod-072-event-task-contract.md)
traces scene-wide E5 polling, mandatory first yield, multiple instances/owners,
BATTLE/WORLD scan differences and the dark-screen 54/55 phases. Selects immutable
scene/run identities, revisions, explicit ordered tick updates, budgets and atomic
errors. Seventeen authored specification cases are not executed tests. ZOD-073
implements the first pure ledger/single-consumer proof; no E5 capability, renderer
workers or runtime change is added here. The 1,105-test baseline was not rerun.

**Scope:** Trace E5's yield-before-check, running-task scan and task lifetimes for
one synthetic proof using the selected BattleStart task kinds. Separate camera,
dark-screen transition/hold and condition-display completion from whole-event
completion. Specify an FFT-owned immutable task state and explicit start/complete/
cancel or acknowledgement protocol before implementing any presentation worker.

**Acceptance criteria:** Pin source/callers and distinguish task kind from instance,
multiple matching tasks, mandatory first yield even when none run, deterministic
tick/update ordering and the dark-screen 54/55 transition. Define budgets, stable
identities, duplicate/stale/unknown completion handling and atomic rejection. Provide
independently authored pending/complete/empty-task and failure/replay expectations,
then one bounded implementation child. Preserve existing conditional/timed profiles,
Core-to-Content independence and pure simulation. No full PS1 scheduler, worker
callbacks, clock/GPU polling, original event export or EventEnd implementation.

### ZOD-073 — Prove the scene task ledger and single barrier consumer

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-072

**Result (2026-10-23):** [Pure scene task/barrier proof](behavior/scene-task-barriers.md)
implements immutable scene/instance identities, revisions, family phases and
terminal tombstones. Explicit ordered ApplyUpdates/BeginBarrier/AdvanceTick
commands enforce live scene-wide polling, mandatory yield and transactional
resource/error handling. All 17 authored specification cases now have full snapshot,
ordered trace and replay regressions, plus 40 boundary checks. Locked restore,
zero-warning Release build and all 1,162 tests pass (57 new). Prepared profiles,
save schemas, package locks and production dependencies are unchanged. E5 remains
rejected by existing profiles; ZOD-074 owns separate interpreter integration.

**Scope:** Implement the selected pure BATTLE scene task ledger/barrier contract
in Zodiac.Framework.Events. Use explicit scene-run identity, monotonic task instances,
owner metadata, revisions, Camera/ConditionDisplay running states and DarkScreen
entry/hold/release phases. Provide transactional task updates, BeginBarrier and
one explicit AdvanceTick; no effect workers or event-interpreter integration.

**Acceptance criteria:** Convert ZOD-072's 17 independent specification cases into
headless state/ordered-trace regressions and cover immutable inputs, phase/resource
boundaries and identical replay. Poll the current scene-wide class set after the
mandatory first tick, across all owners; distinguish matching class from instance
and whole-event completion. Enforce selected live/start/update/ID/counter limits,
terminal tombstones and deterministic duplicate/stale/foreign/unknown diagnostics.
Rejected batches preserve allocation, revisions, tasks, barrier, tick and empty
committed traces. Keep Core independent of Content, with no clock/GPU/I/O callback
or Zodiac task classes. Existing conditional/timed profiles, save formats and E5
rejection remain unchanged. This proof does not execute original opcodes, select
opcode-77 slots, implement EventEnd, or add multiple interpreter consumers.

### ZOD-074 — Integrate task barriers into a separate synthetic event profile

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-071, ZOD-073

**Result (2026-10-23):** [Synthetic event barrier contract](behavior/scene-event-barriers.md)
adds the explicit BATTLE non-tutorial speed-one task-barrier profile. Core composes
conditional operations, F1 and selected E5 classes with immutable shared-tick
VM/ledger state, ordered facts/resumption and atomic rejection. Content validates
and projects the profile; the named offline exporter decodes E5 unsigned and F1
signed. A separate authored fixture proves all classes and seven exact tick
boundaries, with full combined snapshots/traces and fresh-definition replay.
Tasks/tombstones/counters survive explicit exit and next-event start. Old profiles
still reject E5; no actual worker is added. Locked restore, zero-warning Release
build and all 1,230 tests pass (68 new). Packages, project references, old APIs and
save formats remain unchanged.

**Scope:** Compose the proved conditional/frame-wait behavior and scene ledger with
a normalized E5 barrier operation in one explicit new BATTLE synthetic execution
profile. Use recorded logical task facts, one shared logical tick boundary and a
single interpreter consumer. AssetTool owns unsigned original operand decoding;
Content validates the new capability/profile and projects immutable definitions
inward to Core. Existing profiles remain closed to E5.

**Acceptance criteria:** Pin the source revision and declare the exact new profile
and tick/command contracts before implementation. Admit only selected task classes
4/54/55/56, reject unsupported selectors/scenes/opcodes even when unreachable,
and preserve strict prepared validation and one-way dependencies. Independently
authored synthetic fixtures prove empty/multiple/new matching tasks, all selected
classes, F1/E5 sequencing, same-tick continuation after release and a fresh yield
for a newly entered barrier. Budgets, bad updates and continuation failures must
roll back both VM and ledger/tick state with empty committed traces. Replay records
all explicit facts and checks exact combined state/ordered traces. The scene ledger
survives interpreter exit/restart without ID reuse or implicit task cancellation;
define and test that boundary. Preserve old APIs/profiles, saves and package graphs.
No worker algorithms, callback/GPU timing, opcode-77 slot selection, EventEnd,
concurrent consumers, WORLD support or original BattleStart playback.

### ZOD-075 — Trace and select the first deterministic scene effect model

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-074

**Result (2026-10-23):** [General-banner effect selection](architecture/zod-075-general-banner-effect.md)
selects condition 8's logical post-readiness lifecycle, after comparing camera,
dark-screen and banner dependencies. Traces byte operands, inline task-56 lifetime,
20/H/10 iterations, extra exit yield, phase-specific Circle sampling and startup
loading exclusions. Defines immutable effect state, completion authority, native
cancellation and atomic VM/ledger composition. Twenty-four authored cases are
specification-only and unexecuted; ZOD-076 implements the bounded driver/adapter.
No runtime/opcode/graphics change or new .NET run; the 1,230-test baseline remains
historical. Original total loading time and visual/timing equivalence remain open.

**Scope:** Select one bounded camera, dark-screen or condition-display effect from
the original BattleStart path that can generate task phase/completion facts from
pure logical state and ticks. Trace its source before translating a worker. Separate
logical effect progress and scene state from rendering resources and host pacing.

**Acceptance criteria:** Pin reference HEAD, entry/callers, operands/data, arithmetic
widths, update/yield order, completion predicate and observable quirks for the
selected candidate. Explain why it is the smallest independently checkable slice,
which initial scene/content/input values it needs, and which values remain unknown.
Specify immutable Core-owned effect state and its atomic composition with the
proved VM/ledger tick; do not let GPU completion, draw counts, wall-clock time or
I/O callback arrival supply completion facts. Provide independently authored phase/
tick/termination and failure expectations with their evidence classification, then
one bounded implementation child. Account for barrier class versus task identity,
initial/last-tick ordering, retained scene lifetime and cancellation authority.
No broad scheduler/worker port, new engine extraction, full original event playback
or unobserved claim of retail timing equivalence.

### ZOD-076 — Prove the logical general-banner effect and atomic scene adapter

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-075

**Result (2026-10-23):** [Logical general-banner driver](behavior/general-banner-effect.md)
adds immutable condition-8 progress, explicit Circle/native cancellation, managed
task authority and atomic effect/event/ledger ticks. Implements exact 20/H/10/final
yield boundaries, retained scene lifetime, ordered unmanaged/generated facts and
full rollback on budgets, bad updates and VM continuation failure. All 24 authored
cases now have headless checkpoint/trace/failure/replay regressions, plus 47 boundary
cases and an opcode-78 rejection check across old export profiles. Locked restore,
zero-warning Release build and all 1,302 tests pass (72 new). Production dependency
and package graphs, old APIs/profiles and saves are unchanged. No original loading,
rendering, DisplayConditions admission or full event playback is added.

**Scope:** Implement the selected pure condition-8 post-readiness effect in
Zodiac.Framework.Events, with a typed authored definition, one retained managed effect/task
and explicit per-tick logical Circle input. Compose generated task termination
facts atomically with the existing scene event/ledger tick. Do not add graphics,
asset loading or original DisplayConditions opcode admission.

**Acceptance criteria:** Convert the 24 independent specification cases into
headless complete-state/ordered-trace regressions, plus immutable/replay, missing
input, binding, ID/resource and checked-clock boundaries. Preserve exact reveal
20/hold H/hide 10/exit-yield ordering for H 0..255; skip reads only in reveal/hold,
with one more active yield after Circle. All active subphases remain task 56;
original skip completes it, while explicit native owner cancellation retains a
distinct Cancelled outcome and authority. One shared tick applies supplied unmanaged
facts then generated termination before barrier polling/VM resumption. Enforce
managed identity/revision ownership, combined update budgets and complete rollback
of effect/task/VM/tick/allocations on every failure. Other class-56 tasks still
block; effects progress after VM exit and survive explicit next-event start without
resetting scene/task identity or counters. Retain terminal effect/task records;
reject a second attachment in this proof. Keep old APIs/profiles, opcode-78
rejection, save formats, package graphs and Core independence unchanged. No GPU/
clock/I/O callbacks, full startup timing, other conditions, EventEnd, concurrent
consumers or original BattleStart playback.

### ZOD-077 — Trace general-banner assets and the preparation/readiness boundary

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-076

**Result (2026-10-23):** [Asset/readiness trace](architecture/zod-077-general-banner-assets.md)
pins verified BONUS.BIN image 0 and REQUIRE geometry/range/page/color records,
palette bank 4, six-piece blend order, u8 UV wrap and the battle screen origin.
Selected crops stay within initialized pixels despite the native oversized upload.
Specifies immutable prepared files, a preloaded host boundary and an explicit
RGB8 CPU preview policy; original packet parity/loading latency and visual
equivalence remain unresolved. Includes summarized hash evidence and 20 authored,
unexecuted presentation expectations. Selects ZOD-078; no runtime/test/package
changes. Historical implementation validation remains ZOD-076's 1,302 tests.

**Scope:** Trace condition-8 banner/image 0 from the selected BattleStart path to
its local original image/palette and source quad/range/texture-page/color records.
Select one bounded prepared presentation slice driven by the proved logical
effect, while defining where asset readiness sits relative to scene execution.

**Acceptance criteria:** Pin source HEAD and verify the edition/hash/identity of
selected local input ranges. Trace data layouts, dimensions, packing/palette and
piece ordering, source vertex/offset arithmetic, relevant quirks/uncertainty and
renderer requirements. Keep raw/decoded assets and inventories ignored under
scratch; commit only permitted summarized evidence and authored fixtures. Specify
immutable prepared files and logical-ID/manifest projection with no original
parsing or I/O in Core. Separate preloaded readiness from unimplemented original
allocation/file/upload yields; never let callback arrival, draw count or GPU state
decide a logical completion tick. Preserve the source buffer-parity uncertainty
and leave unsupported values explicit. Provide one bounded preparation/presentation
implementation child with independently checkable expectations. No full opcode-78
admission, fake loading duration, other condition/banner families, EventEnd or claim
of original visual/timing equivalence without original observations.

### ZOD-078 — Prepare and preview the bounded general banner

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-077

**Result:** [Prepared general-banner preview](development/prepared-general-banner.md)
(2026-10-23): pinned input/range checks, staged preparation, strict immutable Content
loading and preloaded Windows CPU/GPU preview. All 20 authored expectations have
regressions; locked restore, zero-warning Release build and all **1,388 tests** pass
(86 new). Two preparations match all files; two finite Windows checks reach tick
91 with byte-identical captures/reports and CPU/GPU readback. Prepared-only runtime
uses no BIN/reference paths. Core/event/save/package graphs remain unchanged.
Original comparison, loading latency, packet parity and opcode admission remain
open; next scene capability requires a separate source-traced selection.

**Scope:** Prepare only condition-8/banner-0 artwork from verified local inputs,
load it through Content and preview the existing logical effect in Client using
the selected deterministic CPU compositor. Load all resources before scene
creation. Use a 256x240 authored inspection background and explicit logical ticks/
Circle samples; keep original scene/event execution separate.

**Acceptance criteria:** Implement bounded AssetTool parsing and CLI conversion
with input/range identity checks, stable manifest IDs and repeatable output hashes.
Emit versioned normalized six-piece metadata, 256x200 unpacked indices and selected
RGB555/STP palette 4, retaining per-piece colors and add/subtract operations.
Content strictly validates formats, fields, ranges, references, hashes and the
selected profile; publish immutable data only after all validation succeeds.
Keep prepared outputs/provenance/captures ignored, authored test artwork distinct
from retail provenance, and failed preparation/load from damaging prior output or
allocating a scene task. Implement point-sampled RGB8 composition with neutral-128
modulation, vertical gradients, saturating add/subtract, correct crop/shared-clamp
boundaries and ordering-table traversal. Label modern precision/raster/background
approximations and unresolved native parity explicitly. Convert ZOD-077's 20
authored expectations into regressions, plus strict-load/rollback/replay and scene
restart checks. Render only accepted logical progress; omitted/repeated draws,
GPU/device events and failed ticks must not change effect/task/VM history or supply
termination facts. Demonstrate BIN/reference-free prepared loading, two matching
local preparations and a finite Windows preview/check with repeatable captures.
Retain existing event APIs/profiles and opcode-78 rejection, Core/Engine purity,
save schemas and package graphs. No other banners, original loading delay,
EventEnd, full BattleStart, map/camera integration or original equivalence claim.

### ZOD-080 — Trace bounded DisplayConditions admission and update order

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-074, ZOD-076, ZOD-078

**Result (2026-10-24):** The [source/contract selection](architecture/zod-080-banner-opcode-admission.md)
traces unsigned-byte operands, asynchronous start, one task-56 worker, native
event-before-child scheduling and preloaded readiness. Three retained original
states at source frames 9001/9002/9003 show event offset 807 / live worker,
807 / cleared worker, then 813 / a different condition. The first parent poll
after completion is on the next update. The current synthetic effect-first
adapter is preserved; 28 authored expectations remain unexecuted. No runtime
changes or new original run; last suite counts remain 1,494 .NET / 163 Python.

**Acceptance criteria:** Pin source revision and original evidence; trace operand
widths, callers, startup, slot/task lifetime, worker clear and parent visibility.
Select one condition-8/hold-0..255 preloaded authored profile with explicit phase
order, readiness, ownership, rollback/budget and legacy isolation rules. Record
independently authored expectations and a bounded implementation split; do not
enable original events, loading delays, Ready, other conditions or EventEnd.

### ZOD-081 — Prove ordered event/banner transactions in Core

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-080

**Implementation (2026-10-25):** [Ordered banner transactions](development/ordered-banner-transactions.md)
add a distinct typed `Zodiac.Framework` program/executor with private shared
VM/ledger phases. All 27 authored Core cases and 33 boundary regressions pass;
legacy effect-first APIs/traces remain unchanged. Locked restore, zero-warning
Release build, all 1,554 .NET and 163 Python tests pass. No Content/profile/raw
opcode or graphics changes; BOP-028 remains ZOD-082's gate. No automatic commit.

**Scope:** Implement the selected typed event-before-worker program/adapter using
the existing conditional executor, banner reducer and ledger validation. Poll/resume
the prior VM suspension before post-VM worker facts; do not step a newborn effect
at its birth tick. Publish clock, VM, task/effect states, identities and ordered
trace atomically. Preserve old API behavior/traces and the effect-first adapter.

**Acceptance criteria:** Convert ZOD-080's Core cases to automated regressions and
add meaningful boundary/late-failure checks. Natural task clear remains H+31 while
a waiting parent resumes H+32. Check Circle extra yield, hide ignore, mandatory
E5 yield, other owners, post-poll facts, cumulative budgets, rollback, checked
counters, protected managed identity, retained scene lifetime and deterministic
replay. Keep typed admission private to the atomic executor; no exposed half-tick
or callback. One ready condition-8 effect per scene, including terminal retention;
explicit Circle samples and resource/owner IDs. No AssetTool/Content/profile/schema,
retail import, startup-duration or graphics work. Run locked restore, zero-warning
Release build and appropriate full regressions.

### ZOD-082 — Prepare and execute the authored preloaded banner profile

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-081, ZOD-078

**Implementation (2026-10-25):** [Separate authored prepared profile](development/prepared-ordered-banner-event.md)
adds strict raw/processed conversion, manifest-verified loading/projection and
prepared-files-only Windows composition. Old schemas/profiles remain closed.
BOP-028 and 75 new regressions execute; all 1,629 .NET and 163 Python tests pass,
with locked restore and a zero-warning Release build. Two raw Windows runs repeat
all 17 files; processed state/trace/pixels agree, clear/resume is 91/92, and the
legacy RGB8 preview repeats all 16 files. Skipped admission, failed readiness and
whole original-event rejection are checked. No original event, loader duration,
new renderer or native-slot behavior is admitted. Standing owner Git permission
supersedes the historical no-automatic-commit criterion below.

**Scope:** Add explicit authored BATTLE/non-tutorial/speed-one opcode-78 conversion,
strict Content validation/projection and prepared-files-only host composition for
ZOD-080's proposed ordered preloaded profile. Load/validate artwork/resources
before scene creation and bind stable logical identities at the composition root.

**Acceptance criteria:** Decode condition and hold as separate unsigned bytes;
admit only one condition-8 record and hold 0..255. Keep old profiles/exports and
JSON contracts closed to 78. Reject WORLD, unsupported conditions/records even
unreachable, retail provenance and full event 4/401 playback. Verify authored raw/
processed preparation -> strict load -> ordered Core execution -> accepted-snapshot
presentation, failed-readiness safety and deterministic replay with omitted/repeated
draws. Runtime uses prepared files only; resources never enter Core. Demonstrate
finite repeatable host checks and retain explicit preloaded/startup/latency limits.
No other banner family, native slot emulator, EventEnd, full BattleStart or automatic commit.

### ZOD-083 — Trace Ready display assets and dispatcher/child lifetime

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-082

**Result (2026-10-25):** [Ready source/contract](architecture/zod-083-ready-display.md)
distinguishes condition 0/message 0 from condition 1's buggy helper. Verified image
0/palette 2, six full-corner glyphs, inherited readiness, ordered dispatcher/child
phases and extra yields. Two retained event-4 states independently bind the wait at
813, slots 1/3/4, resident code/tables/VRAM and queued scales 48/128. Two study
reports repeat. Thirty-two authored expectations remain unexecuted. Selected
source-derived clear/resume is H+8 child, H+9 dispatcher and H+10 event, not an
observed complete duration. No runtime, profile or test change; historical full
validation stays 1,629 .NET / 163 Python. Next ZOD-084, then presentation selection
and authored integration in ZOD-085/086.

**Acceptance criteria:** Pin source revision, callers, separate byte operands,
signed arithmetic/byte stores, data ranges/readiness, input sampling, child/task
ownership and scheduling. Preserve uncertainties and unsupported condition 1;
record independently authored cases and a bounded implementation split. Source
and queued-packet evidence must not be labeled original scanout/timing equivalence.

### ZOD-084 — Prove the ordered Ready dispatcher/child aggregate

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-083

**Completion (2026-10-25):** The [ordered Ready aggregate](development/ready-display-transactions.md)
executes all 28 Core contract cases and 25 additional boundaries. Dispatcher and
child lifetimes, logical draws, event-before-worker publication, retained identity,
explicit cancellation and atomic rollback are implemented without graphics or
profile changes. Locked restore, zero-warning Release build, **1,682 .NET / 163
Python tests** pass. Next ZOD-085; ZOD-086 still waits for its presentation policy.

**Scope:** Implement one typed preloaded condition-0/H-0..255 aggregate over the
selected event → dispatcher → child phases. The ledger's task 56 represents its
dispatcher; retain child progress/terminal state privately within the aggregate.
Preserve the old general-banner APIs, behavior and profiles.

**Acceptance criteria:** Execute RDY-001..028 and additional meaningful boundaries:
birth draw 48 (or 128 for H=0), no birth input/double-step, input wait overlapping
fade-in, release reset, seven fade-out submissions, empty extra yield, child clear
R+8/dispatcher clear R+9/parent resume R+10, other-owner waits, post-poll facts,
protected identity, retained lifetime, checked budgets/counters and full rollback.
Keep owner-acknowledged cancellation a separate native policy. Deterministic replay
must not depend on presentation reads. No callbacks/public half-ticks, raw opcodes,
Content/preparation/graphics, native slots or original loading behavior. Run locked
restore, zero-warning Release build and appropriate full regressions.

### ZOD-085 — Select Ready glyph preparation and rendering policies

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-083

**Completion (2026-10-25):** The [Ready presentation policy](architecture/zod-085-ready-rendering.md)
selects a separate RGB5 CPU compositor, four-corner triangle interpolation,
per-texel opaque/average STP handling, explicit draw state and strict prepared
artwork/resource readiness. Bank 2 has 14 opaque nonzero entries, one transparent
and one STP entry. Two source/queued-state/math reports repeat; 32 independently
authored presentation expectations remain unexecuted until ZOD-086. No runtime
renderer or original Ready scanout comparison was added. All 163 Python tests
pass; last full .NET validation remains ZOD-084's 1,682 tests. Next ZOD-086.

**Scope:** Extend the Ready source study into a bounded immutable artwork and
accepted-snapshot presentation contract: image 0, palette 2, six glyph quads,
four-corner scaled colors and average/STP blending with explicit GPU state.

**Acceptance criteria:** Trace arithmetic/order/coverage and relevant source helper
calls before selecting a compositor. Reuse existing original-state evidence only
within verified queued/display boundaries; retain any needed original captures as
separate gates. Specify safe offline input ranges, strict prepared validation,
resource-ready-before-scene ownership, independently authored expectations and
precise preview/fidelity limits. Do not relax the general-banner schema or silently
reinterpret its existing renderer policy. No new runtime renderer in this spike.

### ZOD-086 — Prepare and preview the authored Ready profile

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-084, ZOD-085

**Completion (2026-10-25):** The [prepared Ready preview](development/prepared-ready-event.md)
adds separate strict artwork/event formats, verified offline preparation and a
prepared-only Windows host. All 32 RPR cases and RDY-029..032 have checks; 112 new
.NET tests bring the total to **1,794**, with **163 Python tests** passing. Locked
restore and zero-warning Release build pass. Twelve finite scenarios produce 626
exact CPU/GPU/independent-model captures; repeated/raw-processed outputs agree and
all 16/16/17 legacy artifacts remain unchanged. Missing content/graphics failure
creates no check output. The owner approved the prepared gallery on 2026-10-25;
original displayed Ready comparison remains separate. Next ZOD-087.

**Scope:** Add separate authored raw/processed condition-0 conversion, strict
prepared-content projection and prepared-files-only Windows composition using
ZOD-084's aggregate and ZOD-085's selected presentation contract.

**Acceptance criteria:** Execute RDY-029..032 and RPR-001..032 presentation regressions
from the [selected policy](architecture/zod-085-ready-rendering.md).
Decode unsigned condition/hold separately; reject old profiles, unsupported
conditions/provenance/records even unreachable, missing readiness and retail
events 4/401. Prove finite repeatable receipts and rendering-independent replay;
no renderer completion acknowledgments. Keep one Ready per selected scene and
explicitly exclude concurrent shared-pool effects, combined general→Ready retail
playback, condition 1, original loading duration, EventEnd and full BattleStart.

### ZOD-087 — Validate displayed Ready glyphs against original references

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-086

**Result (2026-10-25):** Six source-bound displayed Ready anchors match actual
C# composition exactly, including 1,202 opaque / 77 background-sensitive STP
samples per visible frame and the empty extra yield. Two finite isolated runs
repeat 96 content artifacts; 1,794 .NET / 179 Python tests pass. No runtime arithmetic changes.
[Comparison record](development/displayed-ready-comparison.md) and
[metadata evidence](evidence/visual/displayed-ready-v1.json). The original-review
gallery was approved by the owner on 2026-10-25: "Visually, they look identical
to my eyes." Birth/next-frame endpoints stop inside interrupt
handling and remain explicitly missing; ZOD-088 owns that bounded context gap.

**Scope:** First inspect retained Ready artifacts and select a bounded original
reference method. Bind queued glyphs to actual displayed frames and recover
background-sensitive STP samples before comparing the C# Ready compositor. Keep
the authored prepared preview separate from original event/startup execution.

**Acceptance criteria:** Pin reference HEAD and artifact/code/table/texture/palette
identities; distinguish queued from displayed work and independent capture histories.
Retain the relevant draw environment and frame/worker context, meaningful opaque
and STP samples, exact comparison results and an owner-review gallery. Cover selected
birth/full/fade-out/empty anchors when supported, recording missing observations
explicitly. Needed new captures and background probes must have a finite, verified
procedure before execution. Do not infer original duration, Circle timing, full
event playback or hardware equivalence from a matching frozen image; preserve
historical evidence and legacy/general behavior.

### ZOD-088 — Bind displayed Ready birth frames through interrupted checkpoints

**Type:** Source/validation task · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-087

**Result (2026-10-25):** The two endpoints are BIOS timer-2 sound callbacks with
verified active exception save/restore records. Saved main VSync context binds
displayed ticks 0/1 (scales 48/56); actual prepared C# matches both original
scanouts exactly, including 1,202 opaque / 77 STP samples per frame. Two finite
runs repeat 32 content artifacts and the retained original states. Old main classification remains closed;
all 16 earlier Ready snapshots and seven C# artifacts are preserved. Locked
restores, zero-warning builds, 1,794 .NET / 192 Python tests pass.
[Source/comparison record](development/ready-birth-comparison.md),
[metadata evidence](evidence/visual/ready-birth-v1.json). The owner approved the new
two-row gallery on 2026-10-25: "That looks correct." No runtime, timing, hardware,
audio or full event playback claim.

**Scope:** Trace the retained birth/next-frame interrupt endpoints before deciding
whether their original display/submission context can be validated. Reuse the
hash-bound ZOD-087 source and failed selection captures; keep the main VSync
classifier closed. Select a finite continuation only if new observation is needed.

**Acceptance criteria:** Pin source/artifact identities and identify the actual
interrupt/return context, saved main registers/stack and submission position, or
document why these endpoints remain unsupported. If supported, add independently
authored negative/boundary checks and compare actual prepared C# birth/next frames
over isolated background controls with exact metrics and an owner-review gallery.
No guessed return chain, timing conversion, alignment fitting or tolerance change;
no original media committed or runtime dependency added. Hardware equivalence,
physical Circle timing, full event/startup playback and later CRT/audio remain
separate work.

### ZOD-089 — Compose the prepared general-banner → Ready scene

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-082, ZOD-088

**Scope:** Trace the event-4 display/barrier handoff and shared native artwork/pool
ownership, then implement one bounded authored preloaded sequence with the proven
workers and renderers. Keep original bytecode, startup duration and other profiles
closed; preload both resources before logical admission.

**Acceptance criteria:** One event/clock/consumer/ledger preserves predecessor
completion, successor birth and Ready child/dispatcher observation gaps without
overlapping live displays or consuming a birth Circle sample. Both bindings must
validate before the first task; late budget failures roll back every phase. Add
strict prepared loading/preparation, reproducible tests and finite Windows review
with repeated/independent CPU/GPU pixels. Preserve historical standalone outputs;
no renderer completion facts, original runtime dependency or full startup claim.

**Result (2026-10-25):** Source trace plus a separate five-operation authored
descriptor, pure composition and prepared-only Windows host are complete.
[Development record](development/banner-ready-sequence.md),
[metadata evidence](evidence/events/banner-ready-sequence-v1.json). All 74 new
managed checks pass; totals 1,868 .NET / 197 Python, zero-warning Release build.
Eight final scenarios yield 1,257 exact CPU/GPU/independent-model PNGs; natural
runs repeat all 164 artifacts. Missing/failed resource readiness creates no output.
Legacy RGB8/ordered/Ready preserves 16/17/72 files. Review gallery
`scratch/banner-ready-089-review/review.html` was approved by the owner on
2026-10-25: "This looks great." No new emulator
run, combined original scanout comparison or full original event playback.

### ZOD-090 — Trace Ready completion and battle-control ownership

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-089

**Scope:** Trace the verified event-4/common-401 tails, EventEnd cleanup and
scenario chaining, event-frame completion, turn selection and input/menu gates.
Keep original source study separate from the authored introduction's synthetic exit.

**Acceptance criteria:** Pin clean reference HEAD and input/code identities;
check retained Ready endpoints without launching an emulator; identify remaining
workers, unsigned/signed fields, scheduling and player/AI alternatives. Record
observation gaps and a bounded authored integration contract with future cases.
Do not infer player control from a hidden Ready image, task clear, input mode or
zero finish word. Preserve production code and historical evidence.

**Result (2026-10-25):** Both tails release/wait for the dark-screen worker and
restore scheduling before EventEnd; event 4 can recheck/chain scenario conditions.
Two retained states bind Ready birth and dispatcher clear, with event ownership
and held task 55 still present. Ten resident functions and 24 source files are
fingerprinted; two offline reports repeat exactly. All 208 Python tests pass
(11 new authored checks); last full managed validation remains 1,868 tests, not
rerun. [Source/contract record](architecture/zod-090-ready-battle-handoff.md),
[metadata](evidence/events/ready-battle-handoff-v1.json). No new original capture,
player-control observation, runtime/renderer change or full startup claim.

### ZOD-091 — Hand the authored introduction to the playable battle

**Type:** Story · **Priority:** P2 · **Status:** Complete for bounded implementation · **Depends on:** ZOD-090

**Scope:** One prepared authored banner/Ready introduction followed by the
existing synthetic two-unit battle in one Windows host. Reuse the existing
descriptor, workers, input latch and battle rules. Add a pure ownership gate;
limit refactors to the shared presentation/input routing this path requires.

**Acceptance criteria:** Execute the twelve
[authored handoff cases](evidence/events/intro-battle-contract-v1.json) from the
[selected contract](architecture/zod-090-ready-battle-handoff.md). Freeze battle
state/CT/RNG/journal before accepted intro event exit; child/dispatcher clear
alone cannot open the gate. Publish availability once, preserve full late-failure
rollback, discard queued/transition inputs and baseline held buttons. A later
fresh explicit command must match the existing battle controller and headless
replay. Prove readiness-before-admission, focus/device/draw independence, natural
and skipped finite native checks, and legacy standalone behavior. No retail
EventEnd, dark-screen release, original scene/startup/input-timing equivalence or
automatic player-turn selection is admitted by this authored path.

**Result (2026-10-25):** The pure one-time gate and prepared-only `--intro-battle`
entry point reuse the existing battle host/rules and input latch. All twelve IBH
cases have executed checks. Locked restore, zero-warning Release build and
1,909 .NET / 215 Python tests pass (41/7 new). Six native scenarios produce 1,049
exact CPU/GPU/independent-model intro PNGs and 36 byte-identical historical battle
PNG comparisons; natural runs repeat all 170 artifacts. Actual readiness failures
create no new output; standalone battle/sequence retain 6/164 files. An isolated
prepared-only run repeats all 170 artifacts. No original startup, EventEnd or
physical input-timing claim. [Implementation/validation record](development/introduction-battle-handoff.md),
[metadata](evidence/events/intro-battle-handoff-v1.json). The owner approved
`scratch/intro-battle-091-review/review.html` on 2026-10-25: "This looks good."
Approval covers the authored gallery; no physical input or original startup claim.

### ZOD-092 — Trace the original dark-screen transition worker

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-091

**Scope:** Trace the original DarkScreen/RemoveDarkScreen worker as the next
missing startup capability. Select one bounded logical/resource/presentation
contract before implementing it; reuse the existing task-ledger foundation.

**Acceptance criteria:** Pin reference HEAD and relevant source/input identities;
trace opcode admission/release, task 54 → 55 → 54 → exit, callers, parameter data,
arithmetic widths and ordering. Separate resource-loading waits from logical
progress and identify graphics/background/shared-resource ownership. Select a
bounded prepared-input/rendering policy and independently authored expectations,
recording unsupported modes and missing original observations. New original
continuations, if needed, require a finite verified procedure first. Do not add
a native reference/emulator runtime dependency, acknowledge tasks from rendering,
or claim full original startup/EventEnd from this source milestone.

**Result (2026-10-25):** Select the shared origin/divisor-12/angle-64/radius-4
style. Actual packets are untextured flat-color quads; BONUS loading prepares
shared later artwork. Source-derived approximate sqrt matches all 288 original
delays; exact integer sqrt would change six. Three retained holding states bind
both tile banks, flash/material and fourteen resident functions. Two reports
repeat exactly. Logical Holding birth is 55, first flash/entry-barrier observation
56; release R clears the task at R+56 and the parent observes at R+57. These are
post-readiness source deductions, not measured original durations. All 229 Python
tests pass (14 new); production C# is unchanged and 1,909 .NET is the prior count,
not rerun. [Source/selected policy](architecture/zod-092-dark-screen-transition.md),
[evidence](evidence/events/dark-screen-source-v1.json). Thirty authored future
cases remain unexecuted; entry/release original scanouts and runtime work remain open.

### ZOD-093 — Implement the pure dark-screen worker and task proof

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-092

**Scope:** Implement the selected post-readiness immutable worker and an atomic
ordered task/ledger/barrier adapter using prevalidated definition data. Keep
prepared conversion/graphics preview as subsequent work.

**Acceptance criteria:** Execute the logical/transaction cases from the
[DarkScreen contract](evidence/events/dark-screen-contract-v1.json), with explicit
ticks, pre-update draws, strict overshoot/extra pass, Holding birth/first-flash gap,
final release flash, exit yield and parent observation. Preserve one task identity,
phase revisions/tombstone, accepted no-match releases, no deferred early signal,
full late-failure rollback and render-independent replay. Reject unsupported
parameters, foreign ownership, concurrent burst workers and external task mutation.
No I/O, wall clock, GPU/renderer completion, PS1 library, raw event import, native
slot emulation, arbitrary modes, cancellation or full EventEnd/startup claim.
Document remaining preparation/presentation cases separately without marking them executed.

**Result (2026-10-25):** A separate typed authored adapter drives the immutable
worker and existing ledger atomically, with event-before-worker order, pre-update
draws, strict overshoot/extra yields, stable identity/revisions and no-match release.
Protected task updates, complete late-failure rollback, retained history and replay
are tested. All 1,967 .NET / 229 Python tests pass (58 new managed checks), with
locked restore and zero-warning Release build. Logical DSC-001..024 portions have
checks; actual prepared/resource readiness, GPU recreation, DSC-025..029 and native
output regression remain separate gates. Existing Content/Client profiles are
unchanged. [Behavior](behavior/dark-screen-worker.md),
[validation](evidence/events/dark-screen-worker-v1.json).

### ZOD-094 — Prepare and preview the bounded dark-screen transition

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-093

**Scope:** Prepare the selected original delay/rotation data offline, strictly load
a separate immutable authored definition/profile and preview accepted dark-screen
submissions with the selected untextured RGB5 flat-quad policy. Integrating the
held worker with banners/Ready and original entry/release capture comparison
remain subsequent milestones.

**Acceptance criteria:** Verify original inputs and pinned provenance; preserve
all 288 approximate-root delays and 117 fixed-point projections through bounded
prepared coordinates/delays and nine pairs of rotation axes. Reject unsupported
parameters, malformed/unready packages and existing output before scene start.
Use explicit opaque background, average blending, native order, signed clipping,
degenerate rejection and single shared-edge coverage; no texture/STP sampling.
Keep loading out of the pure worker and renderer completion out of task authority.
Repeat preparation and finite native/CPU/independent-model captures, test resource
recreation/render-independent replay, preserve old profiles/artifacts and provide
a gallery for owner review. Record DSC-001/022/025..030 layer coverage honestly;
original entry/release pixel equivalence remains unestablished without separately
planned source-bound original captures. Runtime uses prepared files only, without
BIN/EXE/math-table/reference-code or emulator dependencies.

**Implementation result (2026-10-25):** Verified offline EXE/table conversion,
strict separate prepared loading and a Windows untextured RGB5 flat-quad preview
are implemented. All 288 delays and 117 actual C# projections match original-table
arithmetic. Final finite runs yield 957 exact native/CPU/independent-model PNGs;
two preparations repeat both files and repeated/isolated normal runs repeat all
159 artifacts. Real readiness negatives create no new output; all 455 old native
artifacts remain unchanged. Locked restore, zero-warning Release build and all
2,031 .NET / 240 Python tests pass (64/11 new). The selected top-left/integer
coverage policy is provisional pending original entry/release comparison.
[Guide/evidence scope](development/prepared-dark-screen.md),
[metadata](evidence/events/prepared-dark-screen-v1.json).
The owner approved `scratch/dark-screen-094-review/review.html` on 2026-10-25:
"That looks good." Approval covers this authored gallery; original entry/release
pixel/timing equivalence remains open. This approval update reruns no implementation suites.

### ZOD-095 — Bind original dark-screen entry and release references

**Type:** Spike · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-094

**Scope:** Plan finite source-bound original entry/release observations and compare
the selected prepared flat-quad output. Establish any prerequisite retained state
or owner-operated capture before running a continuation. Combined banner/Ready
integration remains separate.

**Acceptance criteria:** Freshly pin clean reference HEAD and original identities;
bind event/worker, task phase, source counters, shared RGB/material, framebuffer/
OT state and queued-versus-displayed history for each selected observation. Use
a finite verified procedure before any new original continuation; preserve inputs
and bound execution/output. Compare actual C# over controlled explicit backgrounds
at zero tolerance and retain differences without guessing a cause. Resolve or
record edge/overlap coverage uncertainty; do not infer original duration or full
startup from frontend budgets. Keep original assets/states/media under scratch,
record metadata-only evidence, automated regression scope and separate owner
visual review. If a reference is missing, record the prerequisite rather than
claiming original pixel equivalence.

**Result (2026-10-25):** Thirteen source-bound entry/hold/release scanouts match
actual prepared C# at zero tolerance. Eleven have visible coverage, including the
24-pixel tail; two bind zero-coverage states. Material-only controls retain all
geometry and require exact CPU, other subsystem and GPU-register equality, with
RAM changes confined to selected RGB/E1 bytes. Earlier degenerate controls remain
rejected; the final policy has no CPU exception. Both final runs repeat 118
artifacts. Locked restores, zero-warning Release builds, 2,031 .NET / 255 Python
tests pass (15 new Python checks); nine actual host negatives preserve output.
Selected edge/overlap coverage agrees; full animation/endpoints, original duration,
map/hardware and combined startup remain open. The gallery
`scratch/dark-screen-095-f/review.html` was approved by the owner on 2026-10-25:
"This looks good." Approval covers these thirteen selected rows; the stated
remaining gaps stay open. This approval update reruns no implementation suites.
[Guide](development/original-dark-screen-comparison.md),
[metadata](evidence/events/original-dark-screen-v1.json).

### ZOD-096 — Specify the combined dark-screen introduction

**Type:** Design task · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-095, ZOD-091

**Scope:** Select the bounded authored composition of entering/held DarkScreen,
general banner, Ready, release/barrier and the existing battle ownership handoff.
Trace source ordering and define the shared-clock/ledger/readiness/presentation
contract before adding an integration profile. Preserve standalone APIs and
existing editable C# rules; avoid a general scene-engine refactor.

**Acceptance criteria:** Freshly pin source and record the selected event-4/401
command and draw/task order, distinguishing source behavior from authored loading
and hold choices. Define admission/preload failure, newborn behavior, event-before-
worker polling, cross-worker release, barriers, task lifetime, Circle ownership,
full rollback/budgets, render layering and event-exit battle/input gates. Specify
closed prepared-profile boundaries and any narrowly required refactor. Record
independently checkable automated cases and a finite native/owner-review plan;
leave future implementation cases explicitly unexecuted. Do not claim original
startup duration, missing DarkScreen endpoints or complete original event playback.

**Result (2026-10-25):** Selected one nine-operation authored program, one clock/
consumer/retained ledger, three task identities, event-before-worker polling,
post-poll facts, full rollback and a gate requiring all workers terminal at
synthetic exit. Frozen original DMA chains confirm DarkScreen before general/
Ready; event401's extra camera gap is preserved and excluded from the authored
program. Forty source files and fifteen resident ranges are fingerprinted; two
offline audit reports repeat exactly. All 269 Python tests pass (14 new authored
inspector checks); the prior full 2,031 .NET count is retained without rerun.
No production code, original execution/capture or combined scanout/timing claim.
Forty future cases remain explicitly unexecuted (24 ZOD-097; 16 ZOD-098).
[Contract and refactor boundaries](architecture/zod-096-combined-dark-screen-introduction.md),
[source metadata](evidence/events/dark-screen-intro-source-v1.json),
[future cases](evidence/events/dark-screen-intro-contract-v1.json).

### ZOD-097 — Implement the pure combined dark-screen introduction

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-096

**Scope:** Implement the selected nine-operation typed aggregate using existing
DarkScreen/general/Ready transitions and scene task/event phases. One pure
transaction owns admission, clock, ledger, signals and synthetic exit/availability.
Keep standalone programs/APIs/traces intact; no prepared profile or renderer yet.

**Acceptance criteria:** Execute DSI-001..024 with independently checkable state/
trace expectations, natural/zero/maximum/skip timelines, newborn and yield gaps,
held-worker coexistence, three stable retained identities, shared budgets,
protected bindings, deterministic replay and complete late-failure rollback.
Retain terminal tombstones; gate on accepted exit with all three tasks terminal,
never on rendering or a single worker's clear. Existing standalone no-match
releases and all old behavior remain unchanged. Run required managed regressions
and record separate executed evidence without changing historical future flags.
Do not implement original event playback, loader duration, EventEnd or battle rules.

**Result (2026-10-25):** Implemented the focused pure aggregate without modifying
existing workers/executors. One clock/consumer/ledger owns nine fixed operations,
three retained tasks, birth/input gaps, same-tick release/final flash, post-poll
facts and full late rollback. Availability requires accepted synthetic exit with
all three terminal; post-exit calls reject. DSI-001..024 and 56 added boundaries
pass. Independent cell/draw/display/barrier equations, variable authored delays,
rollback/retry and complete state/trace replay are checked. Locked restore,
zero-warning Release build, **2,111 .NET / 269 Python tests** pass (80 new managed).
Historical future flags remain unchanged; DSI-025..040 remain ZOD-098 work.
No prepared format, renderer, device/battle routing, native check, emulator or
original-duration claim. [Behavior](behavior/dark-screen-introduction.md),
[executed evidence](evidence/events/dark-screen-intro-worker-v1.json).

### ZOD-098 — Prepare and preview the combined dark-screen introduction

**Type:** Story · **Priority:** P2 · **Status:** Complete · **Depends on:** ZOD-097, ZOD-099

**Result (2026-10-26):** [Prepared integration](development/prepared-dark-screen-introduction.md)
adds a separate closed authored descriptor/CLI, three-resource readiness, ordered
RGB5 composition and the existing single-window battle/input handoff. Pure workers
and combat/save rules remain unchanged. Locked restore, zero-warning Release build,
**2,173 .NET / 278 Python tests** pass (62/9 new). Seven bounded native runs
produce **2,054 exact CPU/GPU/model frames**, six unchanged battle PNGs each,
normal/repeat/prepared-only equality and 581 unchanged retained historical files.
Twenty-seven final readiness negatives reject before window/output. DSI-025..039 have
executed evidence; **DSI-040 is approved**. On 2026-10-26 the owner reviewed
`scratch/dark-screen-intro-098-review/review.html`: "Looks good to me."
This approval update reruns no implementation suites and retains the recorded counts.
No original startup/duration, full renderer, CRT/audio or physical input claim.

**Scope:** Add the separately selected closed authored prepared profile, strict
three-resource readiness, accepted-snapshot DarkScreen-before-display composition
and existing single-window battle/input ownership bridge. Reuse prepared math and
artwork; standalone dark hold policy does not control the combined scene.

**Acceptance criteria:** Execute DSI-025..040 at their declared automated/native/
manual layers. Validate all schema/identity/provenance/readiness boundaries before
logical admission/output. Preserve RGB5 component policies and ordered composition,
render-independent replay, resource recreation and frozen battle/input ownership.
Use a recorded finite native plan (at most seven Windows processes, 4096 ticks/
120 seconds each), zero-tolerance CPU/GPU/independent-model checks, repeated and
prepared-only runs, old artifact/trace regressions and a new owner-reviewed gallery.
Record human approval separately. No original BIN/C/emulator runtime dependency,
original startup duration, new map renderer, CRT/audio policy or combat refactor.

### ZOD-099 — Audit replaceable engine and framework contracts

**Type:** Spike · **Priority:** P1 · **Status:** Complete · **Depends on:** ZOD-097

**Result (2026-10-25):** [Boundary audit](architecture/zod-099-replacement-boundary-audit.md)
pins managed baseline `4bda424`, inventories concrete project/type dependencies,
and distinguishes API, behavior, content/profile and save/replay compatibility.
Following owner clarification, its proposed API/assembly split is withdrawn from
the selected plan: current coupling is audited, but public contracts are premature.
ZOD-100/101 are deferred candidates requiring concrete usage and a fresh scope
decision before activation. No production interfaces/projects or
replacement execution are added. Documentation checks only; the prior full
validation remains 2,111 .NET / 269 Python tests. ZOD-098 is released.

**Scope:** Inventory current consumers, project/type dependencies and public
implementation leaks at both boundaries. Apply ADR 0006 to identify possible
future boundaries without selecting a public API or contract assembly now.
Distinguish application composition, internal introduction reuse, public values,
private state, content schemas and persistence. No blanket interface extraction.

**Acceptance criteria:** Pin the managed baseline and enumerate exact selected
files/types. Record coupling, existing behavior to preserve, future version/profile
implications and explicit non-goals. Define evidence triggers for revisiting
contracts and distinguish wiring from behavioral compatibility. Preserve existing
behavior, Relay and reference guards. Keep future work deferred until justified;
do not claim replacement from interfaces alone. No production type
migration, source-rule translation or runtime plugin system is required by this
spike. Its completion releases ZOD-098; wider migration is staged separately.

### ZOD-100 — Revisit an engine contract when substitution is needed

**Type:** Conditional spike · **Priority:** P3 · **Status:** Deferred · **Depends on:** ZOD-099

**Activation evidence:** A real second engine implementation, independent game/
consumer or demonstrated integration change needs substitution, and the selected
operation's semantics are understood. A synthetic duplicate algorithm alone is
insufficient. Reassess scope explicitly; do not activate from ticket dependencies.

**Scope on activation:** Study actual consumers and select the smallest useful
boundary. No API, duplicate grid types, contract assembly name or adapter design
is selected by ZOD-099. Preserve current behavior, RNG, Relay and ownership guards.

**Acceptance criteria:** Record the concrete need, consumer requirements, behavior
and value ownership before proposing an API. Specify an implementation-independent
proof with shared conformance cases and a real consumer, then create a separately
bounded implementation ticket. No whole-engine replacement or runtime-plugin claim.

### ZOD-101 — Revisit a framework contract when usage establishes a boundary

**Type:** Conditional spike · **Priority:** P3 · **Status:** Deferred · **Depends on:** ZOD-099

**Activation evidence:** A real alternate framework or independent application
consumer needs substitution and enough behavior is implemented to understand the
required boundary. The current two-unit prototype alone does not select that API.

**Scope on activation:** Trace the actual consumer's commands, observations,
content needs, lifecycle, rejection order, RNG/probe behavior and persistence.
No public session API or contract assembly is selected now.

**Acceptance criteria:** Pin the managed baseline, document the concrete need and
type dependencies, then propose minimal contracts and version/profile semantics.
Specify a replacement proof and compatibility cases before creating a bounded
implementation ticket. Preserve readable C# formulas. No full campaign API,
automatic interface extraction, executable plugins or whole-game replacement claim.

## Authoring, extensions, and later goals

### ZOD-047 — Build the first job/ability inspection and editing UI

**Type:** Story · **Priority:** P2 · **Status:** Implemented; further UI work and owner review deferred by owner · **Depends on:** ZOD-016, ZOD-018

**Owner direction (2026-10-26):** Keep the functional editor and its evidence;
defer further UI polish/review unless needed by a selected gameplay/content
milestone or requested by the owner. This is not approval or M3 completion and
does not block unrelated gameplay work. The [live FFTPatcher reference](development/fftpatcher-desktop-reference.md)
records the corrected v0.497.0/original-PSX reference and future workflow ideas.

**Owner-requested revision (2026-10-26):** The initial UI was not approved. The
[workspace revision](development/editor-workspaces.md) studies pinned FFTPatcher
category/list/inspector workflows and implements separate Jobs/Abilities pages,
independent search, source/copy views, related-ability navigation and routed
validation. Shared schemas/export protections remain. Full suites are now
**2,349 .NET / 293 Python**; 18 final native artifacts repeat exactly. New visual
and physical review remain pending; see the guide's isolated launch command.

**Result (2026-10-26):** [Host decision, contract and launch guide](development/job-ability-editor.md)
records the separate Windows Forms host, read-only baseline/namespace copy,
supported Move/Jump/JP fields, shared validation/export and exact simulator-based
movement preview. Twenty-nine new platform-neutral regressions pass; the full
suite is **2,346 .NET / 291 Python** with a zero-warning Release build.
One retained discovery run found a wrapped label; two final native runs repeat
all eleven files and pass an independent source/copy/output audit. No new
original rule, parser, combat formula, save schema or game-client source change.
[Executed evidence](evidence/authoring/job-ability-editor-v1.json) distinguishes
native control renderings from pending owner visual and physical workflow checks.

**Scope:** Choose an editor host for a small search/select/edit/validate/export workflow over the established definitions. Start with the fields and linked ability references from the first content slice; choose the UI technology through a short decision if required.

**Acceptance criteria:** Inspect a local original definition, create an authored copy/add-on, change a supported field, receive field-specific validation, export, and use it in the bounded scenario; read-only baseline source and authored changes have clear provenance. Unsaved changes and failed export are handled predictably. The editor uses shared validators/exporters and never reimplements combat formulas or edits the BIN.

### ZOD-048 — Expand authoring to flags, formulas, text, graphics, scripts, conditions, and audio

**Type:** Epic · **Priority:** P4 · **Status:** Needs breakdown · **Depends on:** ZOD-047

**Owner sequencing (2026-10-26):** Add editor capabilities when supported runtime
or content work needs them. FFTPatcher-inspired UI breadth alone is not a reason
to start this epic; further editor work is currently deferred as recorded above.

**Scope:** Add focused editing/preview workflows as the corresponding definitions and decoders mature. Named flags show verified meanings and unknown bits; formula parameters distinguish data from executable behavior; text has font/layout preview; sprite/animation/map/script editing uses established format and validation APIs.

**Owner's content-management goals:** Add, modify and organize music, graphics, scripted game events and
conditions as supported formats mature. Include logical-ID/reference inspection, asset association, shared
validation, package provenance and appropriate previews. Coordinate audio with ZOD-043, event interpretation
with ZOD-040 and package management with ZOD-049. Menu/action/condition declarations are scoped by ZOD-062.

Weapon/ability formula algorithms should also remain directly readable/editable in
C#; UI and data authoring complement that workflow. See the owner's
[formula/refactoring direction](development/editable-combat-formulas.md).

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

### ZOD-079 — Specify later display, rendering and Settings options

**Type:** Deferred design task · **Priority:** P4 · **Status:** Waiting — owner requested much later · **Depends on:** ZOD-025, ZOD-033

**Owner notes (2026-10-24):** Preserve intended original CRT/4:3 display proportions
through investigated pixel-aspect correction; provide an optional CRT shader;
discuss hardware/GPU versus software/CPU rendering and selectable resolutions;
introduce an in-game Settings menu as these options grow. Accurate audio/effects
discussion remains owned by ZOD-043. Details are in the
[later presentation decisions](development/later-presentation-decisions.md).
This planning entry does not change the active implementation queue.

**Owner clarification (2026-10-26):** Preserve CRT/4:3 visual proportions for
text, sprites, environments and other content inside a widescreen viewport.
Wider camera framing and UI layout supply the extra horizontal area; aspect
correction continues to preserve content proportions. Select no universal
stretch factor or renderer now; implementation remains deferred.

**Scope:** Record mode-aware original display mapping, diagnostic versus displayed
captures, CRT options, rendering-path tradeoffs, resolution terminology and the
Settings/persistence boundaries. Coordinate ZOD-042/043/053/059/060/061/063.
Do not assume a universal stretch factor, select a shader/backend now or require
two complete renderers before the comparison supports that choice.

**Acceptance criteria:** A design record identifies original versus optional
presentation profiles, evidence/open questions, proposed settings and defaults,
aspect/scaling/input transforms, representative visual/audio validation,
performance/deployment constraints and bounded implementation follow-ups.
Presentation settings leave simulation results unchanged; original media remain
local. No shader, renderer replacement, Settings screen or audio implementation
is required to complete this design task.

### ZOD-059 — Add a widescreen presentation mode

**Type:** Story · **Priority:** P4 · **Status:** Waiting · **Depends on:** ZOD-025, ZOD-031, ZOD-033

**Scope:** Add an explicitly selected widescreen viewport/camera and menu/HUD layout mode while retaining
the original presentation/reference profile. Decide how battle scenes, fixed layouts and cinematics fit.
The owner intends to rework the UI for the wider aspect ratio: review screen composition, menu placement,
information density and readability alongside ZOD-061's keyboard/mouse interaction design.

**Acceptance criteria:** Supported aspect ratios render with correct geometry, picking and UI placement;
resize does not stretch the scene or alter simulation coordinates/ranges. Original-profile comparisons
remain available. The same logical commands and seed produce identical gameplay outcomes across modes;
framing and unsupported scenes have documented decisions and manual checks.

**Owner clarification (2026-10-26):** The widescreen viewport must preserve the
CRT-correct appearance of text, sprites, environments and other rendered content.
Show more scene area horizontally and adapt the UI layout while retaining those
proportions. Pixel-aspect correction and wider composition are independent
concerns that operate together; retain the original reference profile and
raw-pixel diagnostics. Optional CRT processing remains a separate choice under
ZOD-079. Fixed scenes/cinematics and exact correction mappings remain unresolved.

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

**Later owner discussion:** Coordinate internal/output resolution and Settings
with ZOD-079's hardware/software rendering evaluation; multiple full rendering
backends remain a decision, not a prerequisite assumed by this ticket.

### ZOD-061 — Complete native Windows keyboard, mouse and controller interaction

**Type:** Story · **Priority:** P3 · **Status:** Waiting · **Depends on:** ZOD-032, ZOD-042

**Scope:** Extend the initial battle input proof into consistent keyboard/mouse and controller navigation
across supported game screens, using device-independent application actions and configurable bindings.
The owner intends UI changes tailored to native keyboard/mouse use. Review menu flows, direct mouse
selection, keyboard focus/shortcuts and device-appropriate prompts together with ZOD-059's wider layouts;
choose the concrete interactions when representative battle/menu workflows exist.

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

### ZOD-070 — Plan and implement selected original-game bug fixes

**Type:** Epic · **Priority:** P4 · **Status:** Needs breakdown · **Depends on:** ZOD-045, ZOD-049

**Owner's goal:** Fix selected outstanding bugs from the original game much later.
Collect reports and candidate corrections in the [original bug catalog](KNOWN_BUGS.md).
Individual fixes and correction semantics remain unselected; this does not change
the current implementation queue or original compatibility target.

Owner clarification (2026-10-25): resolving some original quirks may make future
modifications easier, but explore that much later after the affected behavior is
understood and regression-tested. Readable managed code and behavior-preserving
refactors can improve editability now without changing observable quirks. Do not
make quirk correction or a speculative correction interface an early prerequisite.

**Scope:** Once each affected system has a stable compatibility baseline, select
individual bugs and create bounded source-traced correction tickets. Corrections
belong to explicit FFT rules/profile options; preserve original behavior in the
default compatibility profile. FFT-specific corrections belong to Zodiac Framework,
not Tessera Engine.

**Acceptance criteria:** Each selected correction records a reproducible original
case, pinned source/evidence, intended changed behavior and regression checks for
both original and corrected profiles. Record implications for RNG/order, saves,
replays and content compatibility where relevant. Enabling a correction is explicit;
disabled corrections leave baseline regressions unchanged. No fixes implemented
or general scripting/plugin framework required by this planning entry.

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
