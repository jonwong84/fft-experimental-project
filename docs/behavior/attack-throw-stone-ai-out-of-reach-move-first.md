# Known-score out-of-reach pair Move-first execution — ZOD-207

Implemented 2026-10-29 at clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following
[score provenance](../architecture/zod-199-pair-out-of-reach.md),
[known-score selection](attack-throw-stone-ai-out-of-reach-selection.md) and
the [command-boundary design](../architecture/zod-203-throw-stone-command-boundary.md).

`AttackThrowStoneAiMoveFirstExecution.StartOutOfReach` owns the explicit fresh
out-of-reach selector inside Framework's confirmed-Move callback. It retains
full source masks, filtered priority writes, full M0 proximity, D0=D1, the single
post-score NonBlue reach draw and scan-equivalent fresh origins. An unwritten
actor score outside range4 remains null in diagnostics and is proved unread;
unknown eligible cast keys still reject before any adoption or command.

A private shared execution core handles dispatch/admission/proposals for both
entry points. Each wrapper supplies only its own fresh selector and typed result
construction. Public signatures/results for in-reach execution are unchanged;
the out-of-reach result contains its actual scenario/provenance type rather than
a fabricated in-reach result. Neither entry accepts a caller delegate, cached
query, supplied prior memory or scored mask. This is not a replacement AI interface.

Source dispatch still requires both unused choices, fresh completed selection,
Move-first order and a destination different from the actor's original tile.
Framework adopts decision RNG once and publishes actual path/final heading. It
freezes typed Attack/targeted ACT/Wait for the exact returned session. Continue
uses current post-Move RNG and actual rules without another preparation, score,
reach draw, selection or adoption. Failed ACT preserves committed Move and RNG;
earlier rejection publishes the unchanged incoming session and no partial result.

Source mask overlap at the actor remains visible. Range3/4 unknown keys reject
through the selector's provenance check; Move0's fresh ACT-first or same-tile
fallback also rejects this Move-first entry. No actor rescore or inferred zero
is introduced. In-reach and out-of-reach remain explicit API branches.

## Literal checks and validation

The shared healthy setup yields CT15/95, decision RNG `(1,7)`, actor(1,1) and
target(6,1). Move1 scores only (2,1), leaving the original actor's priority
unwritten with fresh proximity5. The actor remains in both final diagnostic
scenarios but not ScanOrigins. Refresh, one movement score, reach adjustment,
one Stone and lottery cost nine draws, ending `(3210001534,16)`. Confirmed Move
to (2,1) adds no draws. Real Stone deals5, pushes to (7,1), and ends
`(3297241969,23)` with a receipt bound to the actual result.

Move3 scores five cells, selects (3,2), and ends decision RNG `(595337866,20)`.
Real Stone deals10 without push and ends `(3823029677,27)`. Seed20/Move1 scores
before adjustment raw12502/mod7=0 halves reach2 to1, forcing nearest fallback.
It selects (2,1), ends decision `(4007910005,16)` and actual `(3660420676,23)`.

Original distance6/Move1 has no cast and consumes eight empty-slot rolls to
`(180171308,18)`. Distance5 with front evade100 consumes four formula draws,
then eight empty-slot rolls to `(2111915288,22)`. Both move to (2,1), then Wait
at CT35 with no additional RNG or receipt. A healthy maxHP5 target instead
produces an actual KO at `(3791773014,24)`; fixed facing uses its final receipt
without inventing a living opponent.

Exact total budgets of16/20/11/15 draws cover single/multiple-score Stone,
no-cast and zero-accuracy fallback. Late ACT exhaustion retains the moved branch;
exhaustion during refresh, reach adjustment or lottery publishes no Move.
Six new scenarios cover these literals, halving/fallback, range4/5 provenance,
non-Move/branch/source admission, real KO, exact/late budgets, pure-query agreement
and both-actor/order replay with exact continuation binding. The six existing
in-reach execution scenarios also pass against the shared core.

Locked restore, zero-warning/error Release builds, **4,079 .NET cases** across
seven projects (879 AI), **532 Python tests** and the pinned source/prepared-data
audit pass. Reports and seven full TRX files are under ignored
`scratch/ai-pair-out-of-reach-move-207/`. Owner drafts participate in validation
and remain excluded from this commit. No original/C execution or UI review;
frame/VSync stream equivalence remains unresolved.

Next, ZOD-208 should own fresh in-reach ACT-first pair selection and actual ACT
through Framework's existing decision transaction. Stop after real ACT: the old
predicted movement destination is not post-ACT authority. ACT-first out-of-reach,
pair post-ACT reselection, general policies and host/editor activation remain later work.
