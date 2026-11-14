# ZOD-165 — Ordinary command dispatch and selected-action reuse

Source/design, 2026-10-28. Read-only clean fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base
`3c008f84d5fedacd6e40f3fe8ebffbd7e1412372` (ZOD-164).
The [receipt](../evidence/ai/command-continuation-contract-v1.json) extends the
preparation audit. No production C#, C/original execution or host activation.

## Command builder ordering

`ai_command_build` returns one command per completed call. After the frame/resume
gate, **before installing the requested actor**, it checks the previous acting
unit's moved/acted bytes against the workspace's saved pre-command bytes. With
selected.rank.decided exactly **0xff** and both pairs equal, it returns END_TURN
immediately. It does not refresh, initialize the actor, update saved choices or
shift command history on this path. A normal continuation therefore requires the
same initialized actor; a caller-supplied new ID cannot prove that ownership.

After compact-record checks, `acting_unit_init` refreshes actor coordinates/team,
Move and cached delay, and clears only the current action's coordinate zero byte.
It does not replace the selected action or its target/ACT payload. If selected
decided is zero **or ACT has been taken**, refresh/preparation/selection runs and
marks the selected action decided (0xff). Otherwise the selection is reused.
Fresh selection finishes inside that invocation: the earlier no-progress guard
is **not** evaluated again against its newly decided result.

Dispatch then emits MOVE when Move is unused, either ACT is already used or the
selection is move-first, and canonical selected destination differs from the
current actor tile. Otherwise it emits END_TURN when ACT is already used or the
selected skillset is zero, and ACT in the remaining case. ACT copies the selected
action payload; END_TURN leaves the old union payload in memory, which is not an
executable Attack. Managed outputs should use semantic command kinds/optional
payloads, not reinterpret leftover union bytes.

Normal dispatch saves the **current pre-execution** moved/acted flags, restores
the actor's current coordinates after simulations, shifts history[1] to [0] and
appends the issued command to [1]. The static two-entry history has no decision
consumer in this pinned source; it is not the reuse guard. Do not introduce its
fixed array as a managed authority mechanism.

The source tests decided==0xff for the guard but !decided for reselection. A
noncanonical nonzero byte (e.g.1) skips both when ACT is unused. Preserve this
recorded distinction; the first managed child admits only ordinary 0/0xff
semantics instead of silently treating every nonzero source byte as equivalent.

## Real Move and invalidation

The controller issues MOVE through destination/path handling. `move_confirm`
registers the settled tile/facing and calls turn_move_taken_set. With unused,
unrestricted ACT this sets Move without ending the turn, advancing CT or running
the between-turn clock. Existing [confirmed path movement](../behavior/confirmed-path-movement.md)
owns the supported managed physical path and final heading; the older Manhattan
Move API must not be substituted for that future source-compatible movement.

Post-move steps lead through AfterCommand/ContinueTurn, action-menu routing and
ai_command_enter. The latter resets command readiness/frame counters, **not** the
selected action. For the same actor with saved00/current10 and selected0xff,
the guard fails, ACT is unused and the existing selection is reused. It returns
the retained ACT, or END_TURN for a move-only fallback. Calling the fresh prepared
already-moved selector here would incorrectly repeat refresh/search/lottery draws.
Repeating command build with saved10/current10 instead hits the no-progress END.

After ACT, changed choices pass the early guard but action_taken requests a new
selection (e.g. to choose a later Move). Unchanged choices hit the early END even
when ACT is set: ordering matters. Completed ordinary turns go to Wait through
the controller without requiring another AI command; do not fabricate that call.

Reuse is conditional. New-turn setup calls ai_selected_action_init; crystal/
treasure completion, traps and failed teleport can clear the selected rank too.
Scenario-event completion can reinitialize the workspace. These paths require
fresh selection or a broader policy. The proposed ordinary profile excludes
those effects, mounts, restrictions, control changes and intervening battle edits.
Wait facing is chosen on settled positions, not stored from pre-Move coordinates.

The direct dispatch/reuse branches add no gameplay RNG draws or simulation ticks.
Actual ACT uses the current post-Move RNG and real combat; no decision replay or
RNG refund occurs. Original animation/controller frames still have BIOS draws;
managed presentation remains outside gameplay RNG. This is not a retail frame-
stream or timing-equivalence claim.

## Smallest child: ZOD-166 pure command-dispatch query

Implement a pure ordinary `SwordAiCommandDispatch` query over current immutable
battle data, semantic selected Attack/MoveOnly data and saved pre-command choice
facts. Explicit admission certifies same initialized actor, compact-record context,
canonical coordinates and the effect-free ordinary continuation. Caller-provided
selection/cache data is descriptive only; acceptance grants no command authority.

Keep entry inspection separate from dispatch after fresh selection. Inspection
can return **SelectionRequired**, a no-progress END, or dispatch the retained
choice. Dispatch after a freshly completed selection skips the earlier guard;
it must never report no-progress merely because pre-command flags still match.
Represent Move, Attack and EndTurn semantically, with whether normal command
recording/saved-choice update would occur. SelectionRequired describes a managed
stop before unimplemented work, not an extra original return code.

Check the source vectors for fresh Move-first, retained ACT after Move, move-only
END, act-first/same-tile ACT, repeated-command guard, ACT-required reselection and
noncanonical decided rejection. Admit either actor and collection order; reject
wrong active actor, inconsistent selected actor/target/destination/order, unknown
context and invalid ordinary data atomically. No scoring, selection replay, path
execution, receipt/revision mutation or draw/tick reservation (including MAX).
Tests compare immutable input/RNG and expose no partial payload on rejection.

A later live continuation must be issued by Framework from its own fresh planner
and successful real Move, bound to the exact returned snapshot, actor, revision
and lineage. Public query outputs, cached flags, destination equality and numeric
revision alone cannot authorize it. A changed/stale session or replay's token must
reject. A failed later ACT preserves the committed Move and reusable pre-ACT state;
it cannot rewind an earlier published command. No saveable tokens or public RNG
setter is selected here. Fresh unmoved refresh/preparation, post-ACT reselection,
opaque Move/continuation authority and host activation remain subsequent work.

Validation: the clean pinned audit checks nine files, 25 continuation functions
and ten source branch vectors, plus inherited receipts
(`scratch/ai-command-continuation-165/source.json`). All **510 Python tests** pass,
including three new offline checks (`scratch/ai-command-continuation-165/python.txt`).
No production C# changed; the last full .NET run remains ZOD-164's **3,891** cases,
not rerun here. Proposed managed query/continuation cases remain unexecuted.
