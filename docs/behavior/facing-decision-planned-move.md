# Planned Move from one decision callback — ZOD-222

Implemented 2026-11-04 from the [single-entry design](../architecture/zod-221-fresh-pair-command-entry.md),
pinned to read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.

`FacingDecisionContext` now offers `PrepareMoveThenAttack`,
`PrepareMoveThenTargetedAbility` and `PrepareMoveThenWait`. A fresh planner may
choose these alongside its existing immediate ACT, Wait or post-ACT Move
proposals. The result exposes the confirmed path and existing opaque continuation
only after a successful pre-ACT Move; `MoveRejection` retains detailed planned
Move failure alongside the ordinary decision rejection.

Each context owns a private Move context for the same battle and actor. The
existing `finally` closes both after the callback, including when it throws.
The outer proposal must belong to that invocation; the wrapped Move proposal
must belong to its private inner context and exact battle. No public constructor
or cached-proposal entry was added.

`FacingMoveDecisionBattle.ApplyFreshMove` factors existing intent, RNG, actual
path and token publication behind the two Framework entry points. Planning runs
once and Move adopts decision RNG once. Actual follow-up continues through the
existing exact-session `Continue` method without another planner or adoption.
Rejection publishes no path, token, diagnostic, events or staged RNG. A later
ACT failure preserves the already committed Move. Revision limits remain per
published command rather than reserving a whole turn's revisions.

Four new scenario families check all three factories, literal Stone streams,
one callback, immutable inputs, expired/foreign/throwing proposals, cross-replay
tokens, invalid RNG/actor/path, spent choices, revision and late actual exhaustion.
The existing immediate-command, Move continuation and post-ACT authority tests
remain regression coverage. The targeted run passed all 30 selected cases;
TRX: ignored `scratch/ai-command-entry-221-223/222-trx/`.

This extends the concrete command boundary for a real AI consumer. It introduces
no Framework dependency on AI, production friend access, general command bus,
replacement engine contract, save token or host activation.
