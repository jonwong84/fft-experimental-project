# Ordinary spell AI pathway

The [ZOD-442 integration study](../../../docs/behavior/ordinary-spell-list-integration.md)
maps the enclosing original list, scenario, preparation and ranked ownership.
General ability search uses scenario1, with scenario0 baseline/fallback; a
Complement query does not add a second general dispatch pass.
[ZOD-443](../../../docs/behavior/ordinary-spell-context.md) now provides
[OrdinarySpellAiContext](OrdinarySpellAiContext.cs) for the matching captured
state/catalog pair. The ability caller uses `Evaluate(context, request, rng)`;
its request includes the explicit profile. Other query APIs remain unchanged.

For one owned spell's ordinary caller passes in a fresh movement scenario, start
with [OrdinarySpellAiAbilitySearchQuery.Evaluate](OrdinarySpellAiAbilitySearchQuery.cs).
Follow [AiAbilityOutcomeEvaluate](Selection/OrdinarySpellAiAbilityEvaluation.cs)
→ `OriginReachableEvaluate` → `OriginAimTilesEvaluate` → `AiActionSimulateAndScore`.
Unit/tile passes share best and RNG; each rebuilds its remaining aims. The final
baseline gate is diagnostic. See
[ZOD-441](../../../docs/behavior/ordinary-spell-ability-search.md) for exact
caller/reset ownership. List/scenario dispatch, ranked insertion and commands
remain separate.

For one fresh spell pass across cast origins, start with
[OrdinarySpellAiPassSearchQuery.Evaluate](OrdinarySpellAiPassSearchQuery.cs).
It derives fresh aims and movement once, then calls
[OriginReachableEvaluate / OriginAimTilesEvaluate](Simulation/OrdinarySpellAiPassEvaluation.cs)
with shared remaining aims and RNG. Shared simulation restores private units
between hypotheses. Source self-hit consumption, unresolved exits, final-destination
ties and separate baseline admission are documented in
[ZOD-440](../../../docs/behavior/ordinary-spell-pass-search.md).
Cross-pass/list dispatch, lottery and commands remain separate.

For spell simulation at one fresh origin/pass, start with
[OrdinarySpellAiOriginEvaluationQuery.Evaluate](OrdinarySpellAiOriginEvaluationQuery.cs).
It derives the origin internally, creates `ForOrigin`, then follows
`OriginAimTilesEvaluate` → shared `AiActionSimulateAndScore`, ordered movement/
preparation/strikes/clock and strict best comparison. See
[ZOD-439](../../../docs/behavior/ordinary-spell-origin-evaluation.md) for placement
order and baseline admission. This adapter disables cross-origin consumption.

For potential cast origins within one fresh movement scenario, start with
[OrdinarySpellAiOriginQuery.Evaluate](OrdinarySpellAiOriginQuery.cs). Follow
shared `BuildPotentialPasses`, fresh movement `Build`, `AiCastAimTilesBuild`,
priority binding, `OriginDestinationFinalize` and `OriginAimTilesBuild`.
[ZOD-438](../../../docs/behavior/ordinary-spell-cast-origins.md) documents the
unconsumed masks, known-priority boundary and ordering. No moved-origin formula
evaluation or command authority is granted by this query.

For fresh move-only D0/D1, baseline rank and priority-write diagnostics, start
with [OrdinarySpellAiMovementQuery.Evaluate](OrdinarySpellAiMovementQuery.cs).
Follow `DecisionFlagsRefresh`, `ScenariosBuild`, `ReachableTilesScore` and
`RankBaselineSeed`; shared geometry, clocks/scoring and `AiScenarioLayout` own
their existing rules. The certified offensive domain and remaining gates are
in [ZOD-437](../../../docs/behavior/ordinary-spell-movement-baselines.md).
This query does not evaluate spells from moved origins or issue commands.

For one spell's evaluated best at the current origin, start with
[OrdinarySpellAiSearchQuery.Evaluate](OrdinarySpellAiSearchQuery.cs). It freshly
enumerates, narrows each pass's useful mask, simulates in order with one decision
RNG and uses shared strict rank keys. Unsupported work rejects atomically; an
unresolved charge is ranked before stopping only its current origin pass.
See [ZOD-425](../../../docs/behavior/ordinary-spell-aim-ranking.md). This is a
diagnostic, not a complete ability-list decision or command entry point.

For potential aims at the current origin, start with
[OrdinarySpellAiAimQuery.Evaluate](OrdinarySpellAiAimQuery.cs). It freshly binds
one owned slot and calls [OrdinarySpellAiAimEnumeration](Selection/OrdinarySpellAiAimEnumeration.cs)
for working usage, deadline groups and source-ordered unit/tile masks. It returns
diagnostics without RNG, candidate evaluation or command authority. Its current
domain and source mapping are in [ZOD-424](../../../docs/behavior/ordinary-spell-aim-enumeration.md).

Start with [OrdinarySpellAiCandidateQuery.Evaluate](OrdinarySpellAiCandidateQuery.cs)
for one supplied aim at the actor's current origin. It is a pure hypothetical
query, not the complete decision maker and not a command entry point.

Follow these owners in order:

1. [OrdinarySpellAiWork](Simulation/OrdinarySpellAiWork.cs): admit current facts,
   bind the selected slot, initialize lists/parameters and filter useful targets.
   The corresponding rule owners live in `Selection/`.
2. [OrdinarySpellAiActionSimulation.AiActionSimulateAndScore](Simulation/OrdinarySpellAiActionSimulation.cs): capture
   the aim and call Framework's shared primary preparation, privately applying
   its proposal to immutable hypothetical units.
3. [OrdinarySpellAiClock](Simulation/OrdinarySpellAiClock.cs): advance a delayed
   action, or one event after an immediate useful hit. It records movement
   opportunities and retains source event-cap behavior.
4. [OrdinarySpellAiHitSimulation.ActionHitsSimulate](Simulation/OrdinarySpellAiHitSimulation.cs): call
   `StrikesSimulate`, then report whether a valuable target was hit. Strike processing resolves the
   stored aim, call shared area/recipient rules and calculate result frames via
   [OrdinarySpellAiProjection](Simulation/OrdinarySpellAiProjection.cs).
5. [OrdinarySpellAiAccounting](Scoring/OrdinarySpellAiAccounting.cs): exclusions,
   valuable-target gate, average hit and metadata-based priority adjustment.
6. [OrdinarySpellAiScoring](Scoring/OrdinarySpellAiScoring.cs): absolute roster
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
