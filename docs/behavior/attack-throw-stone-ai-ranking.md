# Shared-prefix two-entry ranking — ZOD-193

Implemented 2026-10-29 against clean fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[ZOD-192 design and source receipt](../architecture/zod-192-two-entry-ai.md).

`AttackThrowStoneAiComposition.Evaluate` admits both existing profiles, then owns
one movement prefix, one Attack scan and one Throw Stone scan in that order.
The input supplies original masks/D0/D1, the Throw Stone request, bounded sword
and source certificates. Active target context, ground and baseline unit/death
counters must match. Weapon/grant/profile limitations still apply, including
for inactive scans. Order and fresh empty ranks require explicit assertions.
No original list or prepared usage is inferred from a skill/asset ID.

`AiMovementPrefix` owns map-order fresh scores and one `AiProximityCache` derived
from original turns/positions. Its shared diagnostic record is now named
`AiScoredMovementTile` instead of the Throw Stone-specific name. Public Throw
Stone scan callers use that neutral data type; serialized fields remain the
same. Internal scan continuations consume only privately owned fresh prefix/
cache data. Existing public scan APIs still own fresh work; their admission,
formula, score, RNG and result semantics remain unchanged. No cached-result
entry point or new command/receipt access is exposed.

The composition carries actual RNG from movement through every Attack candidate
and the sole evaluated Throw Stone origin. Both families start from original
units. D0 is read without a second simulation. Best is local to each entry;
earlier ranks survive a later empty/NotValuable pass. Eight compatibility slots
hold up to two real actions with no identity/destination deduplication. Admission
must beat baseline priority/hit strictly; insertion then compares priority, hit,
and lower final-destination proximity. A complete tie retains the earlier action
first and admits the later action into the next empty slot.

`AttackThrowStoneAiAction` has two closed typed payloads: `AttackThrowStoneAiAttack`
and `AttackThrowStoneAiStone`. Each carries its family's complete hypothetical
candidate diagnostics. These outputs grant no executable authority and cannot
be supplied as trusted inputs to planning. No general AI replacement interface
or arbitrary ability-list traversal is introduced.

Literal case: actor(1,1), target(2,1), HP50, PA5, CT15/95, ticks20, mask={(0,1),
(1,1)}, D0=D1=(1,1), sword power2. Prefix draws16838/5758; Attack then ends at
RNG `(3655513600,14)`, Throw Stone at `(836760821,19)` from initial `(1,7)`.
The total is exactly 2+5+5 draws. Both project target HP40 independently, scoring26.
Throw Stone at(0,1) has proximity8 and inserts before Attack's proximity9.
Sword power1 scores13, power4 scores52. A single actor-origin mask gives equal
priority/accuracy/proximity and keeps Attack then Throw Stone without deduplication.

Six scenario tests cover those literals, stronger entries/ties, NotValuable,
inactive scans, exact normal/KO budgets, prefix/first/second-entry failures,
profile mismatch and actor/mask/order replay. All 832 AI cases, locked restore
and a zero-warning/error Release build pass. All 524 Python checks/source audit
passed at ZOD-192; last full solution test count is ZOD-191's 4,013. Logs:
ignored `scratch/ai-two-entry-192-194/`. Owner drafts participate in the build
and remain excluded from these commits. No original/C execution or UI review.

ZOD-194 adds fresh terminal-pair selection with shared lottery arithmetic.
Scenario derivation, wider ability lists, blocked trajectories, live authority,
alternate-AI interfaces and host/editor activation remain separate work.
