# Fresh unmoved preparation and selection — ZOD-173

Reference: read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`;
managed base `1f5ae55af846f1f54b6964dd8072dda82a9b684e` (ZOD-172). This composes
the [fresh unmoved prefix](sword-ai-unmoved-preparation.md),
[in-reach selector](sword-ai-derived-selection.md) and
[out-of-reach selector](sword-ai-out-of-reach-selection.md) in their traced order.

`SwordAiPreparedSelection.EvaluateUnmoved` and EvaluateOutOfReachUnmoved privately
own one fresh both-unused preparation, then the corresponding derived selector.
The actual refresh RNG feeds movement scoring, Attack scan and lottery on the
original immutable snapshot. Temporary refresh CT/turn choices never become live
or feed selection. The full reachable spread remains a preparation diagnostic;
the selector independently derives its filtered S and scores it once, reusing those
actual scores in composition. No public cached preparation, geometry, candidate
or rank outcome is accepted as input.

Owned preparation plus the certified ordinary Attack-only list establishes focus,
active target, baseline/reset and first/terminal usable-entry facts. NonBlue and
the remaining normal-scoring, canonical-coordinate, ordinary scenario/tile,
target-equivalence and no-Defend assertions remain required. Existing already-
moved entries share the same tail and retain their previous admission. Explicit
reach branches reject mismatched geometry; no automatic branch routing is added.

Only complete success exposes preparation, selection and progressed hypothetical
RNG. Admission, refresh, movement, adjustment, scan, lottery and tick failure
return reason/stage diagnostics, original incoming RNG and no partial outcome.
No live RNG, movement, action, facing, CT, receipt or trusted session is committed.

Source-derived synthetic stream: actor x3/target x1, Move1, CT15/95, Speed5/5,
entry word1/count7. Refresh consumes raw16838; five scored cells consume raws5758,
10113, 17515, 31051, 5627. One ordinary Attack projection takes four formula draws
and one upkeep draw. Lottery enters word180171308/count18; first raw12767 passes
full accuracy, ending word836760821/count19. Hit11 instead fails all eight rolls,
retains the first ranked Attack and ends word3794901700/count26. Zero power uses
the same nineteen-draw stream but falls back to MoveOnly at x2 with ChooseAtWait.

Out-of-reach actor x4/target x1 keeps four preparation origins but scores only x3.
With seed1, the post-score adjustment consumes raw10113, keeps reach2 and ends
word180171308/count18 after eight empty-slot rolls. Seed20 consumes raw12502,
halves reach to1 and ends word3368358523/count18; its distance fallback still picks
x3. These streams cost twelve/full-hit, nineteen/low-hit-or-empty-ranking and
eleven/out-of-reach draws, including the one refresh draw. No future actual-command
draws are reserved by these pure queries.

Six new scenario tests cover the streams, actual score reuse, explicit-stage
equivalence, deferred fallback, exact budgets, early/late rollback, source/choice
admission and both actor/order replay with unchanged live snapshots. All nineteen
focused new/existing prepared-selection cases pass. Locked restore, zero-warning/
error Release build, **3,935 .NET cases / 513 Python tests** and the pinned source
audit pass. Logs and seven full TRX files are ignored under
`scratch/ai-unmoved-selection-173/`. Owner content drafts were included in validation
and excluded from this commit. No C/original execution or UI review; bounded
vectors do not establish full original AI equivalence.

ZOD-174 will own the new in-reach prepared selector inside Framework's planned
Move callback and use its frozen Attack/Wait continuation. Current post-prefix
executors still require GeneralSelectionEntryContext. Fresh out-of-reach live
integration, ACT-first/post-ACT, broader policies/search and host activation remain
subsequent work.
