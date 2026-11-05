# Ordinary roster AI preparation and simulation — ZOD-283

Source/design, 2026-11-04; managed base `5b25945d09af258d002e8af6c7d665f8fef64397`.
Read-only fft_decomp checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/roster-preparation-source-v1.json) records symbols
and fingerprints. No original/C execution. This extends the earlier
[refresh](zod-156-ai-refresh-prefix.md), [roster targeting](zod-271-roster-targeting.md)
and [actual roster command](zod-277-roster-combat.md) studies.

## Fresh refresh and restored state

`ai_command_build` admits compact records and calls `acting_unit_init` before
refresh. Actor delay is cached as u8 from live CT and earlier decision flags.
Require clean delay cache rather than deriving this value from simulated CT.
With stable teams, all units targetable and zero cached MP modifiers,
`decision_flags_refresh` rebuilds Enemy from actor current versus target initial
team, preserving ConserveCT/TeamSupportUnavailable. HP-below-half uses
hp < floor(maxHP/2), while three quarters uses hp*4 < maxHP*3. No MP fact can
be inferred merely from zero current/max MP without the cached-modifier gate.

The refresh action is ACT-first/skillset0; it does not visit movement pickup or
an action formula. Actor turn-end CT adds20 for each unused choice in u16 then
caps at60. `ai_action_simulate_and_score` advances all represented units through
the shared clock. With living positive-Speed units and no charges/upkeep/terrain/
script/Mime effects, the first UNIT_READY gives a movement opportunity and ends
the simulation. Pick already-ready units before adding CT; highest CT wins,
then first original slot. CT modulo100 precedes the upkeep draw and resets the
selected unit's choices. Exactly one upkeep draw occurs in this admitted domain,
even when no ticks are needed. Source suspension/frame/VSync draws remain unresolved.

Refresh rebuilds transient flags and Considered from the simulated survivors,
then restores baseline units. All admitted living None/Critical units become
Considered; later summaries/support/proximity use restored live HP/CT/positions,
not the diagnostic clock projection. RNG survives. Separate typed query results
must not become caller-supplied executable state or cached simulation authority.

## Lists and team support are different passes

`ability_considerations_set` first derives usability/summaries for every eligible
unit. Ordinary Attack/self/Stone/Frog initialization is already owned. Attack
and Stone count as harmful physical occurrences, self as helpful, ordinary Frog
is unusable; duplicates count separately. Harmful threats sum **all enemies**,
including different non-actor teams, not just a focused opponent. Source totals
are s32. Helpful share is floor(helpful*128/(helpful+harmful)); allies at share77
or greater gain MostlySupport. The **first source-order minimum** ally loses that
flag, because replacement uses strict less-than. Enemy retained flags stay unknown.

The actor-only pass derives policy/reaches before clearing usable bits after ACT.
Supported lists retain hostile reach Move+1, ally reach Move, random threshold0,
and per-occurrence consume/exhaustive policy. Their summaries do not grant
gameplay abilities or implement target/effect searches; executable grants belong
to a later actual action/entry owner. No self usefulness coin is drawn here.

`decision_team_targeting_flags_update` clears SingleEnemyRemaining before any
exit. Retained actor TeamSupportUnavailable (or unrepresented DeathSentence)
exits before counts. Otherwise count all eligible allies/enemies; Critical/Frog
allies are impaired. Support needs at least two allies and one unimpaired ally.
Failure propagates TeamSupportUnavailable to every cached ally for non-Blue,
but Blue preserves those flags. Early exit does not perform that propagation.
Only the successful support branch sets SingleEnemyRemaining when enemy count1;
one enemy alone never proves that hint. Counts skipped by early exit remain
uncomputed diagnostics rather than fabricated zeros.

## Ordinary movement hypotheses and absolute scoring

`reachable_tiles_score` simulates each selected movement tile from restored state,
in source map order. This batch selects only an isolated supplied legal tile,
not the scenario/policy owner or a full scan. Movement placement changes actor
coordinates and MovementTaken when the tile differs, but keeps facing. ACT stays
as used/unused in live state. End-turn refunds follow those choices, and the same
whole-roster hypothetical clock runs once. Actual commands and live clocks remain
untouched; no repeated pair snapshots or fake target ID are needed.

Absolute score visits source slots, adding floor(HP*128/maxHP), status weights
(Critical=-32; None=0 here), and seven unchanged non-monster equipment terms of51.
Zero current/baseline Golem and MP0 are explicit admission. Cached Enemy negates
each unit total; signed16 priority narrows after each unit, not after combining
teams. Healthy units each contribute485, so two allies and one enemy yield485,
not pair cancellation. A critical ally at10/50 contributes350. Source hit100 and
per-unit running-priority diagnostics remain shared with the pair score kernel.

## Selected children — ZOD-284–286

- **ZOD-284:** separate ordinary-roster simulation profile and fresh refresh query
  before/after Move/ACT. Require complete owned source membership, baselines and
  retained policy rows; effect-free context, clean actor cache, known targetability
  and MP modifiers. Reuse existing private clock/projection arithmetic, preserve
  source ordering, whole-query rollback and immutable inputs.
- **ZOD-285:** own one fresh refresh plus supported owned-list summaries, actor
  policy/reaches and team-support update on restored live state. Derive a target
  roster through existing semantic eligibility/timing owners. Preserve lowest-share
  ties, asymmetric threats, early exit and Blue propagation distinctions. Stop
  before focus/policy/scenario search and executable grant/action checks.
- **ZOD-286:** isolated legal roster movement baseline and absolute scoring through
  extracted shared private move projection/clock and score kernels. Require
  non-monster unchanged equipment/zero Golem facts. No public cached simulation
  input, fake pair result, normalizing of hypothetical facing or command authority.

The original-compatible AI profile admits at most16 present compact records;
the actual roster profile separately admits21 source slots and movement16 other
records. Definition storage remains unbounded. This batch supports living ordinary
None/Critical units, positive Speed, MP0 and stable teams on admitted flat ground.
Preparation limits actor raw Move to124 for nonwrapping byte reaches; isolated
movement scoring retains the shared capped range without that reach assertion.
Full focus/policies/scenarios, action/target/effect traversal, corpse movement/
death scheduling, other statuses/maps, automatic facing, terminal routing, original
frame RNG, host/editor, owner drafts, replacement interfaces and ZOD-106 remain
separate/deferred. Old pair APIs, guards and streams remain intact.

## Implemented follow-up

[ZOD-284–286](../behavior/roster-ai-preparation.md) implement the selected separate
queries through shared private kernels. Thirteen source descriptions, 10 new AI
cases and three new Python cases cover literal clocks/support/scoring, bounds,
pair parity and immutable replay. Full validation passes with 4,335 .NET cases,
551 Python tests, zero-warning/error Release build and pinned source/prepared/
guide/coverage checks. No original/C execution or host/editor activation.
