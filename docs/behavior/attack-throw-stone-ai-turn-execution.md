# Complete bounded pair turn — ZOD-224

Implemented 2026-11-04 at read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[command-flow design](../architecture/zod-221-fresh-pair-command-entry.md) and
[source controller study](../architecture/zod-225-blue-pair-policy.md#turn-driver-source-boundary)
own the source boundary.

`AttackThrowStoneAiTurnExecution.Execute` takes the same current session,
immutable pair request/configuration and dispatch assertions as the single
command entry. It composes that entry with the existing retained continuation
and actual facing receipt until this actor's turn is settled. Callers select
neither reach nor command order. Each accepted step consumes a choice, ends the
turn or settles a receipt; rejection stops immediately rather than retrying.

Move-first retains one private selection, confirms a real path, applies its
frozen Attack/Stone/Wait and finishes actual-receipt facing. ACT-first applies
the real action, then makes a fresh movement decision from actual HP, positions
and RNG; a push may change the reach branch. Empty fallback uses normal END
refunds. Successful output contains the settled state, ordered actual events
and successful fresh-decision diagnostics. The next-turn clock is not advanced.

Failure retains all earlier published progress. Its stage identifies fresh
command, retained command or facing, with the underlying typed rejection.
Only successful earlier steps contribute events/decisions; a failed private
selection or command exposes no partial diagnostic or RNG adoption.

An optional `AttackThrowStoneAiTurnRecovery` freezes the original inputs, exact
partial session and next step, retaining the existing Framework continuation
when needed. `Resume(state, recovery)` requires that exact state instance.
Equivalent replays/foreign states reject with empty output and no new cursor.
The cursor grants no new Framework authority and is not a save/checkpoint token.
Resume returns only newly published events/decisions, so a retry does not replay
the already displayed Move or ACT. Unchanged exhausted budgets remain failures.
If inputs must change, start a newly admitted command flow rather than editing
the frozen recovery cursor.

Terminal policy remains unsupported. A lethal ACT-first result survives a later
terminal-refresh rejection. A lethal Move-first ACT survives a later
NoLivingEnemy facing rejection with its actual pending receipt. The driver does
not manufacture a living target, final heading or battle outcome.

Four scenario families cover complete Move-first/ACT-first/fallback streams,
partial recovery and cross-replay binding, both lethal boundaries and reordered
actor replay. Named vectors retain established word/count checkpoints, CT,
event order, synchronized RNG and unchanged live ticks/input snapshots. One
initial lethal fixture selected ACT-first earlier than intended; the corrected
Move-first witness requires moving into Attack range. Runtime behavior did not
change to satisfy that test. Focused TRX:
ignored `scratch/ai-turn-blue-224-228/224-final-trx/`.

No presentation pause, original frame/VSync draw, host/editor activation,
whole-turn rollback, broad replacement interface or new terminal policy is added.
