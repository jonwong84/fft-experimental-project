# Tessera / Zodiac / FFT handoff

Updated 2026-10-27. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-140: implement fresh hypothetical Attack/MoveOnly selection.**
The [ZOD-139 contract](architecture/zod-139-ai-selection-contract.md) pins lottery
and D0 fallback. A lone positive-hit Attack is selected after one or eight draws;
empty ranking takes eight draws then MoveOnly. Require terminal Attack entry,
NoDefendSupport and fresh ordinary defaults. SLC-001..007 remain unexecuted C#
proposals. Preserve deferred facing and keep commands/live RNG separate.

ZOD-138 evaluates supplied movement mask S, reuses D0's already-scored baseline,
then scans Attack mask S union actor using D1 and the progressed RNG. It validates
all admission before draws and owns original-state evaluations/cache/best/list.
The certified fresh one-pass list admits zero/one real Attack into eight slots;
inactive/NotValuable scans, strict ties, negative/zero scores, original KO cache
and atomic rollback are covered. Standalone scan/baseline APIs remain unchanged.
Returned diagnostics grant no command authority or live RNG commit.

Full AI search, general multi-entry ranking/lottery, replaceable decision interface, authoritative
commands/RNG and presentation activation remain future work. Original UNIT_SLOT_COUNT
is **21**; the old 26-foe wrap example is inadmissible. This does not select a fixed
managed roster size. The contract records the erratum without rewriting old evidence.

## Current implementation and validation

- Latest implementation: ZOD-138, [composition API and evidence](behavior/sword-ai-composition.md).
  CMP-001..008 have current C# coverage through 46 focused cases. No runtime activation.
- Latest source/design: ZOD-139; five offline lottery/fallback checks, no C# changes.
- Most recent locked restore/zero-warning-error Release build and .NET validation:
  **3,679 cases** (ZOD-138). Latest full Python suite: **496 tests** (ZOD-139).
  Fresh selection source audit: eight reference files / 31 functions, clean HEAD below.
  These bounded checks do not establish complete FFT equivalence.
  Logs/TRX/reports: ignored `scratch/ai-composition-138-*` and `scratch/ai-selection-139-*`.
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
  `feature/initial-development`; inspect Git status before editing. Owner drafts
  in the Ability, Common, Item and Status folders under `src/Zodiac.Content/Definitions/` are untracked:
  preserve them and exclude them from unrelated commits. The last build included
  drafts present during validation. Validated completed work may be committed/pushed.
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
- Remote Linux/Windows CI and clean-machine launch evidence remain open under
  ZOD-006/034; original comparison scope remains partial under ZOD-033.
  Later display/Settings options and original bug fixes retain owner sequencing.

## Keep this handoff current

Replace stale state here rather than prepending milestone narratives. The backlog
owns open scope/order; design/evidence notes own details; the history index owns
completed work. Archive significant transitions only when useful. Use historical
snapshot audits for old hashes, not old verifier commands against changed code.
