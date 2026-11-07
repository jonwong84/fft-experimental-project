# Fresh in-reach pair Move-first execution — ZOD-206

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[ZOD-203 command-boundary trace](../architecture/zod-203-throw-stone-command-boundary.md),
[fresh pair selection](attack-throw-stone-ai-prepared-selection.md) and
[targeted Move continuation](targeted-move-continuation.md).

`AttackThrowStoneAiMoveFirstExecution.StartInReach` invokes the existing
Framework-owned confirmed-Move transaction. Its callback owns a fresh in-reach
pair preparation/scenario/scan/rank/lottery query against that exact live snapshot
and incoming RNG. No public cached selection, prior score, destination or rank
is an input. Remaining source/scoring/path/no-Defend certificates are unchanged.
The legacy-named `SwordAiCommandDispatchProfile` supplies only the shared
initialized-context/compact-record/canonical-coordinate/ordinary-continuation
facts; it does not assert an Attack-only ability list.

Framework admits both unused choices before planning. After owned fresh selection,
the source `ai_command_build` branch dispatches MOVE only for a Move-first plan
with a genuinely different destination. ACT-first and same-tile plans reject with
original session/RNG and no partial diagnostics. This occurs after actual fresh
selection, so the earlier decided/no-progress guard is not incorrectly repeated.
General command-history/control context remains bounded by explicit assertions.

Typed selected payloads map directly to frozen Framework proposals: Attack to
MoveThenAttack, Throw Stone to MoveThenTargetedAbility, and MoveOnly to
MoveThenWait with deferred enemy-facing. The selected ability/target stays in its
own typed command; Throw Stone is not disguised as an ordinary sword attack.
Framework validates/retains the proposal, adopts decision RNG once, and publishes
actual path/heading. Diagnostic decisions grant no continuation authority.

Follow-up uses `FacingMoveDecisionBattle.Continue(session, actor, token)` and the
current post-Move RNG. It performs no refresh, selection, movement rescore or
second adoption. Actual combat resolves through existing rules and creates a
receipt only for the completed damage/KO/displacement result. Wait resolves facing
after Move and refunds unused ACT once. Facing/next-turn progression remain
explicit; no host activation or post-ACT pair reselection is introduced.

Rejected selection/context/branch/Move publishes the original session, empty
events and no path, continuation or success diagnostic. Existing Framework tests
cover actual path-failure rollback; these integration scenarios cover owned
selection and pre-Move authority failures without adding an artificial path hook.
Once Move succeeds, late actual ACT failure preserves that moved session and
decision RNG, permits deterministic retry, and can be abandoned with explicit Wait.

## Literal checks and validation

The healthy setup begins at AddCt with actor CT110 and peer CT90, Speed5. The
initial tick/upkeep leaves CT15/95 and decision RNG `(1,7)` using the inverse BIOS
seed. Actor(1,1), target(2,1), Move3, PA5, HP50 and sword power1 select Move-first
Throw Stone toward (1,4). The fresh pipeline costs36 draws, ending
`(3397090333,43)`. The confirmed path heads North and adds no draws. Actual Stone
then deals5 with no push, ending `(2102736524,50)`; prediction had damage10.
The owned result matches a separate pure-query invocation without rescoring in
production. The first focused run corrected initial CT inputs to account for
AddCt before upkeep; runtime behavior did not change.

Actor(3,1), target(1,1), Move1 and sword power5 select Attack at (2,1), ending
decision RNG `(3791773014,24)`; actual damage25 ends `(1343201072,30)`. Front
evade100 makes both actions NotValuable: eight empty-slot rolls end
`(2473755763,29)`, MoveOnly chooses (3,2), and deferred Wait ends at CT35 with
no additional draw or receipt.

With a healthy maxHP10 target, Stone's projection predicts KO. Selection ends
`(835903122,44)`, but actual Stone deals5, leaves the target alive and pushes it
to (2,0), ending `(271520213,51)`. The completion receipt binds that real outcome.
Exact full budgets of43/23/22 draws cover Stone/Attack/MoveOnly; an actual Stone
one draw short leaves the committed Move intact.

Six integration scenarios cover those literal plans/costs, predicted-versus-real
KO, initial/late exhaustion, ACT-first/same-tile/wrong-branch/context rejection,
pure-query agreement and both-actor/order replay with exact continuation binding.
Locked restore, zero-warning/error Release rebuild, **4,073 .NET cases** across
seven projects (873 AI), **532 Python tests** and the pinned source/prepared-data
audit pass. Reports and seven full TRX files are under ignored
`scratch/ai-pair-move-first-206/`. Owner content drafts participate in validation
and remain excluded from this commit. No original/C execution or UI review;
frame/VSync RNG equivalence remains unresolved.

ZOD-207 should add the explicit known-score out-of-reach Move-first entry through
the same authority path, retaining full source masks/provenance and the post-score
reach draw. Unknown cast keys, ACT-first, pair post-ACT reselection, generic
interfaces and host/editor activation remain separate.
