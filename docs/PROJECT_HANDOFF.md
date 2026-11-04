# Tessera / Zodiac / FFT handoff

Updated 2026-11-04. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-247: trace complete three-entry headless-turn orchestration and recovery.**
[ZOD-243–246](behavior/basic-skill-ai-post-act.md) now own fresh post-ACT
preparation, derived explicit in/out-of-reach movement, empty selection and
actual remaining Move/END/facing from boosted state. Next compare complete pair
turn ownership, single-preparation automatic reach routing and recovery before
selecting bounded children. Preserve actual effects and accepted command progress.
Pre-ACT known-score S/D0/D1 remains supplied; broader search/policies, UI and battle
saves remain separate.

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
Full AI search, general multi-entry ranking/lottery, replaceable decision interface
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
- Latest implementation: [ZOD-244–246 three-entry post-ACT](behavior/basic-skill-ai-post-act.md)
  shares preparation admission, retains first-pass harmful/helpful/reach summaries
  and clears every actor action. Existing lower kernels derive movement and the
  empty lottery, with no self coin/action scan. Framework settles actual Move/END
  from boosted state with per-command rollback and no extra ACT/token/tick.
- [ZOD-240–242 fresh Basic Skill command](behavior/basic-skill-ai-command-execution.md)
  shares one pre-ACT selection owner across ACT-first, Move-first, empty END and
  unified dispatch. Existing frozen Attack/Stone/Wait tokens retain exact binding.
- [ZOD-238/239 actual self ACT](behavior/immediate-self-act.md) retains context-bound
  self-command/profile proposals, zero-resolution-draw PA+1/cap and full-result
  receipt binding. ACT-first leaves Move unused and adopts no projected PA/CT.
- [ZOD-233–236 three-entry AI](behavior/basic-skill-ai-selection.md) owns one fresh
  preparation/movement prefix, source-order ranks and terminal lottery. Supplied
  known-score S/D0/D1/general-entry assertions remain required; derived scenarios
  and complete-turn orchestration remain open. Explicit post-ACT branches now exist.
- [ZOD-230–232 Accumulate foundation](behavior/accumulate-ai.md) retains self PA
  arithmetic, +3/hit100 even at PA99, closed preparation/usefulness and the old
  isolated entry. Pair/map-entry APIs now share the extracted private owners;
  all prior AI cases preserve their streams and expectations.
- Existing pair execution: ZOD-224 adds [complete headless pair turns](behavior/attack-throw-stone-ai-turn-execution.md)
  with exact partial-progress recovery. ZOD-226–228 add [bounded Blue support](behavior/blue-attack-throw-stone-ai.md):
  explicit player-control flag clear, HP*128/maxHP>=45 and no NonBlue reach draw.
  Existing Attack/Stone scoring/selection/execution and both teams are preserved;
  terminal routing and Blue low-HP/player-controlled policies remain open.
- Latest source study: [ZOD-243](architecture/zod-243-three-entry-post-act.md)
  pins 25 functions across seven files for post-ACT counts/clearing, actual PA,
  movement/empty selection and settlement. [ZOD-237](architecture/zod-237-self-act-command.md)
  retains the actual self-action boundary; [ZOD-233](architecture/zod-233-three-entry-ai.md)
  and [ZOD-229](architecture/zod-229-accumulate-ai.md) retain pre-ACT entry evidence.
- Organization: AI source/tests use Shared, Sword, ThrowStone, AttackThrowStone,
  Accumulate and BasicSkill folders with stable namespaces; [layout](development/ai-source-layout.md).
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
- Validation: locked restore and zero-warning/error full Release build pass;
  **4,211 .NET cases** (1,467 Framework, 999 AI), **539 Python tests**,
  pinned-source/prepared-data, algorithm-guide and coverage checks pass.
  Thirteen new AI cases cover actual boost/cap/damage/push, cleared entries,
  retained summaries/grants, derived reach branches/RNG, empty lottery, Move/END,
  exact budgets, late facing rollback and both teams/actor identities. Logs and
  seven full TRX files are under ignored `scratch/three-entry-post-act-243-246/`.
  Python uses repository-pinned Pillow 12.3.0 from ignored
  `scratch/accumulate-ai-229/python-deps`, with TEMP/TMP in its `python-temp`.
  Bundled Pillow 13.0.0 triggers six existing version guards; WindowsApps Python
  is unavailable. The preceding cleanup is committed/pushed with all owner code
  preserved; reports remain under `scratch/content-cleanup/`. AbilityToggles and
  other unused definition drafts are unchanged by this AI work. Nine historical
  checkpoint audits last passed at ZOD-158; unchanged snapshots were not rerun.
  No original/C execution, host/editor activation or UI review.
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
