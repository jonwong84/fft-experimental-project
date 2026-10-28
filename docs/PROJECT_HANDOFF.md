# Tessera / Zodiac / FFT handoff

Updated 2026-10-28. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-169: integrate one in-reach post-prefix AI Move-first decision.**
The [Framework boundary](behavior/planned-move-continuation.md) now owns fresh
planning, actual path Move and exact-session frozen Attack/Wait continuation.
Connect privately owned selection and fresh dispatch to it, requiring an actual
Move-first result. Keep explicit general-entry/source certificates; fresh unmoved
preparation and host activation remain separate.

The [original post-prefix executor](behavior/sword-ai-execution.md) and new
[prepared executor](behavior/sword-ai-prepared-execution.md) live in Zodiac.Framework.Ai.
The [Framework-owned boundary](architecture/ai-command-boundary.md) invokes fresh
planning, verifies RNG continuity and publishes one actual Attack or Wait. AI
cannot construct trusted sessions/receipts and has no production friend access.
Prepared entries own refresh/preparation and explicit in/out-of-reach selection
inside that transaction; the old entry retains its general-selection certificate.
The original VSync callback also draws from BIOS RNG. Managed rendering/pauses add no
gameplay draws; frame-interleaved retail stream equivalence remains explicitly unresolved.

The current pure queries derive S/D0/D1 for focused-opponent, ordinary-sword
reach/policy facts and raw Move<=124 through explicit in/out-of-reach entries.
Movement is evaluated once; out-of-reach Blue/NonBlue adjustment runs afterward.
Private scores/RNG feed one Attack scan/ranked admission then the source lottery.
Empty ranking takes eight draws then MoveOnly at D0. The new prepared entry
derives focus/cache/active-target and first/terminal-entry defaults; earlier direct
selector APIs retain their certificates. No-Defend and other unrepresented policy
facts stay explicit; plans retain deferred ChooseAtWait. Failures restore whole-query
RNG without partial output. Original supplied-scenario APIs stay unchanged; no command
authority or live RNG commit.

Full AI search, general multi-entry ranking/lottery, replaceable decision interface,
Move-first/post-ACT command lifecycle and presentation activation remain future
work. Original UNIT_SLOT_COUNT is **21**; the old 26-foe wrap example is inadmissible.
This does not select a fixed managed roster size. The contract records the erratum
without rewriting old evidence.

## Current implementation and validation

- Latest implementation: ZOD-168, [planned Move boundary](behavior/planned-move-continuation.md).
  Eight new cases plus all 1,437 Framework tests pass; Framework build has no
  warnings/errors. Covers frozen follow-up, exact-state authority, per-step rollback,
  deferred Wait, replay/cancellation and counters. Logs: `scratch/move-continuation-168/`.
  Full solution results below are the prior ZOD-166 run, not rerun at this checkpoint.
- Latest design: ZOD-167, [Move/continuation boundary](architecture/zod-167-move-continuation-boundary.md).
  Defines fresh proposal binding, real path movement, exact-state frozen follow-up,
  cancellation via explicit Wait, immutable replay and per-step failure/counters.
  Boundary implemented by ZOD-168. Inherited pinned audit last passed
  (`scratch/ai-move-continuation-167/source.json`); protocol cases now execute in Framework tests.
- Last full validation (ZOD-166; not rerun for this design): locked restore,
  zero-warning/error Release build,
  **3,906 .NET cases / 510 Python tests**, including the owner's content drafts.
  These bounded checks do not establish full original decision equivalence.
  Results/logs: ignored `scratch/ai-command-dispatch-166/` (seven full TRX files).
  Owner refactor/drafts are excluded from AI commits. Nine historical checkpoint
  audits last passed at ZOD-158; unchanged historical snapshots were not rerun.
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
