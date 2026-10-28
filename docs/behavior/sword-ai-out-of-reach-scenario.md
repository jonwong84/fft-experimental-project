# Fresh out-of-reach sword movement scenario

ZOD-147, 2026-10-28. Implements the [ZOD-146 contract](../architecture/zod-146-ai-out-of-reach-scenario.md)
at reference **11464943bbdbd2a0af022d90cb7f10455cf911df**.
[Implementation receipt](../evidence/ai/out-of-reach-scenario-implementation-v1.json).

`SwordAiMovementScenario.EvaluateOutOfReach` explicitly selects the focused
ordinary-sword branch with d>Move+1, raw Move<=124 and the existing closed
state/source certificates. It requires `SwordAiScenarioTeam.Blue` or `NonBlue`;
Unknown and undefined values reject. Team means original masked 0x30 bits,
independent of player control, actor ID or opponent relation.

The existing in-reach entry retains its behavior. Both entries share admission,
one fresh layer/y/x movement-scoring loop and signed destination comparison.
Out-of-reach S filters R at distance<=d-1, copying R on no match. Blue adds no
draw; NonBlue draws exactly once after all S scores, halving reach when raw%7=0.
D1 uses priority within effective reach, then unrestricted distance fallback.
D0 copies D1. Actor insertion into a later Attack mask does not create a fresh
movement score for the actor.

Immutable wrapper diagnostics retain original/effective reach, team, optional
raw/modulo adjustment and D1 fallback separately from the old D0 fallback flag.
Only S has movement scores/order diagnostics. Failures restore original query
RNG and publish no partial results, including failure at the new ReachAdjustment
stage after successful scoring. No Attack, lottery, commands, live RNG commitment
or host/editor activation is added.

16 source-derived managed cases execute OOR-001..006 and relevant boundaries:
seed1's post-prefix raw17515/remainder1 still draws; seed19's raw224/remainder0
changes reach4 to2 and D0/D1 from x5 to x3. Blue skips both draws. Empty-filter
singleton copying, Move0 reach1->0, actual last-tile ties, counter/tick limits,
original-state replay and both actor identities are covered. Existing in-reach
and derived-selection regressions pass (54 focused cases total).

Locked restore, zero-warning/error Release build, **3,760 .NET / 496 Python**
tests passed. Full results: ignored `scratch/ai-out-of-reach-147-full-trx/`;
focused: `scratch/ai-out-of-reach-147-focused-trx/`; Python/source reports:
`scratch/ai-out-of-reach-147/`. No C/original execution or UI review. The owner's
current definition/refactor drafts were included in the build/tests and excluded
from this milestone commit. Derived-selector integration is a separate follow-up.
