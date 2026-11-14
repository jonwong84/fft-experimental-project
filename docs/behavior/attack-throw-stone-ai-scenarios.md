# Fresh in-reach pair scenarios — ZOD-197

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[ZOD-195 preparation/scenario trace](../architecture/zod-195-pair-preparation.md).

`AttackThrowStoneAiMovementScenario.Evaluate` owns one fresh both-unused NonBlue
pair preparation and one movement-score/proximity prefix. Normal scoring,
canonical coordinates and no other scenario overrides remain explicit assertions.
The last excludes stay-near, reflection, crystal and special scenario behavior
while allowing the freshly derived ranged flag. It does not reuse the old
Attack-only `OrdinaryScenarioPolicy`, whose exclusions would be false for this pair.
An out-of-reach request rejects with no partial diagnostics and original query
RNG; no NonBlue reach-halving draw belongs to this in-reach entry.

Source `scenarios_build` reads restored-state target distance, compares minimum
enemy reach Move+1, and filters scenario1 by Move+distance. In this flat ordinary
profile the triangle bound contains every reachable tile, so the scored mask and
scenario1 both retain all ordinary origins. Source distance rows come from the
existing pure movement query. Adding the actor after D0/D1 selection leaves this
mask unchanged. No caller supplies a scored mask or destination.

The shared prefix scores each tile once in layer/y/x order and derives proximity
from original CT/positions. `AiMovementPrefix` can now defer its internal baseline
index until a destination is known; public queries do not accept a provisional
or cached baseline. D1 orders signed negative score/proximity key, then target
distance, then later layer/y/x. D0 uses the same priority-first order inside
`max(3,distance-1)`, as the source's ranged enemy branch requires. If none qualifies,
the source distance-first fallback is retained. This raises an upper distance
bound; it does not force standing exactly three tiles away. The final D0 cell
becomes the private baseline without another score/draw.

An internal fresh result retains its private prefix for a later owning composition.
The public result contains immutable preparation/scenario/movement diagnostics
and RNG only. No public cached-result input, action scan, rank, lottery, command
receipt, live choice or RNG adoption is added. Failures restore whole-query RNG
and expose reasons only, including after refresh or a partial prefix. This is
managed atomic-failure policy rather than original C rewind.

Literal actor(1,1), target(2,1), Move3, CT15/95, Speed5, HP50: refresh costs one
draw, then14 original-state movement cells each draw once. D0=(1,3), D1=(1,4),
bound3; incoming `(1,7)` ends `(2111915288,22)`. The plain-sword query retains
D0=(2,2) through its closer-distance fallback. Moving the target to(4,1) gives
16 cells, D0=(2,2), D1=(1,4), final `(3791773014,24)`. With target CT0, all
proximity keys are zero; distance and later-coordinate ties give D0=D1=(2,2).
Move0 retains the actor tile; Move4/target(6,1) gives bound4, D0=(3,2), D1=(1,5).

Six scenario tests cover those literals, source draw costs, legacy policy,
original-delay ordering, exact/short budgets, late draw/tick failure, independently
restored ticks, branch/source admission and both-actor/policy-order replay.
Locked restore, zero-warning/error Release build and all 849 AI cases pass.
The clean source/prepared audit passes; last full runtime/Python validation is
ZOD-196's 4,030 .NET/526 Python cases. Logs/report: ignored
`scratch/ai-pair-scenarios-197-198/`, including `source-197.json`.
Owner content drafts are included in the build and excluded from this commit.
No original/C execution or UI review; frame/VSync stream equivalence stays open.

ZOD-198 composes this privately owned prefix/preparation with pair ranking and
selection without rescoring. Out-of-reach derivation, general lists/statuses,
blocked trajectories, live authority, replacement interfaces and presentation
activation remain separate work.
