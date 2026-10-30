# Planned path Move and frozen follow-up — ZOD-168

The [ZOD-167 design](../architecture/zod-167-move-continuation-boundary.md) is now
implemented in Framework, without an AI reference or changes to existing APIs.
Original behavior remains pinned to read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df` through the linked source contracts.

[ZOD-205](targeted-move-continuation.md) additionally supports frozen targeted ACT
using the same transaction/token. The original Attack/Wait behavior described
below remains unchanged.

FacingMoveDecisionBattle.ExecuteMove validates active ownership, both unused
choices and revision room before invoking one synchronous planner. The context
issues only invocation-bound MoveThenAttack/Wait proposals. Ownership, structural
intent and forward RNG continuity are checked before private adoption into both
snapshot copies and actual FacingTurnPathMovement.MoveAlongPath execution.
Failed Move returns the identical original session, empty events and no path,
diagnostic or continuation. Successful Move publishes its real path/final heading
and one revision, then issues an opaque continuation bound to that exact returned
state/battle, actor, revision and lineage.

Continue accepts only session/actor/token and executes the frozen Attack or Wait
on current post-Move RNG. No planner, query output, replacement payload or second
RNG adoption is accepted. Attack returns its existing actual completion receipt;
Wait resolves deferred enemy-facing after Move and refunds unused ACT once.
Failure preserves the already-committed moved session and permits deterministic
retry. Explicit ordinary Wait abandons the intent on its returned newer branch.
Same original state/token replay is allowed; cross-replay, foreign, newer-state
and wrong-actor use rejects. No mutable consumed-token registry or save support.

Eight behavioral tests cover real path/heading and actual combat, deferred facing
at MAX RNG, full failed-Move rollback, later Attack exhaustion/retry/cancellation,
exact token binding, expired/cached/throwing proposals, invalid intent/RNG/ownership
and per-step revision limits. All 1,437 Framework tests pass with a warning-free
Framework build. Logs: ignored `scratch/move-continuation-168/`. Last full solution
validation remains ZOD-166's 3,906 .NET / 510 Python; the two-ticket batch reruns
full validation after AI integration. No C/original execution or host activation.

ZOD-169 adds one post-prefix in-reach AI Move-first adapter, owning fresh selection
and dispatch inside this callback. Fresh unmoved refresh/preparation, ACT-first,
post-ACT reselection and broader policies remain separate.
