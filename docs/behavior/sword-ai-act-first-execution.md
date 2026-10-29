# Fresh prepared ACT-first Attack — ZOD-177

Implements the first child of the [ACT-first/post-ACT design](../architecture/zod-176-ai-act-first.md)
at read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.

`SwordAiActFirstExecution.StartPreparedInReach` checks an active actor with both
Move/ACT unused, owns fresh unmoved preparation/selection and fresh dispatch inside
FacingDecisionBattle, then requires an Attack plan with ActBeforeMove and actual
Attack dispatch. Framework adopts the decision RNG and executes the real Attack
as one atomic transaction. No external decision RNG, public cached result,
planning callback or GeneralSelectionEntryContext is accepted. Remaining NonBlue,
list/cache/policy and ordinary dispatch facts remain explicit.

Complete preparation, decision and descriptive dispatch are returned only after
successful actual ACT. Admission, selection, dispatch or actual command failure
returns the identical pre-ACT session, empty events and no partial diagnostics.
Actual roll exhaustion rolls back the owned prefix too. The boundary reserves
only the draws actually required by that command, not future post-ACT work.

Success publishes one revision, real combat effects and an active actor with ACT
used/Move unused, unchanged live CT/ticks and no PendingFacing receipt. No Move
occurs or token is issued. The selected hypothetical future destination remains
diagnostic, not authority to reuse it after ACT. Settled actor facing retains its
incoming value through the existing sword/reward-restoration contract. Source
saved pre-command choices are descriptive00; current01 requires a fresh decision
on the next changed-progress entry. An unchanged01/01 entry instead hits the
early END guard. The new executor does not automatically run either path.

Source-derived actor x2/target x1, Move1, CT15/95, Speed5/5 on the 5x3 board,
word1/count7: one refresh, four scored origins, one five-draw projected Attack
and a full-accuracy lottery roll decide at word180171308/count18. Actual
noncritical Attack deals25, ending word3791773014/count24. Hit11 instead decides
after eight rolls at word3535774167/count25; its actual miss ends
word2473755763/count29. Thus full-hit costs seventeen total draws and miss costs
twenty-two. Actor stays at x2, settled East, CT15 and can still move. None of the
temporary simulation CT/positions becomes live.

Five scenario tests exercise those complete streams, exact MAX/short budgets,
whole-transaction failure, ACT-first/remaining-context admission, actual KO and
either actor/order replay. Actual KO is published without selecting another
policy or automatically waiting; existing owner-directed Framework Wait remains
separate. All twenty-four focused ACT/Move-first cases pass. Locked restore,
zero-warning/error Release build, **3,949 .NET cases / 516 Python tests** and the
clean pinned source audit pass. Logs and seven full TRX files are ignored under
`scratch/ai-act-first-177/`. Owner content drafts were included in validation and
excluded from these AI commits. No original/C execution or UI review; bounded
vectors do not establish full original AI equivalence.

ZOD-178 adds a pure fresh post-ACT refresh entry using actual post-ACT state/RNG
and only the unused-Move refund. Post-ACT preparation/scoring/selection and live
handoff remain subsequent work. Existing Move-first/already-moved APIs, deferred
interfaces, broader policies/search and host/editor activation are unchanged.
