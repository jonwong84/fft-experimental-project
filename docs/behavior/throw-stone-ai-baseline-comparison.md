# Fresh Throw Stone versus move-only baseline — ZOD-189

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[ability-iteration contract](../architecture/zod-185-ai-ability-iteration.md).

`ThrowStoneAiBaselineComparison.Evaluate` owns one fresh movement-only baseline,
then one supplied [Throw Stone candidate score](throw-stone-ai-scoring.md).
Both evaluations start with original units; only private decision RNG carries
forward. Public cached outcomes cannot be supplied. The result reports a rank
comparison and hypothetical data, with no command/session authority.

The caller explicitly certifies scenario-zero destination, fresh source score
cell, normal scoring and canonical coordinates. Every assertion defaults false.
The closed candidate/score profiles still certify slot/death-counter, unchanged
human equipment, stable opponents and effect-free clock/scoring facts. Original
living movement geometry determines baseline membership before hypothetical
damage. Ability/grant/setup admission uses the Framework's read-only validator;
being out of range on the original tile is allowed because a move-first cast
can enter range. Fresh candidate evaluation checks its actual cast origin.
No equipped sword is required.

`AiMovementSimulation` factors the existing movement-only implementation behind
family-specific admission. It preserves HP/status/facing/death counters, retains
unused ACT and Move refunds, caps CT at 60, then runs the shared clock and score.
The existing public `SwordAiMovementBaseline` contracts and pre/post-ACT guard
ordering remain unchanged. Shared outcome data uses `AiMovementBaselineOutcome`;
the legacy Sword wrapper translates it into its existing outcome type.

Source `turn.c::rank_baseline_seed` seeds marker 0/hit 100 and restores the signed
priority from the negated unsigned16 score cell. `rank_compare` compares marker:u8,
priority:s16, then hit:u8. Both fresh markers here are zero. `AiCandidateRank`
compares signed priority then accuracy and also serves the existing Attack
composition gate. Above is strictly better; equal priority/hit100 is Equal,
equal priority/lower hit is Below. The later `ai_rank_is_higher` foe-proximity
tie-break does not participate in comparison with the movement baseline.
This query stops before `ai_best_rank_insert`, scan, list iteration or lottery.

Literal examples from actor PA5/HP50 and target HP50, CT15/95, ticks20:

- Stay-only baseline ends at CT55, reaches the target's upkeep after one tick,
  and scores0 with hit100. Candidate damage10 leaves HP40 and scores26 with
  hit100 or25. Baseline upkeep draws16838; candidate upkeep draws5627.
  Incoming RNG `(1,7)` becomes `(2516284547,13)`: one baseline, four formula,
  one candidate upkeep draw. The candidate starts from original CT/units.
- Target HP50000/max65535 with PA1 gives baseline and candidate priority31.
  Candidate hit100 ties; hit25 is below. Damage2 remains unweighted by accuracy.
- Zero accuracy retains one baseline plus four formula draws, ending
  `(4182499122,12)`, with no candidate score/clock/rank. A supported NotValuable
  result still contains the baseline; it does not erase the baseline's cost.
- Target HP10 becomes Dead and priority320. Candidate clock needs corpse and
  living-actor upkeep picks, totaling seven query draws, ending `(3655513600,14)`.

Rejected admission, draw exhaustion or tick exhaustion exposes only typed
failure reasons, no partial baseline/candidate payload and the original query
RNG. This atomic managed failure policy does not claim original search rewind.
Successful NotValuable and losing comparisons retain their actual draws.
These are isolated evaluation costs, not a full original decision schedule.

Six scenario tests exercise the literals, move/stay/already-moved refunds,
move-first versus ACT-first range, exact and short budgets, early/late tick
failure, source-fact admission, actor/order replay and live-state isolation.
Existing baseline/post-ACT/composition regressions cover the shared refactor.
Locked restore, zero-warning/error Release build and **4,006 .NET cases** pass,
including 819 AI cases, alongside all 519 Python tests. The source/data audit passes with prepared input hash
`287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`;
it checks the inherited receipts and 12 files/29 functions. Logs and seven full
TRX files: ignored `scratch/ai-throw-stone-comparison-189/`. Owner content drafts
participate in validation and remain excluded from this commit.

Next, ZOD-190 traces Throw Stone's origin/target pass before implementing its
scan. General ability traversal/ranking, replaceable AI interface, live ability
execution and host/editor activation remain separate. No original/C execution
or UI review was performed.
