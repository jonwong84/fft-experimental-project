# Tessera / Zodiac / FFT handoff

Updated 2026-10-28. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-145: implement private derived-scenario composition/selection.**
The [ZOD-144 contract](architecture/zod-144-ai-derived-selection.md) removes supplied
S/D0/D1 inputs and derives their provenance. Keep remaining context facts explicit,
preflight weapon/policy before movement, reuse only privately owned fresh scores
through shared internal composition/lottery tails, and restore whole-query RNG
after any later failure. No command/native activation or out-of-reach extension.

ZOD-140 privately owns one ZOD-138 composition and the source lottery. An admitted
Attack is selected after one/eight draws; empty ranking takes eight then MoveOnly
at D0. New terminal-entry/no-Defend/fresh-default certificates are required. Plans
retain deferred ChooseAtWait; no projected damage, facing or commands execute.
Failures restore whole-query RNG without partial output. Standalone APIs remain
unchanged. Results grant no command authority or live RNG commit.

Full AI search, general multi-entry ranking/lottery, replaceable decision interface, authoritative
commands/RNG and presentation activation remain future work. Original UNIT_SLOT_COUNT
is **21**; the old 26-foe wrap example is inadmissible. This does not select a fixed
managed roster size. The contract records the erratum without rewriting old evidence.

## Current implementation and validation

- Latest source checkpoint: ZOD-144, clean reference below; inherited selection audit
  plus 11 scenario functions rechecked (`scratch/ai-derived-selection-144/source.json`).
  No production change, original execution or .NET suite rerun for this checkpoint.
- Latest implementation: ZOD-143, [movement-scenario API and evidence](behavior/sword-ai-movement-scenario.md).
  22 new focused cases; shared baseline admission extracted. No runtime activation.
- Complete validation: locked restore, zero-warning/error Release build,
  **3,728 .NET cases / 496 Python tests**, including all 3,706 prior .NET cases.
  Fresh selection source audit: eight reference files / 31 functions, clean HEAD below.
  These bounded checks do not establish complete FFT equivalence.
  Current .NET results: ignored `scratch/ai-scenario-143-full-trx/`;
  Python log: `scratch/ai-scenario-143-python.txt`. The current owner refactor/drafts
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
