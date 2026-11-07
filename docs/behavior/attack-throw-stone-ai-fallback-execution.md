# Fresh same-tile pair fallback END — ZOD-218

Implemented 2026-11-04 from the [ZOD-216 source design](../architecture/zod-216-pair-act-and-end.md),
pinned to fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.

`AttackThrowStoneAiFallbackExecution.ExecuteInReach` / `ExecuteOutOfReach`
own fresh both-unused selection inside `FacingDecisionBattle`. They admit
only an empty MoveOnly plan whose destination is the actor's actual tile.
Different-tile movement and nonempty actions reject before adoption. An empty
lottery cannot be used to skip a selected Move; a same-tile selected action
cannot be replaced with END. Existing source/profile assertions still apply.

Framework verifies and adopts the private decision RNG once, then `PrepareWait`
settles deferred facing and refunds both unused choices. In the supplied setup
CT15 becomes55, peer CT95 and ticks stay unchanged, and revision increases once.
There is no action, path, continuation token or separate facing receipt.

Three scenario families cover zero-accuracy in-reach and no-cast out-of-reach
END, eight empty lottery slots, refunds, choice/branch/context/provenance guards,
exact/short budgets, actor identities and reordered replay. Out-of-reach Move0
at distance5 costs11 draws (refresh1, score1, reach1, lottery8), ending at
`(180171308,18)` from `(1,7)`. Last-slot exhaustion returns the identical input
session with no events or private diagnostic. Targeted TRX:
ignored `scratch/ai-pair-execution-215-220/218-trx/`.

This is fresh fallback dispatch, distinct from the original earlier
saved-choice/no-progress END guard. No new no-progress history model, full
AI-turn router, host activation or terminal-battle policy is introduced.
