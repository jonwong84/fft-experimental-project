# Fresh prepared in-reach Move-first execution — ZOD-174

Reference: read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [fresh prepared selector](sword-ai-unmoved-prepared-selection.md),
[source command lifecycle](../architecture/zod-165-ai-command-continuation.md)
and [Framework protocol](planned-move-continuation.md) establish this composition.

`SwordAiMoveFirstExecution.StartPreparedInReach` owns one EvaluateUnmoved call
inside Framework's planned Move callback, followed by actual fresh dispatch.
It takes no external decision RNG, cached outcome or GeneralSelectionEntryContext.
The owned prefix establishes its entry facts; remaining NonBlue/list/cache/policy
and ordinary dispatch assertions stay explicit. Existing post-prefix entries
still require their general-entry certificate, and already-moved APIs are unchanged.

All Move-first entries share dispatch, path publication and frozen Attack/Wait
construction. A private planner supplies either certified post-prefix selection
or owned preparation/selection; no public execution callback or cached-plan input
is added. Successful diagnostics include Preparation only for the prepared entry.
Prepared failures expose reason/stage enums through PreparedSelectionFailure,
never partial preparation or advanced RNG. Framework still checks authority
before planning and adopts RNG only with a real, different-tile path Move.

ACT-first, same-tile EndTurn and non-Move dispatch reject without publishing the
prefix. A successful Move mints the existing exact-session continuation and one
revision. Continue executes retained Attack or deferred ChooseClosedEnemy Wait
on current post-Move RNG without refresh, reselection or another adoption. A late
failure preserves Move and its input continuation for retry; explicit Wait can
abandon that intent on a newer branch. Frame/timing effects remain separate.

Synthetic actor x3/target x1, Move1, CT15/95, Speed5/5, entry word1/count7:
the full-accuracy decision ends word836760821/count19. Real Move reaches x2,
facing West, with live actor CT15; temporary refresh CT55/clock CT60 are diagnostic.
Retained noncritical Attack deals25 and ends word3535774167/count25. Hit11 instead
decides at word3794901700/count26, then its actual miss ends word1343201072/count30.
Thus the full hit costs eighteen draws, the miss twenty-three, including refresh.
Zero-power fallback costs nineteen decision draws and its later Wait adds none,
even at MAX, settling CT35 and facing West. Actual commands never refund a
published decision stream or replace live CT with simulated CT.

Five scenario tests cover those streams, owned-prefix equivalence, actual path/
heading, deferred Wait, exact budgets, early/late failure, cancellation,
admission and both-identity/order replay with cross-token rejection. Fifteen
focused Move-first cases pass. Locked restore, zero-warning/error Release build
and **3,940 .NET tests** pass; logs and seven full TRX files are ignored under
`scratch/ai-prepared-move-174/`. The pinned source audit passes there. Python is
unchanged; its last full run remains **513 tests at ZOD-173**. Owner content drafts
were included in validation and excluded from this commit. No C/original execution
or UI review; bounded checks do not establish full original AI equivalence.

ZOD-175 extends this owned-prefix path through explicit out-of-reach selection
and its frozen Wait. ACT-first/post-ACT, automatic branch routing, broader search,
replacement interface and host activation remain subsequent work.
