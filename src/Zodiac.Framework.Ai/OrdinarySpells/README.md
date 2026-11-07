# Ordinary spell AI pathway

Start with [OrdinarySpellAiCandidateQuery.Evaluate](OrdinarySpellAiCandidateQuery.cs)
for one supplied aim at the actor's current origin. It is a pure hypothetical
query, not the complete decision maker and not a command entry point.

Follow these owners in order:

1. [OrdinarySpellAiWork](Simulation/OrdinarySpellAiWork.cs): admit current facts,
   bind the selected slot, initialize lists/parameters and filter useful targets.
   The corresponding rule owners live in `Selection/`.
2. [OrdinarySpellAiSimulation](Simulation/OrdinarySpellAiSimulation.cs): capture
   the aim and call Framework's shared primary preparation, privately applying
   its proposal to immutable hypothetical units.
3. [OrdinarySpellAiClock](Simulation/OrdinarySpellAiClock.cs): advance a delayed
   action, or one event after an immediate useful hit. It records movement
   opportunities and retains source event-cap behavior.
4. [OrdinarySpellAiHitWork](Simulation/OrdinarySpellAiHitWork.cs): resolve the
   stored aim, call shared area/recipient rules and calculate result frames via
   [OrdinarySpellAiProjection](Simulation/OrdinarySpellAiProjection.cs).
5. [OrdinarySpellAiAccounting](Scoring/OrdinarySpellAiAccounting.cs): exclusions,
   valuable-target gate, average hit and metadata-based priority adjustment.
6. [OrdinarySpellAiScoring](Scoring/OrdinarySpellAiScoring.cs): absolute roster
   score. The candidate query returns diagnostics and decision RNG.

For a delayed action the clock precedes the ready strike. For an immediate
action the strike precedes the required clock event. The numbered owners above
identify responsibilities; `OrdinarySpellAiSimulation.Run` owns that ordering.

`Selection/` owns lists, considered parameters, statistics and usefulness.
`Simulation/` owns private work, clocks, strikes and formula projection.
`Scoring/` owns accounting and absolute score. `Diagnostics/` contains the
older ready-hit query whose movement opportunities are explicitly supplied.
All existing public declarations and namespaces are preserved.

For the supported physical roster's complete decision, start with
[AiDecisionMaker.Decide](../Decision/AiDecisionMaker.cs). For actual commands,
start with [AiCommandExecutor.Execute](../Execution/AiCommandExecutor.cs), or
[AiTurnController.Execute](../Execution/AiTurnController.cs) for a complete
admitted turn. Magic aim search, rank insertion/lottery and automatic spell turns
are not connected to those entry points yet. This organization does not enable
them. [ZOD-418 source study](../../../docs/behavior/ordinary-spell-aim-search.md)
defines the next bounded search work.

See the [player/AI pathway guide](../../../docs/development/action-pipelines.md)
for shared rules, source mappings, modification points and current limitations.
