# One fresh pair command entry — ZOD-221

Source/design review, 2026-11-04. Read-only fft_decomp HEAD is clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base is `72cd3ec`.
The [verified algorithm index](../evidence/ai/algorithm-guide-v1.json) pins
`turn.c::ai_command_build`, `scenarios_build`, `target_reach_get`,
`general_select`, `ranked_select` and `fallback_select`, plus the origin
evaluation functions. This design follows the [composed command review](../development/ai-pair-command-flows.md).

## Source decisions and lifetime

`ai_command_build` checks saved-choice no-progress and compact records before
fresh refresh. An undecided entry or a changed ACT-used entry then owns refresh,
ability preparation, movement and selection. Only after simulation restoration
does it dispatch: unused Move plus (ACT used or not ACT-first) and a different
destination emits MOVE; otherwise ACT used or skillset0 emits END; otherwise ACT.
No second search is used to choose which command executor to call.

`scenarios_build` reads target distance and `target_reach_get` before scoring.
Focused enemy reach comes from prepared enemy minimum reach. The out-of-reach
NonBlue modulo-seven draw occurs **after** movement scoring and changes the
destination threshold, not which branch initially ran. The admitted pair has a
focused enemy, ordinary flat geometry and no reflected/stay-near/crystal override.
Use fresh prepared reach and shared target-distance rows; do not infer the branch
from whether an ability can target the current tile or from the later halved reach.

The new entry is explicitly **fresh**. Existing refresh/dispatch assertions still
certify source context, including passing the earlier no-progress guard. It does
not reconstruct retained command history or expose a general original controller.
Move-used follow-up continues through the exact existing opaque continuation;
it must not restart fresh selection. Post-ACT input uses actual state and a new
refresh, not the previous predicted D1. Terminal input still rejects explicitly.

## ZOD-222: one Framework planning callback can choose a planned Move

Extend the existing `FacingDecisionContext` with typed Move-then-Attack,
Move-then-targeted-ability and Move-then-Wait factories. Each wraps a private
`FacingMoveDecisionContext` for the same immutable battle and actor. Close both
contexts in the existing `finally` path. The outer proposal remains bound to
its issuing callback; callers cannot construct or transplant either proposal.

Factor the current planned-Move validation/application into one internal function
used by both `FacingMoveDecisionBattle.ExecuteMove` and the extended
`FacingDecisionBattle.Execute`. Reuse its intent, forward-RNG, path and exact-token
logic. Invoke no second planner, adopt no stream twice and duplicate no command
implementation. Return the confirmed path and existing continuation on accepted
Move; expose its typed failure reason on rejection. All rejection paths retain
the original session, empty events and no diagnostic/path/token. Existing ACT,
Wait and post-ACT Move behavior and authority guard ordering stay unchanged.

Check callback count, actual Move then frozen Attack/Stone/Wait, exact-session
binding, expired/foreign/null/throwing proposals, invalid actor/RNG/path/intents,
choice and revision limits, and late follow-up failure preserving the Move.
Use the existing Framework scenario families as regression coverage rather than
duplicating every authority test under a new name.

## ZOD-223: one pair entry owns preparation, reach and dispatch

Add `AttackThrowStoneAiCommandExecution.Execute(state, request, configuration,
dispatchProfile)` inside the existing AI assembly. Framework validates the actor,
pending facing and revision before planning. Require Move unused; actual ACT-used
selects the fresh post-ACT path, otherwise use the both-unused path.

Own preparation once, derive the reach branch from its restored geometry/reach,
and call internal continuations of the existing scenario/selection owners.
Reuse in-reach D0/D1, out-of-reach provenance, scoring/composition and empty
post-ACT lottery kernels. Internal continuations accept only private freshly
owned results; add no public cached-result input. Keep explicit older entries
working with the same behavior.

Dispatch the private typed plan through Framework proposal factories:

- Before ACT: different-tile Move-first freezes the selected Attack, Stone or
  deferred Wait in a planned Move; nonempty ACT-first/current-tile action emits
  actual ACT; empty same-tile fallback emits Wait.
- After ACT: different-tile Move ends the turn and settles actual-receipt facing;
  same-tile fallback emits Wait. No second ACT or Move-first token is issued.

Return typed decision diagnostics, actual events/state/path and a continuation
only for a committed pre-ACT Move. A successful ACT keeps unused Move available.
Leave receipt finishing after Move-first ACT and next-turn clock advancement to
the existing caller flow. Preserve per-command atomicity, source/profile guards,
unknown cast-key rejection, terminal restrictions and frame/RNG uncertainties.

Check literal streams against established separate entries for both reach
branches, Attack/Stone/empty plans, actual miss/push/KO, post-ACT branch changes,
exact/short budgets, both actors/reordered inputs and replay. No host/editor
activation, whole-turn rollback, generic replacement interface or alternative
AI policy is selected. The source C remains a study reference only.

Pinned source paths: [turn.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/turn.c),
[evaluation.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/evaluation.c).
The source-guide audit verifies local file/function hashes; it does not execute
C or establish full original-game equivalence.
