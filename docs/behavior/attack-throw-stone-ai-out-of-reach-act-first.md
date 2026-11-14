# Fresh out-of-reach pair ACT-first — ZOD-217

Implemented 2026-11-04 from the [ZOD-216 source design](../architecture/zod-216-pair-act-and-end.md),
pinned to fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.

`AttackThrowStoneAiActFirstExecution.StartOutOfReach` owns the current
known-score selector in Framework's ACT transaction. It shares active-choice,
dispatch-context and actual typed command handling with `StartInReach` while
retaining a distinct out-of-reach decision diagnostic. Public inputs remain
state, request, configuration and dispatch assertions. No cached plan is accepted.

Move0 freshly scores the actor through the no-closer-tile fallback. At distance3
the actor can cast Stone before movement. Starting at word1/count7, selection
ends `(3210001534,16)` and the real seven-draw Stone deals5, pushes, and ends
`(3297241969,23)`. Word20 halves reach1 to0 after scoring, yet still selects
ACT-first; actual Stone deals10 without push and ends `(3660420676,23)`.
HP10 instead selects at `(267834847,17)` and real lethal damage ends
`(3791773014,24)`. Lethal ACT remains published without terminal reselection.

Four scenario families cover these independent literal streams, low-accuracy
miss/exact budgets, short actual-command rollback, unknown cast priority,
empty plans, wrong branches, dispatch/choice guards and both-actor reordered
replay. The shared in-reach executor's six existing scenarios also pass.
Targeted TRX: ignored `scratch/ai-pair-execution-215-220/217-trx/`.

Only ACT is spent. Move remains unused, the actor remains active, and no path,
completion receipt or automatic END is issued. Subsequent movement requires a
fresh post-ACT decision from actual effects. Unknown range-four actor priority
still rejects before adoption; retained/event history remains unresolved.
