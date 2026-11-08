# AI routine ownership

The owner requested classes that follow the routines studied in FFHacktics and
the decompilation. This incremental refactor names the existing decision and
simulation responsibilities with corresponding methods. Source is the clean
read-only fft_decomp revision `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The class-ownership pass started at managed `b92b755`; the subsequent routine
method extraction starts at `641b860`. No new original-game behavior is translated.

## Follow a decision

Start at [AiDecisionMaker.Decide](../../src/Zodiac.Framework.Ai/AiDecisionMaker.cs).
It validates the request, prepares fresh roster/target facts and enters its
`ActionSelect` method. That method names the currently admitted branch of
`turn.c::action_select` and calls
[AiGeneralActionSelection.GeneralSelect](../../src/Zodiac.Framework.Ai/Decision/AiGeneralActionSelection.cs).
Read the supported general-search sequence as:

```text
AiDecisionMaker.Decide
  fresh preparation (including RosterAiRefresh.DecisionFlagsRefresh)
  ActionSelect
    GeneralSelect
      RosterAiGeneralScenarios.ScenariosBuild
        ReachableTilesScore
      AbilityListEvaluate
        AiAbilityEvaluator.AiAbilityOutcomeEvaluate (each occurrence)
          existing supported candidate searches
          AiRankedInsertion.AiBestRankInsert
      AiRankedLottery.RankedSelect
      FallbackSelect (when no ranked action is selected)
  publish successor priority knowledge
```

Each PascalCase routine name maps to the corresponding snake_case symbol in
`src/battle/ai/turn.c`, except `ai_ability_outcome_evaluate`, which lives in
`evaluation.c`. Open
[RosterAiGeneralScenarios](../../src/Zodiac.Framework.Ai/Shared/RosterAiGeneralScenarios.cs)
for `ScenariosBuild` / `ReachableTilesScore`,
[AiAbilityEvaluator](../../src/Zodiac.Framework.Ai/Decision/AiAbilityEvaluator.cs)
for per-occurrence outcome evaluation and insertion,
[AiRankedInsertion](../../src/Zodiac.Framework.Ai/Shared/AiRankedInsertion.cs)
and [AiRankedLottery](../../src/Zodiac.Framework.Ai/Shared/AiRankedLottery.cs)
for their shared source arithmetic.

`GeneralSelect` now calls scenario construction itself, before ability search,
matching the original ownership/order. `AbilityListEvaluate` visits the prepared
occurrences; `AiAbilityOutcomeEvaluate` owns each occurrence's final insertion,
as the source does. List order, eight ranked slots, strict ties, baseline and
fallback behavior and one chained decision RNG are preserved. The unsupported
keep-charging branch is not represented by a pretend success stub.

The existing `RosterAiGeneralScenarios.Evaluate` remains the fresh internal query
adapter: it composes `Prepare` and `ScenariosBuild` and restores its entry RNG on
any failure. `AiDecisionMaker` separately owns atomic rejection across the entire
decision and publishes the workspace only on success. Internal stage results do
not grant authority or become a public cached-preparation API.

`turn.c::action_select` also dispatches status-imposed, retreat and auto-battle
policies before general search. Those policies and keeping an existing charge
are outside the current decision admission and are rejected by the existing
prefix/profile gates. The named `ActionSelect` currently dispatches only to
`GeneralSelect`; adding branches requires implementing those policies first.
Preparation still combines several bounded source responsibilities; this pass
does not claim to reproduce every helper in the top-level command builder.

For an actual command continue into
[AiCommandExecutor](../../src/Zodiac.Framework.Ai/AiCommandExecutor.cs), which
obtains a fresh decision inside Framework's checked transaction and publishes
one typed MOVE, ACT or END. This is the admitted `ai_command_build` boundary.
[AiTurnController](../../src/Zodiac.Framework.Ai/AiTurnController.cs) follows
actual receipts, retained Move continuations and facing to finish the supported
turn. It is a managed controller, not another hypothetical simulation routine.
Its recovery token is separate from the original scanline-budget suspension.

## Follow an ordinary spell hypothesis

The separate ordinary spell query/search enters
[DefaultAbilityAiActionSimulation.AiActionSimulateAndScore](../../src/Zodiac.Framework.Ai/DefaultAbilities/Simulation/DefaultAbilityAiActionSimulation.cs).
This is the bounded counterpart of
`evaluation.c::ai_action_simulate_and_score`, address `0x8019D37C`: privately
prepare the cast, process immediate/delayed hits in their existing order,
advance the clock and score the resulting state. An immediate useful hit
precedes its required clock event; a delayed cast advances to readiness first.

It calls
[DefaultAbilityAiHitSimulation.ActionHitsSimulate](../../src/Zodiac.Framework.Ai/DefaultAbilities/Simulation/DefaultAbilityAiHitSimulation.cs),
the named counterpart of `action_hits_simulate` (`0x8019DCBC`, the wiki's Call AI
Ability Processing). That wrapper calls `StrikesSimulate`, corresponding to
`strikes_simulate` (`0x8019DD8C`, AI Ability Processing), and returns a boolean
valuable-hit result alongside immutable hit diagnostics. Its callers use that
result to reject an unhelpful candidate or continue clock/scoring work.

`StrikesSimulate` owns stored aim/recipient resolution, formula projection,
exclusions/accounting and included hypothetical result application. The existing
optimization skips applying a nonvaluable private result that will be discarded.
`DefaultAbilityAiProjection` and `DefaultAbilityAiAccounting` remain shared
mechanics owners; the latter still computes the fresh accounting average.
Thus this is a bounded structural correspondence, not a literal mutable-memory
transcription. The wrapper's general accounting sentinel, repeated strikes and
recursive reactions are not claimed by this subset. The earlier internal
`Run` / `Resolve` names are removed; public queries retain their signatures.

`DefaultAbilityAiClock` / `AiSimulationClock` retain between-turn simulation;
Framework retains actual preparation, turn-end rules, combat formulas and result
application. Immutable private state isolates hypotheses, while decision RNG
continues across them. It does not reproduce the original mutable baseline and
partial suspended-status save/restore protocol.

## Refactoring boundary

Prefer a class for a meaningful routine responsibility with its own inputs and
results. Keep short arithmetic helpers together when they belong to that owner;
keep shared formulas and combat rules in Framework. Source names/addresses in
remarks and this map aid navigation without introducing raw RAM structures,
one class per MIPS function or an ability-specific formula architecture.

The owner clarified that recognizable source routine names should also be
method names. Use a named method for the corresponding responsibility rather
than leaving that logic embedded in a broader simulator. For example,
[KnockbackRules.KnockbackCalculation](../../src/Zodiac.Framework/CombatRules/KnockbackRules.cs)
now mirrors Knockback Calculation and calls `KnockbackDestinationCheck` for
the currently admitted part of Knockback 2. Both actual physical-ability and
sword settlement call it. Its source address and remaining limits are documented
at the method; trigger RNG and actual movement remain with their source owners.

Public types, namespaces, JSON/content definitions and query contracts remain
unchanged. Routine methods replace the generic internal entry names; no new
class hierarchy or command authority is introduced. Existing source
receipts retain their original paths and hashes at their recorded revisions.
Full magic origin search/dispatch, status policies, reactions, pickups,
terrain/falls/mounts and frame/RNG scheduling remain gated as documented.
ZOD-436 remains open, followed by the eventual ZOD-432 full readability review.

## Validation

Use the existing public decision tapes, command/turn regressions and ordinary
spell query tests to protect behavior, RNG, atomic rejection and private state.
The stored-hit tests follow the renamed internal owner. No tests assert the new
private class layout. Release build passed with zero warnings/errors. All 5,181
.NET tests (including 1,384 AI) and 794 Python tests passed. The 58-entry coverage
inventory validates with its regenerated view. All ten owner drafts, approved
controller formatting and disabled CI configuration retain their prior blobs.
The method extraction was rebuilt and revalidated with the same full totals;
logs/TRX are under ignored `scratch/ai-routine-methods-*` (the earlier class pass
uses `scratch/ai-routine-refactor-*`). Stored-hit regressions now also check the
wrapper's valuable-hit boolean. No new test cases or historical evidence changes
were needed for these behavior-preserving extractions.
