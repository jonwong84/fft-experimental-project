# Fresh pair post-ACT movement-only selection — ZOD-214

Implemented 2026-10-30 at clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[post-ACT source design](../architecture/zod-209-pair-post-act.md) and
[fresh scenarios](attack-throw-stone-ai-post-act-scenarios.md).

`AttackThrowStoneAiPostActSelection.EvaluateInReach` and `EvaluateOutOfReach`
accept actual state, immutable pair request/configuration and query RNG. They
require explicit NoDefendSupport, own the appropriate fresh scenario query, then
use `AiRankedLottery` on eight private zero-hit slots. No public cached scenario,
ranked list or replacement plan input exists.

The closed initialized pair includes non-Frog Frog Attack already unusable;
post-ACT preparation clears both remaining actor entries after computing reach.
Thus no action scan/formula or unknown cast-priority read occurs. Fresh ranks
stay empty, but all eight original lottery slots still consume a draw. The
result is MovementFallback and an existing typed MoveOnly plan at D0=D1, with no
action, act-before-move false and deferred ChooseAtWait facing. Eight slots are
original selection behavior, not a proposed managed content capacity.

Complete diagnostics retain source masks, null score provenance, fresh preparation/
movement, reach adjustment, lottery-entry RNG, eight rolls and the hypothetical
plan. Failure returns only reasons and original whole-query RNG, including when
the final lottery draw exhausts after all scenario work. The real ACT, live state
and RNG copies are unchanged. No command, continuation, receipt or adoption is issued.
ClearDirectTrajectories is not asserted for actions that are skipped.

## Literal streams and validation

Words below are internal RNG checkpoints; counts are cumulative. Each scenario
checkpoint receives exactly eight more draws:

- Move3 Attack hit: `(2240736910,64)` → `(1223573862,72)`, plan(1,4).
- Move0 Stone hit: `(2473755763,29)` → `(3834121963,37)`, stay(1,1).
- Move0 Stone miss: `(1969681615,33)` → `(1448521415,41)`, stay(1,1).
- Move0 Stone push: `(2438815311,30)` → `(713082951,38)`, stay(1,1).
- Move3 far Stone push: `(2000392299,34)` → `(1073193443,42)`, plan(3,2).

A Move1 target pushed to(5,1) has an unwritten actor within Stone range4. The
query retains that null key and overlapping source masks, skips all casts, and
selects(2,1) at `(713082951,38)` without inventing a prior score.

Four new scenario families cover these literals, all eight failed rolls, agreement
with a separately run pure scenario query, source/branch/choice/terminal admission,
no-Defend handling, exact total budgets and exhaustion at lottery slots0/7, and
both reach branches/actor identities/reordered-input replay. Exact scenario+lottery
costs23/10/10/11/15 fit at draw-count MAX; short queries discard every private prefix.

Zero-warning/error Release builds and all **4,099 .NET cases** across seven
projects (899 AI, 1,455 Framework) pass. The batch also passed locked restore,
**534 Python tests**, pinned post-ACT source/prepared-data and unified guide
audits. Reports and two sets of seven full TRX files are under ignored
`scratch/ai-pair-post-act-213-214/`. Owner drafts and old evidence remain preserved.
No original/C execution, presentation review or full AI equivalence is claimed.

Next, ZOD-215 should own fresh pair post-ACT selection inside Framework's existing
turn-ending Move/Wait transaction and settle facing from actual placement. This
pure plan grants no authority; terminal policy, general lists and host activation
remain outside the completed scope.
