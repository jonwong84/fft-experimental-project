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
  without spell commands or live state publication.
- `Battle/Turns/`: CT, turn progression, facing, completion receipts and settlement.
- `Battle/Commands/`: invocation-bound decision transactions, frozen follow-ups
  and retained Move continuations.
- `Events/`: deterministic scene/script tasks and event state.
- `Campaign/`: the current world checkpoint model.
- `Random/`: explicit gameplay RNG state and the verified generator.
- `Properties/`: assembly metadata. `AssetId.cs` stays at the root as a shared ID.

These folders describe existing ownership; they introduce no dependency or new
abstraction. Files containing closely related records and rules stay together.
Battle namespaces remain `Zodiac.Framework`; Events, Campaign and Random retain
their existing namespaces. SDK recursive compilation needs no project-file edits.
Tests mirror these folders; reusable fixture runners stay in `Fixtures/` and the
cross-suite `RosterCombatTestData` helper lives under `Battle/Combat/`.

See the [move and validation note](../../docs/development/framework-source-layout.md).
Current documentation and coverage follow this layout. Historical evidence JSON and
archived planning retain paths at their original managed revisions. Namespace
changes, class splitting and gameplay expansion are separate work.
