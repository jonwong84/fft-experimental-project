# Tessera / Zodiac / FFT handoff

Updated 2026-11-09. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-443–446 are complete. Next: the owner's review, before further development.**
Read [BACKLOG.md](BACKLOG.md), the
[pathway names](architecture/default-ability-pathways.md),
[execution names](architecture/default-ability-execution-names.md),
[context API](behavior/ordinary-spell-context.md) and
[list/scenario study](behavior/ordinary-spell-list-integration.md).
The caller now exposes `Evaluate(context, request, rng)`. `DefaultAbilityAiContext`
holds only the immutable captured battle state and matching catalog; constructor
validation rejects nulls and mismatched definition instances. The request owns
actor/slot/profile/scenario. RNG remains explicit, and all per-call admission,
private simulation and whole-entry rejection are preserved. Constructor/null
argument exceptions and rejected-search results have documented distinct contracts.

Create a new context for a new live state. Old contexts still replay their old
snapshot and grant no command authority. Begin review at
`src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiContext.cs`, then the request
and `DefaultAbilityAiAbilitySearchQuery.cs`. Other query parameter shapes are
unchanged; former OrdinarySpell and OrdinaryMagic type/file prefixes now use DefaultAbility.
This interim review does not complete ZOD-432 or the full AI port. Select a bounded
shared-preparation/list child after review; no further ticket is activated now.

The study confirms general ability evaluation runs scenario1 once; scenario0
owns the move-only baseline/fallback. Complement is a diagnostic mask, not an
automatic second general list. Original lists include Attack, primary, secondary
and Frog; current spell rows are a subset. Initialization, current usability and
entry loading have distinct ownership. Ranked insertion resets best even on a
failed gate and retains stable duplicate ranks when space remains. Full dispatch,
ranked insertion/lottery and command integration remain separate.

Start one-slot/scenario caller search at
`src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiAbilitySearchQuery.cs`.
It binds fresh work and movement once, then calls
`DefaultAbilityAiAbilityEvaluation.AiAbilityOutcomeEvaluate` →
`OriginReachableEvaluate` → `OriginAimTilesEvaluate` → shared
`AiActionSimulateAndScore`. Moving/still unit groups and tile search retain one
best and RNG; remaining aims rebuild per pass. The source best reset belongs at
the later insertion boundary, which this query does not execute. Exact ties
retain the earlier pass; equal ranks may improve final-destination proximity.
Only the final rank-only movement-baseline gate is reported. Private resources
restore between hypotheses and late rejection restores the entire entry RNG.
Standalone pass and single-origin queries retain their previous contracts.

ZOD-444 renames the former `OrdinarySpell` prefix to `DefaultAbility`, including
shared geometry/targeting, adapters, diagnostics and tests; AI folders are
`DefaultAbilities`. Source routine method names, C# namespaces, CLI commands,
prepared schemas/IDs, gameplay and supported-domain gates are unchanged. The
[naming guide](architecture/default-ability-pathways.md) records the owner's
anticipated Default/Attack/Defend/Change Equipment/Item Inventory/Aim/Elements/
Jump/Katana Inventory/Weapon Inventory/Mathematics/Monster use cases. Those names
do not activate new loaders or ability support. Historical hash receipts remain
unchanged; use the rename map for their older paths.

ZOD-445 extends DefaultAbility naming to the former `OrdinaryMagic` Framework
battle/commands/events/units/preparation/publication/learning family and dependent
content, import, client and test classes. `DefaultAbilityBattle.Apply` and
`DefaultAbilityBattleState` are the current execution/state entry names. The
three project links for the moved `AuthoredDefaultAbility` fixture are updated.
The same magic-focused mechanics, formats/IDs and CLI commands remain; no
general ability support or AI command integration is activated.

ZOD-446 adds the owner-requested [formula dispatcher](architecture/ability-formula-dispatch.md).
`AbilityFormula`/`AbilityFormulaId` replace the old physical definition names.
Definitions own `AbilityFormulaParameters` (X/Y/Z); `Evaluate(context, rng)`
dispatches to the supported0x32/0x37 handlers. `AbilityFormulaContext` owns the
admitted PA/Brave/mode inputs; `AbilityFormulaMode` replaces PhysicalDamageMode.
Formula-specific critical/Brave RNG moves from PhysicalDamageRules into the
handlers; evasion, upper999 cap and whole-calculation rollback remain with the
combat rules. Actual, preview and AI callers use the shared entry. The raw-roll
CalculateDamage adapter remains for existing arithmetic callers. Z is reserved
and nonzero values reject; no Faith/equipment/status context, new formula family
or prepared schema expansion is activated. Owner review of this API and the AI
flow resumes next; no further development ticket is activated.

Latest validation: zero-warning/error Release build, **5,302 .NET tests**
(1,861 Framework, 1,471 AI) and **810 Python tests** pass. Existing source fixtures
and gameplay expectations are unchanged; 26 dispatcher/context cases are added.
Logs/seven full TRX reports are in ignored `scratch/ability-formula-*`. The
[receipt](evidence/maintenance/ability-formula-refactor-v1.json) records source
fingerprints, prior-test parity and preservation. Owner enum/test formatting is
carried forward under the API edits; owner drafts and disabled CI remain intact.
Historical receipts/fixtures retain their original payloads. Coverage remains
58 planning entries and partial.

The owner-authorized query/battle-state formatting was committed in `973b39e`.
Its layout is preserved under the subsequent prefix substitutions. The old
`OrdinaryMagicBattleState.cs` blob `2dc345d4887b2ac3de98b5f74f7b4ddc9300cd54`
belongs to that historical spelling; the ZOD-445 receipt maps it to the current
`DefaultAbilityBattleState.cs` without other code changes.

The owner's concurrent controller formatting is preserved exactly in separate
commit `0e868ac`; its non-whitespace text is unchanged. Current
`AiTurnController.cs` blob is `05d8eb9b12cbd1430daacfb5e960d53d1199ef3a`.
All ten owner drafts/immutable JobStats and disabled CI remain unchanged.
Mixed/healer lists, retained workspaces, general status/terrain/Golem and
terminal/frame RNG keep their gates. No C, original executable or native
presentation ran. Reference remains clean at
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
study, bounded ZOD-437–441 implementations, ZOD-442 integration study,
ZOD-443 context API, ZOD-444/445 naming maintenance and ZOD-446 formula refactor. ZOD-428 and complete inventory
groups remain open. Prior helper
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
