# Prepared out-of-reach selection and Wait — ZOD-164

Reference: read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`;
managed base `aac3689db167907e8083cac9644f608589445f1e`. The
[out-of-reach source contract](../architecture/zod-148-ai-out-of-reach-selection.md)
and [ordinary preparation contract](../architecture/zod-160-ai-preparation.md)
cover the composed branch. No C/original execution or host activation.

`SwordAiPreparedSelection.EvaluateOutOfReach` owns fresh NonBlue preparation and
one existing out-of-reach derived selection. It shares admission/reset policy
with the in-reach entry, carries actual post-refresh RNG and returns the original
out-of-reach adjustment/selection diagnostics alongside the common decision shape.
It accepts no cached result. The explicit branch APIs reject the wrong reach
branch rather than silently dispatching or consuming a different source schedule.

The already-moved domain contains only the actor's origin. When the closer-tile
filter finds none, source scenario S falls back to that origin; movement scoring
runs once. NonBlue then draws once and halves reach if raw%7==0. Even though the
origin remains the only destination, the adjustment draw survives. The real scan
has no adjacent enemy candidate, the ranked list is empty, and all eight lottery
draws execute before MoveOnly fallback. No formula or extra actor scoring runs.

`SwordAiExecution.ExecutePreparedOutOfReachAlreadyMoved` invokes this fresh pure
query inside the same Framework callback and plan/command path as the other
entries. It publishes one actual Wait, resolves facing, refunds unused ACT once
and commits both RNG copies with one revision. It emits no Attack, movement,
pending completion receipt or live hypothetical clock advancement. Failed prefix,
wrong branch, unknown policy/team or authority preserves the identical original
session and emits no partial diagnostics/events.

At word1/count7, CT15/95, Speed5/5 and Move1 with target distance3: refresh raw16838,
movement raw5758, adjustment raw10113 leaves reach2, then eight zero-hit rolls end
word180171308/count18. Seed20's adjustment raw12502 halves reach2 to1 and ends
word3368358523/count18. Both Wait at the unchanged actor tile, face the opponent
and settle actor CT35. Exactly eleven available draws succeed, including at MAX;
ten fail before any command publication. Fresh Blue preparation remains excluded.

Six focused cases cover both literal adjustments, real Wait results, explicit-stage
pure equivalence, exact budgets and failures during refresh/movement/adjustment/
lottery, wrong-branch/authority admission and both-identity/order replay. Broader
abilities, Blue preparation, Move-first continuation, frame fidelity and playable
host/editor activation remain separate. ZOD-165 traces the selected-action reuse
and command continuation needed before admitting a fresh Move-first turn.

Batch validation: locked restore, zero-warning/error Release build and **3,891
.NET / 507 Python tests** pass (thirteen new managed cases across ZOD-163/164).
Pinned source audit rechecks preparation and all inherited execution/out-of-reach
receipts. Seven full TRX files and Python/build/source logs are ignored under
`scratch/ai-prepared-out-of-reach-164/`. Owner content drafts were included in
validation and excluded from the AI commits. No original/C execution or UI review.
