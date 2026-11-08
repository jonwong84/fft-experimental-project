# AI ownership and entry points

## Start with these three files

Open [AiDecisionMaker.cs](AiDecisionMaker.cs) to follow how the AI chooses its
next action. `Decide` prepares the current battle, evaluates each supported
ability, ranks its best candidate and selects a plan through the source lottery.

For a complete supported AI turn, start at
[AiTurnController.cs](AiTurnController.cs). The main call flow is:

`AiTurnController.Execute` → `AiCommandExecutor.Execute` → `AiDecisionMaker.Decide`

[AiCommandExecutor.cs](AiCommandExecutor.cs) obtains that decision inside the
checked battle boundary and executes one actual command. The turn controller
handles subsequent commands, movement continuations and turn completion;
`Resume` continues an interrupted admitted turn.

These three public entry files live at the project root. Their configuration,
result records and internal helpers remain in `Decision/` and `Execution/`.
To read decision logic, follow `AiDecisionMaker.Decide` through fresh preparation
and then [Decision/AiGeneralActionSelection.cs](Decision/AiGeneralActionSelection.cs).
The selector scans the prepared list through
[Decision/AiAbilityEvaluator.cs](Decision/AiAbilityEvaluator.cs), inserts each
ability's best candidate and runs the ranked lottery or movement fallback.
`Shared/` contains the common preparation, simulation and ranking rules.
The [routine ownership guide](../../docs/architecture/ai-routine-ownership.md)
maps these classes to the original routines and distinguishes supported subsets
from remaining work.

The Windows host is not wired to this pipeline yet. Ordinary spell searches
currently return diagnostic hypotheses and are not part of its executable
ability dispatch. Follow `OrdinarySpells/README.md` for that separate path.

## Further reading

The [action pathway guide](../../docs/development/action-pipelines.md) walks one
player cast and one AI decision, with source symbols and modification points.
The [ordinary spell source map](OrdinarySpells/README.md) follows fresh selection,
private simulation and scoring; it explicitly identifies the unimplemented
search/command connection.

The supported roster path has three public static entry points:

- `AiDecisionMaker.Decide`: pure decision, diagnostics and successor priority knowledge. Hypotheses restore battle state while decision RNG chains.
- `AiCommandExecutor.Execute`: fresh decision inside Framework's checked callback, followed by one actual typed command. Public plans grant no authority.
- `AiTurnController.Execute` / `Resume`: complete admitted turn through actual receipts, frozen Move continuations and exact-session recovery. Stops before the next clock; facing remains explicit.

All share immutable `AiDecisionConfiguration`; a turn adds dispatch/facing through `AiTurnConfiguration`. See the [migration note](../../docs/development/ai-refactor-331.md) and [external-assembly test](../../tests/Zodiac.Content.Tests/PublicAiPipelineTests.cs). Lower-level static owners are internal; older pair, supplied-scenario and derived paths retain their regression tests and admission guards.

## Source map

- Project root: the public decision, command and turn entry files above.
- `Decision/`: general action selection, configuration, decision records and internal ability dispatch used by the root decision maker.
- `Execution/`: command/turn configuration, diagnostics, recovery and common stage routing used by the root execution entries.
- `Shared/`: hypothetical clocks/state, roster preparation, movement/scenarios, target masks, scoring/ranking/lottery and immutable priority knowledge. `AiDamageHypotheses` completes admitted HP projections through the all-unit clock/score. `AiUnitPolicy`, `AiPreparedAbility` and `AiReachAdjustment` name shared data.
- `Sword/`, `ThrowStone/`, `Accumulate/`, `Dash/`: specialized admission and searches. Stone/Dash now project through Framework's shared selected physical formula/rule owners; delivery and metadata policies remain distinct. Accumulate retains usefulness and self-PA hypotheses.
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

`OrdinarySpellAiCandidateQuery` now captures a supplied aim at the current origin
and derives readiness/movement opportunities through `OrdinarySpellAiClock`.
It shares private fresh admission/stored-hit owners with the ready query and
returns hypothetical diagnostics and decision RNG, including the unresolved
source event-cap score. It cannot execute a command. Both queries retain their
separate contracts; general aim/origin search, ranking, lottery and autonomous
magic turns remain outside this path. [Clock contract and validation](../../docs/behavior/ordinary-spell-ai-clock.md).

`OrdinarySpellAiAimQuery` now lists fresh potential unit/tile aims at the current
origin, with source working usage, deadline groups and scan order. It does not
draw RNG, evaluate/rank candidates or execute commands.
[Aim-list contract and validation](../../docs/behavior/ordinary-spell-aim-enumeration.md).

`OrdinarySpellAiSearchQuery` freshly composes one owned slot's current-origin
aims and simulations, carries one decision RNG and retains the strict best.
Pass-specific valuable masks and unresolved per-pass exits follow the caller.
It returns diagnostics; broader origin search, dispatch, lottery and commands
remain separate. [Search contract](../../docs/behavior/ordinary-spell-aim-ranking.md).

Within `OrdinarySpells/`, `Selection/` owns catalog/list/parameter/usefulness work,
`Simulation/` owns private state, charge clocks and hits, `Scoring/` owns hit
accounting and absolute scores, and `Diagnostics/` owns the ready-hit query.
The candidate query stays at that feature's root. Namespaces and APIs are unchanged.
