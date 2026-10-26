# ZOD-099: Engine/framework replacement boundary audit

Date: 2026-10-25. Managed baseline:
`4bda424c2f862661f6b8e244517c937a8baec432`.
Implements the bounded audit selected by
[ADR 0006](../adr/0006-replaceable-engine-and-framework-contracts.md).
This is a coupling audit; replacement contract design and demonstrations are deferred.
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

## Deferred candidate: flat-grid operation

The bounds-only operation is small enough to inspect, but that alone does not
justify freezing a replacement API. The earlier proposed interface, duplicate
grid values and contract project are withdrawn from the selected plan following
the owner's timing clarification. No API, assembly name or alternate provider
is selected. ZOD-100 is deferred until a real substitution need justifies a fresh
design; a synthetic second algorithm alone is not that need.

If this operation is later chosen, preserve the existing public APIs and inspect
the real consumers before deciding whether to move values, adapt them or change
construction. A replacement proof must eventually use the same actual consumer
under two compositions without stale/transitive implementation dependencies.
Do not introduce mutable global provider selection for a static calling convention.

## Existing behavior to preserve if this boundary changes

These are observed managed prototype semantics, not a selected new public contract:

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

## Deferred candidate: framework session

The two-unit command session reveals current coupling, but it is too narrow to
determine a stable framework replacement API. A headless journal consumer and
`BattleInteraction` are possible later investigation points, not selected public
contract consumers. ZOD-101 is deferred until a real alternate implementation or
independent consumer makes the required boundary concrete.

A later investigation should identify the commands/observations its consumer
needs, distinguish private execution state from portable values and consider
content translation, rejection order, non-mutating probes, RNG, restart and
save/replay compatibility. Do not project every current nested state type into
a public contract merely because it exists today. An eventual wiring stand-in
would not establish original-game fidelity.

## Versioning, exclusions and completion

The current flat-grid operation has no persisted format, IDs or RNG. A later
extraction must not silently change any `fft.*` schema or save/replay/profile identity.
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

The current-coupling audit is complete; its earlier contract sketch is deferred,
not an approved design. ZOD-100/101 retain future work identities but are not Ready.
ZOD-098 can resume with a local client adapter if actual reuse justifies it;
neither public replacement contracts nor a replacement implementation is a prerequisite.
Documentation/link/whitespace checks are recorded for this change. No production
code, contract assembly or replacement/conformance execution is added; the last
implementation baseline remains ZOD-097's 2,111 .NET / 269 Python tests.
