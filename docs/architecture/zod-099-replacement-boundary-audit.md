# ZOD-099: Engine/framework replacement boundary audit

Date: 2026-10-25. Managed baseline:
`4bda424c2f862661f6b8e244517c937a8baec432`.
Implements the bounded audit selected by
[ADR 0006](../adr/0006-replaceable-engine-and-framework-contracts.md).
This is a design specification; all replacement demonstrations below are pending.
No original rule is translated and no new decompilation revision is relied upon.

## Current consumers and coupling

The project references are Framework -> Engine, Content -> Framework, and Client
-> Content + Framework. These references are implementation references today.

- `src/Tessera.Engine/GridPosition.cs`, `GridBounds.cs` and
  `FlatGridMovementRange.cs` define the bounds-only calculation and its values.
  `src/Zodiac.Framework/FlatGridMovementRange.cs` validates the FFT map and
  directly constructs/calls those types. `BattleSimulator.cs` consumes that FFT
  facade. Neither static entry point supports per-instance provider selection.
- `src/Tessera.Engine/IDeterministicRandom.cs` also owns `RandomDraw<TState>`.
  `src/Zodiac.Framework/FftBiosRandom.cs` implements the interface, while
  `AttackBattleSimulator.cs` and `TurnCycle.cs` construct the FFT generator.
  RNG selection is not currently an injection seam. It must not be changed as a
  side effect of the first movement proof.
- `samples/Relay/RelayScenario.cs` uses Engine's concrete grid values/calculation.
  It is linked into `tests/Tessera.Engine.Tests/Tessera.Engine.Tests.csproj` and
  Framework tests, not a standalone sample project. Its invented delivery rules
  demonstrate independence from FFT, but not replacement of Tessera.
- `src/Fft.Client/Presentation/BattleInteraction.cs` consumes
  `TwoUnitBattleDefinition`, nested `TwoUnitBattleState`, command/result types,
  positions and unit IDs. It directly invokes `TwoUnitBattleScenario.Apply` for
  commands and legal-action probes. `src/Zodiac.Framework/TwoUnitBattleScenario.cs`
  owns this bounded lifecycle and exposes implementation snapshots/definitions.
  `Presentation/IntroductionBattleSession.cs` adds typed worker/gate/trace state.
  Wrapping either class in an interface would retain those type dependencies.
- `src/Zodiac.Content/Samples/SyntheticTwoUnitBattle.cs` constructs framework
  maps, units, weapons, RNG and scenario definitions. Content's
  `LooseAssetStore.cs` also uses framework `AssetId`; schemas/projections and
  storage are therefore additional framework replacement concerns, not mere UI
  implementation details. Do not migrate every Content type in the first proof.
- `Directory.Build.targets` protects Engine project references through a narrow
  allowlist. New contracts require a deliberate allowlist update and negative
  reference checks, not deletion of the guard.

## Selected first proof: flat-grid operation

ZOD-100 will introduce `Tessera.Contracts` for this one operation. Its proposed
namespace/API is:

```csharp
namespace Tessera.Contracts;

public readonly record struct GridCell(int X, int Y, int Layer);
// Sealed immutable value with validated positive constructor dimensions,
// get-only Width/Depth/LayerCount and a half-open Contains(GridCell) predicate.
public sealed record GridExtent { /* specified behavior below */ }

public interface IFlatGridRange
{
    IReadOnlySet<GridCell> Calculate(GridExtent bounds, GridCell origin, int move);
}
```

This is a proposed source API, not compilable production code. Distinct value
names allow existing `Tessera.Engine.GridBounds/GridPosition` APIs and historical
tests to remain unchanged. Their adapter performs explicit lossless integer
conversion. Do not move the RNG interface, FFT positions or existing public types
as part of this proof.

`Tessera.Engine` supplies an instance adapter to its existing calculation. A
synthetic alternative uses independently implemented Manhattan membership on
bounded test grids, with wide intermediate arithmetic. Both implement the same
contract. The alternative is verification/sample code, not a new production
engine or an FFT movement implementation.

Add one common sample consumer accepting `IFlatGridRange` through its constructor.
It refers only to Contracts and uses contract values, with no engine-specific
casts or defaults. Two separately built small composition roots instantiate that
unchanged consumer: one references Tessera; the other references only Contracts,
the common consumer and the synthetic alternative. The alternative graph/output
must contain no Tessera.Engine project, assembly or transitive package dependency.
Run from separate fresh output directories to exclude stale copied assemblies.
The contract assembly itself must have no implementation/package references.

This proves one neutral operation and consumer. It does not claim the existing
Zodiac facade, Relay source, full engine or framework is already replaceable.
Their migration requires explicit per-instance construction changes in a later
bounded implementation; do not introduce mutable global provider selection to
preserve a static calling convention.

## Shared engine conformance cases

Use the same tests against both providers, with independent membership expectations:

- Positive extent dimensions; invalid width, depth and layer count rejected in
  that order with the corresponding constructor parameter name. Coordinates are
  signed int32; a layer is an index, not elevation. Extents allocate no tile array.
- Calculation checks null `bounds`, negative `move`, then out-of-bounds `origin`,
  retaining those exception types/parameter names and precedence. Invalid origins
  still fail at zero budget.
- Reachable set is exactly in-bounds cells on the original layer with positive
  Manhattan distance at most `move`. Zero budget and a one-cell board are empty;
  include corners, non-square grids, upper layers and oversized budgets.
- Maximum integer budget on a tiny board and maximum integer dimensions with
  zero/one budget do not overflow. The alternate implementation must restrict its
  search to the reachable clipped interval rather than scan the entire extent.
- No diagonal/layer movement, terrain, obstacles, occupancy or RNG. Same inputs
  produce equal membership; enumeration order is unspecified. Inputs/results
  remain immutable across subsequent calls, and attempted mutable-interface
  writes cannot alter the published result.
- Preserve the existing Engine and FFT facade tests, Relay results and all
  managed regressions. Current prototype behavior is not PS1 movement fidelity.

Existing `tests/Tessera.Engine.Tests/FlatGridMovementRangeTests.cs`,
`GridBoundsTests.cs` and `tests/Zodiac.Framework.Tests/FlatGridMovementRangeTests.cs`
are baseline evidence to preserve, not new conformance executions. The proof
must not change old test expectations to fit its new API.

## Bounded framework candidate and follow-up

ZOD-101 will specify a two-unit command-session contract before implementation.
Its first consumers are a headless journal runner and, subsequently,
`BattleInteraction`; it does not attempt a complete campaign/intro API.
Required capabilities are explicit scenario/profile selection, observation,
Advance/Move/Attack/Wait, non-mutating legal-action queries and fresh restart.
Specify minimal immutable commands, observations, ordered events and rejection
diagnostics in `Zodiac.Contracts`; do not return `TwoUnitBattleState`,
`AttackBattleSnapshot`, mutable sessions' internals or engine values.

Current nested state access must be replaced with explicit observations needed
for unit positions/HP, turn state, outcome, legal actions and presentation.
Determine observable replay/RNG diagnostics separately from persistence of private
state. The session adapter owns implementation state; projection must not consume
live RNG. Rejected commands retain state and produce no effects, while the host
can still journal attempts according to its current policy.

The adapter may reference Zodiac Framework and existing Content to construct the
selected synthetic setup. The common headless consumer must reference neither.
An alternate adapter can demonstrate wiring without claiming source-compatible
FFT rules. Exact command semantics, current rejection precedence, fixture cases,
content translation and save/replay version policy need their own specification
and separately bounded implementation ticket after that audit.

## Versioning, exclusions and completion

The initial engine operation has no persisted format, IDs or RNG. Its extraction
must not change any `fft.*` schema or existing save/replay/profile identity.
Framework replacement must bind explicit supported capability/contract versions
and rules/content profiles; incompatible saves and unsupported capabilities fail
clearly. Original-compatible providers must satisfy existing appropriate vectors,
draw traces and replay evidence. Changing algorithms intentionally requires a
distinct rules profile; matching interfaces is insufficient.

Public contract evolution needs review of all providers/consumers. Do not promise
binary stability before a public compatibility/versioning policy is selected.
Keep formula code readable and FFT-owned, separate calculation from resolution
when combat work justifies it, and avoid executable plugin discovery, universal
contexts, service locators, rendering/audio changes and broad type migration here.

The audit is complete with this specification and the ZOD-100/101 follow-ups.
ZOD-098 can resume with its bounded internal client adapter; neither the larger
framework contract nor the first replacement implementation is a prerequisite.
Documentation/link/whitespace checks are recorded for this change. No production
code, contract assembly or replacement/conformance execution is added; the last
implementation baseline remains ZOD-097's 2,111 .NET / 269 Python tests.
