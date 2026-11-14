# Confirmed Move with frozen targeted ACT — ZOD-205

Implemented 2026-10-29 using the
[ZOD-203 source/design contract](../architecture/zod-203-throw-stone-command-boundary.md)
and [ZOD-204 targeted ACT transaction](targeted-ability-decision.md). Source remains
clean read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.

`FacingMoveDecisionContext.PrepareMoveThenTargetedAbility` extends the existing
invocation-bound Move proposal with an immutable `UseTargetedAbilityCommand` and
`TargetedAbilityBattleProfile`. Structural actor/target/ability/grant/setup checks
run before adopting RNG or moving. OutOfRange is allowed at this stage because
the Move may enter range. All other targeted-command admission must hold.

Framework validates the proposal owner and forward RNG checkpoint, stages RNG
into both battle/turn copies once, and performs the actual confirmed path Move.
Failure publishes the identical incoming session, empty events and no path,
continuation or diagnostic. Success publishes actual position/final heading and
one revision, then freezes the targeted payload/profile inside the existing
opaque continuation bound to exact state/battle, actor, revision and lineage.

`Continue` still accepts only session/actor/token. It performs no planner call,
replacement-payload acceptance, second RNG adoption or movement preview. The
actual targeted ACT executes against current post-Move RNG and range, using
the existing real Throw Stone rules. Successful ACT adds one revision and binds
a receipt to fully settled damage/KO/displacement. Explicit facing adds its own
revision without another draw, clock tick or unused-choice refund.

Each command is atomic. Failed ACT returns the already-published moved session
and empty events; it does not discard the earlier Move or its adopted RNG.
Retry on that exact immutable branch is deterministic. Ordinary Wait can abandon
the intent on a newer branch; the old continuation is stale there. Equal-valued
replays, foreign/newer sessions and wrong actors reject. This adds no global
consumed-token registry, save token or production friend access.

## Literal checks and validation

The shared healthy setup starts seed1/count7 and reaches active actor CT15 after
one upkeep draw/tick. Eight supplied planner draws produce `(3210001534,16)`;
these are boundary witnesses, not an actual AI selection. Actor(2,1) moves to
(3,1), heading East, without another draw. Actual Throw Stone against (6,1) deals
5, pushes to (7,1) and ends `(3297241969,23)`. Front evade100 instead misses after
four actual draws at `(595337866,20)`. KO and edge-canceled push retain seven draws.

Target(7,1) starts out of range and becomes legal after Move to (3,1). Move to
(1,1) instead succeeds but ACT still rejects OutOfRange, preserving that position
and decision RNG. A late seventh-draw failure also preserves the moved branch;
retries return the same failure, and Wait refunds ACT once to CT35. Exact four/
seven-draw budgets succeed. Move, ACT and facing each need their own revision;
later revision exhaustion never rewinds an earlier published command.

Six scenarios cover these literal outcomes, frozen-payload retention, pre-Move
admission, failed paths/RNG, post-Move range/late-draw failure, exact-state and
proposal lifetime, budgets and both-actor/order replay. They share the existing
ZOD-204 setup helpers instead of duplicating them. Attack/Wait expectations are
unchanged; current targeted support remains the bounded Throw Stone profile.

Locked restore, zero-warning/error Release rebuild, **4,067 .NET cases** across
seven projects (1,455 Framework, 867 AI), **532 Python tests** and the pinned
source/prepared-data audit pass. Logs and seven full TRX files are under ignored
`scratch/targeted-move-continuation-205/`. Owner content drafts participate in
validation and remain excluded from this commit. No original/C execution or UI
review; frame/VSync RNG equivalence remains unresolved.

Next, ZOD-206 should own fresh in-reach pair Move-first selection inside this
Framework callback and map typed Attack/Throw Stone/MoveOnly plans to frozen
proposals. ACT-first, pair post-ACT reselection, out-of-reach execution adapters
and host/editor activation remain separate. Unknown-score restrictions stay intact.
