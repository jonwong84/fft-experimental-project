# Framework source layout

Framework owns deterministic FFT rules and runtime state. It references Tessera;
AI policy and orchestration remain in the separate `Zodiac.Framework.Ai` project.
Content supplies immutable definitions and prepared data through outer adapters.

For a player action or AI decision, start with the
[action pathway guide](../../docs/development/action-pipelines.md). Ordinary
spell selection/commands enter through `DefaultAbilityBattle.Choice` / `Apply`;
the cast branch calls `DefaultAbilityCastPreparation.PrepareAndQueue`.
The [targeting source map](Battle/Combat/Targeting/README.md) links selection,
geometry and readiness resolution in execution order. Shared rules remain here;
AI choice/ranking remain in the AI project.

- `Battle/State/`: snapshots, map/unit geometry and identifiers, and the bounded
  two-unit scenario wrapper.
- `Battle/Movement/`: terrain and movement profiles, range/path algorithms,
  movement commands/results, and direct projectile trajectory geometry.
- `Battle/Combat/`: attacks, ability resolution, formulas, evasion and bounded
  combat profiles. Actual action resolution remains separate from AI hypotheses.
  `Battle/Combat/Magic/` contains [shared raw Faith/magic amount arithmetic](../../docs/behavior/faith-magic-amounts.md),
  and [stored element/result routing](../../docs/behavior/magic-elements-and-routing.md),
  plus [conditional-proc decisions](../../docs/behavior/magic-conditional-proc.md).
  [FaithMagicFormula.ResolveAfterHit](../../docs/development/shared-magic-calculation.md)
  is the shared already-hit calculation entry through bounded finalization.
  [FaithMagicFormula.Resolve and MagicEvasion](../../docs/behavior/magic-evasion.md)
  add certified nonzero-ability entry with ordered magic evade work. Both return
  pure result data without spell commands or live publication; targeting remains separate.
  The [shared-consumer review](../../docs/behavior/ordinary-magic-pipeline-review.md)
  maps damage/healing modifiers, evasion, element routing and result consumers
  to named source routine methods and their retained bounds.
  [FaithMagicInputs, MagicResources, MagicCharge and MagicCastQuery](../../docs/behavior/magic-current-inputs-and-casts.md)
  add current immutable stat projection, source resource/timing queries and one-cost
  supplied-order ready-cast results. They do not broaden sword state, discover
  targets, advance the clock or publish battle state.
  `Battle/Combat/Targeting/` contains ordinary spell panel/occupant queries and
  their public geometry, profile, request and result types in matching files,
  grouped into `Selection/`, `Geometry/` and `Resolution/`.
  [Stored aims and pure casting phases](../../docs/behavior/stored-spell-aim-pipeline.md)
  add current unit-follow centres, blue choice/confirmation and immutable charge
  plans through ready one-cost calculations. They publish no battle state or RNG,
  create no commands and advance no global clock.
  `Battle/Combat/Magic/Publication/` owns private living vital/RNG projections.
  `Battle/Combat/Rewards/` owns [outcome markers and action reward calculations](../../docs/behavior/action-outcomes-and-rewards.md),
  including EXP maxima, JP spillover and explicit growth/unlock work still required
  before adopting a complete career state. These helpers publish no battle state.
- `Battle/Turns/`: CT, turn progression, facing, completion receipts and settlement.
- `Battle/Progression/`: [raw battle growth and job unlock calculations](../../docs/behavior/growth-and-job-unlocks.md),
  with explicit five-draw RNG continuation and immutable stable-ID prerequisites.
  Derived/status refresh, career adoption and original packed adapters remain separate.
  [Mode1 and equipment numeric projections](../../docs/behavior/mode1-derived-attributes.md)
  share raw-to-maxima arithmetic and retain base PA/MA/Speed; armor bonuses precede
  current HP/MP clamps. Complete status/equipment refresh is still separate.
- `Battle/Commands/`: invocation-bound decision transactions, frozen follow-ups
  and retained Move continuations.
- `Events/`: deterministic scene/script tasks and event state.
- `Campaign/`: the current world checkpoint model.
- `Random/`: explicit gameplay RNG state and the verified generator.
- `Formulas/`: selected physical formula IDs/X/Y and neutral raw arithmetic.
- `CombatRules/`: shared physical execution/preview/AI evasion, RNG, critical/
  knockback decisions, final cap and ordinary primary preparation.
  [KnockbackRules.KnockbackCalculation](CombatRules/KnockbackRules.cs) mirrors
  the named source routine for admitted flat-ground landings; its destination
  check is separate from trigger RNG and actual movement publication.
  `PhysicalAbilityBattleProfile` / `PhysicalAbilityBattleSimulator` own shared
  delivery admission and settlement. [Delivery and limits](../../docs/architecture/physical-delivery-profiles.md).
- `Properties/`: assembly metadata. `AssetId.cs` stays at the root as a shared ID.

Public classes, records, structs and enums should normally have their own
matching `.cs` file within the relevant feature folder. Small implementation-only
helpers may remain with their owner when that improves readability. Apply this
layout to new code and focused cleanup. Framework's namespace-level public types
now follow it; existing partial types retain clearly named `.PlannedMove.cs`
parts. The [Framework pass](../../docs/development/framework-public-type-files.md)
extends the earlier [targeting split](../../docs/development/targeting-file-layout.md)
without changing declarations or namespaces.

These folders describe existing ownership; they introduce no dependency or new
abstraction. Small internal helpers may remain with their related public owner.
Battle namespaces remain `Zodiac.Framework`; Events, Campaign and Random retain
their existing namespaces. SDK recursive compilation needs no project-file edits.
Tests mirror these folders; reusable fixture runners stay in `Fixtures/` and the
cross-suite `RosterCombatTestData` helper lives under `Battle/Combat/`.

See the [move and validation note](../../docs/development/framework-source-layout.md).
Current documentation and coverage follow this layout. Historical evidence JSON and
archived planning retain paths at their original managed revisions. Namespace
changes and gameplay expansion are separate work.
