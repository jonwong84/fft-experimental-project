# Tessera / Zodiac / FFT handoff

Updated 2026-11-08. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-438 is complete. Next: ZOD-439, evaluation at one fresh spell origin/pass.**
Read [BACKLOG.md](BACKLOG.md), the
[origin study](behavior/ordinary-spell-movement-origins.md),
[movement baselines](behavior/ordinary-spell-movement-baselines.md) and
[cast-origin enumerator](behavior/ordinary-spell-cast-origins.md).
Evaluate one freshly derived potential origin/pass through shared spell
simulation before adding cross-origin traversal or consumed-aim handling.
Full magic dispatch/lottery/commands remain separate.

Start potential origin diagnostics at
`src/Zodiac.Framework.Ai/OrdinarySpells/OrdinarySpellAiOriginQuery.cs`.
Follow shared `BuildPotentialPasses`, fresh movement `Build`,
`AiCastAimTilesBuild`, `BindFresh`, `OriginDestinationFinalize` and
`OriginAimTilesBuild`. The query preserves unit/tile masks, ranked reverse ties,
exhaustive map order, ACT-first D1 substitution and per-origin selectable ranges.
Unread priority and byte-count aliases reject atomically. No aim is consumed,
no spell formula executes and no draw occurs after movement preparation.
Explicit Main/Complement selection is diagnostic, not source policy dispatch.

Latest validation: Release build with zero warnings/errors, **5,217 .NET tests**
(1,835 Framework, 1,412 AI) and **802 Python tests** pass. Twelve new managed
cases include a real 256-panel aim union, unknown reads, replay and rollback.
Final logs and seven separate TRX files are under ignored `scratch/zod438-*`;
the [receipt](evidence/ai/spell-cast-origins-implementation-v1.json) records pinned
source/prepared evidence and preservation. Current coverage has 58 entries.
All ten owner drafts, immutable JobStats, approved controller formatting and
disabled CI are preserved. Mixed/healer lists, retained workspaces, moved-origin
formula evaluation and autonomous magic remain gated. No C, original executable
or native presentation ran. The reference remains clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

## Current navigation and owner direction

Start AI decisions at `src/Zodiac.Framework.Ai/AiDecisionMaker.cs`, actual commands
at `AiCommandExecutor.cs`, and supported complete turns at `AiTurnController.cs`.
The [routine ownership guide](architecture/ai-routine-ownership.md) follows
`ActionSelect` / `GeneralSelect`, `ScenariosBuild` / `ReachableTilesScore`,
`AbilityListEvaluate` / `AiAbilityOutcomeEvaluate`, `AiBestRankInsert`,
`RankedSelect` and `FallbackSelect`. Ordinary spell hypotheses follow
`AiActionSimulateAndScore` → `ActionHitsSimulate` → `StrikesSimulate`.
These are bounded routine counterparts; full original AI and host activation
are not complete. The [completion roadmap](development/ai-completion-roadmap.md)
records the remaining areas.

Recognizable source routines should have corresponding named methods.
`CombatRules/KnockbackRules.KnockbackCalculation` now shares the admitted landing
proposal between physical abilities and sword criticals, calling
`KnockbackDestinationCheck`. Trigger RNG, lethal suppression and actual movement
retain separate owners. General layers/falls/mounts are still deferred; see the
[managed/source mapping](behavior/knockback-calculation.md#managed-entry-point).
Use explicit if/else for nontrivial choices; keep shared mechanics/formulas in
code and individual abilities/content data-driven. Preserve inactive toggle and
EquipmentSlot drafts; original packing/unknown flags remain adapter/provenance work.

After full AI coverage and the ZOD-432 owner readability walkthrough, prepare
and review a PR into `main` for the owner's trunk-based workflow. Review the
entire branch and actual SonarQube/CodeRabbit findings before merging. CI is
intentionally disabled with the owner's committed approval and must stay so
until explicitly re-enabled; current validation is local.

The [review programme](development/documentation-review-programme.md) and
[progress receipt](evidence/maintenance/documentation-review-progress-v1.json)
record completed bounded batches through ZOD-436, the supporting ZOD-426
study and bounded ZOD-437/438 implementations. ZOD-428 and complete inventory groups remain open. Prior helper
cross-checks and annotation corrections live
in their behavior/evidence notes; use [history](history/README.md) for completed
ticket checkpoints, not old next-task advice.

## Current implementation and validation

Milestone-specific validation counts below are historical checkpoints. The latest
full validation and next task are recorded above.

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
  owner's weapon/job/content-package/editor/exporter/test work and the tracked
  Ability/Common/Item/Job/Status drafts. Keep unrelated edits out of commits.
  The current validation includes those preserved definitions. Validated
  completed work may be committed/pushed.
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
