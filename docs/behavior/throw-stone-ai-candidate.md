# Pure Throw Stone candidate projection — ZOD-187

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`. Reuses the
[ZOD-185 simulation contract](../architecture/zod-185-ai-ability-iteration.md)
and [fresh formula projection](throw-stone-ai-formula.md).

`ThrowStoneAiCandidateProjection.Evaluate` accepts immutable live state, a typed
actor/ability/target/destination with boolean ActBeforeMove, matching ability/
movement/slot-death-counter certificates and explicit hypothetical RNG. It
certifies reachable destinations on the original living baseline before any KO.
The formula origin is the original actor tile for ACT-before-Move or the supplied
destination for Move-before-ACT. A private snapshot preserves original facing and
choice flags; no real Move/ACT command is invoked.

Zero accuracy returns NotValuable after four formula draws, without running the
clock. Otherwise ACT is consumed hypothetically; only unused Move refunds20 CT,
capped at60. Current full midpoint damage and resulting HP/Critical/Dead feed
the shared effect-free clock. No Throw Stone AI knockback occurs. A corpse uses
upkeep RNG/modulo but receives no turn or death-counter decay. Input state and
live RNG remain untouched. Any failure rolls back the whole query's hypothetical
RNG and hides partial formula/unit/clock output; formula failures retain a typed
reason, including missing learned grant.

The second real consumer extracts neutral `AiUnitBaseline`, `AiProjectedUnit`,
`AiUpkeepPick`, internal `AiCandidateState` and `AiSimulationClock`. Repository
callers use those shared diagnostic type names. Existing Attack-specific entry
points remain scoped to swords; their state preparation, clock arithmetic and
RNG behavior are unchanged. No second clock/KO implementation or general plugin
interface is introduced.

From word1/count7, ordinary stay/move consumes four formula draws then upkeep,
ending word4182499122/count12/raw31051. Live CT15/opponent95 gives turn-end CT35
for stay or15 for move; one tick selects the opponent. A lethal HP10 projection
consumes corpse then actor upkeep, ending word2516284547/count13 after13 ticks;
the corpse keeps death counter3 and never moves. Candidate ordering changes
the actual formula origin/range/evasion, not the final requested destination.

Five scenario tests cover those literals, both action orders, zero accuracy, KO,
exact/late exhaustion, baseline occupancy, grants, choices and actor/order replay.
All 809 AI cases pass after a clean Release build, including unchanged Attack
regressions. Logs/TRX: ignored `scratch/ai-throw-stone-186-188/`. No original/C
execution, live authority, scoring, candidate enumeration or host/UI activation.
ZOD-188 adds one fresh absolute score using shared source arithmetic.
