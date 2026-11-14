# Out-of-reach Move-first execution — ZOD-170

Reference: read-only clean fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`. This integrates the
[existing out-of-reach selector](sword-ai-out-of-reach-selection.md) and
[source command lifecycle](../architecture/zod-165-ai-command-continuation.md)
through the [Framework Move boundary](planned-move-continuation.md).

`SwordAiMoveFirstExecution.StartOutOfReach` requires an explicit known source
Blue/NonBlue team and the same certified post-prefix entry as StartInReach.
Both entries share command publication. The new entry owns one fresh out-of-reach
selection inside the Framework callback, retaining its post-score adjustment and
eight empty-slot lottery draws. It dispatches only an actual Move-first result;
then Framework freezes deferred ChooseClosedEnemy(target) for the later Wait.
There is no second refresh/selection, Attack, or draw/tick added by Move or Wait.

Successful diagnostics include the original out-of-reach team/reach adjustment
alongside the common selection and dispatch. Failure publishes no diagnostic,
path, token or progressed RNG and returns the identical pre-Move session.
Continuation failure retains the successful Move, using the same exact-session
binding and immutable replay rules as the in-reach adapter. Unknown teams and
the wrong explicit reach branch reject; team is never inferred from actor ID.

Synthetic source-derived stream: actor x4/target x1, Move1, CT15/95, Speed5/5,
entry word1/count7. Only x3 is scored. Blue consumes one clock upkeep draw and
eight lottery draws, ending word3210001534/count16. NonBlue also consumes raw5758
after scoring, retaining reach2 and ending word267834847/count17. With seed6,
NonBlue raw25515 halves reach2 to1; the distance fallback still chooses x3 and
ends word3146034828/count17. These words are independent BIOS scalar calculations,
not execution of original C. Actual path movement ends facing West at x3; the
frozen Wait keeps that facing, refunds unused ACT to CT35 and leaves both HP50.

Four scenario tests cover all three streams, exact nine/ten-draw budgets through
MAX and short-prefix rollback, explicit branch/team/context rejection, both actor
identities/collection order, same-input replay and wrong/stale continuation
preservation. Ten focused Move-first tests pass. Locked restore, zero-warning/error
Release build and **3,924 .NET tests** pass; logs and seven TRX files are ignored
under `scratch/ai-move-first-170/`. The inherited pinned source audit passes there.
The last Python run remains 510 tests at ZOD-169; no Python changed in this ticket.
Owner content drafts were included in validation and excluded from the commit.
No C/original execution or UI review was performed.

Fresh unmoved preparation remains separate: existing fresh preparation requires
Move used and cannot establish this entry. ZOD-171 traces that difference before
extending it. ACT-first/post-ACT lifecycle, automatic branch routing, broader
policies/search and host activation remain open.
