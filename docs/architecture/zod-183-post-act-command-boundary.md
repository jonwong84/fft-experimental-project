# ZOD-183 — Framework-owned post-ACT Move/END handoff

Design completed 2026-10-29; implemented by
[ZOD-184](../behavior/sword-ai-post-act-execution.md). The design below retains
its original prospective acceptance criteria; the implementation note records results.
Read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`;
managed base `6bc2a90`. Builds on [fresh post-ACT selection](../behavior/sword-ai-post-act-selection.md),
the [controlled planning transaction](ai-command-boundary.md) and
[ACT-first lifecycle](zod-176-ai-act-first.md). ZOD-184 is the implementation child.

## Source and existing authority

`turn.c::ai_command_build` first compares decided/saved choices against current
choices. Equality returns END without refresh. Otherwise ACT used forces fresh
selection. After selection, different coordinates with unused Move emit MOVE;
ACT used or skillset zero otherwise emits END. The saved choice pair records
the pre-command state, not an executable continuation or authorization token.
The existing `DispatchFreshSelection` already models this post-selection tail.
It must not rerun `InspectEntry` against the newly recorded current pair.

`clock.c::turn_move_taken_set` retains actual ACT used and delegates to
`turn_move_act_taken_set`; `turn_should_end` then ends this ordinary turn.
`turn_end` refunds no choice after the real Move, or unused Move only for END.
The 20 pinned ACT-first function anchors/inherited source receipts remain intact;
the clock file also pins these spent-choice setters. No frame-interleaved RNG
equivalence or original execution is inferred.

Framework already owns the pieces:

- `FacingDecisionBattle.Execute` binds one fresh planner invocation, validates
  forward BIOS RNG and stages both live RNG copies before Attack/Wait.
- `FacingTurnPathMovement.MoveAlongPath` validates an actual path, publishes its
  final heading and ends an ACT-used turn. Only its fully settled result mints
  the exact-state/lineage/revision `FacingCompletionReceipt`.
- `FacingTurnBattle.FinishFacing` consumes that pending authority against the
  settled destination. `WaitWithFacing` handles a same-tile END directly.
- `FacingMoveDecisionBattle` requires both choices unused and issues a retained
  Attack/Wait continuation. Its assumptions do not fit post-ACT Move.

## Selected narrow extension

Extend the existing invocation-bound `FacingDecision<T>` with a **turn-ending
Move** intent, rather than another session factory or continuation system.
Proposed `FacingDecisionContext.PrepareTurnEndingMove` accepts a Move command,
the existing flat ordinary movement profile, semantic deferred `FacingChoice`,
progressed RNG and diagnostics. These are proposed API names, not current APIs.
Append an internal intent kind; keep existing Attack/Wait admission unchanged.
The new intent requires the active actor's ACT used and Move unused and must
reject another actor's Move. No pre-ACT path may use it.

After checking proposal ownership and forward RNG, Framework privately stages
RNG and calls the existing path Move. Require its actual pending completion
receipt and ended actor, then immediately call `FinishFacing` with that exact
receipt. Resolve `ChooseClosedEnemy` on the destination, never convert it to a
fixed facing at the old origin. Publish only the fully settled combined events,
confirmed path, final session and diagnostics. A path heading is an intermediate
real movement result, not the final wait-facing choice.

Keep the existing generic result constructor compatible; additive path and typed
path-rejection diagnostics can describe this actual consumer. Failure exposes
no path, success diagnostics or intermediate session. The command/receipt rules
stay in Framework; AI receives no friend access or session/receipt constructor.
Do not introduce a general command bus, saveable token or replacement-AI interface.

## AI adapter and transaction boundary

Add explicit opt-in in/out-of-reach post-ACT execution entries. They accept the
current opaque `FacingTurnState`, original request and existing profiles, with
no supplied RNG, plan, cached result or public planning callback. Inside the
Framework callback, check ACT used/Move unused, own the matching fresh selection
once, then call fresh dispatch. Derive FreshSelectionCompleted from success;
ordinary dispatch context and FreshRefreshEntry remain explicit source facts.
ACT used alone cannot prove that the original early no-progress guard passed.

For MOVE, prepare the turn-ending Move with deferred `ChooseClosedEnemy` facing.
For same-tile END, prepare ordinary Wait with that semantic facing choice. Reject
Attack, reselection or an inconsistent destination instead of routing them
implicitly. No Move-first continuation or second Attack is issued. Both commands
stop after settlement; do not refresh or advance the clock for an ended turn.

RNG adoption adds no revision. Move plus facing uses the existing **two** revision
increments; same-tile Wait uses **one**. Support exact available budgets without
overflow. Any failure, including invalid path, invalid RNG, unavailable revision
or late facing failure, returns the identical post-ACT input session, empty
events and no partial diagnostics/path. Once the earlier Attack was published,
later failure may not restore the pre-Attack HP, positions, RNG or revision.

## Implementation acceptance

Use the existing real ACT-first hit/miss tapes. Selection ends at
word3834121963/count37 or word3776134388/count42 and D0=(2,2). Real Move from
(2,1) then ends actor CT15, with no clock tick or RNG draw. Its path heads North;
deferred facing toward the sole target at (1,1) resolves South at (2,2).
Same-tile END instead refunds only Move, ending CT35 on this live CT15 baseline.
These are prospective integration expectations, not a completed executor claim.

ZOD-184 must cover invocation closure/cross-invocation rejection, wrong actor and
choice mode, valid/invalid forward RNG, confirmed path and actual receipt,
exact/insufficient revision budgets and late rollback. Add bounded AI integration
cases for actual hit/miss, stay, out-of-reach branch, Critical/knockback, both
actors/order replay, and no duplicate prefix/Attack/facing events. Reuse existing
path/facing matrices. KO/terminal states remain rejected by ordinary selection;
scripts, control changes and automatic battle-end policy require later admission.

Reviewed existing decision, confirmed-path and receipt implementations/tests;
the ZOD-181/182 full build, 3,976 .NET tests, 516 Python tests and clean pinned
source audit pass. Logs: ignored `scratch/ai-post-act-181-183/`. No new runtime
entry, C/original execution, UI review or host activation is introduced here.
