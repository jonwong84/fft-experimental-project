# Framework source layout

Framework owns deterministic FFT rules and runtime state. It references Tessera;
AI policy and orchestration remain in the separate `Zodiac.Framework.Ai` project.
Content supplies immutable definitions and prepared data through outer adapters.

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
  [FaithMagicInputs, MagicResources, MagicCharge and MagicCastQuery](../../docs/behavior/magic-current-inputs-and-casts.md)
  add current immutable stat projection, source resource/timing queries and one-cost
  supplied-order ready-cast results. They do not broaden sword state, discover
  targets, advance the clock or publish battle state.
  `Battle/Combat/Targeting/` contains ordinary spell panel/occupant queries and
  their public geometry, profile, request and result types in matching files.
- `Battle/Turns/`: CT, turn progression, facing, completion receipts and settlement.
- `Battle/Commands/`: invocation-bound decision transactions, frozen follow-ups
  and retained Move continuations.
- `Events/`: deterministic scene/script tasks and event state.
- `Campaign/`: the current world checkpoint model.
- `Random/`: explicit gameplay RNG state and the verified generator.
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
