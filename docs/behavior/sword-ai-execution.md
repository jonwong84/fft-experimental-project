# Fresh already-moved ordinary AI command

ZOD-155, 2026-10-28. Implements the [ZOD-154 contract](../architecture/zod-154-ai-execution-rng.md)
against read-only clean fft_decomp **11464943bbdbd2a0af022d90cb7f10455cf911df**.
[Implementation receipt](../evidence/ai/execution-implementation-v1.json).

Current ownership after ZOD-158: implementation and namespace are
`Zodiac.Framework.Ai`; Framework owns the [planning/command boundary](../architecture/ai-command-boundary.md).
AI no longer constructs staged sessions or adopts RNG itself. The boundary
invokes its fresh planning callback, verifies the RNG checkpoint and applies
one command. The ZOD-155 results below retain their original provenance.

`SwordAiExecution.ExecuteAlreadyMoved` takes an existing facing session, bounded
weapon and the existing semantic selection/context profiles. It requires the
active actor with Move used, ACT unused, no pending facing and revision room.
`GeneralSelectionEntryContext` defaults false: earlier original refresh/policy
work must already be represented in current state/RNG. The existing first/
terminal Attack, no-Defend, ordinary movement/scoring and Wait/end facts still
apply. This is not a complete original command-build entry point.

## Execution and publication

The boundary runs fresh in-reach derived selection once, starting from the
session's current RNG. It accepts no external RNG, cached plan, projected result
or raw snapshot to attach. Already-used movement restricts selection to the
actor's current tile; other plan shapes reject explicitly.

Only after selection succeeds does a private intermediate session adopt its
progressed RNG into both Battle and Turns. All other fields, lineage and revision
remain unchanged. That intermediate session is never published.

- **Attack:** construct an actual command and delegate to FacingTurnBattle.Attack.
  Roll evasion/critical/proc from the decision's final RNG. Publish actual HP,
  KO/knockback and the existing receipt after the fully settled result. Projected
  damage is diagnostic only. Both choices being spent, CT has already ended;
  finish facing separately through ZOD-153 without another refund or RNG draw.
- **MoveOnly:** no movement remains and no ACT payload is active. Delegate to
  SwordAiWaitSettlement.Wait after adoption. Preserve decision draws, refund the
  unused ACT, settle facing and return with no pending receipt.

Either successful command increments revision exactly once; decision/adoption
does not increment it. The outcome includes fresh decision diagnostics, decision
RNG, actual command kind and optional Attack/Wait-facing data. No live host/editor
activation or general plan-continuation interface is introduced.

Every rejected operation returns the identical input session, empty events and
no outcome. Selection and command failures carry typed reasons; they contain no
private transition snapshots, intermediate RNG or partial plan. A command that
runs out of RNG discards its successful decision prefix too. This managed atomic
policy does not claim original C rewinds its stream.

A successful lethal Attack remains committed with its pending receipt. The
current two-living-opponent AI facing query rejects that later completion; it
does not undo the Attack or guess a no-enemy facing. This remains a host-activation
gate. Replaying an old immutable command input is valid; its receipt cannot
authorize another branch/session or a newer settled state.

## Validation scope

Fifteen focused new cases use real Begin/Advance/Move sessions and fresh managed
selection. The ZOD-154 entry word1/count7 is reached through a real upkeep draw.
The one-roll and eight-roll cases project damage25/HP25, but the actual Attack
misses and keeps HP50, ending at word180171308/count18 or word3535774167/count25.
Fallback retains all nine/fourteen decision draws and refunds CT15->35 once.

Other cases cover real noncritical damage, critical push versus lethal KO, exact
MAX success, late command and selection rollback, required context, choice/
revision authority, both RNG copies and identity/order/immutable replay. All 142
focused execution/selection/facing-session cases passed. Friend construction is
used only for the revision-exhaustion boundary; ordinary command cases do not
fabricate active snapshots or completion receipts.

The pinned source audit rechecks inherited receipts plus six execution files,
16 functions and three supplied-prefix tapes. These offline arithmetic checks
are distinct from the managed command tests. Locked restore and a zero-warning/
error Release build passed, followed by **3,836 .NET / 500 Python tests**. Local
result paths are recorded in the implementation receipt. No original/C execution or UI
review occurred. The owner's definition drafts remain outside this commit.

Earlier refresh-prefix work, Move-first continuation, post-ACT reselection,
out-of-reach execution, general abilities/maps and exact frame-interleaved retail
RNG remain open. Rendering and brief presentation pauses add no gameplay draws.
