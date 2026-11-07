# Fresh prepared Throw Stone scenario scan — ZOD-191

Implemented 2026-10-29 from the
[ZOD-190 source contract](../architecture/zod-190-throw-stone-ai-scan.md), pinned
to clean read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.

`ThrowStoneAiCandidateScan.Evaluate` owns a supplied movement-scoring prefix
and one prepared Throw Stone scenario. The copied scenario includes scored
origins, scenario origins, baseline D0 and ACT-first destination D1. D0/D1 must
have fresh score cells; every scenario origin must belong to the scored mask
and original living `AiOrigins`. A nonempty scenario must contain D1. Duplicate,
unreachable, occupied or missing-cell masks reject before simulation.

The query scores each movement tile in layer/y/x order on original units,
carrying private RNG. Source priority is the signed16 negative of that tile's
absolute score. Proximity derives from original positions and fresh turn delays:
the ordinary enemy contributes max(0,10-distance) when it acts no later than the
actor. It is independent of active target selection. Only original turn/cache
state determines that value, never a projected upkeep result.

For the active enemy, intersect scenario origins with range4, then take the
lowest signed priority×65536+proximity key, preferring later layer/y/x on ties.
The closed effect-free movement scores are equal across positions; the complete
key still follows source arithmetic. ACT-first substitutes D1 only after choosing
the original actor tile; its origin key is not replaced by D1's key. Other origins
are Move-first. CT0 does not exclude a ready enemy from this instant ability.

`ThrowStoneAiBaselineComparison` now shares pure admission and an internal fresh
continuation. The scan uses D0's privately produced outcome and prefix RNG to own
one fresh candidate without rescoring D0 or the other movement cells. Its public
single-candidate API retains its original guard order/results. No public query
accepts cached projected outcomes or caller-populated score/proximity records.

The sole aim is consumed after Scored or NotValuable. Zero accuracy keeps four
formula draws, no candidate clock/score/rank, and never retries another angle.
Inactive, empty or entirely out-of-range scenarios return NoCandidates with all
prefix draws retained. A scored candidate reports Above/Equal/Below against D0;
the query does not insert it into a ranked list or choose an executable action.

Caller assertions remain explicit: original scoring/scenario masks, fresh
decision and best-reset context, prepared usable Throw Stone entry, no intervening
random work and clear direct trajectories. The baseline's four provenance/
normal-scoring/coordinate assertions and existing score/clock/grant certificates
still apply. Clear trajectories include original sprite collision facts absent
from managed geometry. An ability ID or Manhattan distance alone proves neither
those paths nor original eligibility. Obstructed-path traversal is rejected by
the absent clear-path certificate; this child does not implement that traversal.

Literal default example: actor(1,1), target(4,1), PA5, both HP50, CT15/95, ticks20,
scored/scenario mask={(1,0),(1,1),(2,1)}, D0=D1=(2,1). The prefix visits map order,
draws16838/5758/10113 and yields priority0, proximity6/7/8. It chooses Move-first
at(1,0), damage10/priority26/Above. Four formula draws precede upkeep7419; incoming
RNG `(1,7)` ends `(2633739833,15)`, exactly eight draws. KO HP10 needs nine draws,
ending `(3210001534,16)`; zero accuracy needs seven, ending `(3655513600,14)`.
Each projection still starts with original units and clocks.

Seven scenario tests cover literal prefix/RNG ordering, ACT-first substitution,
later-origin ties, readiness, range4/5, inactive/empty scans, no retry at zero
accuracy despite an available nonfront hit, exact/short draw budgets, early/late
clock failure, KO, admission and actor/mask/collection replay. Existing formula,
clock, score and comparison regressions remain in use. Failure publishes typed
reasons only, no partial prefix/candidate/cache, and original whole-query RNG.
This managed atomic-failure policy does not claim original C search rewind.

Locked restore, zero-warning/error Release build, **4,013 .NET cases** across
seven projects (826 AI), **522 Python tests** and the clean source/prepared-data
audit pass. Logs, `source-191.json` and seven full TRX files are in ignored
`scratch/ai-throw-stone-scan-190-191/`. Python validates source descriptions;
.NET executes the managed implementation. Owner drafts participate in validation
and remain excluded from these commits. No original/C execution or UI review.

ZOD-192 designs shared-prefix Attack/Throw Stone ranking before implementation.
Scenario derivation, broader entry lists, ranked insertion/lottery, alternate-AI
interfaces, live Throw Stone AI authority and host/editor activation remain
separate work. This isolated prefix/entry query is not a full retail decision
or frame/VSync-equivalent RNG schedule.
