# Tessera / Zodiac / FFT handoff

Updated 2026-10-27. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-138: implement certified movement scoring/Attack ranked admission.**
The [ZOD-137 contract](architecture/zod-137-ai-composition-contract.md) separates
pre-scoring mask S from Attack mask A=S union actor. Evaluate every S cell, read
D0's baseline without rescoring, then scan A/D1 with the progressed RNG. Require
fresh source context/no intervening draws and an empty list; one pass admits at
most one entry. Stop before lottery/fallback/commands. CMP-001..008 are unexecuted
managed proposals for the next milestone.

Opt-in helpers now project, score, compare and scan candidates in one certified
scenario. ZOD-131 preserves source emission order, evaluates each candidate once
on original state, carries supported decision RNG and keeps a private best/cache.
ZOD-136 evaluates one supplied movement-only baseline, preserves unused-ACT CT
refunds/HP/status and returns a fresh absolute score with one upkeep draw.
Empty/inactive scans still validate admission. Stay-once, duplicate destinations,
score absence/zero/negative, baseline cache after KO and atomic whole-query failure
are covered. Returned diagnostics grant no command authority or live RNG commit.

Full AI search, ranked list/lottery, replaceable decision interface, authoritative
commands/RNG and presentation activation remain future work. Original UNIT_SLOT_COUNT
is **21**; the old 26-foe wrap example is inadmissible. This does not select a fixed
managed roster size. The contract records the erratum without rewriting old evidence.

## Current implementation and validation

- Latest implementation: ZOD-136, [baseline API and evidence](behavior/sword-ai-movement-baseline.md).
  Attack/baseline now share an internal hypothetical clock and fresh-unit scorer;
  existing candidate API behavior is unchanged. 25 focused baseline cases pass.
- Latest source/design: ZOD-137; six offline mask/RNG-stage checks. No C# changes.
- Most recent locked restore/zero-warning-error Release build and .NET validation:
  **3,633 cases** (ZOD-136). Latest full Python suite: **491 tests** (ZOD-137).
  Fresh source receipt audit: four reference files / 27 functions, clean HEAD below.
  These bounded checks do not establish complete FFT equivalence.
  Logs/reports: ignored `scratch/ai-baseline-136-*` and `scratch/ai-composition-137-*`.
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
