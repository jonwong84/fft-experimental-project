# Tessera / Zodiac / FFT handoff

Updated 2026-10-29. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-205: retain a targeted ACT across a confirmed Move.**
The [targeted-ability transaction](behavior/targeted-ability-decision.md) now owns
actual ACT/RNG adoption and complete-result receipts. Extend the existing Move
proposal/token with a frozen targeted command/profile, adopting RNG once and
continuing on actual post-Move state. Failed ACT preserves the committed Move.
Stop before AI adapters or post-ACT pair reselection. Unknown scores within range4
still reject; retained/event workspace history and general lists stay open.

The [AI completion roadmap](development/ai-completion-roadmap.md) preserves the
remaining breadth, integration, compatibility and replaceability goals. Use it
when selecting later children; the backlog still owns work order and deferral.

The [original post-prefix executor](behavior/sword-ai-execution.md) and already-moved
[prepared executor](behavior/sword-ai-prepared-execution.md) live in Zodiac.Framework.Ai.
Framework owns [Attack/targeted ACT/Wait/turn-ending Move transactions](architecture/ai-command-boundary.md) and
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

Post-ACT execution now privately owns selection/dispatch, adopts verified RNG,
and commits either a confirmed path plus actual-receipt facing, or same-tile END.
It stops after settlement without a Move-first token, extra Attack or clock advance.
Full AI search, general multi-entry ranking/lottery, replaceable decision interface
and presentation activation remain future work.
Original UNIT_SLOT_COUNT is **21**; the old 26-foe wrap example is inadmissible.
This does not select a fixed managed roster size. The contract records the erratum
without rewriting old evidence.

## Current implementation and validation

- Latest implementation: ZOD-204, [targeted ACT authority](behavior/targeted-ability-decision.md).
  Invocation-bound command/profile proposals use actual Throw Stone rules, atomic
  RNG adoption and complete-result receipts. Seven scenarios cover four/seven
  actual draws, PA/push/KO, stale authority, budgets, rollback and replay.
  Pure pair AI selection remains separate from this Framework primitive.
- Latest investigation: ZOD-203, [Throw Stone authority](architecture/zod-203-throw-stone-command-boundary.md).
  Ten source files/21 functions and fourteen managed study snapshots distinguish
  prediction from real resolution, dispatch/reselection and per-command transactions.
  Eight supplied checkpoint descriptions cover four/seven draws and push/KO/rollback;
  those descriptions do not execute the Framework transactions validated by ZOD-204.
- Validation: locked restore, zero-warning/error Release rebuild,
  **4,061 .NET cases** (1,449 Framework, 867 AI, including owner drafts),
  **532 Python tests** and pinned source/prepared audit passed. Logs and seven
  full TRX files: ignored `scratch/targeted-ability-decision-204/`.
  These bounded checks do not establish full original decision equivalence.
  Owner refactor/drafts are excluded from AI/Framework commits. Nine historical
  checkpoint audits last passed at ZOD-158; unchanged snapshots were not rerun.
  No original/C execution or UI review.
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
  unknown bits preserved. ZOD-134 migrates one family when ready, not a broad rewrite.

## Deferred and unresolved gates

- **Editor ZOD-047/048:** further UI work/review waits for a real gameplay/content
  need or owner request. Jobs/Abilities pages exist but are unapproved.
  [Workspace guide](development/editor-workspaces.md); [correct FFTPatcher 0.497 reference](development/fftpatcher-desktop-reference.md).
- **Battle checkpoints ZOD-106:** wait for a concrete debugging/suspension need.
  Accumulate PA is battle-only; this does not block ability or campaign work.
- **ZOD-100/101:** broad replacement contracts require a real alternate consumer
  or implementation. ZOD-134 semantic-flag work also remains deferred.
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
