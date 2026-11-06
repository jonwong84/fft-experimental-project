# Tessera / Zodiac / FFT handoff

Updated 2026-11-06. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-421: Consolidate physical delivery profiles and definition ownership.**
The owner-selected [ZOD-422 preparation/AI refactor](architecture/ai-preparation-and-simulation.md)
is complete. `CombatRules.OrdinaryMagicPreparation` now proposes a pending plan
and primary-action caster statuses for both live ordinary casting and AI.
`MagicChargePlanQuery` remains a compatible plan-only adapter. AI query admission,
private candidate simulation and strike processing have distinct owners; existing
accounting and scoring remain shared. Current immutable hypotheses preserve
isolation and decision RNG rules. Original baseline/suspended-status semantics
are documented, but frame suspension and additional command families stay gated.

The owner-selected [ZOD-420 physical formula refactor](architecture/physical-formula-reassignment.md)
is complete. Shared `Zodiac.Framework.Formulas` and `.CombatRules` owners now
resolve neutral0x32/0x37 from immutable formula ID/X/Y data. Custom schema2 makes
assignment explicit; original schema1 import bytes remain identical. A custom
Dash selects Repeating Fist's formula through shared calculation, preview,
ranked AI, direct execution and frozen Move continuation, retaining its own
identity, range, execution evasion, metadata and grants. Formula-only edits retain
X/Y; they do not copy another ability's parameters. Named adapters remain for
existing API/event contracts; profile/definition consolidation is next.

Locked restore, zero-warning/error Release build, **5,087 .NET tests** and
**776 Python tests** pass. All1,308 AI cases pass. New public regressions compare
live/AI counters for immediate, delayed and raw-high-bit casts, replay/isolation,
support precedence and primary status proposals. The ZOD-422 receipt pins eight
source files/nine functions at clean read-only reference
`11464943bbdbd2a0af022d90cb7f10455cf911df`. All ten owner blobs, immutable
JobStats, int MP costs and the approved `b7a222d` formatting remain preserved.
New public Framework types have matching files. The native Basic Skill121-step/
two-replay and four magic eighteen-checkpoint proofs remain the prior ZOD-420
evidence; those native checks were not rerun for this refactor. Current logs and
TRX are under ignored `scratch/preparation-refactor-*`.

The [equipment selector study](architecture/equipment-effect-selection.md)
still owns the weapon-required-level/right-tie and right-first shield findings.
The owner also supplied formula/status-set documentation and pre-formula
disassembly; [the shared setup boundary](architecture/ability-effect-families.md#pre-formula-setup-boundary)
remains the design for wider families. General status/flag/equipment behavior,
namespace migration, editor, automatic facing, ZOD-106 and custom cone stay
separate. ZOD-418 ordinary magic aim search remains queued after the selected
refactor. Earlier five-hour runs are not active.

[Managed MP costs](development/managed-magic-mp-costs.md) now use nonnegative `int`
through spell definitions, resource calculation and recorded cast/resource cost.
Original packed readers retain byte fields; source-domain behavior is preserved.
This owner-requested representation adjustment is preserved by the later magic work.

[Framework public type files](development/framework-public-type-files.md) now give
all namespace-level public declarations matching files across Framework. The two
existing partial types keep `.PlannedMove.cs` parts; small internal helpers stay
with their owners. All 383 declarations, bodies, attributes, comments, imports and
namespaces are preserved. Nine folder batches compiled cleanly. AI, Content, host,
owner drafts/JobStats and the subsequent magic behavior scope are unchanged.

[Magic evasion](behavior/magic-evasion.md) records completed ZOD-342, ZOD-344, ZOD-345, ZOD-346, ZOD-347
from clean source `11464943bbdbd2a0af022d90cb7f10455cf911df` (13 files/21 functions).
`FaithMagicFormula.Resolve` now owns certified nonzero fresh formula entry,
including damage0x08's magic evasion and healing0x0C's bypass. `MagicEvasion`
owns loading, ordered status preparation, percentages and real accessory/right/
left/class draws even in AI/preview. Secondary execution Evadeable is independent
of primary AI metadata. Abandon/blocking labels remain diagnostic data. Branch
on hit, not rounded accuracy; finalization clears zero accuracy only afterward.
Counter failure returns the whole entry RNG with no partial outcome, even after
four evade draws before a failed proc draw. Fresh context also certifies the
stored result reaction clear: source initialization alone preserves that field.
No spell commands, live HP/status/RNG adoption or magic AI activation.

[ZOD-343](development/shared-magic-calculation.md) retains the already-hit
`FaithMagicFormula.ResolveAfterHit(request, rng)` calculation entry from explicit
amount/element/target/mode/profile inputs through finalized result data and
candidate RNG. Existing rule helpers and projection/raw APIs remain compatible;
the composed tail has one owner in FaithMagicFormula. Geometry, live publication,
commands and magic AI remain separate. Future formulas and targeting shapes are
independent selections. The owner-specified cone has centered forward rows of
1/3/5 tiles, excluding the actor, rotated with attack direction; its geometry is
recorded for later work and is not activated. Current limits and deferred gates
remain in force. The subsequent evasion trace is now complete.

[ZOD-341](behavior/magic-result-finalization.md) now finalizes explicitly living,
non-Petrify/Wall, no-Golem, empty-status HP work. It preserves signed fields and
miss labels, clears hit on zero accuracy, applies upper999 caps only, and retains
special effects through pseudo-status typing even on misses. Its composed path
privately owns routing/proc/empty-status/finalization and returns candidate RNG.
Unsupported facts or exhausted counters reject without partial output/draws.
Full output requires known entry flags even in preview; nullification proves
flags clear but still requires all target/work facts. No live HP/status/command
publication. Next trace evasion before widening the already-hit boundary.

[ZOD-340](behavior/magic-conditional-proc.md) now supplies an isolated proc decision:
execution uses floor(raw*100/32768)<19 and exactly one draw, even for empty status
record0 or zero amount. Preview requests the status without a draw/flag set; fresh
AI excludes it. Explicit primary/no-pending context and relevant clear-flag facts
are required. Unsupported context and counter exhaustion reject atomically.
Misses/nullification and0x0C skip without reading otherwise-unused profile facts.
After-hit composition uses the original formula family, never the HP route.
Returned RNG/flag-set/status-attempt data grants no live authority or status effect.

[ZOD-337–339](behavior/magic-elements-and-routing.md) now add stored damage weather/
affinities before Faith and raw absorption/undead routing afterward. Element work
must be explicitly fresh; Fire needs known no Oil, Earth known no Float/rider.
Unknown weather rejects. Healing bypasses damage's entire element context.
Nullification skips Faith and clears absorption markers. Absorbed damage can heal
undead, while0x0C healing becomes damage and requests clearing canceled-status
byte0. These earlier APIs retain signed/raw output; the separate ZOD-341 owner now adds
bounded final caps without status execution or live HP change.

[ZOD-334–336](behavior/faith-magic-amounts.md) retain the shared pre-Faith modifier/
product and signed Faith arithmetic. The old neutral-damage API and all44 prior
source descriptions remain unchanged. The new element-aware method reuses that
owner. New source audit pins nine files/14 functions with50 further descriptions.
No spell command, magic AI entry, charging, live MA/Faith state or target search
has been activated. Proc decisions now retain original family identity rather
than infer it from the routed HP direction; absorption does not change family.
[ZOD-333](architecture/ability-effect-families.md) remains the full formula-family
inventory; generated catalogs stay in scratch. Shared effects use data parameters
instead of per-spell coordinator classes.

[ZOD-329](development/dash-content-binding.md) now imports verified Dash into the
existing targeted schema1 companion and binds explicit learned grants and known
AI slots. Stone's previous output/API remains compatible through a shared importer.
Retail Dash's primary AI Evadeable trait contributes to list summaries while its
secondary combat flags still disable evasion. Actual effects remain authoritative:
AI uses midpoint damage; real Dash uses three draws. Both turn orders and missing
grant rejection pass through the public roster API.

[ZOD-330](behavior/readable-ai-authoring.md) keeps named traits authoritative in
AbilityMetadataDefinition. [ZOD-331](development/ai-refactor-331.md) supplies
AiDecisionMaker/AiCommandExecutor/AiTurnController and shared immutable configuration;
[ZOD-332](development/framework-source-layout.md) organizes Framework source/tests
with unchanged namespaces. Owner toggle drafts, JobStats, host/editor activation,
automatic facing, full corpse/terminal/frame RNG, broad interfaces and ZOD-106
remain separate/deferred.

The [original AI algorithm guide](architecture/original-ai-algorithm.md) maps
policy precedence and all core source functions, with bounded implementation
coverage and unresolved behavior marked. Start there for the overall algorithm.
The [AI completion roadmap](development/ai-completion-roadmap.md) preserves the
remaining breadth, integration, compatibility and replaceability goals. Use it
when selecting later children; the backlog still owns work order and deferral.

The [original post-prefix executor](behavior/sword-ai-execution.md) and already-moved
[prepared executor](behavior/sword-ai-prepared-execution.md) live in Zodiac.Framework.Ai.
Framework owns [Attack/targeted and self ACT/Wait/turn-ending Move transactions](architecture/ai-command-boundary.md) and
[planned Move continuations](behavior/planned-move-continuation.md), verifying
fresh planning/RNG before publishing commands. AI cannot construct trusted
sessions/receipts and has no production friend access.
Prepared entries own refresh/preparation and explicit in/out-of-reach selection
inside those transactions; post-prefix entries retain their general-selection certificate.
The original VSync callback also draws from BIOS RNG. Managed rendering/pauses add no
gameplay draws; frame-interleaved retail stream equivalence remains explicitly unresolved.

The current pre-ACT pure queries derive S/D0/D1 for focused-opponent, ordinary-sword
reach/policy facts and raw Move<=124 through explicit in/out-of-reach entries.
Movement is evaluated once; out-of-reach Blue/NonBlue adjustment runs afterward.
Private scores/RNG feed one Attack scan/ranked admission then the source lottery.
Empty ranking takes eight draws then MoveOnly at D0. Prepared moved/unmoved entries
derive focus/cache/active-target and first/terminal-entry defaults; earlier direct
selector APIs retain their certificates. No-Defend and other unrepresented policy
facts stay explicit; plans retain deferred ChooseAtWait. Failures restore whole-query
RNG without partial output. Original supplied-scenario APIs stay unchanged. These
pure queries grant no command authority or live RNG commit; execution adapters
own that separate boundary.

Attack-only and pair post-ACT execution privately own selection/dispatch, adopt verified RNG,
and commit either a confirmed path plus actual-receipt facing, or same-tile END.
It stops after settlement without a Move-first token, extra Attack or clock advance.
Broader original-ability search/ranking, a replaceable decision interface
and presentation activation remain future work.
Original UNIT_SLOT_COUNT is **21**; the old 26-foe wrap example is inadmissible.
This does not select a fixed managed roster size. The contract records the erratum
without rewriting old evidence.

## Current implementation and validation

- Content maintenance (ZOD-134, learning and current sword fields): renamed
  `AbilityMetadataDefinition` now stores immutable boolean learning switches.
  Packed type flags/enum are removed; offline adapters own original packing.
  Classification stays numeric, unresolved bit 4 stays opaque, AI bytes and JSON
  v1 are unchanged. [Mapping](architecture/zod-014-typed-definitions.md#definitions-and-preserved-values).
  `WeaponDefinition` now owns immutable weapon behavior/element records with
  original encoding in adapters; schemas 1/2 and the supported sword domain stay
  unchanged. [Weapon mapping](behavior/retail-sword-representation.md).
- Job content: sealed `JobStats` retains constructor-validated Move/Jump and
  construction-only growth/multiplier/evade initialization. Immutable per-game
  limits are passed through package/editor validation. Additional stats remain
  scaffolding outside metadata v1 and gameplay. [Limits](development/content-limits.md).
- Owner-draft cleanup: active content work and import-order cleanup are committed
  separately. Nine unused [definition drafts](development/content-definition-drafts.md)
  are preserved byte-for-byte and tracked as deferred scaffolding. The owner
  explicitly left `AbilityToggles` consolidation for later; it is not wired into
  current metadata, the editor or rules. Other unimplemented flag families stay deferred.
- Latest pure roster AI: [ZOD-296 fresh Attack](behavior/roster-sword-ai-attack.md)
  owns one general prefix and the derived first Attack occurrence. All useful
  enemy teams contribute range masks; origins and aim panels use map order.
  Actor origin substitutes D1/ACT-first, all others Move-first. Every candidate
  restores input state while supported RNG chains. Complete ties keep first;
  best compares against D0 without ranked insertion or lottery. Post-ACT skips
  before invocation; unentered Stone's unknown priority cannot gate Attack.
  Shared private formula/cast geometry/candidate/clock/scoring preserve pair
  results and guards. Earlier [sword candidate](behavior/roster-sword-ai-candidate.md)
  and [cast/self foundations](behavior/roster-ai-action-foundation.md) remain.
  [Roster Stone candidates](behavior/roster-throw-stone-ai-candidate.md) now
  share the existing formula and all-unit state/clock/scoring under a supplied
  clear-trajectory certificate. [Computed direct trajectories](behavior/roster-ai-direct-trajectory.md)
  now use exact SDK rounding, explicit sprite heights and every initial occupant.
  [Fresh ranked Stone search](behavior/roster-throw-stone-ai-entry.md) now owns
  a selected occurrence through one prefix with blocked retry and consumed aims.
  [Full supported roster list selection](behavior/roster-basic-skill-ai-selection.md)
  now composes those private fresh owners with stable rank insertion and source
  lottery. Its public plans remain pure; the new command owner performs its own
  fresh selection privately inside Framework. No cached trusted input or full-game
  stream equivalence is claimed. AI compact16,
  Framework slot21 and 16 other movement records stay distinct from uncapped
  storage; general policy/flat/MP0 guards remain explicit.
- Latest authoritative prerequisites: [ZOD-306–309 live Stone](behavior/roster-live-throw-stone.md)
  share exact Framework geometry, preserve AI compact16/legal origins and actual
  slot21 admission, resolve the first intercepted unit, and reuse the actual
  Stone formula/choice/result kernel with all-occupant push checks. Pair guards
  stay intact. Both roster ability kinds now share the opaque session publisher,
  real completion receipts, explicit facing and late atomic failures. Earlier
  [roster Move/Attack foundations](behavior/flat-roster-combat.md) remain.
  [Verified decisions/continuations](behavior/roster-decision-and-continuation.md)
  now support the fresh AI command/turn integration described above.
- Latest targeting foundation: [ZOD-272–276 roster queries](behavior/ai-roster-targeting.md)
  own ordered semantic live/decision facts without a fixed roster capacity.
  Live untargetability, cached out-of-play, active ability targets and imminent
  enemies remain distinct. Eligible-enemy nearest search preserves first-slot
  ties, dead/Reraise filtering and actor fallback. Active masks retain Confusion,
  actor exclusion and signed default-mode behavior; proximity sums only scenario0
  writes against the cached actor byte. Full status timing stays explicitly resolved.
  An internal adapter now supplies these owners from admitted pair live state;
  no two-unit guard, command authority, API, draw stream or recovery boundary changes.
- [ZOD-265–270 variable supported lists](behavior/variable-basic-skill-ai.md)
  retain preparation/scenarios, per-occurrence composition/lottery and actual
  complete turns. Missing/repeated/reordered self/Stone entries preserve source
  passes/ranks, grant inclusion, asymmetric threats and action-dependent cast keys.
  Post-ACT uses actual state with cleared entries and one fresh empty decision.
  Exact-session recovery retains accepted progress and priority knowledge.
- [ZOD-262/263 content-backed lists](behavior/content-backed-ai-lists.md) retain
  immutable source slots/order/duplicates, explicit known state, unusable Frog
  initialization and outer metadata/effect binding. Content references AI for
  setup; AI remains Framework-only and Framework never references AI.
- [ZOD-256–260 priority knowledge](behavior/ai-priority-workspace.md) retains
  owned fresh/retained keys, explicit unknown/invalidation boundaries and exact
  transactional publication/recovery. No event/loader memory reconstruction.
- [ZOD-250–254 derived three-entry turns](behavior/derived-basic-skill-ai.md)
  retain the shared pair scenario kernel, one preparation/movement evaluation,
  written-or-unread stateless guards and complete command/recovery ownership.
- [ZOD-248 supplied-scenario complete turns](behavior/basic-skill-ai-turn-execution.md)
  retain automatic post-ACT reach, published progress and terminal failure boundaries.
- [ZOD-244–246 three-entry post-ACT](behavior/basic-skill-ai-post-act.md) retains
  shared preparation, cleared actor entries, source movement/empty selection,
  explicit reach APIs and authoritative remaining-command settlement.
- [ZOD-240–242 fresh Basic Skill command](behavior/basic-skill-ai-command-execution.md)
  shares one pre-ACT selection owner across ACT-first, Move-first, empty END and
  unified dispatch. Existing frozen Attack/Stone/Wait tokens retain exact binding.
- [ZOD-238/239 actual self ACT](behavior/immediate-self-act.md) retains context-bound
  self-command/profile proposals, zero-resolution-draw PA+1/cap and full-result
  receipt binding. ACT-first leaves Move unused and adopts no projected PA/CT.
- [ZOD-233–236 three-entry AI](behavior/basic-skill-ai-selection.md) owns one fresh
  preparation/movement prefix, source-order ranks and terminal lottery. Supplied
  known-score S/D0/D1/general-entry assertions remain in the supplied entry;
  the separate derived entry now owns masks/destinations within provenance guards.
- [ZOD-230–232 Accumulate foundation](behavior/accumulate-ai.md) retains self PA
  arithmetic, +3/hit100 even at PA99, closed preparation/usefulness and the old
  isolated entry. Pair/map-entry APIs now share the extracted private owners;
  all prior AI cases preserve their streams and expectations.
- Existing pair execution: ZOD-224 adds [complete headless pair turns](behavior/attack-throw-stone-ai-turn-execution.md)
  with exact partial-progress recovery. ZOD-226–228 add [bounded Blue support](behavior/blue-attack-throw-stone-ai.md):
  explicit player-control flag clear, HP*128/maxHP>=45 and no NonBlue reach draw.
  Existing Attack/Stone scoring/selection/execution and both teams are preserved;
  terminal routing and Blue low-HP/player-controlled policies remain open.
- Latest source check: [readable AI authoring](behavior/readable-ai-authoring.md)
  rechecks the five-file/six-function static flag trace and37 original vectors.
  Area shape names preserve every bit;1,024 byte patterns roundtrip through
  semantic construction and old packages. No original/C execution.
- Organization: AI source/tests use Decision, Execution, Shared, Sword, ThrowStone,
  AttackThrowStone, Accumulate, Dash and BasicSkill folders with stable namespaces; [layout](development/ai-source-layout.md).
  Owner RNG migration is completed as `Zodiac.Framework.Random.RandomStateGenerator`
  / `RandomState`, with generic Tessera contract under Interfaces. Arithmetic,
  output, counters and persisted algorithm ID are unchanged.
- Algorithm reference: ZOD-212, [unified source algorithm guide](architecture/original-ai-algorithm.md).
  Ten chapters map initialization, policy precedence, search/evaluation, simulation,
  ranking/lottery, commands and RNG. The source index covers 105 core functions,
  eight external boundaries and 11 direct core rand sites at the pinned revision.
  Structural completeness is not full behavioral equivalence or implementation.
  [ZOD-209](architecture/zod-209-pair-post-act.md) remains the detailed post-ACT source design;
  [ZOD-221](architecture/zod-221-fresh-pair-command-entry.md) traces single-entry reach/dispatch;
  [ZOD-225](architecture/zod-225-blue-pair-policy.md) traces Blue policy boundaries.
- Validation: ZOD-420 passes locked restore, a zero-warning/error Release build
  and **5,080 .NET tests** (1,807 Framework,1,305 AI,895 Content,396 CLI,206
  AssetTool,447 Client,24 Engine). All **776 Python tests** pass. Final seven TRX
  reports/logs are in ignored `scratch/physical-formula-420/trx-all-final-cap`.
  [Receipt](evidence/combat/physical-formula-assignment-implementation-v1.json).
  Eighteen independent physical vectors and focused source fingerprints cover
 0x32/0x37 arithmetic, critical/Brave knockback, evasion, mode RNG, caps and
  rollback. Custom JSON integration reaches full ranked AI and actual turns in
  both action orders. Original Dash/Stone imports repeat retained schema1 bytes.
  Native `native-basic-reviewed` matches all121 retained steps, two journal
  replays, drawing and final gameplay hash; `native-fire`, `-elemental`, `-healing`,
  `-learning` retain eighteen full replay checkpoints and prior gameplay hashes.
  These programmed routes do not establish physical input or original fidelity.
  No original or C execution is claimed. Owner-file blobs and approved formatting
  are unchanged; all474 public Framework declarations have matching files and
  existing partial parts. No asset inventories are committed. MP remains int;
  JobStats, AbilityToggles, AttributeToggles, EquipmentSlot and other owner drafts
  remain preserved. Broader status/modifier/terrain/equipment/terminal/frame RNG,
  general flag/status catalogs, editor, automatic facing, ZOD-106 and custom cone
  remain separate. Python uses pinned Pillow12.3.0 under ignored
  `scratch/accumulate-ai-229/python-deps`, with TEMP/TMP in its `python-temp`.
  Historical receipts retain their baselines; they are not refreshed for this
  refactor. The prior delayed-magic implementation remains validated by the
  whole-solution suites and unchanged native magic checks.
- A playable synthetic battle, Accumulate/Throw Stone slices, prepared scene/
  introduction proofs and synthetic world Save/Load exist. Original campaign,
  full combat/content, audio and general AI remain incomplete. See the
  [capability overview](../README.md#what-works-today) and [coverage inventory](coverage/README.md).
- Native Save/Load ZOD-069 has visual and owner-reported interactive approval;
  [procedure and evidence](development/world-save-load-ui.md). Editor approval is
  separate and still pending.

## Constraints and owner preferences

- Continue in `C:/repositories/fft-experimental-project`, branch
  `feature/initial-development`; inspect Git status before editing. Preserve the
  owner's tracked weapon/job/content-package/editor/exporter/test refactor and
  untracked configuration plus Ability/Common/Item/Job/Status definitions. Exclude
  them from unrelated commits. Last build includes drafts present at validation.
  Validated completed work may be committed/pushed.
- Reference `C:/repositories/fft_decomp` is read-only. Last clean recorded HEAD:
  `11464943bbdbd2a0af022d90cb7f10455cf911df`. Recheck before new source tracing.
  Runtime uses managed code/prepared files; BIN/extracted assets stay ignored.
- Preserve original behavior as default, including quirks. Keep formulas readable
  C# and future AI implementations replaceable. Brief enemy presentation pauses
  must stay outside deterministic simulation/RNG. Pause between bounded milestones.
- [Test policy](development/test-maintenance.md): meaningful behavior, boundaries
  and replay matter; test count/private structure/historical hashes are not current
  correctness contracts. Keep original and custom expectations scoped appropriately.
- [Content flag policy](architecture/overview.md#content-flag-representation): named
  booleans for independent semantic flags, original byte packing in adapters,
  unknown bits preserved. ZOD-134's learning/current-sword families are migrated; extend other
  families only when their mapping is ready or the owner requests it.
- [Equipment-break slot intent](development/content-definition-drafts.md#equipment-break-slot-selection-deferred):
  the owner created `EquipmentSlot` for Knight's Break Helm/Armor/Weapon/Shield.
  ZOD-419 now traces the original selector, hand/category mappings and caller
  boundaries. Preserve the draft until managed equipment-family implementation;
  its ordinals are not source encodings. Shared formula/rule code and data-defined
  abilities are the agreed design direction, with explicit typed selector policies.

## Deferred and unresolved gates

- **Editor ZOD-047/048:** further UI work/review waits for a real gameplay/content
  need or owner request. Jobs/Abilities pages exist but are unapproved.
  [Workspace guide](development/editor-workspaces.md); [correct FFTPatcher 0.497 reference](development/fftpatcher-desktop-reference.md).
- **Battle checkpoints ZOD-106:** wait for a concrete debugging/suspension need.
  Accumulate PA is battle-only; this does not block ability or campaign work.
- **ZOD-100/101:** broad replacement contracts require a real alternate consumer
  or implementation. Remaining ZOD-134 semantic-flag families stay deferred.
- **ZOD-201:** investigate unknown flags as original content is mapped to readable
  packages; decide semantic names, compatibility needs and evidence-backed omission.
  Keep unresolved raw values in offline provenance pending that decision.
- **ZOD-142:** Lion War event instruction upgrade investigation is deferred.
  ZOD-039 owns remaining toggle mappings and armor/accessory/consumable definitions;
  review the owner's evolving types before selecting an import child.
- Remote Linux/Windows CI and clean-machine launch evidence remain open under
  ZOD-006/034; original comparison scope remains partial under ZOD-033.
  Later display/Settings options and original bug fixes retain owner sequencing.

## Keep this handoff current

Replace stale state here rather than prepending milestone narratives. The backlog
owns open scope/order; design/evidence notes own details; the history index owns
completed work. Archive significant transitions only when useful. Use historical
snapshot audits for old hashes, not old verifier commands against changed code.
