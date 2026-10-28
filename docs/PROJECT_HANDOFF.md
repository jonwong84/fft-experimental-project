# Tessera / Zodiac / FFT handoff

Updated 2026-10-28. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-146: source/design one bounded out-of-reach sword movement branch.**
Trace filtering/fallback, team-conditioned post-score rand()%7 reach-halving,
D1 selection/D0 copying, actor insertion and score/proximity-cell provenance.
Pin source/team facts and private RNG ownership before implementation. Start
from the [completed in-reach selector](behavior/sword-ai-derived-selection.md).

The current pure query derives S/D0/D1 for focused-opponent, ordinary-sword
reach/policy facts, raw Move<=124 and d<=Move+1. It evaluates movement once, reuses
its private scores through one Attack scan/ranked admission, then runs the source
lottery. Empty ranking takes eight draws then MoveOnly at D0. Source-context and
terminal-entry/no-Defend/fresh-default facts remain explicit; plans retain deferred
ChooseAtWait. Failures restore whole-query RNG without partial output. Original
supplied-scenario APIs stay unchanged; no command authority or live RNG commit.

Full AI search, general multi-entry ranking/lottery, replaceable decision interface, authoritative
commands/RNG and presentation activation remain future work. Original UNIT_SLOT_COUNT
is **21**; the old 26-foe wrap example is inadmissible. This does not select a fixed
managed roster size. The contract records the erratum without rewriting old evidence.

## Current implementation and validation

- Completed batch: ZOD-141/143/144/145. Latest implementation:
  [derived-scenario selector and evidence](behavior/sword-ai-derived-selection.md).
  22 scenario + 16 integration cases added; shared internal tails preserve old APIs.
  No native/editor activation or original decision-equivalence claim.
- Complete validation: locked restore, zero-warning/error Release build,
  **3,744 .NET cases / 496 Python tests**, including all 3,706 prior .NET cases.
  Clean source audit: inherited eight-file/31-function selection receipt plus
  11 inspected scenario functions (`scratch/ai-derived-selection-145-source.json`).
  These bounded checks do not establish complete FFT equivalence.
  Current .NET results: ignored `scratch/ai-derived-selection-145-full-trx/`;
  Python log: `scratch/ai-derived-selection-145-python.txt`. The current owner refactor/drafts
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
  owner's tracked weapon/content-package/exporter/test refactor and untracked
  WeaponDefinition plus Ability/Common/Item/Status definition drafts. Exclude
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
