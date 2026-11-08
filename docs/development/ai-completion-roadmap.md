# AI completion roadmap

Current managed ownership: [ZOD-331](../development/ai-refactor-331.md) uses
`AiDecisionMaker`, `AiCommandExecutor` and `AiTurnController` with shared immutable
configuration. Older names below identify the historical source-traced slices;
lower-level pilot APIs are now internal. Original rules and evidence remain unchanged.

Recorded 2026-10-29 from the owner's requested completion discussion, after
ZOD-202 (`7d979d0`). This document preserves the overall destination and remaining
work areas. The [backlog](../BACKLOG.md) owns selected work/order; the
[handoff](../PROJECT_HANDOFF.md) owns current implementation and validation.
Update this roadmap as areas close; do not append ticket-by-ticket history here.

The [original AI algorithm guide](../architecture/original-ai-algorithm.md) owns
the unified source map, policy precedence and documented implementation gaps.
Use it to place each bounded child within the overall algorithm.

The [ordinary spell origin study](../behavior/ordinary-spell-movement-origins.md)
now traces ranked/exhaustive origins, ACT-first destination substitution,
baseline widths, remaining aims and per-origin exits. The
[movement baseline query](../behavior/ordinary-spell-movement-baselines.md) now
derives fresh offensive D0/D1, ranks and priority provenance through shared
clocks/scoring. The [cast-origin enumerator](../behavior/ordinary-spell-cast-origins.md)
now derives potential pass masks, order and destinations without formula
evaluation. The [origin evaluation query](../behavior/ordinary-spell-origin-evaluation.md)
now simulates one fresh origin/pass with ordered placement and baseline admission.
The [pass search query](../behavior/ordinary-spell-pass-search.md) now traverses
one fresh selected pass with remaining aims/RNG, proven adverse-self consumption,
unresolved continuation and final-destination ties. The
[ability caller query](../behavior/ordinary-spell-ability-search.md) now composes
one owned offensive slot/scenario's passes with fresh masks, shared best/RNG and
the final baseline gate. ZOD-442 next traces fresh list/scenario integration and
ranked ownership before a bounded runtime child. Full spell decisions,
autonomous magic and the review gates remain open.

## Required readability review after the full port

Owner direction recorded2026-11-07: once the full original AI routine set is
implemented and representative behavior is validated, complete ZOD-432 before
calling the AI work finished. The owner must be able to start at
`AiDecisionMaker` or `AiTurnController` and follow preparation, useful-target
policy, formula simulation, strike accounting, scoring, ranking and checked
execution through `AiCommandExecutor`. Maintain a source-routine-to-C# map and
clear entry-point documentation, including the player-selected ability path.

Walk through real decisions with the owner; review naming, responsibilities,
file/namespace organization and branching. Prefer explicit if/else for nontrivial
conditions. Use shared mechanics/formula families and data-defined individual
abilities. Refactor only where it improves comprehension, in bounded changes
that preserve compatibility, arithmetic/order, RNG and deterministic replays.
Document remaining gaps and rerun checks appropriate to each change. Small
clarity fixes can continue during the port; this gate does not postpone them or
authorize a blanket reorganization now. Passing tests alone do not close the
owner walkthrough or full AI coverage gates.

The [backlog](../BACKLOG.md) owns this gate and current review order; the wider
ZOD-428 retrospective remains separate from completing original-AI breadth.

## Review and integration into main

The owner uses trunk-based development. After the AI logic/pipeline completion
and ZOD-432 walkthrough, prepare a pull request into `main` and review the entire
branch diff, including any changes outside AI. Keep the PR's scope, compatibility
limits and validation clear. The owner has SonarQube and CodeRabbit configured;
inspect their actual quality, coverage and review results, address actionable
findings, and rerun checks appropriate to any fixes before merging. Automated
results supplement source/behavior validation and the owner's readability review.

## Current foundation

`Zodiac.Framework.Ai` owns a source-traced prepare → evaluate movement/actions →
rank → choose pipeline for restricted ordinary sword and Attack/Throw Stone
profiles. Shared hypothetical clocks, scoring, ranking and lottery machinery
already exist. Ordinary sword paths also support Framework-owned command
execution, Move-first/ACT-first continuation and post-ACT movement/settlement.
The pair's [in-reach](../behavior/attack-throw-stone-ai-prepared-selection.md) and
[known-score out-of-reach](../behavior/attack-throw-stone-ai-out-of-reach-selection.md)
entries produce hypothetical selections. A fresh
[in-reach Move-first adapter](../behavior/attack-throw-stone-ai-move-first-execution.md)
and [known-score out-of-reach entry](../behavior/attack-throw-stone-ai-out-of-reach-move-first.md)
now connect pair selection to authoritative commands. A fresh
[in-reach ACT-first adapter](../behavior/attack-throw-stone-ai-act-first-execution.md)
also applies real Attack/Throw Stone and leaves Move unused; predicted D1
grants no authority after that ACT.
Framework's [targeted ACT transaction](../behavior/targeted-ability-decision.md)
and [targeted Move continuation](../behavior/targeted-move-continuation.md) now
exist; [out-of-reach ACT-first](../behavior/attack-throw-stone-ai-out-of-reach-act-first.md)
and [pair post-ACT execution](../behavior/attack-throw-stone-ai-post-act-execution.md)
now reuse those authorities.
The pair's [post-ACT preparation](../behavior/attack-throw-stone-ai-post-act-preparation.md)
now reuses fresh refresh and derives retained summaries with both actor entries
unusable. [Post-ACT movement scenarios](../behavior/attack-throw-stone-ai-post-act-scenarios.md)
and [selection](../behavior/attack-throw-stone-ai-post-act-selection.md) also reuse
shared kernels. [Same-tile fallback END](../behavior/attack-throw-stone-ai-fallback-execution.md)
also exists. [Composed command checks](ai-pair-command-flows.md) cover actual
turn flows and recovery. The [single command entry](../behavior/attack-throw-stone-ai-command-execution.md)
now derives reach/order through one private preparation and selection.
[Complete headless turns](../behavior/attack-throw-stone-ai-turn-execution.md) compose
it with exact continuation/receipt recovery. [Bounded Blue support](../behavior/blue-attack-throw-stone-ai.md)
shares these owners, with explicit source control/HP admission and no NonBlue
reach-adjustment draw. Broader Blue policies remain open.
Source/tests are [grouped by family](ai-source-layout.md) with stable namespaces.

[Accumulate AI](../behavior/accumulate-ai.md) now has pure PA projection/scoring,
certified Attack/Accumulate/Throw Stone preparation, a separate source-order
usefulness filter and one isolated map-target entry against a supplied movement
baseline. Shared clocks/scoring/rank comparisons remain the owners. Three-entry
[supplied-scenario composition and terminal selection](../behavior/basic-skill-ai-selection.md)
now reuse these owners in source order. [Actual ACT-first execution](../behavior/immediate-self-act.md)
also uses the Framework-owned self-action transaction.
[Fresh command dispatch](../behavior/basic-skill-ai-command-execution.md) now
shares one selection owner across ACT-first, Move-first and same-tile empty END,
reusing frozen Attack/Stone/Wait tokens.
[Post-ACT movement and settlement](../behavior/basic-skill-ai-post-act.md) now
derive both explicit reach branches from actual boosted state, with cleared
actor entries and the empty lottery.
[Complete three-entry turns](../behavior/basic-skill-ai-turn-execution.md) now
add automatic post-ACT reach with one preparation and exact-session recovery
across published commands. [Derived pre-ACT scenarios and turns](../behavior/derived-basic-skill-ai.md)
now share pair movement ownership, derive S/D0/D1 and prove cast keys written
or unread. [Retained priority knowledge](../behavior/ai-priority-workspace.md)
now carries owned writes through range3/4 queries, commands and complete turns.
Unknown/event-written values remain unknown and unmodeled workspace use forgets
knowledge. [Content-backed list construction](../behavior/content-backed-ai-lists.md)
now preserves source slots/order/duplicates and explicit known state, with an
outer setup adapter and canonical four-entry preparation consumer.
[Variable supported lists](../behavior/variable-basic-skill-ai.md) now own
missing/repeated/reordered self/Stone preparation, shared movement, per-occurrence
evaluation/lottery and actual complete recoverable turns.
[Roster targeting queries](../behavior/ai-roster-targeting.md) now supply ordered
live/cache membership, eligible-enemy nearest focus, active masks and imminent
proximity over resolved facts, shared with admitted pair preparation. Full roster
ability/effect traversal, other policies/maps and independent validation remain open. [Actual roster commands](../behavior/flat-roster-combat.md)
now support ordinary living-roster ally transit/empty endpoints, selected sword
resolution and explicit-facing settlement through shared kernels. Existing AI
pair profiles/drivers retain two-opponent guards; separate supported roster
drivers now exist through ZOD-315. Corpse movement/death scheduling remain
unimplemented. [Pure roster preparation](../behavior/roster-ai-preparation.md)
now owns one fresh whole-roster refresh, supported-list/team flags and an isolated
legal move score through shared clock/projection/scoring owners.
[Roster general scenarios](../behavior/roster-ai-general-scenarios.md) now derive
support-aware general admission, nearest focus, ANY diagnostics and ordered
movement masks/destinations/provenance.
[Roster action foundations](../behavior/roster-ai-action-foundation.md) now bind
cast keys across all active enemies and evaluate one isolated self occurrence
with all-ally usefulness and whole-roster scoring.
[One roster sword candidate](../behavior/roster-sword-ai-candidate.md) now shares
formula/clock/scoring and preserves peer HP, with projected corpse upkeep.
[Fresh roster Attack](../behavior/roster-sword-ai-attack.md) now walks origin/
aim-map order across every useful enemy through one owned general prefix.
Other policies, Stone and full-list ranking/selection still require separate work.

The [ZOD-210 cleanup](ai-pair-selection-refactor.md) shares pair admission and
composition/lottery handling while retaining separate scenario/provenance rules.
Immutable request/configuration records simplify selection and execution inputs;
this introduces no broader replacement contract or behavior change.

These are bounded implementations: ordinary flat geometry, pair command drivers
and separate living-roster queries, restricted lists/statuses and explicit
assertions about unrepresented context.
They do not establish general original-game AI equivalence. Existing source
studies pin fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`; recheck the
read-only reference revision before new tracing. Test count is not a completion measure.

## Remaining work areas

1. **Complete ability decisions through authoritative commands.** Throw Stone's
   handoff now supports both Move-first and ACT-first reach branches. The
   [post-ACT study](../architecture/zod-209-pair-post-act.md) traces pair-specific
   refresh/preparation, unused movement and settlement from actual results.
   Fresh preparation/scenarios/selection and authoritative post-ACT movement/
   settlement are implemented, with explicit score-provenance limits and
   unsupported terminal routing. The single fresh command entry is implemented;
   complete headless turns and the admitted Blue branch now share its authority.
   Expand ability evaluation and real turn/controller coverage next.
   AI midpoint/no-knockback predictions must stay separate from real accuracy,
   damage, knockback and execution draws. Reuse the existing
   [Framework/AI boundary](../architecture/ai-command-boundary.md).

2. **Expand roster, ability and target search.** Derive available entries from
   content, equipment, learned abilities and battle state. Search a full roster
   of allies/enemies and eligible targets, then compare candidates across the
   complete original lists in source order. Reuse established ranking machinery;
   general traversal and eligibility remain work. Original compatibility table
   behavior must not become an arbitrary managed content/roster limit.

3. **Expand hypothetical combat evaluation.** Predict healing, status changes,
   resurrection, area effects, resource costs, charging, reactions and support
   interactions using the actual supported rules. Broader targeting includes
   self/ally/enemy effects, collateral effects and relevant original quirks.
   This depends on underlying combat/status rules under ZOD-036/038; AI cannot
   accurately evaluate mechanics the game has not implemented.

4. **Handle real battle maps.** Extend beyond flat open geometry to elevation,
   multiple layers, obstacles, water, special movement and path/tie ordering.
   Cover direct/arcing trajectories and original reach/line-of-sight behavior.
   Coordinate with ZOD-037 and shared authoritative movement/targeting rules.

5. **Port remaining original decision policies.** Trace retreat, ally protection,
   healing/status recovery, preserving charges, Defend, auto-battle modes,
   team/control differences and configured special behaviors. Preserve their
   order, prerequisites and RNG effects; they determine which searches run and
   must not be approximated as one universal damage-maximizing policy.

6. **Resolve compatibility uncertainties and replace temporary assertions.**
   Derive currently certified facts as content/state models become capable of
   representing them. Investigate retained workspace scores and shared event
   memory without guessing zero or silently rescoring. Resolve original
   frame/VSync RNG behavior through evidence and an explicit compatibility
   decision; managed rendering/wall time must not silently advance gameplay RNG.
   Record uncertainty, preserved quirks and any intentional deviations. Known
   original behavior cannot be removed merely to simplify managed code.

7. **Integrate complete AI turns into the running game.** Drive enemy and
   automatic turns through the client, handling interruptions, deaths, remaining
   choices, facing and battle completion. Present movement/actions with the
   owner's preferred brief pauses outside deterministic simulation/time/RNG.
   Validate interactive turns separately from headless queries. Further editor
   UI work remains deferred unless gameplay/content needs it or the owner asks.

8. **Validate broadly and establish the replaceable decision contract.** Compare
   representative original encounters, full-roster battles and difficult
   interactions; check deterministic replays, command legality and RNG ordering.
   Prefer meaningful source vectors and scenario families over repetitive tests
   for every private detail or a fixed case count. Establish a focused stable
   contract for explicit state/definitions/RNG → typed decision, validated by
   authoritative rules, when the supported domain and concrete consumer justify
   it. The AI assembly already exists; broad engine/framework replacement
   contracts under ZOD-100/101 retain their real-consumer gate.

## Sequencing and completion

The largest remaining work is gameplay breadth and integration. Progress through
bounded source-traced children alongside combat, movement and content completion;
do not repeatedly rebuild ranking/selection for each new family. These eight
areas are a roadmap, not eight already-sized tickets or a promise of strict
serial execution. Select new children through the backlog and keep deferred work
deferred. The pair now supports one fresh command entry, complete headless turns
and the certified Blue branch through shared Framework authority. Three-entry
turns now include actual self ACT, derived scenarios and explicit retained
priority knowledge and content-backed lists. ZOD-265–270 now carry variable
supported-action occurrences through shared search, actual commands and complete
turn recovery, retaining canonical guards. ZOD-271–276 now provide shared roster
targeting queries. ZOD-277–282 add source-bounded actual roster movement/sword/
explicit-facing commands without weakening old pair admission. ZOD-283–286
add pure roster refresh, summaries/team flags and isolated movement scoring.
ZOD-287–289 add fresh general admission/focus and source movement scenarios.
ZOD-290–293 add cast-priority binding, roster self usefulness and an isolated
fresh self entry. ZOD-294/295 add an isolated roster sword candidate and source
study. ZOD-296 now connects exhaustive Attack search to a fresh general prefix.
ZOD-297/298 now trace ranked aims and project one supplied certified-clear
roster Stone candidate. ZOD-299/300 now compute direct trajectories
with explicit heights and exact SDK normalization. ZOD-301 implements one
fresh ranked Stone occurrence with computed blockage and consumed aims;
ZOD-302/303 now implement full supported roster list selection through
one prefix, stable ranking and source lottery. ZOD-304/305 map roster
command/turn integration prerequisites
and expose the existing actual self kernel through roster sessions. ZOD-306
now traces/implements live Stone interception, actual effects and session
publication through ZOD-306–309. ZOD-310–313 now implement verified roster
decisions and retained Move authority. [ZOD-314/315](../behavior/roster-ai-command-and-turn.md)
now integrate fresh dispatch and bounded turns/recovery. ZOD-316 verifies
multi-unit real-turn replay with explicit clock advancement.
Broader control/low-HP/terminal policies remain separate.
A certificate for one closed ability list cannot stand in for another list.

Original-AI completion means it can control applicable original encounters and
automatic units using the required actions, targeting, policies, movement and
turn transitions, with representative original evidence, deterministic replay
and explicitly accounted-for compatibility gaps. A successful two-unit demo or
large passing test suite alone is insufficient. No unexplained unsupported
original AI behavior should be hidden behind a claim of completion.

The owner's longer-term design goal is an interchangeable AI implementation,
with original behavior as the default and refinements/alternatives selectable
separately. Delivering that boundary is distinct from implementing a new custom
algorithm; the latter need not block original-game completion. C reference code
remains a study/validation source, never a runtime dependency.

[ZOD-316](../behavior/roster-ai-replay.md) now proves eighteen real roster turns
with explicit clock advancement and all-peer state/RNG/priority history.
[ZOD-317–320](../architecture/zod-317-dash.md) now trace Dash and implement its
actual3-draw rule, verified proposals and frozen Move follow-ups.
[ZOD-321–325](../behavior/dash-ai.md) now integrate its semantic list, hypothetical
candidate, ranked search, full-list dispatch and actual complete turns, including
six further real turns with retained priorities. [ZOD-326–328](../behavior/semantic-ai-metadata.md)
now trace content/AI binding and feed immutable static metadata traits into
supported lists, preserving working-policy separation and unsupported-edit
guards. [ZOD-330](../behavior/readable-ai-authoring.md) now makes named booleans
and structured shape traits primary for authoring, retaining version1 package
and original import/export compatibility. [ZOD-329](../development/dash-content-binding.md)
now binds prepared Dash effects, grants and AI setup, preserving the distinct
primary AI/secondary combat evasion flags. [ZOD-333](../architecture/ability-effect-families.md)
now inventories shared formulas and data/targeting/presentation boundaries.
[ZOD-334–336](../behavior/faith-magic-amounts.md) now trace shared Faith magic and
implement pure signed/Faith arithmetic and already-hit amount projection.
[ZOD-337–339](../behavior/magic-elements-and-routing.md) now add bounded weather/
element composition and raw absorption/undead routing, preserving Oil/Float/rider
guards. [ZOD-340](../behavior/magic-conditional-proc.md) now adds the isolated
conditional-proc decision/RNG step. These do not add spell commands or AI entries.
[ZOD-341](../behavior/magic-result-finalization.md) now supplies bounded
empty-status/living-target result finalization with caps and retained effect types.
[ZOD-342/344–347](../behavior/magic-evasion.md) now add bounded magic evasion and
fresh formula entry, including real mode-specific draws and whole-query rollback.
[ZOD-348–357](../behavior/magic-current-inputs-and-casts.md) now project current
MA/Faith and named facts, preserve distinct actual/AI timing, query MP and charge
steps, and resolve supplied ready-cast targets with one cost and whole-query
rollback. ZOD-358 next traces original targeting geometry; commands, state
publication and magic AI activation remain separate.
No automatic-facing/corpse/terminal/host gate is activated.

ZOD-358–402 subsequently add targeting, prepared player casting/movement and
learn-on-hit through the shared formula owners. [ZOD-403–412](../behavior/ordinary-spell-ai.md)
bind semantic primary metadata to a fresh ready-hit AI query with usefulness,
formula/resource projection, strike accounting and MP-aware scoring.
[ZOD-413–417](../behavior/ordinary-spell-ai-clock.md) now add fixed-origin delayed
candidates with source clock phases, derived movement opportunities and stored
aim resolution. Prepared-only diagnostics independently validate their timing,
RNG and scores. Fixed-origin aim search/rank insertion is the next source study;
origin enumeration, full decision integration and autonomous magic turns remain
separate. These slices do not complete original AI breadth.
