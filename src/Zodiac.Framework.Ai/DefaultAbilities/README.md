# Default ability AI pathway

The [pathway naming guide](../../../docs/architecture/default-ability-pathways.md)
records the owner-approved `DefaultAbility` prefix and future menu use cases.
Current support remains the existing magic-focused subset of default-menu
abilities; the rename does not activate additional formulas or skillsets.

The [ZOD-442 integration study](../../../docs/behavior/ordinary-spell-list-integration.md)
maps the enclosing original list, scenario, preparation and ranked ownership.
General ability search uses scenario1, with scenario0 baseline/fallback; a
Complement query does not add a second general dispatch pass.
[ZOD-443](../../../docs/behavior/ordinary-spell-context.md) now provides
[DefaultAbilityAiContext](DefaultAbilityAiContext.cs) for the matching captured
state/catalog pair. The ability caller uses `Evaluate(context, request, rng)`;
its request includes the explicit profile. Other query APIs remain unchanged.

For one owned spell's ordinary caller passes in a fresh movement scenario, start
with [DefaultAbilityAiAbilitySearchQuery.Evaluate](DefaultAbilityAiAbilitySearchQuery.cs).
Follow [AiAbilityOutcomeEvaluate](Selection/DefaultAbilityAiAbilityEvaluation.cs)
→ `OriginReachableEvaluate` → `OriginAimTilesEvaluate` → `AiActionSimulateAndScore`.
Unit/tile passes share best and RNG; each rebuilds its remaining aims. The final
baseline gate is diagnostic. See
[ZOD-441](../../../docs/behavior/ordinary-spell-ability-search.md) for exact
caller/reset ownership. List/scenario dispatch, ranked insertion and commands
remain separate.

For one fresh spell pass across cast origins, start with
[DefaultAbilityAiPassSearchQuery.Evaluate](DefaultAbilityAiPassSearchQuery.cs).
It derives fresh aims and movement once, then calls
[OriginReachableEvaluate / OriginAimTilesEvaluate](Simulation/DefaultAbilityAiPassEvaluation.cs)
with shared remaining aims and RNG. Shared simulation restores private units
between hypotheses. Source self-hit consumption, unresolved exits, final-destination
ties and separate baseline admission are documented in
[ZOD-440](../../../docs/behavior/ordinary-spell-pass-search.md).
Cross-pass/list dispatch, lottery and commands remain separate.

For spell simulation at one fresh origin/pass, start with
[DefaultAbilityAiOriginEvaluationQuery.Evaluate](DefaultAbilityAiOriginEvaluationQuery.cs).
It derives the origin internally, creates `ForOrigin`, then follows
`OriginAimTilesEvaluate` → shared `AiActionSimulateAndScore`, ordered movement/
preparation/strikes/clock and strict best comparison. See
[ZOD-439](../../../docs/behavior/ordinary-spell-origin-evaluation.md) for placement
order and baseline admission. This adapter disables cross-origin consumption.

For potential cast origins within one fresh movement scenario, start with
[DefaultAbilityAiOriginQuery.Evaluate](DefaultAbilityAiOriginQuery.cs). Follow
shared `BuildPotentialPasses`, fresh movement `Build`, `AiCastAimTilesBuild`,
priority binding, `OriginDestinationFinalize` and `OriginAimTilesBuild`.
[ZOD-438](../../../docs/behavior/ordinary-spell-cast-origins.md) documents the
unconsumed masks, known-priority boundary and ordering. No moved-origin formula
evaluation or command authority is granted by this query.

For fresh move-only D0/D1, baseline rank and priority-write diagnostics, start
with [DefaultAbilityAiMovementQuery.Evaluate](DefaultAbilityAiMovementQuery.cs).
Follow `DecisionFlagsRefresh`, `ScenariosBuild`, `ReachableTilesScore` and
`RankBaselineSeed`; shared geometry, clocks/scoring and `AiScenarioLayout` own
their existing rules. The certified offensive domain and remaining gates are
in [ZOD-437](../../../docs/behavior/ordinary-spell-movement-baselines.md).
This query does not evaluate spells from moved origins or issue commands.

For one spell's evaluated best at the current origin, start with
[DefaultAbilityAiSearchQuery.Evaluate](DefaultAbilityAiSearchQuery.cs). It freshly
enumerates, narrows each pass's useful mask, simulates in order with one decision
RNG and uses shared strict rank keys. Unsupported work rejects atomically; an
unresolved charge is ranked before stopping only its current origin pass.
See [ZOD-425](../../../docs/behavior/ordinary-spell-aim-ranking.md). This is a
diagnostic, not a complete ability-list decision or command entry point.

For potential aims at the current origin, start with
[DefaultAbilityAiAimQuery.Evaluate](DefaultAbilityAiAimQuery.cs). It freshly binds
one owned slot and calls [DefaultAbilityAiAimEnumeration](Selection/DefaultAbilityAiAimEnumeration.cs)
for working usage, deadline groups and source-ordered unit/tile masks. It returns
diagnostics without RNG, candidate evaluation or command authority. Its current
domain and source mapping are in [ZOD-424](../../../docs/behavior/ordinary-spell-aim-enumeration.md).

Start with [DefaultAbilityAiCandidateQuery.Evaluate](DefaultAbilityAiCandidateQuery.cs)
for one supplied aim at the actor's current origin. It is a pure hypothetical
query, not the complete decision maker and not a command entry point.

Follow these owners in order:

1. [DefaultAbilityAiWork](Simulation/DefaultAbilityAiWork.cs): admit current facts,
   bind the selected slot, initialize lists/parameters and filter useful targets.
   The corresponding rule owners live in `Selection/`.
2. [DefaultAbilityAiActionSimulation.AiActionSimulateAndScore](Simulation/DefaultAbilityAiActionSimulation.cs): capture
   the aim and call Framework's shared primary preparation, privately applying
   its proposal to immutable hypothetical units.
3. [DefaultAbilityAiClock](Simulation/DefaultAbilityAiClock.cs): advance a delayed
   action, or one event after an immediate useful hit. It records movement
   opportunities and retains source event-cap behavior.
4. [DefaultAbilityAiHitSimulation.ActionHitsSimulate](Simulation/DefaultAbilityAiHitSimulation.cs): call
   `StrikesSimulate`, then report whether a valuable target was hit. Strike processing resolves the
   stored aim, call shared area/recipient rules and calculate result frames via
   [DefaultAbilityAiProjection](Simulation/DefaultAbilityAiProjection.cs).
5. [DefaultAbilityAiAccounting](Scoring/DefaultAbilityAiAccounting.cs): exclusions,
   valuable-target gate, average hit and metadata-based priority adjustment.
6. [DefaultAbilityAiScoring](Scoring/DefaultAbilityAiScoring.cs): absolute roster
   score. The candidate query returns diagnostics and decision RNG.

For a delayed action the clock precedes the ready strike. For an immediate
action the strike precedes the required clock event. The numbered owners above
identify responsibilities; `AiActionSimulateAndScore` owns that ordering.

`Selection/` owns lists, considered parameters, statistics and usefulness.
`Simulation/` owns private work, clocks, strikes and formula projection.
`Scoring/` owns accounting and absolute score. `Diagnostics/` contains the
older ready-hit query whose movement opportunities are explicitly supplied.
All existing public declarations and namespaces are preserved.

For the supported physical roster's complete decision, start with
[AiDecisionMaker.Decide](../AiDecisionMaker.cs). For actual commands,
start with [AiCommandExecutor.Execute](../AiCommandExecutor.cs), or
[AiTurnController.Execute](../AiTurnController.cs) for a complete
admitted turn. Potential lists, supplied-aim simulation and the current-origin
one-slot search remain diagnostic entry points. Multiple origins, full dispatch,
rank insertion/lottery and automatic spell turns are not connected to the
physical decision/command entry points yet.
[ZOD-418 source study](../../../docs/behavior/ordinary-spell-aim-search.md)
defines the broader search boundaries.

See the [player/AI pathway guide](../../../docs/development/action-pipelines.md)
for shared rules, source mappings, modification points and current limitations.
