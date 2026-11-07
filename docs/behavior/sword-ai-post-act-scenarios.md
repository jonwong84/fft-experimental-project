# Fresh post-ACT movement scenarios — ZOD-181

Source: clean read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`,
`turn.c::scenarios_build`, `reachable_tiles_score` and the inherited
[ACT-first contract](../architecture/zod-176-ai-act-first.md). Implemented 2026-10-29.

`SwordAiPostActScenarios.EvaluateInReach` and `EvaluateOutOfReach` own one fresh
post-ACT preparation, then score the restored actual snapshot in layer/y/x mask
order. Inputs retain the ordinary list/cache/team/geometry/score certificates,
plus normal-scoring, canonical-coordinate and ordinary-scenario-policy assertions.
The wrapper derives focus/reach/cache facts from its privately owned preparation;
it accepts no cached preparation, mask, destination or scored outcome. No Attack
scan or command authority is introduced. NonBlue remains the admitted prepared team.

The in-reach mask uses target distance <= raw Move + original target distance;
out-of-reach uses distance <= original distance - 1. A no-match filter copies
the original reachable mask. Each scored tile consumes one clock draw after
the refresh draw, using the actual ACT-used baseline and unused-Move-only refund.
Out-of-reach NonBlue adjustment draws once **after** all cells, halving reach
when raw modulo7 is zero. Existing priority/distance/reverse-coordinate ordering
selects D1. With ACT already used, D0 always copies D1; the pre-ACT stricter D0
chooser is skipped. Legacy scenario entries retain their ACT-unused admission.

For actual hit/miss actor x2, opponent x1, Move1, the four cells are (2,0),
(2,1), (3,1), (2,2). D1=D0=(2,2), baseline index3. Cell priorities are64 after
the actual hit and0 after the miss. The owned refresh plus four cells finishes
at word2473755763/count29 or word1116175964/count34, respectively. Critical
preserves priority135; actual knockback frees the former target tile and changes
the restored mask. No hypothetical clock state becomes the live baseline.

Five scenario tests cover those literals, source order, both reach-adjustment
branches, zero Move/filter fallback, exact budgets, late clock/adjustment failure,
legacy/wrong-branch/profile admission, actor/order replay and input isolation.
Failures return the incoming whole-query RNG and no partial preparation or cells.
All 27 focused old/new scenario cases pass; full AI results and logs are in ignored
`scratch/ai-post-act-181-183/`. No original/C execution or UI review. Shared
clock/scoring matrices are reused. ZOD-182 adds the empty-ranking lottery/fallback;
live command publication and settled facing remain separate.
