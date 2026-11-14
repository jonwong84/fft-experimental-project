# Calling the bounded pair command adapters — ZOD-220

Integration review, 2026-11-04, at read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source design](../architecture/zod-216-pair-act-and-end.md) and
[command boundary](../architecture/ai-command-boundary.md) own the rules.
The adapters currently support an ordinary two-living-unit Attack/Throw Stone
profile on flat geometry with explicit remaining source assertions. This is
not a complete original AI turn controller.

## Inputs and entry selection

Start from the exact current `FacingTurnState` issued by Framework, with an
immutable `AttackThrowStoneAiSelectionRequest`, configuration and truthful
dispatch profile. The selected entry owns its pure selection inside Framework's
fresh planning callback. Pass neither a precomputed plan nor an externally
advanced RNG; actual rules continue from the verified decision checkpoint.

The recommended [single fresh command entry](../behavior/attack-throw-stone-ai-command-execution.md)
now derives reach and command order internally from one preparation/selection.
The [complete-turn driver](../behavior/attack-throw-stone-ai-turn-execution.md)
composes it with continuation/receipt settlement and exact partial-progress
recovery. Its admitted NonBlue and [Blue profiles](../behavior/blue-attack-throw-stone-ai.md)
share these command paths.
It accepts the same four inputs and returns the actual state, events, path and
retained continuation when applicable. The explicit phase adapters below remain
supported. Their original integration tests select fixed admitted scenarios;
callers must not retry adapters until one succeeds. Selection diagnostics are
observational and never authorize commands.

## Move-first

1. `AttackThrowStoneAiMoveFirstExecution.StartInReach` or `StartOutOfReach`
   performs one fresh decision and confirms the actual path. Store the returned
   session and opaque continuation together. The out-of-reach entry still
   rejects unknown cast-priority reads before adoption.
2. `FacingMoveDecisionBattle.Continue` consumes that exact session/token. It
   executes the frozen typed Attack, Stone or Wait using current post-Move RNG.
   No second selection occurs. An action's predicted HP/push is never copied.
3. A completed actual ACT ends the already-moved turn and issues a receipt.
   Pass the returned session/receipt to `FacingTurnBattle.FinishFacing`, choosing
   the closed enemy only when that living-profile assertion is still valid.
   A retained Wait already settles facing and needs no extra receipt call.

A late ACT failure leaves the committed Move intact. Return/display that state
and failure; a deterministic retry uses the same session/token. A replay-created
equivalent session is not the original token's authority.

## ACT-first

1. `AttackThrowStoneAiActFirstExecution.StartInReach` or `StartOutOfReach`
   performs one fresh decision and actual ACT. The result leaves Move unused
   and the actor active; it issues no completion receipt.
2. Inspect actual outcomes before selecting the post-ACT reach entry. A real
   miss still spends ACT; damage and knockback may differ from prediction.
   For example, Move0 Stone can start in reach at distance1 and push the enemy
   to distance2, requiring the out-of-reach post-ACT entry.
3. `AttackThrowStoneAiPostActExecution.ExecuteInReach` or `ExecuteOutOfReach`
   owns a new decision from that exact result. It commits a turn-ending path
   or same-tile END and settles facing internally from actual placement.
   Do not execute predicted pre-ACT D1 or invoke another ACT.

If the sole opponent dies, current living-pair post-ACT admission rejects. Keep
the successfully published lethal ACT, report the unsupported terminal route,
and stop. Do not invent a living target, final heading or battle-end rule.
Similarly, post-ACT budget/command failure preserves the published ACT rather
than reverting the entire turn.

## Fresh empty fallback

`AttackThrowStoneAiFallbackExecution.ExecuteInReach` / `ExecuteOutOfReach`
admit both-unused selection only when its empty plan stays at the actor tile.
Framework refunds both unused choices and settles facing in one END transition.
Different-tile fallback belongs to Move-first; nonempty same-tile action belongs
to ACT-first. This entry is separate from the earlier source no-progress guard.

None of these adapters advances the clock to the next actor. Keep simulation
steps distinct from presentation scheduling; the owner's brief pauses consume
no gameplay RNG or simulation time. Host activation and broader policies remain
future work in the [roadmap](ai-completion-roadmap.md).

## Checkable integration coverage

Three composed scenario families in
[AttackThrowStoneAiTurnFlowTests](../../tests/Zodiac.Framework.Ai.Tests/AttackThrowStone/AttackThrowStoneAiTurnFlowTests.cs)
exercise both Move-first reach branches/actor roles through actual ACT and
receipt-facing; ACT-first through fresh post-ACT settlement including knockback
changing branches; and failure/terminal recovery across published command
boundaries. They check literal RNG checkpoints, event order, choice/CT/revision,
no extra clock advance and immutable-input replay. The individual adapter tests
retain their source vectors and detailed admission/budget coverage.

Final validation: locked restore, zero-warning/error Release builds, all 4,113
.NET cases (913 AI, 1,455 Framework), all 534 Python tests, current coverage and
pinned source/prepared-data plus algorithm-guide audits pass. Seven final TRX
files are under ignored `scratch/ai-pair-execution-215-220/220-full-trx/`.
Owner content/editor drafts participate in validation and remain excluded from
these commits. No original/C execution or interactive UI review is claimed.

This review led to the single fresh pair command entry under ZOD-221–223.
ZOD-224 now composes complete headless turns from that entry and the existing
retained continuation/facing receipt, preserving explicit partial progress on failure.
The broad engine replacement contracts under ZOD-100/101 remain deferred.
