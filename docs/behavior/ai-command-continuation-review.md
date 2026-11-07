# AI command and continuation retrospective — ZOD-435

Completed 2026-11-07 as a bounded batch of the
[ZOD-428 programme](../development/documentation-review-programme.md), following
the [rank/lottery review](ai-rank-selection-review.md). No new defect was confirmed
in admitted checked commands, continuations or turn recovery. This batch changes
no runtime code, tests, fixtures, owner drafts or historical receipts.

Managed baseline: `edc1aec71638219e4c9228f811cb91bda723a978`. The read-only
reference was rechecked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [receipt](../evidence/ai/command-continuation-review-v1.json) pins current
inputs, source functions and validation. Broader controller/status/frame branches
remain outside this review. The owner's controller formatting was read in place
and preserved unstaged.

## Native dispatch and managed entry

`AiCommandExecutor.Execute` enters `RosterDecisionBattle.Execute`, then owns one
fresh `AiDecisionMaker.Decide` invocation against that callback's actual state
and RNG. No public cached plan or caller RNG is accepted as command authority.
The original `ai_command_build` ordering is preserved:

- With Move unused, a different destination, and ACT used or a Move-first plan,
  emit MOVE.
- Otherwise emit END if ACT is used or no action was selected.
- Otherwise emit ACT, dispatched to the actual supported effect family.

An exhaustive comparison of the raw32 boolean dispatch combinations matches;
these are branch cases, not32 assertions of playable turn states. Framework's
actor/choice/profile guards impose the stronger execution conditions.

Native selection is retained after a normal Move-first command. After ACT,
`ai_command_build` refreshes and selects again from actual results. Managed Move
continuations and fresh post-ACT commands preserve that distinction. The native
saved-choice/no-progress END guard, frame suspension and command history are
not recreated by a general-purpose managed token; current explicit entry
certificates and monotonically consumed choices delimit the supported domain.

`battle_state_ai_command_update` prepares and dispatches the native command;
`action_execute_ability` marks ACT before `action_commit_preparation` and effects. Confirmed
movement commits coordinates/heading and marks Move. `turn_move_act_taken_set`
ends a turn once both choices are unavailable. In this ordinary domain,
`TurnCycle.Apply` and actual command owners preserve those choice consequences,
unused-choice20 CT refunds and cap60. Wait ends without falsely consuming unused
choices. No command or facing operation advances the next AddCt step.

## Checked publication and frozen follow-ups

`RosterDecisionBattle` closes the context on return or exception. Only that
invocation's opaque proposal, for its actor and permitted choices, can publish.
It verifies the full32-bit RNG word and nondecreasing64-bit count using affine
LCG composition, stages both RNG copies, and applies a concrete actual command.
This establishes continuity, not proof that a policy needed every reported draw.
The independent affine/direct check matches1,032 seed/delta combinations; existing
managed vectors also cover huge valid deltas and wrong checkpoints.

Invalid selection, unsupported dispatch facts, actual admission, exhausted RNG,
revision limits or late private settlement return the exact input session,
incoming priority workspace, empty events/path/token and no diagnostic. Actual
success publishes real effect RNG and the accepted successor knowledge. A failed
post-ACT operation cannot rewind the earlier public ACT.

Planned Move requires both choices unused and freezes the concrete follow-up.
Structural target/grant/setup checks precede publication; range, interception,
weapon and actual effects are checked at the post-Move state. This permits an
initially out-of-range Stone and preserves live interception after movement.
No projected HP, PA, displacement or formula roll is adopted as the actual result.

`RosterMoveDecisionBattle.Continue` requires the exact session and battle object,
revision, lineage, actor, moved choice and unused ACT. It accepts no replacement
command, planner or new RNG. A failed follow-up preserves the accepted Move and
permits deterministic same-state retry or explicit Wait abandonment. Tokens do
not cross independently replayed branches; there is no mutable consumed registry.
The older pair owners retain the same invocation/continuation contract at their
inspected edges; this is not a full review of every compatibility adapter.

## Facing and complete-turn recovery

`RosterFacingTurnBattle` publishes actual session revisions and exact-result
completion receipts. Pending facing blocks further commands. Completion verifies
receipt identity, expected battle, lineage, revision, actor and ended turn.
Null facing keeps the actual heading; cardinal facing is explicit. Invalid facing
and revision exhaustion reject atomically. A post-ACT path plus its facing is one
public transaction, preserving a prior ACT if its private finish fails.

`AiTurnController` routes retained command first, pending facing next, then fresh
post-ACT selection while the actor remains active, otherwise completion. Eight
raw routing combinations match the explicit precedence. It stops before the next
clock. ACT-first actual KO can hit the deferred corpse guard on re-selection;
that is a retained real KO with failure/recovery, not a rolled-back action or a
claim that full corpse/terminal AI is implemented.

Failure returns already accepted state/events/decisions/workspace and an opaque
cursor. Resume requires the exact returned session and freezes stage, request,
configuration, workspace and continuation. It emits newly accepted work only.
Foreign/reconstructed/newer branches reject. Retrying invalid frozen facing does
not replace the configuration; explicit manual receipt completion creates a
different branch. A last representable revision may preserve an accepted Move
whose later follow-up cannot publish another revision; it never wraps silently.

These managed authority/atomicity/recovery contracts are deliberate architecture,
not native save tokens or VSync resume semantics. The native controller's camera,
menu, scenario events and31-frame gate remain outside the deterministic headless
domain. Automatic `ai_wait_facing_choose`, including the unresolved overlapping
viability read, is not substituted by fixed/kept facing.

## Validation, documentation and follow-up

Fresh full runs pass5,181 .NET tests, including1,384 AI, and794 Python tests.
Existing source audits pass21 authority,8 self-settlement and12 command/turn
compositions. Actual regression suites cover stale/foreign proposals, both command
orders, post-Move interception, exact-session retries, partial KO/facing/RNG
failures and state/event/priority isolation. No new regression was needed without
a confirmed defect. No reference C, original executable or native UI was run.

Ten owner draft blobs, immutable JobStats, approved targeting formatting and the
owner controller edit remain preserved. Existing supplied documentation plus the
pinned source were sufficient for this bounded pass; no additional FFHacktics
routine was required. For the next magic review, the formula0x08 and0x0C pages
with their called-routine traces would provide a useful additional cross-check.

Next is ZOD-436, ordinary magic arithmetic/evasion/result routing. General magic
AI commands/origins, special policies, Defend, host activation, full corpse/
terminal scheduling, frame RNG and final readability gate ZOD-432 remain separate.
