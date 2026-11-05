# Roster targeting queries — ZOD-272–276

Implemented 2026-11-04 from managed base `95e25fe97dd9a1025d8b36aa1cd8596da92b79ca`,
following [ZOD-271](../architecture/zod-271-roster-targeting.md). Read-only
fft_decomp was checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/roster-targeting-source-v1.json) pins
25 functions across seven files. The
[implementation receipt](../evidence/ai/roster-targeting-implementation-v1.json)
records managed validation. No original or C program was executed.

## Owned facts and membership — ZOD-272

`AiTargetRoster` copies units into unique source-order keys, with an explicit
actor and cached actor-delay byte. Stable IDs are separate from source slot
numbers and scheduling order. `AiRosterUnit` owns immutable live facts, cached
decision facts, byte coordinates, resolved turn delay and an explicitly resolved
disabled-through-actor-delay fact. It represents query inputs, never a second
authoritative battle state. There is no fixed managed roster capacity.

`AiRosterEligibility` keeps live untargetability separate from cached out-of-play.
It preserves absent/inert/mount exclusions and the Transparent enemy/rider
exception. A compatibility adapter must resolve the source's four-bit mount
partner lookup; this query never masks stable IDs. Team classification compares
the actor's current semantic team with the target's initial semantic team and
always clears Enemy for the actor. Ordinary delay arithmetic rounds up and returns
INT_MAX for Speed0. Full status/inert/dead-cache delay rules remain unresolved
inputs rather than a new status simulator.

## Eligible-enemy nearest search — ZOD-273

`AiRosterNearest.FindEligibleEnemy` copies explicit working targetable membership
and rejects unknown IDs. It traverses the owned source order and uses strict
Manhattan X/Y improvement, ignoring layer. Equal distance retains the first
source slot even if another stable ID sorts earlier. No eligible candidate returns
the actor with a null distance, explicitly distinguished from a chosen candidate.

The source ELIGIBLE_ENEMY filter uses live untargetability, Petrify, cached Enemy,
DeadWithoutReraise, working membership, Considered and BloodSuck. It deliberately
does not apply generic Dead exclusion: an otherwise eligible dead-with-Reraise
record can win. Cached Untargetable is not substituted for the live helper.
Other nearest modes, including healer/crystal and imminent-height checks, remain
unimplemented. This method cannot stand in for those policies.

## Active ability targets — ZOD-274

`AiRosterActiveTargets.Select` returns a complete newly rebuilt flag map plus
selected units in source order. Cached Untargetable always excludes; Confusion
can bypass only Considered. Any, enemy, ally, specific and status modes preserve
the source signed dispatch: unknown positive/negative enum values behave as Any.
Status mode includes Dead/Petrify/Critical/DeathSentence without a side filter.
Semantic `excludeActor` carries the source target-ID255 behavior without reserving
a managed ID. Specific-target selection cannot bypass that exclusion or cached
untargetability; a missing specific ID produces an empty mask.

This stage does not rerun live/out-of-play filters or per-ability usefulness.
An explicitly supplied stale cached record can therefore differ from live facts;
silently sanitizing those differences would change the source contract.

## Proximity writes — ZOD-275

`AiRosterProximity.Evaluate` rebuilds imminent-enemy membership from cached
out-of-play, Enemy, resolved delay <= cached actor byte and resolved disabling
status at that byte. Neither Considered nor the current active/focus mask replaces
this membership. The actor byte is captured before refresh: full resolved
delay355 stores99; targets at99 qualify and targets at100 do not.

Every scenario0 tile receives the u8 sum of max(10-ManhattanDistance,0) over
eligible enemies. All layers share the same height-independent distance rule.
Results contain only writes; cells outside the mask retain their previous values
or remain unknown. Empty masks write nothing. A literal three-enemy example sums
22 at(1,1),20 at(5,1), and zero far away, including an unconsidered enemy that
would not pass active/nearest selection. Distances9/10/11 contribute1/0/0.

Original source slot extent21 and compact extent16 are recorded independently.
A 20-enemy arithmetic case across the source slot extent sums81 on both layers;
it is not a claim that the original command owner admits21 present compact
records. A separate32-unit managed capacity probe contributes31 and makes no
original-battle equivalence claim. The old26-enemy wrap example remains excluded.

## Existing pair consumers — ZOD-276

The internal `AiOrdinaryPairRoster` projects only already-admitted ordinary
opponent state. Existing candidate certificates establish original source order
in TieKey, stable opposing roles, no timed/control/mount effects and valid live
CT. Existing preparation and movement proximity now consume the shared roster
owners. Focus and active membership are derived through their respective queries;
movement cache and origin proximity use the same proximity arithmetic.

Restored live CT supplies delays, rather than hypothetical refresh CT. For both
actors and moved/unmoved/post-ACT modes, a literal CT15/Speed5 actor and
CT95/Speed5 target retain delays17/1 and origin proximity9. The one refresh draw
ends word1103527590/count8; hypothetical actor CT40 or60 and targetCT0 do not
replace those live delays. No added clock, RNG draw, rescoring or change to
selection/command/recovery APIs occurs.

Framework's two-unit sword/movement/targeted command admission and AI's existing
profile guards remain intact. A three-unit read-only roster can be queried while
the same wider battle is still rejected by preparation, movement and sword
command setup. There is no public cached executable plan, new production friend
access, runtime source reference, Content dependency or host activation.

## Validation and remaining work

Seventeen literal source descriptions cover live/cache disagreement, mount and
Transparent exclusions, source ties, dead/Reraise eligibility, actor fallback,
all active modes and signed defaults, proximity sum/mask/radius and cached-byte
timing. The offline verifier checks these independently and fingerprints the
pinned source. Managed tests consume the same authored descriptions; neither
the checker nor the fixture runs original/C code or claims full status simulation.

Eleven new managed cases also cover ownership/replay, invalid facts, wider semantic
capacity, current/initial teams, delay rounding, source-order ties, complete-mask
rebuild, cross-stage membership differences, pair entry modes and preserved
broader-roster admission guards. The prior pair/variable-list actual-turn and
recovery regressions retain their literal streams and results.

Locked restore and Release build pass with zero warnings/errors;
**4,309 .NET cases** (1,467 Framework,1,097 AI) and **545 Python tests** pass.
Pinned roster/prepared-data, algorithm-guide, local-link and coverage audits pass.
Seven full TRX reports and other logs are under ignored
`scratch/roster-targeting-271-276/`. Python uses the
[existing pinned Pillow setup](accumulate-ai.md#validation). Historical checkpoint
snapshots and UI were not rerun; all nine owner drafts remain unchanged.

This completes a shared targeting foundation, not full-roster combat/search.
The [ZOD-277–282 follow-up](flat-roster-combat.md) now implements source-bounded
actual roster movement, sword and explicit-facing command prerequisites. Existing
AI drivers retain their two-opponent limits and this document's validation is historical. Full roster refresh/team/ability
preparation, hypothetical combat scoring, per-ability target enumeration, other
nearest policies, statuses/maps, terminal routing and original frame RNG remain
open. Owner AbilityToggles consolidation, editor work, broad replacement
interfaces, host activation and ZOD-106 remain separate/deferred.
