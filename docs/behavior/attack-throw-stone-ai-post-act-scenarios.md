# Fresh pair post-ACT movement scenarios — ZOD-213

Implemented 2026-10-30 at clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[post-ACT design](../architecture/zod-209-pair-post-act.md) and
[fresh preparation](attack-throw-stone-ai-post-act-preparation.md).

`AttackThrowStoneAiPostActScenarios.EvaluateInReach` and `EvaluateOutOfReach`
accept actual state, immutable pair request/configuration and query RNG. Each
owns fresh ACT-used/Move-unused preparation exactly once. It requires normal
scoring, canonical coordinates and no other scenario overrides; clear-trajectory
and no-Defend facts are not needed for this movement-only stage.

Existing pair scenario builders now expose internal continuations for their
own fresh pre-ACT or post-ACT preparation. Geometry, score-mask filtering,
movement-prefix ordering, destination ties, source masks/provenance and reach
adjustment are shared. No Attack-only list/scenario assertion or public cached
prefix is introduced. `AiMovementSimulation` already refunds only actually
unused choices: post-ACT moving ends at live CT capped60; staying adds20.

In-reach post-ACT copies D1 to D0 without a ranged distance-floor search.
The legacy in-reach diagnostic's BaselineDistanceLimit is now nullable to record
that absent bound; existing public pre-ACT entries still return their original
non-null values and unchanged behavior. Out-of-reach still scores closer cells
before the NonBlue modulo7 reach draw, then copies D1 to D0. Actor insertion can
overlap scenario1 and scenario2 at an unwritten actor priority. Preserve null
there: no action scan reads or invents that value in this stage.

The post-ACT outcome contains both source masks, scored cells, full provenance,
fresh preparation, D0=D1, baseline index, reach/fallback diagnostics and optional
reach adjustment. Failure hides the entire owned prefix and restores original
query RNG, including late scoring/tick/reach-draw exhaustion. Live state/RNG is
unchanged; no action, lottery, plan or authority is produced.

## Literal scenarios and validation

From real ACT-first results on the eight-square board, the Move3 Attack-hit
entry `(995405759,49)` costs one refresh plus14 scored cells and ends
`(2240736910,64)`, D0=D1=(1,4). Each moved cell ends temporary CT15; staying ends35.
All these healthy HP40 target cells score26. Move0 Stone hit ends `(2473755763,29)`
after two draws; miss ends `(1969681615,33)` and scores0. Move0 push goes out of
reach, scores its singleton fallback, then raw4445/mod7=0 halves reach1 to0,
ending `(2438815311,30)` while staying at (1,1).

A real Stone push from target(5,1) to (6,1), Move3, scores five of17 reachable
cells. Its actor priority remains null with proximity5 and overlapping masks.
Raw30523/mod7=3 keeps reach4; D0=D1=(3,2), final `(2000392299,34)`. A nearer pushed
target at(5,1), Move1, leaves an unwritten actor within Stone range. From supplied
pure-query checkpoint `(20,7)`, refresh/score/reach raw12502 halves reach2 to1;
nearest fallback selects(2,1), final `(2966873603,10)`. Neither live RNG copy changes.

Five new scenario families check these literals, branch/source/choice/terminal
admission, exact/short budgets, delayed movement tick exhaustion after a completed
refresh, both actor identities, reordered inputs and replay. Existing pre-ACT
in/out-of-reach scenarios also pass against the shared continuations. Locked
restore, zero-warning/error Release builds, **4,095 .NET cases** (895 AI, 1,455
Framework), **534 Python tests** and the pinned source/prepared audit pass.
Logs and seven full TRX files: ignored `scratch/ai-pair-post-act-213-214/`.
Owner drafts and historical evidence are preserved; no original/C execution,
presentation review or full decision equivalence is claimed.

Next, ZOD-214 should own these fresh scenarios and eight empty ranked lottery
slots, returning a pure movement-only fallback with explicit no-Defend support.
Dispatch, actual movement/facing and host activation remain later work.
