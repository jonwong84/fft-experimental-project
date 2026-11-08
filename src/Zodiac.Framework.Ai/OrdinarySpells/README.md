# Ordinary spell AI pathway

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
