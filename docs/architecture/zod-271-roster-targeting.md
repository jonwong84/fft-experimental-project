# Roster focus and target-search foundation — ZOD-271

Source/design, 2026-11-04, managed base `95e25fe97dd9a1025d8b36aa1cd8596da92b79ca`.
Read-only fft_decomp checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/roster-targeting-source-v1.json) pins the symbols,
files and arithmetic widths behind this study. No original or C execution.

## Three separate memberships

`decision_flags_refresh` calls live `ai_target_is_untargetable` before rebuilding
enemy classification. An excluded record only gains cached Untargetable; other
cached fields remain untouched. For admitted units, Enemy compares the actor's
**current** team with the target's **initial** team, except the actor is never
an enemy. Initial/current teams must not be conflated. Refresh preserves ConserveCT
and TeamSupportUnavailable, rebuilds HP/MP/dead facts, and initially considers
allies and charging enemies. A skillset-zero hypothetical clock then updates
transient status flags and may set Considered for eligible simulated survivors.
`ai_action_simulate_and_score` restores baseline units before returning; its
flags and RNG survive. Neither the simulated next-turn position nor its CT
becomes the live input to focus or proximity.

Live untargetability excludes absent units, Crystal/Treasure, mounts, and
Transparent cached enemies unless a rider's mount is also Transparent. The
rider partner lookup uses **four bits**, unlike other five-bit mount consumers.
Resolve that lookup in an original adapter; never mask a managed stable ID.
Cached `target_is_out_of_play` instead uses cached Untargetable,
DeadWithoutReraise and current Petrify. It does not call the live helper. Preserve
this distinction even when flags disagree because refresh retained earlier data.

`target_active_units_select` clears every active flag, then excludes cached
Untargetable and unconsidered units unless Confusion bypasses Considered. It
does not rerun out-of-play/live eligibility. The source target ID255 excludes
the actor before mode dispatch. Modes ANY0, ENEMIES1, ALLIES2, SPECIFIC3 and
STATUS4 have signed dispatch; unknown positive or negative modes behave as ANY.
STATUS admits Dead, Petrify, Critical or DeathSentence, irrespective of side.
SPECIFIC and Confusion still cannot bypass cached Untargetable. Translate the
255 sentinel into a separate semantic exclude-actor input, not a reserved ID.

## Focus search and later target traversal

`decision_target_consideration_init` preserves direct-controlled Blue or applicable
special behavior. Otherwise healthy AI Blue and ordinary NonBlue manual focus
set all unit-targetable flags and call `ai_target_find_nearest(ELIGIBLE_ENEMY)`.
Blue low HP selects a healer/crystal; supported NonBlue MostlySupport is cleared
in the single-ally domain. Team-support and special-policy branches remain
outside the selected ordinary adapter.

Nearest traversal uses original slot order, Manhattan X/Y distance ignoring
layer/height, and strict improvement: **first slot wins equal distance**.
No eligible unit returns the actor, even when the actor fails eligibility; it
does not produce an invented enemy or choose randomly. ELIGIBLE_ENEMY requires
live targetability, no Petrify, cached Enemy, no DeadWithoutReraise, supplied
unit-targetable membership, Considered, and no BloodSuck. It intentionally skips
the generic Dead exclusion, so an otherwise eligible dead-with-Reraise record
can win. Other modes differ: healer mode admits Crystal before ordinary checks;
nearest enemy excludes Dead; imminent enemy adds delay/status and adjacent-height
checks; nearest unit can select the actor. Do not expose these unimplemented
modes through a universal nearest-enemy implementation.

`general_select` builds movement once, seeds its baseline, handles retained charge,
then selects ANY active units. `ability_list_evaluate` preserves source occurrence
order. Each entered usable outcome copies the active mask into a fresh working
targetable mask before usefulness, self/unit/tile/reflect/charge passes. Roster
focus, active membership and per-ability target eligibility are distinct stages.
A broad port must preserve their separate source traversal, restoration and RNG
ordering; repeating standalone pair queries is not a multi-target search.

## Imminent enemies and proximity

`acting_unit_init` caches turn delay in **u8 before refresh**. Ordinary delay is
ceil(max(0,100-CT)/Speed), with INT_MAX for zero Speed. Full delay also includes
Stop/Sleep and cached dead/inert checks; these are not yet managed status rules.
For example a full resolved delay355 stores cached byte99. Do not recompute this
cache from simulated refresh CT or compare targets against the full355.

`foe_proximity_grid_fill` rebuilds its working targetable set from cached
out-of-play and imminent-enemy exclusions. A member must be Enemy, have resolved
turn delay <= cached actor delay and have no Stop, Don'tAct, Sleep, Chicken or
Charm active through that cached delay. `ai_status_is_active_through_delay`
uses inclusive equality and innate/non-countdown exceptions. This foundation
accepts an explicitly resolved disabled-through-actor-delay fact; it does not
invent status durations or port a status simulator.

Only scenario0 cells are written, across layers in Y/X order. Every admitted
enemy contributes max(10-ManhattanDistance,0); contributions accumulate in u8.
Distance10 contributes zero;11 is skipped. Other cells retain their earlier
contents, which may be unknown. Return writes rather than a fictitious cleared
grid. Proximity is independent of Considered, per-ability active flags and focus.
At the original21-slot extent at most20 fresh enemies contribute, so the old
26-enemy wrap illustration remains inadmissible as an original battle vector.

## Managed limits and selected milestones

Current Sword refresh/preparation, proximity cache and action scans assume two
living opponents. Framework `FlatSwordBattleProfile`, `FlatOrdinaryMovement` and
targeted ability setup independently enforce two-unit domains. Broader snapshots
and turn-cycle storage alone do not authorize broader combat. There are21 source
slots and16 compact AI records; `ai_command_build` ends if any present unit lacks
a compact record. Neither constant is a managed catalog/roster capacity.

The next five independently checkable children are selected for this batch:

- **ZOD-272:** immutable ordered roster facts and live/cached membership helpers.
  Explicit stable IDs, unique nonnegative source-order keys and byte-coordinate
  facts; no fixed roster capacity. Keep resolved decision facts separate from
  live facts and from authoritative battle state. Include current/initial-team
  classification and ordinary delay arithmetic, without a packed runtime model.
- **ZOD-273:** pure eligible-enemy nearest search over explicit working targetable
  membership. Preserve first-slot ties, actor fallback, dead/Reraise distinction,
  height-independent distance and exclusion order; no other nearest modes.
- **ZOD-274:** rebuild active target flags for all represented units, preserving
  cached eligibility, Confusion exception, semantic actor exclusion and signed
  unknown-mode fallback. No ability usefulness or target damage simulation.
- **ZOD-275:** pure roster proximity writes and imminent-enemy membership from
  resolved delay/status facts at the cached actor byte. Preserve sum/order/width,
  mask-only writes and unknown cells. No RNG, movement scoring or rescoring.
- **ZOD-276:** consume these shared owners in existing admitted pair preparation
  and movement proximity. Use an internal ordinary-pair adapter after existing
  admission, with live restored state and certified original order in TieKey.
  Keep every two-unit guard, API/result, draw stream, command and recovery boundary.
  Compare literal multi-unit descriptions plus old pair query/actual-turn parity.

These read-only queries accept resolved source facts; they neither reconstruct
arbitrary event history nor grant executable state/plan/RNG authority. Boolean
facts are semantic and immutable, not new packed flags or changes to owner drafts.
Future full-roster refresh, team support, ability summaries, movement/scoring,
target effects, authoritative commands and terminal routing require separate
source-selected milestones. General statuses/maps/policies, original frame RNG,
host/editor activation, broad replacement interfaces and ZOD-106 stay deferred.

## Implemented and validated follow-up

[ZOD-272–276](../behavior/ai-roster-targeting.md) implement the selected shared
queries and admitted pair consumers. Seventeen literal source descriptions and
three new Python cases validate the isolated source contracts; managed tests
consume those authored vectors independently. Full validation passes with
4,309 .NET cases and 545 Python tests, zero-warning/error Release build and pinned
source/prepared-data/guide/coverage checks. No original/C execution or UI review.
