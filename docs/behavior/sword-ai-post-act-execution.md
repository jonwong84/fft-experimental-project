# Live post-ACT Move/END execution — ZOD-184

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base `2bd9a4a`.
Follows the [handoff design](../architecture/zod-183-post-act-command-boundary.md)
and existing [ACT-first source contract](../architecture/zod-176-ai-act-first.md).

`SwordAiPostActExecution.ExecuteInReach` and `ExecuteOutOfReach` accept the current
opaque facing session, request and existing ordinary profiles. Inside the
Framework-owned planning invocation they require ACT used/Move unused, own the
matching fresh selection once and call fresh dispatch. No external RNG, cached
plan/outcome, public callback or Move-first continuation is accepted. Fresh
selection completion is derived; original entry/dispatch context and no-Defend
facts remain explicit certificates. ACT used alone does not prove FreshRefreshEntry.

Framework's additive `PrepareTurnEndingMove` proposal keeps invocation binding,
forward BIOS checkpoint validation and both-RNG-copy adoption in Framework.
The actual confirmed path ends the turn and mints its settled completion receipt.
Framework immediately verifies/uses that exact receipt to finish facing before
publishing the combined events, path and success diagnostics. No intermediate
session or receipt escapes. A semantic `ChooseClosedEnemy` choice resolves from
the actual destination; the path heading is not substituted for wait-facing.

For same-tile END, the adapter uses ordinary Wait with the deferred facing choice.
Different-tile Move consumes both choices and refunds neither; END refunds unused
Move only. Move/facing uses two existing revision increments and Wait uses one;
RNG adoption uses none. Neither path advances the live clock or refreshes an ended
turn. Existing Attack/Wait transactions and Move-first continuations retain their
behavior. Path and typed path-rejection diagnostics are additive result properties.

Every failure returns the identical **post-ACT** input session, empty events,
no path and no success diagnostics, including failure after private RNG adoption,
path completion or receipt issuance. A rejected later decision never rewinds the
already published Attack. No production friend access or new dependency is added.

## Checked complete flows

The real ACT-first hit/miss fixtures select D0=(2,2), commit word3834121963/count37
or word3776134388/count42 into both live RNG copies and move from (2,1) to (2,2).
Path heading is North; final facing toward target (1,1) is South. Actual actor CT
stays15 and opponent CT95, with actual target HP25/50 unchanged. Hypothetical
clock units are never adopted. Exactly one path event, one facing event and no
second Attack are emitted; the session revision advances twice with no pending
facing left.

Move0 after a real miss selects same-tile END. A real critical push with Move0
makes the opponent out of reach and exercises the explicit out-of-reach END
branch. Both refund only Move, end CT35 and settle West facing in one revision.
The push fixture's pre-upkeep seed601690112 advances to Attack-entry word57;
using the pre-upkeep word as the Attack entry would not reproduce that push.

Five Framework tests cover the actual path/receipt transaction, invocation
closure/reuse rejection, actor/choice/RNG admission, late path/facing rollback and
exact/insufficient revision budgets. Five AI scenarios cover literal hit/miss
flows, stay/out-of-reach END, exact draw exhaustion, source/branch/terminal guards,
actual Critical/knockback and both actor/order replay. Existing geometry, clock,
facing, lottery and source matrices are reused.

Locked restore, zero-warning/error Release build, **3,986 .NET cases / 516 Python
tests** and the clean pinned source audit pass. This includes 1,442 Framework and
799 AI cases. Logs/seven full TRX results: ignored
`scratch/ai-post-act-execution-184/`. Owner content drafts are validated but excluded
from this commit. No C/original execution, UI review or host activation; this is
the closed ordinary profile, not full original AI equivalence.

The ordinary two-opponent Attack lifecycle now has explicit opt-in pre-ACT,
Move-first and post-ACT command paths. ZOD-185 investigates original ability-list
iteration and chooses the next independently traceable ability-search child.
General multi-ability search, replacement interfaces, terminal/KO policy,
presentation activation and other deferred work remain separate.
