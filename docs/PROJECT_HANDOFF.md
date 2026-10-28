# Tessera / Zodiac / FFT handoff

Updated 2026-10-28. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-160: trace ordinary preparation between refresh and general selection.**
The [pure refresh query](behavior/sword-ai-refresh.md) now derives flags and one
hypothetical clock/upkeep draw for the closed already-moved context. Study later
ability, reachable-tile and team/target policy preparation and the earlier delay
cache before specifying fresh composition. Preserve current command authority and
general-selection certificates until those remaining inputs and draws are proved.

The [current executor](behavior/sword-ai-execution.md), now in `Zodiac.Framework.Ai`,
derives one in-reach decision for a real Move-used/ACT-unused session. The
[Framework-owned boundary](architecture/ai-command-boundary.md) invokes planning,
verifies RNG continuity and publishes one Attack or Wait. AI cannot construct
trusted sessions/receipts and has no production friend access. Selection starts
at certified general-selection entry; earlier refresh work is not included.
The original VSync callback also draws from BIOS RNG. Managed rendering/pauses add no
gameplay draws; frame-interleaved retail stream equivalence remains explicitly unresolved.

The current pure queries derive S/D0/D1 for focused-opponent, ordinary-sword
reach/policy facts and raw Move<=124 through explicit in/out-of-reach entries.
Movement is evaluated once; out-of-reach Blue/NonBlue adjustment runs afterward.
Private scores/RNG feed one Attack scan/ranked admission then the source lottery.
Empty ranking takes eight draws then MoveOnly at D0. FirstAttackEntry, terminal
entry, no-Defend and other source-context facts remain explicit; plans retain deferred
ChooseAtWait. Failures restore whole-query RNG without partial output. Original
supplied-scenario APIs stay unchanged; no command authority or live RNG commit.

Full AI search, general multi-entry ranking/lottery, replaceable decision interface,
wider command lifecycle/refresh-prefix integration and presentation activation remain
future work. Original UNIT_SLOT_COUNT
is **21**; the old 26-foe wrap example is inadmissible. This does not select a fixed
managed roster size. The contract records the erratum without rewriting old evidence.

## Current implementation and validation

- Latest implementation: ZOD-159, [pure ordinary refresh](behavior/sword-ai-refresh.md).
  Direct private clock reuse, retained policy booleans, semantic flags and whole-query
  rollback; no movement search, score or command/session authority. Eighteen managed
  cases cover source vectors, HP thresholds, policies, counters and immutable replay.
  The [separate AI assembly and Framework transaction](architecture/ai-command-boundary.md)
  remain in place; no native/editor activation or original decision-equivalence claim.
- Latest source/design: ZOD-156, [refresh-prefix contract](architecture/zod-156-ai-refresh-prefix.md).
  Clean pinned audit checks three refresh files/18 functions and six closed clock
  vectors, plus inherited receipts (rechecked: `scratch/ai-refresh-159/source.json`).
  The six clock/counter vectors now also execute in the managed refresh tests.
- ZOD-159 validation: locked restore, zero-warning/error Release build,
  **3,863 .NET cases / 504 Python tests**, including the owner's current content drafts.
  All nine historical checkpoint audits last passed at ZOD-158 after the file moves.
  These bounded checks do not establish complete FFT equivalence.
  Current .NET results: ignored `scratch/ai-refresh-159/full-trx/`;
  Latest Python log: `scratch/ai-refresh-159/python.txt`. The current owner refactor/drafts
  were included in the build and tests, excluded from the implementation commit.
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
