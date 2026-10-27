# Tessera / Zodiac / FFT handoff

Updated 2026-10-27. Read [AGENTS.md](../AGENTS.md), the [active backlog](BACKLOG.md)
and the selected task's design before editing. This file describes current state;
[planning history](history/README.md) holds completed tickets and old checkpoints.

## Next work

**ZOD-131: implement one certified ordinary-sword AI candidate scan.** No scan
implementation has started. Read the [source/design contract](architecture/zod-130-ai-scan-contract.md),
its ASN-001..016 cases and the [current test workflow](development/test-maintenance.md).

Existing opt-in helpers project, score and compare supplied candidates. The scan
must generate source-ordered candidates from one supplied scenario, evaluate each
once on the original state, carry supported decision RNG and keep a private best.
Preserve stay-once, duplicate final destinations, absent versus zero/negative
scores, baseline cache after KO and atomic whole-query rejection. A repeated pair
comparison tournament would re-evaluate incumbents and consume extra draws.

Full AI search, ranked list/lottery, replaceable decision interface, authoritative
commands/RNG and presentation activation remain future work. Original UNIT_SLOT_COUNT
is **21**; the old 26-foe wrap example is inadmissible. This does not select a fixed
managed roster size. The contract records the erratum without rewriting old evidence.

## Current implementation and validation

- Latest runtime AI helper: ZOD-129, commit `5d3e757`; next scan design: ZOD-130,
  commit `667ed46`. Test consolidation ZOD-133 is `a6c0b2b`; flag-policy base is `dedc95a`.
- Last complete validation: locked restore, zero-warning/error Release build,
  **3,548 .NET cases / 477 Python tests**, and nine historical checkpoint audits.
  These are recorded results, not a new run or a claim of complete FFT equivalence.
  Logs/TRX/reports: ignored `scratch/test-consolidation-*`.
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
