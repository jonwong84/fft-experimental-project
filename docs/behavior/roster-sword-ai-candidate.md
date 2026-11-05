# One ordinary roster sword candidate — ZOD-295

Implemented 2026-11-05 from managed base
`d284c92f9ccb64fb292b8269bf06acef520d08df`, following
[ZOD-294](../architecture/zod-294-roster-sword-candidate.md). Read-only fft_decomp
was checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/roster-sword-source-v1.json) pins 30 functions
across twelve files. No original or C program was executed.

## Formula, one target and whole-roster scoring

`RosterSwordAiCandidate.Evaluate` owns one supplied `SwordAiCandidate`, an actual
typed/equipped ordinary sword binding, complete source-order living simulation
inputs and explicit normal human/unchanged-equipment/zero-Golem scoring facts.
The selected target must be a different team. This is the ordinary useful enemy
slice; actual roster commands still permit friendly fire. No preparation,
general-policy admission, usefulness filter, scan or command authority is implied.

`SwordAiFormulaProjection.EvaluateRoster` shares the existing private formula core
with the old pair API. The roster formula has the Framework profile's slot21
boundary; the candidate separately enforces AI compact16 and initially living
input. Pair profiles and APIs retain their two-unit guards. Legal destinations
come from the initial living roster before a possible KO. Ally transit and enemy
blockers remain owned by movement; a corpse does not create a new endpoint.
Byte raw Move remains capped to 124 by the shared movement geometry; this isolated
entry does not assert the general prefix's nonwrapping reach calculation.

Move-first formula origin is the destination; ACT-first uses the original tile.
Target facing is settled input; actor facing is never rotated by a hypothetical
path. The typed weapon ID must match the actor and the existing execution-safe
signed-16 envelope is checked before drawing. Neutral PA zero floors to XA one;
PA255/power64 fits, power65 rejects. Four real evade-source draws remain even for
zero/certain evade. AI midpoint critical/proc steps add no draws or displacement.
Positive accuracy applies full capped normal damage, including at hit50. Zero
power still counts as a positive-accuracy hit; zero accuracy returns NotValuable
after four draws without clock/scoring. Late failures restore incoming RNG.

The private `AiCandidateState` now has an explicit changed-target overload. Old
pair callers delegate through their sole non-actor; roster callers name the
selected target. Only that unit's HP/status/death counter changes. Other allies
and enemies retain their own HP/maxHP/status/counter. Hypothetical ACT is consumed;
unused Move refunds20 before cap60. The shared clock and score kernels still own
ready ordering, corpse upkeep, all-unit signs and per-unit signed16 narrowing.
Ordinary useful enemy strike credit is zero. No cached public outcome is accepted
as executable input or a trusted score.

## Source literals and corpse clocks

Literal actor HP50, PA5, enemy HP50 and ally HP31/max50 with power2 projects only
the enemy to HP40. The three terms yield score462, not a repeated target HP on
the ally. Hit50 applies the same damage10 and score462. Power0 leaves target HP50
and score436. Target HP15 becomes HP5/Critical and score584. A different-team
actor scores the other enemy peers with the correct negative signs.

A lethal hit on target HP5 sets Dead/counter3. With target CT95, its corpse gets
the first upkeep after one tick; the actor's movement opportunity follows after
13 ticks total. Score is756 and formula4 plus upkeep2 draws end at word2516284547/
count13 from word1/count7. If a living peer has an earlier equal-ready source slot,
it can end the simulation before the corpse gets upkeep. Counter3 never decays
in AI mode, and live KO count is not incremented or published.

The extreme actor/ally Speed1, target Speed255/CT100 case gives 101 corpse upkeep
picks before the living peer, 100 elapsed ticks, and 102 total upkeep draws.
With four formula draws, word1/count7 ends at word446673791/count113. A failure
after a corpse draw still discards the whole candidate. Already-ready corpse
then living peers need no tick and can succeed even at the maximum tick counter.

A separate actual-command regression starts at seed57: AI projects damage10
without displacement, while the actual shared roster sword command gets critical
damage18 and knockback. Both return new results; the original live state/RNG is
unchanged. Actual and hypothetical streams are deliberately distinct.

## Validation and remaining work

Thirteen independently authored source descriptions and three Python tests
cover formula/order/width, peer HP, Critical/KO, ready ties, corpse clocks and
scores. Eight new managed cases also cover pair parity, old guards, numeric/
weapon/target/choice admission, compact16 versus uncapped storage, exact/late
draw and tick budgets, either actor, collection reordering and immutable replay.

Locked restore and Release build pass with zero warnings/errors. **4,363 .NET
cases** (1,483 Framework, 1,135 AI) and **560 Python tests** pass. Pinned source/
prepared-data, guide, link and coverage audits pass. The
[implementation receipt](../evidence/ai/roster-sword-implementation-v1.json)
records checks; seven TRX reports/logs are ignored under
`scratch/roster-sword-294-295/`. Use the existing
[pinned Python setup](accumulate-ai.md#validation). No historical snapshot or
manual UI validation was repeated. All nine owner drafts, including AbilityToggles,
are unchanged; no Framework/Content/host/editor or project/lock/package changes.

The [ZOD-296 follow-up](roster-sword-ai-attack.md) now connects ordinary roster
Attack enumeration to one fresh general prefix through this candidate owner,
preserving source exhaustive map/aim order and unread priority keys. Stone search, full-list ranking/lottery, roster AI
commands/turns, other abilities/policies/statuses/maps, full corpse lifecycle,
automatic facing, terminal routing and original frame RNG remain open. Owner
drafts, host/editor, broad replacement interfaces and ZOD-106 stay deferred.
