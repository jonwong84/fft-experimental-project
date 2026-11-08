# AI routine ownership

The owner requested classes that follow the routines studied in FFHacktics and
the decompilation. This incremental refactor names the existing decision and
simulation responsibilities. Source is the clean read-only fft_decomp revision
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed starting point is `b92b755`.
No new original-game behavior is translated by this change.

## Follow a decision

Start at [AiDecisionMaker.Decide](../../src/Zodiac.Framework.Ai/AiDecisionMaker.cs).
It validates the request, prepares fresh roster facts/movement scenarios and
passes them to
[AiGeneralActionSelection.Select](../../src/Zodiac.Framework.Ai/Decision/AiGeneralActionSelection.cs).
Only a successful selection publishes successor priority knowledge. Every
rejected decision returns the entry RNG and the original workspace.

The selector owns the admitted tail of `turn.c::general_select`: evaluate the
prepared ability list, insert each ability's best candidate, run `ranked_select`,
and fall back to the movement baseline. The existing `RosterAiGeneralScenarios`
owns the earlier preparation/scenario work. `AiAbilityEvaluator` dispatches to
the existing supported searches; `AiRankedInsertion` and `AiRankedLottery` own
their independent source rules. List order, eight ranked slots, strict ties,
fallback behavior and one chained decision RNG are preserved.

`turn.c::action_select` also dispatches status-imposed, retreat and auto-battle
policies before general search. Those policies and keeping an existing charge
are outside the current decision admission. Naming the implemented selector
`AiGeneralActionSelection` keeps that distinction explicit.

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
[OrdinarySpellAiActionSimulation.Run](../../src/Zodiac.Framework.Ai/OrdinarySpells/Simulation/OrdinarySpellAiActionSimulation.cs),
formerly `OrdinarySpellAiSimulation`. This is the bounded counterpart of
`evaluation.c::ai_action_simulate_and_score`, address `0x8019D37C`: privately
prepare the cast, process immediate/delayed hits in their existing order,
advance the clock and score the resulting state. An immediate useful hit
precedes its required clock event; a delayed cast advances to readiness first.

It calls
[OrdinarySpellAiHitSimulation.Resolve](../../src/Zodiac.Framework.Ai/OrdinarySpells/Simulation/OrdinarySpellAiHitSimulation.cs),
formerly `OrdinarySpellAiHitWork`. This owns the admitted combination of
`action_hits_simulate` (`0x8019DCBC`, the wiki's Call AI Ability Processing)
and `strikes_simulate` (`0x8019DD8C`, AI Ability Processing). It resolves the
stored aim and recipients, calls shared formula/resource projection, applies
strike exclusions/accounting and publishes included hypothetical results.
`OrdinarySpellAiProjection` and `OrdinarySpellAiAccounting` remain separate
mechanics owners; an additional forwarding class for each original call adds
no ownership today. The wrapper's general accounting sentinel, repeated
strikes and recursive reactions are not claimed by this subset.

`OrdinarySpellAiClock` / `AiSimulationClock` retain between-turn simulation;
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

Public types, namespaces, JSON/content definitions and query contracts remain
unchanged. Only the two internal spell owners are renamed. Existing source
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
Logs/TRX are under ignored `scratch/ai-routine-refactor-*`. No new tests or
historical evidence changes were needed for this behavior-preserving extraction.
