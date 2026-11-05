# Focused AI refactor — ZOD-331

Completed 2026-11-05 from managed base
`b4617e0f2c8a05b4205e8a8aca03f0e9d5673ce7`. The read-only original reference is
clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`. This owner-requested cleanup
precedes further ability/content work and translates no new original rule.

The supported roster path is now `AiDecisionMaker.Decide`,
`AiCommandExecutor.Execute` and `AiTurnController.Execute` / `Resume`.
All share immutable `AiDecisionConfiguration`; a turn adds dispatch and explicit
facing through `AiTurnConfiguration`. See the [source map](../../src/Zodiac.Framework.Ai/README.md).

## API migration

The prior `RosterBasicSkillAiSelection`, `RosterBasicSkillAiCommandExecution`
and `RosterBasicSkillAiTurnExecution` become the three entry points above.
`Evaluate` becomes `Decide`. Separate simulation, refresh, list, general, movement
and ability arguments become one configuration. Associated
`RosterBasicSkillAi*` decision/command/turn records have `Ai*` names.
`BasicSkillAiCommandKind`, `BasicSkillAiCommandRejection` and
`BasicSkillAiTurnStage` become `AiCommandKind`, `AiCommandRejection` and
`AiTurnStage`. Shared `SwordAiRefreshPolicy`, `SwordAiReachAdjustment` and
`BasicSkillAiPreparedOccurrence` become `AiUnitPolicy`, `AiReachAdjustment` and
`AiPreparedAbility`.

At the managed base, an inventory found 87 public static AI owners with no
production callers outside AI or test callers outside its existing friend test
assembly. 84 lower-level owners are now internal, leaving three public entry
points. Public diagnostic/input records remain available. This is an intentional
C# source API change; compatibility with the old public pilot APIs is not
promised. No new friend access or runtime dependency is introduced. Content's
semantic list adapter retains its public data types. An external-assembly test
exercises decision, actual command and complete turn without friend access.

## Behavior boundaries

Internal `AiAbilityEvaluator` owns the existing specialized ability switch.
The decision coordinator retains fresh prefix, ordered occurrences, eight-slot
ranking and source lottery, including rejection/RNG/workspace ordering.
`AiDamageHypotheses` consolidates only admitted post-formula HP projection:
unchanged peers, actor movement/CT, all-unit clock and fresh scoring. Attack,
Stone and Dash retain distinct range, trajectory, accuracy, formula and draw
rules, including failure rollback.

Older pair and supplied/derived algorithms remain internal with their existing
tests and guards. Their different admission and continuation domains are not
forced through fabricated roster states. Pure hypotheses grant no authority.
Actual execution still decides inside Framework's callback from current state
and RNG, submits typed commands and publishes knowledge only on acceptance.
Recovery binds exact state identity and frozen inputs. No cached-plan executor
or general strategy interface is introduced.

All existing behavior assertions, source fixtures and replay checkpoints remain
unchanged. Validation: locked restore, zero-warning/error Release build, 4,531
.NET cases (1,217 AI, 1,521 Framework, 833 Content), 596 Python cases and pinned
source, coverage and local-link audits. One external public-pipeline test is new.
See the [implementation receipt](../evidence/ai/ai-refactor-implementation-v1.json).

Owner drafts and JobStats are untouched. Host/editor activation, automatic
facing, full corpse/terminal/frame RNG, broad replaceable interfaces and ZOD-106
remain deferred. Prepared Dash content binding remains ZOD-329, next after this
cleanup; no new gameplay breadth is claimed.
