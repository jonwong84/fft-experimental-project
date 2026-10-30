# ZOD-008: FFT flat movement adapter

Date: 2026-10-22. Managed baseline: `fab0079`. This integrates the managed prototype selected by [ZOD-001](zod-001-type-ownership.md); it translates no new PS1 rule and makes no source/original-game equivalence claim.

`Zodiac.Framework` now references `Tessera.Engine`. Its existing `FlatGridMovementRange.Calculate` facade preserves the FFT signature and validation order (null map, negative Move, invalid origin), constructs neutral bounds/position inputs, invokes Zodiac's flat cardinal calculation, and converts the result to an immutable set of FFT positions. Bounds, budget, X/Y and layer retain their integer values. The facade selects the flat prototype policy explicitly; no tile semantics or FFT types enter the engine. Result enumeration is not an ordering contract.

`BattleSimulator` still calls the FFT facade. It retains rejection precedence (unknown unit, already moved, destination occupied, unreachable), original state reference and zero events on rejection, and one ordered movement event on acceptance. The updated snapshot preserves map, unit list order, other units and effective profile while moving the selected unit and setting HasMoved. Existing replay and immutability tests remain in place.

Additional synthetic regressions cover combined rejection conditions, rectangular upper-layer translation, zero range, immutable output, origin exclusion, overlapping initial positions, ignored terrain flags/geometry/missing costs, and traversal through an occupied intermediate tile. These preserve observable prototype behavior; they do not endorse it as FFT compatibility behavior. `TerrainAwareMovementRange`, FFT profiles, tiles and the command/state/event types remain game-owned and unchanged. Terrain-policy integration remains ZOD-012. No project or namespace was renamed.

The project graph is acyclic: FFT callers depend on Core, Core depends on Zodiac, and Zodiac has no project references. The automated forbidden-reference guard and neutral sample remain ZOD-009. The client remains a prepared-asset console proof, not an interactive battle client.

Adding the project dependency required regenerating affected lock files. Locked restore exposed committed inline versions remaining in Core, Content and Zodiac test projects; those references now use the existing central package versions without upgrades. Reviewed lock changes add project dependency records only; package versions remain unchanged.

Validation on Windows SDK 10.0.402: locked solution restore passed; Release build passed with zero warnings/errors; all 162 tests passed (59 Core, 21 engine, 82 other FFT). Commands: `dotnet restore Fft.sln --locked-mode`, `dotnet build Fft.sln -c Release --no-restore`, `dotnet test Fft.sln -c Release --no-build --no-restore`. Linux CI and remote Sonar were not run locally. No BIN, C runtime, network oracle, renderer or emulator is required by the adapter/tests.
