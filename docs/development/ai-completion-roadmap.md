# AI completion roadmap

Recorded 2026-10-29 from the owner's requested completion discussion, after
ZOD-202 (`7d979d0`). This document preserves the overall destination and remaining
work areas. The [backlog](../BACKLOG.md) owns selected work/order; the
[handoff](../PROJECT_HANDOFF.md) owns current implementation and validation.
Update this roadmap as areas close; do not append ticket-by-ticket history here.

## Current foundation

`Zodiac.Framework.Ai` owns a source-traced prepare → evaluate movement/actions →
rank → choose pipeline for restricted ordinary sword and Attack/Throw Stone
profiles. Shared hypothetical clocks, scoring, ranking and lottery machinery
already exist. Ordinary sword paths also support Framework-owned command
execution, Move-first/ACT-first continuation and post-ACT movement/settlement.
The pair's [in-reach](../behavior/attack-throw-stone-ai-prepared-selection.md) and
[known-score out-of-reach](../behavior/attack-throw-stone-ai-out-of-reach-selection.md)
entries currently produce hypothetical selections, not authoritative ability commands.

These are bounded implementations: ordinary flat geometry, two opponents,
restricted lists/statuses and explicit assertions about unrepresented context.
They do not establish general original-game AI equivalence. Existing source
studies pin fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`; recheck the
read-only reference revision before new tracing. Test count is not a completion measure.

## Remaining work areas

1. **Connect ability decisions to authoritative commands.** ZOD-203 first traces
   and designs Throw Stone's handoff. Follow with bounded implementation of fresh
   selection/RNG adoption, actual command validation and execution, Move-first/
   ACT-first ordering, continuation, actual movement/facing receipts and turn
   completion. AI midpoint/no-knockback predictions must stay separate from real
   accuracy, damage, knockback and execution draws. Reuse the existing
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
deferred. The [ZOD-203 handoff design](../architecture/zod-203-throw-stone-command-boundary.md)
selects ZOD-204's Framework-only targeted ACT transaction as the immediate next step.

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
