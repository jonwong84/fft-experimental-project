# ZOD-001: Current ownership and first engine slice

Date: 2026-10-22. Audit baseline: repository commit `a262940125fc04d9275c5153c341441b6c23ae75`; working tree clean before this task. Read together with [ADR 0004](../adr/0004-zodiac-engine-and-fft-game-boundary.md) and the [overview](overview.md). This is a discovery recommendation, not an implemented boundary or an acceptance of the still-proposed ADR.

Reference checkout HEAD observed read-only: `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. No new original-game rule was traced here. FFT interpretations below refer to the existing [movement input analysis](../behavior/movement-inputs.md) and [transition proof](../battle-transition-poc.md), whose historical source revision matches that observed HEAD. Managed source and synthetic tests establish the extraction recommendation; they do not establish PS1 equivalence.

## Decision

Extract only the unobstructed, bounded, same-layer cardinal range calculation, with neutral integer position and bounds contracts. Keep the FFT map, tile records, movement profiles, and battle transition in `Zodiac.Framework`. Retain an FFT-owned `FlatGridMovementRange` facade that translates the existing map and positions to the engine inputs and translates the result back. This performs useful work for the existing command transition without requiring a general battle framework.

The neutral description is: given positive rectangular dimensions, a valid position including a layer index, and a nonnegative step budget, return all other positions on that layer reachable by at most that many cardinal unit-cost steps. No terrain vocabulary, unit identity, game asset, or turn policy is needed.

## Actual dependency and consumer evidence

All links below refer to the audited tree; filenames and symbols, rather than moving line numbers, identify evidence.

- [Core project](../../src/Zodiac.Framework/Zodiac.Framework.csproj) has no project references. It uses managed collections and argument validation; the audited simulation contains no file, presentation, clock, random, or C-reference calls.
- [Content project](../../src/Zodiac.Content/Zodiac.Content.csproj) references Core. Its [manifest and entry](../../src/Zodiac.Content/PreparedAssetManifest.cs) and [loose store](../../src/Zodiac.Content/LooseAssetStore.cs) consume only `AssetId` from Core, not battle/map types.
- [Client project](../../src/Fft.Client/Fft.Client.csproj) references Core and Content. [Program](../../src/Fft.Client/Program.cs) only constructs `AssetId` and loads prepared bytes; it does not run the battle proof.
- [AssetTool](../../src/Zodiac.AssetTool/Zodiac.AssetTool.csproj) has no Core reference or Core type consumers. [CLI](../../src/Zodiac.AssetTool.Cli/Zodiac.AssetTool.Cli.csproj) references AssetTool and Content; [ExtractCommand](../../src/Zodiac.AssetTool.Cli/ExtractCommand.cs) consumes Core's `AssetId` through the transitive Content reference when creating manifest entries. Its other commands do not consume the map/movement models.
- [Core tests](../../tests/Zodiac.Framework.Tests/Zodiac.Framework.Tests.csproj) directly reference Core. [Content tests](../../tests/Zodiac.Content.Tests/PreparedAssetManifestTests.cs) consume `AssetId` through Content's reference. AssetTool and CLI tests do not directly consume Core types; CLI extraction tests exercise the manifest workflow indirectly.

Consequently every battle/map/movement type currently has production consumers only inside Core. The range calculation reaches the client neither directly nor through a playable loop. Moving it proves a headless seam, not a completed game engine.

## Complete Core ownership map

There are 21 declared types in 20 source files, including the rejection enum colocated with its result. **Reusable candidate** means its invariant can be neutral, not that the current file should move unchanged. **FFT-owned** includes the current FFT prototype policy. **Unresolved** means the current representation is mixed and a broader contract lacks evidence. Only the range kernel and neutral supporting values are selected now.

### Reusable candidates

- [AssetId](../../src/Zodiac.Framework/AssetId.cs): namespaced lowercase ASCII identifier independent of storage paths. Used by `TerrainSurfaceId`, Content, CLI extraction, client and [AssetIdTests](../../tests/Zodiac.Framework.Tests/AssetIdTests.cs). Its concrete manifest use supports reuse, but no engine asset service needs it in this slice. Leave it in Core; preserve grammar and default-value validation at consumers.
- [TilePosition](../../src/Zodiac.Framework/TilePosition.cs): three integer coordinates, with layer distinct from height; no dependencies. Consumed throughout map, state, commands, events and both algorithms. [TilePositionTests](../../tests/Zodiac.Framework.Tests/TilePositionTests.cs) cover value equality. A neutral equivalent can represent rectangular layers; retain the existing FFT type initially to avoid migrating all consumers.
- [BattleUnitId](../../src/Zodiac.Framework/BattleUnitId.cs): nonblank string instance identity; used by state lookup, commands and events. No original numeric encoding. Candidate for future runtime identity, but no identity is needed by range calculation. Keep it in Core; default struct values remain possible and are rejected by unit construction.
- [FlatGridMovementRange](../../src/Zodiac.Framework/FlatGridMovementRange.cs): BFS depends on `BattleMapDefinition` only for dimensions and origin validation, and on `TilePosition` for coordinates. Called by `BattleSimulator` and [FlatGridMovementRangeTests](../../tests/Zodiac.Framework.Tests/FlatGridMovementRangeTests.cs). Select its calculation, not its FFT map parameter, for extraction.
- [TerrainMovementCost](../../src/Zodiac.Framework/TerrainMovementCost.cs): positive traversable entry cost or blocked marker; used by profiles and terrain range. This invariant can be neutral, but the selected unweighted algorithm does not need it. Keep it in Core until a weighted contract is justified; default zero/unblocked is invalid and profiles reject it.
- [MoveUnitCommand](../../src/Zodiac.Framework/MoveUnitCommand.cs): unit ID plus destination, consumed by simulator and its tests. Neutral intent is plausible, but current parameter types tie it to FFT. Defer moving it until an engine transition actually needs it.
- [BattleMovementEvent](../../src/Zodiac.Framework/BattleMovementEvent.cs): accepted unit/from/to fact, produced by simulator through result and consumed by simulator tests. Same dependency and deferral as the command; no event bus is justified.

### FFT-owned

- [BattleTile](../../src/Zodiac.Framework/BattleTile.cs): surface, whole/half height, water, encoded slope, ceiling and flags; consumed by map, terrain calculation, and [map tests](../../tests/Zodiac.Framework.Tests/BattleMapDefinitionTests.cs)/[input tests](../../tests/Zodiac.Framework.Tests/MovementInputModelTests.cs). These fields combine FFT terrain interpretations and validation; a generic-looking tile is not a neutral engine contract.
- [BattleTileMovementFlags](../../src/Zodiac.Framework/BattleTileMovementFlags.cs): consumed by tile validation/accessors and terrain destination filtering. The separation of propagation from stopping for Untargetable/CannotStop is game policy, not a universal flag taxonomy.
- [TileSlope](../../src/Zodiac.Framework/TileSlope.cs): two-bit edge values and packed-byte bridge consumed by `BattleTile`; input tests round-trip all 256 bytes. The encoding belongs to FFT import/compatibility, not engine geometry.
- [TerrainSurfaceId](../../src/Zodiac.Framework/TerrainSurfaceId.cs): wraps `AssetId`, includes `base:terrain/unclassified`; consumed by tiles and profile cost dictionaries. Stable syntax does not generalize the surface vocabulary. Keep FFT ownership of identifiers and their meaning.
- [UnitMovementMode](../../src/Zodiac.Framework/UnitMovementMode.cs): normalized FFT movement classes consumed/validated by profiles, not implemented by either range algorithm.
- [UnitMovementTraits](../../src/Zodiac.Framework/UnitMovementTraits.cs): ability-derived flags, including TeleportPlusTwo and water restrictions, consumed/validated by profiles. Keep resolution and precedence game-owned.
- [UnitMovementProfile](../../src/Zodiac.Framework/UnitMovementProfile.cs): effective costs, Move, Jump, body height, mode and traits; consumed by unit state and terrain range. Owns the prototype factory with body height 6. Immutable copying and equality are useful design patterns, but the actual vocabulary and factory are FFT-specific. Input tests cover copying, invalid costs and attachment to unit state.
- [TerrainAwareMovementRange](../../src/Zodiac.Framework/TerrainAwareMovementRange.cs): consumes FFT map/profile and interprets flags, surface costs, missing costs and stopping eligibility. [TerrainAwareMovementRangeTests](../../tests/Zodiac.Framework.Tests/TerrainAwareMovementRangeTests.cs) cover synthetic weighted routes, flags, blockers and missing costs. Keep it separate and unintegrated in this extraction. A future neutral weighted search could receive game-resolved edges/stopping eligibility, but that API is not required now.
- [BattleSimulator](../../src/Zodiac.Framework/BattleSimulator.cs): owns the current command rejection order, once-moved rule, destination occupancy check, flat policy selection, state replacement and one movement event. Consumed only by [BattleSimulatorTests](../../tests/Zodiac.Framework.Tests/BattleSimulatorTests.cs). Retain as FFT prototype orchestration; none of these choices is established as a universal engine or PS1 turn policy.
- [BattleTransitionRejection](../../src/Zodiac.Framework/BattleTransitionResult.cs): current command-policy outcomes consumed by simulator/result/tests. Preserve values and precedence on the FFT side; no generic error taxonomy is needed.

### Unresolved; retain in Core

- [BattleMapDefinition](../../src/Zodiac.Framework/BattleMapDefinition.cs): neutral checked dimensions, defensive copy and layer-major lookup are coupled to FFT `BattleTile`. Consumed by state, both ranges and map tests. Do not move the class wholesale or introduce a generic tile container for this task. Extract only a bounds value needed by range calculation; retain storage/indexing and tiles.
- [BattleUnitState](../../src/Zodiac.Framework/BattleUnitState.cs): identity/position coexist with FFT movement profile and prototype HasMoved. Used by `BattleState`, simulator and input/simulator tests. No evidence yet for a reusable complete unit-state shape.
- [BattleState](../../src/Zodiac.Framework/BattleState.cs): immutable ordered unit list and ID lookup depend on the above map/unit types. Constructor checks bounds and duplicate IDs, but permits overlapping initial units. Used by simulator/result/tests. Preserve these validations without inventing a general world/ECS contract.
- [BattleTransitionResult](../../src/Zodiac.Framework/BattleTransitionResult.cs): immutable accepted/rejected result carries FFT state, rejection enum and movement-only events. Used by simulator/tests. A neutral transition pattern is plausible; this concrete result is not reusable independently of those types.

## Proposed smallest contract and FFT adapter

Names here are design proposals for ZOD-007, not newly introduced APIs. In `Tessera.Engine`, use a small immutable rectangular-bounds value (positive width/depth/layer count), an immutable integer grid position (X/Y/layer), and a static flat cardinal range calculation accepting bounds, origin and integer budget and returning a read-only set of positions. Keep the layer coordinate so the existing two-layer proof retains its identity while forbidding layer transitions. No map tile allocation, asset ID, unit state, interface hierarchy, RNG, I/O or callback is needed.

The engine validates nonnegative budget and in-bounds origin, excludes origin even for positive budgets, and returns empty for zero. It preserves the existing BFS neighbor visit order (west, east, north, south) internally. The result contract is set membership only: FrozenSet enumeration and queue tie order are not replay/event ordering guarantees. Do not expose paths or pick a canonical path as part of the move.

The FFT facade keeps its existing signature and null-map/negative-budget/origin validation behavior and order. It creates bounds from `BattleMapDefinition` dimensions, translates `TilePosition` values without changing layer or interpreting height, invokes Zodiac, then returns the converted immutable set. It must not read tile flags or profiles. `BattleSimulator.Apply` continues calling this facade: no FFT type enters the engine signature. This adapter is pure mapping, with no file or asset preparation step.

ZOD-007 adds the engine calculation and independent synthetic tests. ZOD-008 connects the existing facade to it and retains FFT regressions. Core then references Zodiac; Content, client and tools retain their current references. Zodiac references no FFT project. Do not move `AssetId`, rename existing projects/namespaces, or replace the terrain-aware calculation during either step. ZOD-009 adds the neutral sample and forbidden-reference check.

## Preservation and verification requirements for the extraction

- Preserve rectangular clipping, same-layer membership, origin exclusion, budget validation and immutable output. Existing flat-range tests cover center/edge cases and invalid inputs. Add independent engine cases for zero budget, non-square bounds, multiple layers and oversized budget; use synthetic positions without allocating FFT tiles.
- Preserve simulator validation order: unknown unit, already moved, other unit at destination, then reachability. Rejections retain the exact input state reference and no events. Success replaces only the selected unit in list order, keeps its profile and map, sets HasMoved, and emits exactly one event with original and destination positions. Existing simulator tests cover individual rejections, accepted immutability and equivalent-state replay; add combined-failure precedence checks when adapting.
- Preserve quirks currently untested: flat movement ignores blocked/untargetable/cannot-stop tiles, costs and geometry; occupied intermediate tiles do not block traversal; moving to the origin rejects; overlapping starting units are allowed by state construction. Add focused characterization coverage during ZOD-008, without presenting these as source-faithful FFT behavior.
- Keep the terrain proof's checked cost addition, missing-cost exception, positive costs, cost-on-entry and pass-through/non-stopping distinctions unchanged. Do not route the transition through it until ZOD-012 explicitly selects that policy.

## Alternatives, limits and unresolved work

Moving all of Core would import FFT terrain encodings and profiles into Zodiac. Moving only identifiers would create no useful simulation slice. Extracting weighted search now would require a new edge/stop policy and could silently replace the currently selected flat behavior. The unweighted bounds-only seam is supported by actual data reads and an existing command consumer, so no additional original-game evidence is needed for this prototype extraction.

This is deliberately modest: it demonstrates reusable reachability, not a reusable complete battle transition. A future weighted boundary needs evidence for directional geometry, layer edges, occupancy/mounts, stopping rules and special movement, including whether edge costs depend on the unit and direction. ZOD-004 and ZOD-010 own the next source-traced geometry work; ZOD-012 owns policy integration. CT, RNG, campaign and broad state contracts remain outside this recommendation.

Keep explicit compatibility selection in FFT. The flat prototype must remain labeled as synthetic; the terrain proof is only a bounded policy. Neither is a replacement specification for original movement. Fixture provenance and stable replay comparisons remain ZOD-002/ZOD-011 work; object hash codes and collection iteration must not become persistent replay contracts.

Documentation validation: inventory checked against all declared Core types and repository-wide consumers/project references. No production code or project files changed; no behavior tests are added or run for this documentation-only task.
