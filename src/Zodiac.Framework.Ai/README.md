# AI ownership and entry points

The supported roster path has three public static entry points:

- `AiDecisionMaker.Decide`: pure decision, diagnostics and successor priority knowledge. Hypotheses restore battle state while decision RNG chains.
- `AiCommandExecutor.Execute`: fresh decision inside Framework's checked callback, followed by one actual typed command. Public plans grant no authority.
- `AiTurnController.Execute` / `Resume`: complete admitted turn through actual receipts, frozen Move continuations and exact-session recovery. Stops before the next clock; facing remains explicit.

All share immutable `AiDecisionConfiguration`; a turn adds dispatch/facing through `AiTurnConfiguration`. See the [migration note](../../docs/development/ai-refactor-331.md) and [external-assembly test](../../tests/Zodiac.Content.Tests/PublicAiPipelineTests.cs). Lower-level static owners are internal; older pair, supplied-scenario and derived paths retain their regression tests and admission guards.

## Source map

- `Decision/`: configuration, decision records, fresh prefix/list/ranking/lottery orchestration and internal ability dispatch.
- `Execution/`: actual commands, complete turns, diagnostics, recovery and common stage routing.
- `Shared/`: hypothetical clocks/state, roster preparation, movement/scenarios, target masks, scoring/ranking/lottery and immutable priority knowledge. `AiDamageHypotheses` completes admitted HP projections through the all-unit clock/score. `AiUnitPolicy`, `AiPreparedAbility` and `AiReachAdjustment` name shared data.
- `Sword/`, `ThrowStone/`, `Accumulate/`, `Dash/`: specialized admission, formulas and searches. Attack/Stone retain distinct trajectory/evasion/RNG rules; Dash retains AI midpoint versus actual damage behavior; Accumulate retains usefulness and self-PA hypotheses.
- `AttackThrowStone/`, `BasicSkill/`: earlier bounded composition, selection and execution paths, retained internally for source vectors and guards.

Namespaces remain `Zodiac.Framework.Ai` and `.Tests`. AI references Framework; Framework never references AI. Content supplies semantic lists and typed grants from immutable metadata. AI does no file I/O and cannot construct trusted sessions/receipts. Actual combat, geometry, movement, RNG primitives and turn rules remain in Framework. The live host is not yet wired to these entry points.

Folders describe ownership, not original table limits or general weapon support. Keep unsupported policies explicit. Broader abilities, automatic facing, corpse/terminal/frame RNG and presentation activation remain deferred. Avoid another composition class or broad abstraction for each ticket.

Start with the [original algorithm guide](../../docs/architecture/original-ai-algorithm.md) and [completion roadmap](../../docs/development/ai-completion-roadmap.md). Historical evidence retains names/paths at its pinned managed revision; current coverage follows today's layout.


## Ordinary spell ready-hit diagnostics

`OrdinarySpells/` adds source-traced primary learned-slot admission, considered
parameters/working usage, initialized MP shares, ordinary HP usefulness, shared
AI formula projection, strike exclusions and absolute HP/status/MP scoring.
`OrdinarySpellAiReadyQuery` owns all fresh calculations against a definition-bound
catalog and a current initial aim; it returns no executable plan or battle state.
Movement opportunities are explicit boundary inputs. Charge-clock forecasting,
origin/target search, rank insertion and autonomous magic turns remain outside
this query and the existing decision maker is unchanged. Content supplies the
metadata bridge; it rejects unrepresented secondary learned rows/loadouts.
See [behavior and validation](../../docs/behavior/ordinary-spell-ai.md) and the
[prepared-only inspector](../../docs/development/ordinary-spell-ai-inspection.md).
