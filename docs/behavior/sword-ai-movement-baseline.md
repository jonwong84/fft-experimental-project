# Movement-only AI baseline

ZOD-136 implements a fresh hypothetical move-only evaluation for one supplied,
certified scenario-0 baseline destination. It preserves unused-ACT CT refunds,
advances the closed clock, and returns the absolute allied-minus-enemy score.
Source: read-only fft_decomp **11464943bbdbd2a0af022d90cb7f10455cf911df**;
[ZOD-135 contract](../architecture/zod-135-ai-rank-admission.md) and
[implementation evidence](../evidence/ai/movement-baseline-implementation-v1.json).

## Calling and interpreting the query

[`SwordAiMovementBaseline.Evaluate`](../../src/Zodiac.Framework/SwordAiMovementBaseline.cs)
takes original `AttackBattleSnapshot`, a request with actor/target IDs and destination,
existing candidate/score certificates, new baseline certificates, and explicit
decision RNG. No weapon or cached outcome is an input. For example, after obtaining
the original state and certifying its unrepresented source facts:

```csharp
var result = SwordAiMovementBaseline.Evaluate(state,
    new(actorId, targetId, baselineDestination), candidateProfile, scoreProfile,
    new(OriginalScenarioZeroDestination: true, FreshSourceScoreCell: true,
        NormalScoring: true, CanonicalCoordinates: true), decisionRng);
```

All four new assertions default false. The caller must know that this is the
original scenario-0 destination, its source score cell was freshly written in
the same context, normal scoring is enabled and coordinate padding is canonical.
Reachability/asset IDs alone cannot prove those facts. The destination need not
be the Attack scan's scenario-1 destination. Unvisited/stale source grid cells
are not reconstructed by this query.

Existing certificates constrain this to two living, positive-speed ordinary
opponents on open flat dry ground, no pickups/charges/upkeep/status effects,
unchanged seven-slot equipment, zero MP/Golem and original slot order in TieKey.
Admission checks the original active, unused action and original `AiOrigins`;
an already-moved actor can stay but cannot choose a new destination. Numeric
movement/geometry limits remain those of the existing closed query.

On success, the immutable outcome contains destination, turn-end CT, units,
first move-opportunity unit, elapsed/total ticks, upkeep pick and score contributions.
HP/status/facing/death counters are preserved. ACT stays unused; a changed tile
consumes Move. Each unused choice refunds 20 CT, with the result capped at 60.
The first ready unit uses highest CT then original slot order, gets CT modulo
100, and has its movement/action flags cleared. It is hypothetical data, not an
active turn or executable command.

Every successful evaluation consumes **one upkeep draw and zero formula draws**.
It returns the progressed decision stream; original snapshots/live RNG stay
unchanged. An already-ready opponent needs no tick addition, including at MAX.
Unsupported inputs or RNG/tick exhaustion return a typed rejection, original
decision RNG and no partial outcome. Programmer null arguments throw.

Score is absolute, with hit=100: healthy equal opponents score zero; a Critical
10/100 actor versus a healthy 100/100 target scores -148. Closed final range is
-160..160. The source's u16 negation/storage and s16 restoration preserve these
values, so no packed grid is exposed or allocated. The shared internal scorer
retains signed narrowing after each source-ordered unit.

## Implementation and validation

Attack and movement-only evaluations share an internal
[`AiSimulationClock`](../../src/Zodiac.Framework.Ai/AiSimulationClock.cs)
and fresh-unit scoring routine. Their action preparation remains distinct: no
fake zero-damage Attack is used. Neither routine accepts public cached outcomes
as trusted input. Existing candidate APIs retain their behavior and RNG schedule.

The [25 baseline cases](../../tests/Zodiac.Framework.Tests/SwordAiMovementBaselineTests.cs)
cover refunds/cap, a real changed readiness order relative to Attack, slot ties,
score zero/negative/extrema, HP/status/facing preservation, replay/collection order,
source certificates, invalid destinations, admission and MAX RNG/tick rollback.
Full validation: locked restore, zero-warning/error Release build,
**3,633 .NET cases / 485 Python tests**. All previous .NET cases passed too.
The pinned source receipt verifies four files / 21 functions. Reports/TRX/logs
are ignored under `scratch/ai-baseline-136-*`; no C/original execution or UI check.

Use the standard [build/test commands](../development/test-maintenance.md#commands).
For a focused run after building, filter `FullyQualifiedName~SwordAiMovementBaselineTests`.
Source audit: `python tools/verification/verify_ai_rank_admission_contract.py --reference
C:/repositories/fft_decomp --output <new scratch report>`.

This query does not reproduce full scenario-scoring RNG costs. In the original,
all visited movement tiles are scored before Attack scans; seeding the baseline
from the grid adds no draw. [ZOD-138 composition](sword-ai-composition.md) owns this
distinction with an internal fresh movement-tile evaluator and a cached D0 cell;
it does not call this public baseline query again after scoring the movement mask.
Scenario generation, general ranked search/lottery, replaceable decision interface,
authoritative commands and runtime activation remain separate work.
