# Fresh prepared out-of-reach Move-first execution — ZOD-175

Reference: read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.
Extends the [prepared in-reach adapter](sword-ai-prepared-move-first.md) through
the [owned out-of-reach selector](sword-ai-unmoved-prepared-selection.md) and
existing [Framework path-Move/continuation protocol](planned-move-continuation.md).

`SwordAiMoveFirstExecution.StartPreparedOutOfReach` owns fresh unmoved
preparation, explicit out-of-reach selection and fresh dispatch inside Framework's
Move callback. It shares the prepared planner and authoritative publication path
with StartPreparedInReach. Neither prepared entry accepts external decision RNG,
public cached results or GeneralSelectionEntryContext. Source/cache/list/policy
and dispatch facts remain required. Fresh preparation remains NonBlue; existing
certified post-prefix Blue execution retains its separate admission.

The owned prefix retains the refresh upkeep draw, actual filtered S scoring,
NonBlue post-score reach adjustment and eight real empty-slot lottery draws.
The preparation's full reachable origins are diagnostic and distinct from S.
Only a successful real Move publishes its actual path/heading, one revision,
adopted decision RNG and complete Preparation/OutOfReach diagnostics. It freezes
ChooseClosedEnemy(target) for a later Wait on settled positions, using the existing
exact-session token. No Attack, new refresh/selection or extra gameplay RNG/tick
is added by Move or Wait.

Synthetic actor x4/target x1, Move1, CT15/95, Speed5/5, entry word1/count7:
four preparation origins reduce to one scored x3 cell. Adjustment raw10113 keeps
reach2; after eight fallback rolls the decision ends word180171308/count18.
Seed20 instead draws raw12502, halves reach to1 and uses distance fallback at x3,
ending word3368358523/count18. Actual Move ends at x3 facing West with live CT15;
the frozen Wait then refunds unused ACT to CT35, keeping HP50/50 and that RNG.
Both streams cost eleven draws, including refresh; Wait can finish at MAX.

Every incomplete prefix returns the identical pre-Move session, empty events,
original live RNG and no path/token/partial diagnostics. Wrong/stale continuation
rejection preserves the committed Move or newer completed session. Replaying the
same moved input/token deterministically repeats its result without double refund
or mutable token consumption. Explicit wrong reach branches reject without routing
to another entry. Old post-prefix and already-moved APIs remain unchanged.

Four scenario tests cover both literal streams, halving/fallback, owned-prefix
equivalence, actual Move/Wait, exact limits, branch/team/source admission, either
actor/order replay and failed continuation isolation. All nineteen focused
Move-first cases pass. Locked restore, zero-warning/error Release build,
**3,944 .NET cases / 513 Python tests** and the clean pinned inherited/unmoved
source audit pass. Logs and seven full TRX files are ignored under
`scratch/ai-prepared-move-175/`. Owner content drafts were included in validation
and excluded from the AI commits. No original/C execution or UI review; bounded
checks do not establish full original AI equivalence.

Both prepared Move-first reach branches are implemented. ZOD-176 will trace
ordinary ACT-first execution and the required fresh post-ACT reselection before
selecting the next boundary. Automatic branch routing, broader search/policies,
replacement interface and host activation remain separate.
