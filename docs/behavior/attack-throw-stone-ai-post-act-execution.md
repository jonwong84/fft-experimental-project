# Fresh post-ACT pair execution — ZOD-215

Implemented 2026-11-04 against read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[post-ACT source design](../architecture/zod-209-pair-post-act.md) and
[fresh selection](attack-throw-stone-ai-post-act-selection.md).

`AttackThrowStoneAiPostActExecution.ExecuteInReach` / `ExecuteOutOfReach` own
selection inside `FacingDecisionBattle.Execute`. Require the active actor with
ACT used/Move unused, fresh refresh context and the existing dispatch assertions.
The selected MoveOnly plan is classified by the shared command dispatcher.
A different destination proposes a turn-ending path Move; the actual original
tile proposes Wait. Framework verifies/adopts decision RNG once and resolves
closed-enemy facing from actual final placement and its completion receipt.

Moving leaves CT15 in the supplied setup; staying refunds unused Move to CT35.
Live peer CT95 and ticks remain unchanged. Move plus facing adds two revisions;
same-tile END adds one. No ACT, hypothetical unit-state adoption, pre-ACT D1
reuse, Move-first token or automatic next-turn advance occurs.

Four scenario families cover real Attack and Stone hit/miss/push, both reach
branches, paths, refunds, actual target placement/facing, synchronized RNG,
last-lottery exhaustion, choice/source/terminal guards and deterministic replay
for both actors and reordered inputs. Literal final word/count checkpoints are
`(1223573862,72)` after Attack/move; `(3834121963,37)` after Stone hit/END;
`(1448521415,41)` after miss/END; `(713082951,38)` after push/END; and
`(1073193443,42)` after a far direct Stone push followed by Move to (3,2).
The far-push fixture applies an actual Stone directly; it does not claim a
pre-ACT selector chose that command.

All 4,103 .NET cases passed at this checkpoint, including 903 AI cases and the
owner's drafts. Seven TRX files are under ignored
`scratch/ai-pair-execution-215-220/215-full-trx/`. The 105-function source guide
audit passes at the pinned revision. Rejection preserves the incoming post-ACT
session with no events/diagnostic/path, so an already published ACT survives.
Framework's existing tests cover late path/facing revision rollback. No C or
original-game execution, UI review or general/terminal AI equivalence is claimed.
