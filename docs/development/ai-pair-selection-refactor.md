# Pair-selection cleanup — ZOD-210

Owner-requested maintenance after ZOD-208, completed 2026-10-29. This refactors
the existing [in-reach](../behavior/attack-throw-stone-ai-prepared-selection.md)
and [out-of-reach](../behavior/attack-throw-stone-ai-out-of-reach-selection.md)
pair queries and their Move-first/ACT-first execution callers. The read-only
reference remains clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
No new original rule is translated.

## Inputs and shared flow

Two immutable records replace repeated positional arguments:

- `AttackThrowStoneAiSelectionRequest`: turn actor/target/policy request,
  Throw Stone identity and equipped weapon. A missing weapon remains an ordinary
  rejected query input.
- `AttackThrowStoneAiSelectionConfiguration`: Attack and Throw Stone candidate
  rules, scoring, refresh, preparation/selection assertions and team. State and
  RNG stay explicit per call; configuration contains no cached query or authority.

Both selectors now expose `Evaluate(state, request, configuration, decisionRng)`.
The pair's execution entries take `(state, request, configuration, dispatchProfile)`.
All current callers are migrated; the old twelve-argument overloads are removed.
Results and nested diagnostics keep their existing types and meanings.

The internal `AttackThrowStoneAiSelectionPipeline` owns common null/profile
admission, composition-profile derivation, validation, both scans/ranking and
the final lottery handoff. It reuses the owning branch's fresh movement prefix
and current RNG, including the out-of-reach reach draw. Failure still restores
the original whole-query RNG and publishes no partial outcome.

Each public selector retains explicit scenario construction. Out-of-reach also
retains its unknown-score rejection and scan-origin filtering before entering
the shared continuation. Full diagnostic masks/provenance remain available;
unknown priorities are neither guessed nor rescored. Framework still owns
command validation, RNG adoption, session revisions and receipts.

## Validation and remaining cleanup

Locked restore and a zero-warning/error Release rebuild pass. All **4,085 .NET
cases** (885 AI, 1,455 Framework), **532 Python tests** and the pinned source/
prepared-data audit pass. Existing literal plans, RNG, rollback, actual effects,
replay and provenance assertions are unchanged; test edits only migrate calls
and share configuration setup. No tests are added or removed. Logs and seven
full TRX files are in ignored `scratch/ai-pair-selection-refactor-210/`.

Owner drafts remain untouched and excluded. Old evidence/hashes are historical
and unchanged. No original/C execution or presentation review is performed.
Broader shared-type naming, folder organization and older public-entry/test
consolidation can be separately scoped; this is the first bounded cleanup.
ZOD-209 remains the next behavior investigation, with pair post-ACT policy and
the existing deferred work still unresolved.
