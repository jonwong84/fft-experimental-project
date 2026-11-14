# Pure settled ordinary AI Wait-facing query

ZOD-151, 2026-10-28. Implements the [ZOD-150 contract](../architecture/zod-150-ai-wait-facing.md)
at reference **11464943bbdbd2a0af022d90cb7f10455cf911df**.
[Implementation receipt](../evidence/ai/wait-facing-implementation-v1.json).

`SwordAiWaitFacing.Evaluate` takes settled battle state, distinct actor/target IDs,
ordinary movement profile, false-default OriginalAiChooseAtWaitContext and
EligibleOpponent facts, plus explicit query RNG. It requires the current active
UNIT_TURN actor and two living ordinary flat opponents. Either movement or ACT
may already be used. It does not accept a selected plan, cached facing or pending
completion as authority.

The source's exact cardinal/diagonal branches return semantic BattleFacing names;
there is no raw-ordinal cast. The scalar calculation is shared with the existing
`FacingTurnBattle` closed-enemy choice, preserving its older behavior and avoiding
a second copy of the rule. Clear flat rectangle admission proves the chosen
neighbor viable, even when occupied by the adjacent enemy. No movement-destination
filter or invented cached-delay diagnostic is used.

Accepted results contain actual actor/target positions and requested facing.
The query does not apply facing, consume a receipt/revision, end the turn, advance
ticks or draw RNG. All results retain the explicit input stream, including MAX.
Unknown context/IDs, wrong phase/actor, dead target, unsupported terrain and
overlap reject with no partial outcome. No-enemy/overlap stack-byte behavior is
excluded rather than repaired.

21 focused cases cover eight source directions plus unequal axes, narrow-map
occupied neighbors, an actual settled Move, already-used ACT, target timings,
context/state admission and both-identity/order replay. Existing facing-session
and all other .NET regressions pass after sharing the direction helper.

Locked restore, zero-warning/error Release build, **3,808 .NET / 496 Python**
tests passed. The full run includes the owner's pending content refactor/tests
(3,787 local cases before this milestone); none of those files is committed here.
Results: ignored `scratch/ai-wait-facing-151-full-trx/`; focused:
`scratch/ai-wait-facing-151-focused-trx/`; Python/source: `scratch/ai-wait-facing-151/`.
No C/original execution or UI review. Applying facing through the existing
active-Wait/completion-receipt boundary is a separate follow-up; native activation
and full AI remain open.
