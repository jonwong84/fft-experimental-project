# Movement boundary behavior note

## Behavior under test

A movement candidate outside the configured map width or depth is not propagated as reachable. The C# command transition therefore rejects a move to an out-of-map destination and leaves battle state unchanged.

## Reference evidence

- Decomp checkout: local `adamrt/fft_decomp`
- Commit: `611b504d25d4a9b03fab8023d7dbea75fe381d5d`
- `src/battle/move_range.c:573-620`, `move_range_build`, repeats spread for each remaining Move point.
- `src/battle/move_range.c:1113-1128`, `move_spread_check_one_tile_around`, computes a neighbor and calls the coordinate check before trying destination propagation.
- `src/battle/move_range.c:924-938`, `move_spread_check_tile_coordinates`, rejects x/y outside configured width and depth.

This supports the bounds invariant only. It does not establish the complete reachable set for an FFT unit; the spread also depends on tile geometry, terrain costs, movement type, unit records, and height rules.

## Managed coverage

- `FlatGridMovementRangeTests.Calculate_returns_only_adjacent_in_bounds_tiles_from_edge` checks the helper's bounded-grid behavior.
- `BattleSimulatorTests.Apply_rejects_destination_outside_map_bounds` checks that the transition rejects an out-of-map request without changing state or emitting events.

The transition test is an invariant-level match to the cited source check, not an executable differential comparison. The remaining flat-grid outputs are still prototype behavior.

The [v1 source-traced fixture description](../fixtures/behavior/map-boundary.source-description.json) records signed coordinate boundary vectors and labels the managed transition expectations separately as synthetic. See the [fixture evidence and comparison rules](fixture-format.md). This description has not been executed by a fixture runner.
