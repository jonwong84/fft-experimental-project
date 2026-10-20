# Headless battle transition proof of concept

## Scope and status

This is an architecture proof, not an FFT movement port. `BattleState` is an immutable snapshot of a map and a small set of unit instances. `MoveUnitCommand` is validated by `BattleSimulator.Apply`, which returns either the unchanged state with a typed rejection or a new state with one `BattleMovementEvent`. A successful move marks that unit as having moved once in this proof model.

Movement reachability currently calls `FlatGridMovementRange`. That helper models an unobstructed, cardinal, same-layer grid. The simulator additionally rejects movement into a tile occupied by another unit. `BattleTile` and `UnitMovementProfile` now capture several movement-relevant inputs, but the flat-grid helper does not use them. These are deliberate prototype rules and are not claimed to match the original game.

## Reference trace

Reference: local `adamrt/fft_decomp` snapshot `611b504d25d4a9b03fab8023d7dbea75fe381d5d` (observed 2026-10-20).

- `src/battle/move_range.c:573-620`, `move_range_build`, spreads reachable movement one pass per remaining Move point; it special-cases Teleport and uses frontier state across both terrain layers.
- `src/battle/move_range.c:778-815`, `move_range_set_tile_flags`, filters candidate destinations using height/half-height, corner, remaining range, cannot-stop and other state; it clears enterable flags under dead or jumping units first.
- `src/battle/move_range.c:1108-1129`, `move_spread_check_one_tile_around`, uses directional spread and ground/unit standing-height records, rather than a simple two-dimensional cardinal flood fill.
- `src/battle/move_range.c:924-938`, `move_spread_check_tile_coordinates`, bounds-checks candidate x/y against configured terrain width/depth.
- `src/battle/move_range.c:1412-1435`, movement-path setup checks cached range validity and unit identity, then examines unit occupancy and mountability.

These functions depend on terrain records, movement type, unit state, frontier/path records, and globals. Porting only the outer loop would not reproduce their behavior. The current `BattleSimulator` therefore demonstrates state-transition boundaries and rejection semantics only. It does not claim source-derived output vectors.

One narrow invariant is independently traced: candidates outside map bounds are rejected before destination propagation. The C# transition has a regression for this invariant in [`behavior/movement-boundary.md`](behavior/movement-boundary.md); this does not validate the prototype's full reachable set.

## Proof acceptance checks

- Battle state defensively owns a stable list of distinct unit IDs and validates initial positions against its immutable map.
- Applying a legal prototype move returns a new state; the original remains unchanged.
- Applying an illegal command returns the original state and no events, with a typed rejection reason.
- Replaying the same command against equivalent initial states produces equal unit state and event output.
- Tests run headlessly and need no game assets, BIN, emulator, clock, or platform service.

## Known prototype limits

- The simulator's flat-grid helper does not consume terrain height, slope, half-height, ceiling, surface, flags, or effective unit movement inputs. The separate `TerrainAwareMovementRange` uses surface costs and a small subset of flags only; it is not integrated into the simulator.
- No jump, fly, teleport, ride/mount, status, dead/jumping-unit, or movement-class rules are implemented.
- No path choice or path reconstruction, action phase, turn order, CT, RNG, damage, or campaign state.
- A unit may move once in this proof model; it does not yet model all turn-phase choices or movement abilities.
- Occupied destinations are rejected; original mount/standing-height behavior is not represented.
- No equality or serialization contract is defined for a complete battle snapshot.

## Next fidelity slice

Keep this command/state boundary only if it remains useful after the movement model becomes richer. The modeled fields and their source mapping are recorded in [`behavior/movement-inputs.md`](behavior/movement-inputs.md). A first terrain-cost propagation policy now exists separately with synthetic tests. Next, trace a concrete height/slope/jump eligibility case and add it as a separate focused rule/fixture. Do not extend the flat-grid helper to appear more faithful without tracing the relevant source paths and data first.
