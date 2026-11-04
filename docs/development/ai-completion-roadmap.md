# AI completion roadmap

Recorded 2026-10-29 from the owner's requested completion discussion, after
ZOD-202 (`7d979d0`). This document preserves the overall destination and remaining
work areas. The [backlog](../BACKLOG.md) owns selected work/order; the
[handoff](../PROJECT_HANDOFF.md) owns current implementation and validation.
Update this roadmap as areas close; do not append ticket-by-ticket history here.

The [original AI algorithm guide](../architecture/original-ai-algorithm.md) owns
the unified source map, policy precedence and documented implementation gaps.
Use it to place each bounded child within the overall algorithm.

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
or unread. Unwritten cast priorities still reject; their retained lifecycle,
broader original policies/rosters/maps and independent validation remain open.

The [ZOD-210 cleanup](ai-pair-selection-refactor.md) shares pair admission and
composition/lottery handling while retaining separate scenario/provenance rules.
Immutable request/configuration records simplify selection and execution inputs;
this introduces no broader replacement contract or behavior change.

These are bounded implementations: ordinary flat geometry, two opponents,
restricted lists/statuses and explicit assertions about unrepresented context.
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
and the certified Blue branch through shared Framework authority. The next child,
ZOD-229, studies Accumulate's usefulness, hypothetical PA change and scoring
before extending the candidate model. Broader control/low-HP/terminal policies
remain separate. The Attack-only list/scenario certificate cannot stand in for a pair.

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
