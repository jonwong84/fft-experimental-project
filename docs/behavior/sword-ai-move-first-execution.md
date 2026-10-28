# In-reach post-prefix Move-first execution — ZOD-169

Original behavior is pinned to read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; the
[source command contract](../architecture/zod-165-ai-command-continuation.md)
and [boundary design](../architecture/zod-167-move-continuation-boundary.md)
explain reuse and authority. The [Framework implementation](planned-move-continuation.md)
owns actual path movement and the opaque follow-up.

`SwordAiMoveFirstExecution.StartInReach` takes an existing active session with
unused Move/ACT and the original in-reach selection/source certificates. It
requires GeneralSelectionEntryContext and remaining ordinary dispatch facts.
Inside the Framework callback it privately runs one fresh derived selection and
DispatchFreshSelection. FreshSelectionCompleted is established by that actual
call. ACT-first, same-tile END or any non-Move dispatch rejects without publishing
the decision, RNG or state. No caller-provided plan/result is accepted.

For a Move-first Attack it freezes the matching actual Attack and weapon; for
MoveOnly it freezes ChooseClosedEnemy(target), retaining the source's deferred
ChooseAtWait behavior. Framework adopts the actual decision stream only with a
successful confirmed path Move, including its final heading, one revision and
exact-session continuation. The owner later calls FacingMoveDecisionBattle.Continue;
that operation has no AI callback or replacement payload. It executes real Attack
or Wait on the moved state's current RNG without refreshing or selecting again.

Failure before published Move returns the identical original session, empty events
and no partial decision/path/token. Later command failure preserves the moved
session, its already-adopted decision RNG and reusable continuation for that input.
Receipts, cancellation through explicit Wait, same-input replay and cross-replay
rejection follow the existing Framework protocol. Old prepared/already-moved
entries remain unchanged.

Source-derived synthetic vector: actor x3/target x1, Move1, CT15/95, Speed5/5 and
entry word1/count7. Five movement cells and one ordinary Attack candidate produce
one lottery roll at full accuracy, ending decision word180171308/count18. Actual
Move goes to x2 facing West, adds no draw/tick, and the retained noncritical Attack
deals25 and ends word3791773014/count24. Hit11 instead takes eight lottery rolls,
ending word3535774167/count25; its actual miss ends word2473755763/count29 and leaves
HP50. Zero-power fallback takes eighteen decision draws, then moves to x2 and
Waits with no new draw, facing West and settling actor CT35.

Six focused managed cases cover these complete streams, actual path/facing and
combat, fallback at MAX, exact seventeen-draw full-hit budget, early versus late
rollback, explicit admission and both-identity/order replay. Framework's eight
new protocol cases cover ownership, path rollback, deferred facing, retry,
cancellation and per-step counter limits. No new C/original execution or UI review.

This starts at certified **post-prefix general-selection entry**. Existing fresh
refresh/preparation APIs require already-moved actors and are not used here.
Fresh unmoved preparation, ACT-first/post-ACT reselection, out-of-reach Move-first
integration, broader policies, saved continuations and host activation remain
separate. ZOD-170 selects the existing out-of-reach branch for this handoff.

Batch validation: locked restore, zero-warning/error Release build and **3,920
.NET / 510 Python tests** pass. This adds fourteen managed scenarios across
Framework and AI. The pinned source audit checks the inherited continuation,
selection and earlier receipts. Seven full TRX files and build/Python/source logs
are ignored under `scratch/ai-move-first-169/`. Owner content drafts were included
in validation and excluded from these commits. No original/C execution or UI review.
