# Roster cast priorities and isolated self entry — ZOD-291–293

Implemented 2026-11-05 from managed base
`ddf9e628f15c399b5413b6c474019f02b84b19f1`, following
[ZOD-290](../architecture/zod-290-roster-action-search.md). Read-only fft_decomp
was checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/roster-action-source-v1.json) pins 32 functions
across nine files. No original or C program was executed.

## Fresh cast-origin provenance — ZOD-291

`RosterAiCastOrigins.Evaluate` owns one fresh
[general scenario prefix](roster-ai-general-scenarios.md). Its false-default
profile certifies ordinary range-one sword geometry, without granting an actual
weapon/effect binding. Per-occurrence masks intersect scenario1 with range
diamonds around every active enemy, including other non-actor teams. Focus is
not the only ability target. Attack reports its exhaustive map-order origins
with no ranked priority reads; the self map branch reports no such origins.
Stone resolves all initial ranked keys, with current proximity and source
last-tile ties. Reporting this initial ranking does not claim every origin will
be evaluated after aim consumption.

Fresh writes override retained knowledge. Unknown ranked cells reject the whole
query, identifying occurrence and tile. Success publishes only actual score-mask
writes to the existing immutable map-owned `AiPriorityWorkspace`; failure returns
the incoming workspace instance and RNG. A reinserted actor can need a retained
priority without receiving a fresh write. Existing Forget/map-identity rules
remain; no raw priority import, loader/event replay or Framework authority is
added. Post-ACT and absent/unusable Stone entries read no ranked keys. Repeated
Stone occurrences retain separate indices and initial masks.

Literal actor (1,1), enemy (5,1), ally (1,2), Move 1 leaves actor and (2,1) in
scenario1, scoring only (2,1). Stone requires the actor key and rejects unknown.
A prior already-moved query with ally HP 25/50 writes actor priority -421.
The new query retains -421 with current proximity 6; the fresh forward key is
-485/proximity 7 and sorts first. The old workspace remains unchanged.

## Roster usefulness — ZOD-292

`RosterAccumulateAiUsefulness.Evaluate` is a separately admitted single active-mask
entry filter, without a general prefix or candidate simulation. It validates
the existing typed immediate PA effect, complete zero-bonus roster bindings,
actor grant, current PA 1..99 and explicit retail allied-stat/map-entry context.
The selected source-compatible simulation profile still admits living ordinary
compact16, stable teams and effect-free clocks. Active membership must be an
explicit distinct subset, including an explicitly empty mask when appropriate.

The private filter is shared with the existing pair implementation. It walks
source order, draws once per active ally, and classifies enemies Adverse without
a draw. Distant allies draw despite real self range zero. Inactive units do no
work. An even raw value is Useful, odd is None; PA 99 does not suppress the coin.
Late counter exhaustion rolls back the entire filter, including earlier ally
draws. The pair API retains its original two-opponent admission and stream.

Starting word 1/count 7 with actor, enemy and ally in source order gives raw
16838 and 5758 for the two allies, both Useful, ending word 2524885223/count 9.
Three active allies receive 16838,5758,10113; placing the actor last changes its
classification to None without changing the three-draw endpoint.

## Isolated fresh self occurrence — ZOD-293

`RosterAccumulateAiEntry.Evaluate` owns one fresh general prefix and selects an
actual self occurrence by index from the owned actor list. It derives the
ability ID, active mask and D1 destination. A post-ACT occurrence returns Skipped
before usefulness or executable invocation checks. Entered occurrences validate
the actual effect/grant/numeric binding and run one roster usefulness pass.
This query omits earlier/later ability evaluations; it is not a complete list's
RNG stream, ranked array or selected command.

No useful ally means no candidate attempts. If only another ally is Useful,
the map pass still attempts ACT-first and, for a different D1, Move-first. Each
self candidate is NotValuable before the hypothetical clock, consuming no
additional draw. Successful self candidates share a private PA/placement/choice/
clock kernel with the existing pair self implementation and move-only queries.
Each restores actual PA, applies PA+1/clamp99, consumes hypothetical ACT and
retains facing. Every unit contributes its source-order team-signed absolute
score after the +3 self stat-hit credit at hit 100. Equal ranks retain ACT-first;
the best candidate compares strictly with D0's fresh move-only baseline.

On the literal 8x4 in-reach map, the prefix draws five times. Starting word 1/
count 7, actor raw 5627 is odd and ally raw 23010 is even. Both candidate orders
are NotValuable and the query ends after seven draws at word 3655513600/count 14.
Seed 3 makes self Useful: both orders restore PA 5 and project PA 6, score 488,
and end at word 3746093720/count 16 after two candidate upkeep draws. PA 99
remains 99 with the same +3 credit. A Critical ally reduces the score to 353.
Two allied and two enemy healthy units give score 3, preserving all four terms.

## Validation and remaining work

Nineteen independent source descriptions and three new Python tests cover
read provenance, ally coins and isolated self entry. Twelve new managed cases
cover those literals, all-enemy masks, retained/current facts, pair parity,
binding/numeric/context admission, exact and late draw/tick failures, duplicate
occurrences, either actor and immutable replay. All old pair/variable-list
command/turn/recovery regressions pass unchanged.

Locked restore and Release build pass with zero warnings/errors. **4,355 .NET
cases** (1,483 Framework, 1,127 AI) and **557 Python tests** pass. Pinned source/
prepared-data, guide, link and coverage audits pass. The
[implementation receipt](../evidence/ai/roster-action-implementation-v1.json)
records validation; seven TRX reports/logs are ignored under
`scratch/roster-actions-290-293/`. Use the existing
[pinned Python setup](accumulate-ai.md#validation). No historical snapshot or UI
validation was repeated. All nine owner drafts, including AbilityToggles, are
unchanged; no Framework/Content/host/editor or project/lock/package changes.

ZOD-294 next traces the ordinary roster sword candidate/effect boundary. Full
Attack/Stone roster formula/target/aim traversal, list composition/ranked lottery,
roster AI commands/turns, other abilities/policies/statuses/maps, corpse lifecycle,
automatic facing, terminal routing and original frame RNG remain open. General
policy, compact16, MP0/positive Speed, raw Move <=124 and flat-ground guards stay
explicit. Owner drafts, host/editor, broad replacement interfaces and ZOD-106
remain separate/deferred.
