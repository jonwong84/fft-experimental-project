# Movement input model note

## Scope

The managed core has data shapes for the terrain and effective unit values that the original movement code reads. `TerrainAwareMovementRange` now consumes surface costs and basic tile movement flags for a bounded same-layer, flat-grid propagation slice. `FlatGridMovementRange` and the existing `BattleSimulator` transition remain the earlier simplified prototype and do not yet use the new policy.

## Source trace

ZOD-004 extends this historical input inventory with the [ordinary adjacent-step geometry investigation](adjacent-step-geometry.md) (2026-10-22). It records a newly observed reference HEAD, exact caller/edge-height/Jump arithmetic, bounded accept/reject vectors, slope direction and steep-slope quirks, and the recommended ZOD-010 scope. Its evidence is static source tracing; no executable reference or original-game observation was performed.

Reference snapshot: `adamrt/fft_decomp` commit `611b504d25d4a9b03fab8023d7dbea75fe381d5d`.

- `include/fft/battle_map.h:323-335` describes the eight-byte terrain record. Movement-relevant fields include surface type, whole-unit height, half-height/water depth, slope byte, ceiling depth, and blocked/untargetable/cannot-stop flags.
- `include/fft/unit.h:152-185` defines active movement ability bits in three groups, including ignore-height, Any Ground, Any Weather, water restrictions, fly, float, lava, and water modes.
- `src/battle/move_range.c:290-431` resolves movement inputs, selects a movement/cost-table mode, and creates the per-surface costs. Any Ground changes every non-blocked terrain cost to one; blocked remains blocked.
- `src/battle/move_range.c:434-444` prepares jump-height values and raises the effective jump for flying or ignore-height movement.
- `src/battle/move_range.c:417-430` copies the selected terrain cost table into effective costs, substitutes the resolved weather cost, and applies Any Ground to non-blocked surfaces.
- `src/battle/move_range.c:573-620` spreads for the unit's Move allowance. `src/battle/move_range.c:1002-1105` validates candidate destinations, subtracts the entered tile's surface cost from remaining range, and queues an improved path when range remains.
- `src/battle/move_range.c:696-709` prepares blocked/enterable tile state; `src/battle/move_range.c:812-845` filters final destinations including Cannot Stop and Untargetable tiles.
- `src/battle/move_range.c:924-938` bounds-checks candidate coordinates before tile propagation.
- `include/fft/move.h:84-99` gives the semantic movement-class choices; `move_range.c:367-396` shows their precedence (Fly, Teleport, Float, Lava, Water Surface, Water Depth One, Underwater, Normal).

## Managed representation

- `BattleTile` stores a stable `TerrainSurfaceId`, height, half-height, water depth, four edge slope values, ceiling depth, and movement flags. `TileSlope` can round-trip the source's packed two-bit-per-edge byte.
- `UnitMovementProfile` captures effective Move, Jump, body height, normalized movement mode, traits, and a per-surface movement-cost map. It copies and exposes that map read-only, and it has no fixed 64-surface capacity.
- `BattleUnitState` can carry the effective profile. Its existing integer-Move constructor creates a clearly named flat-grid prototype profile for the transition proof.

The domain values do not impose the original tile field widths on custom content. An importer for original files must still validate source ranges. The model intentionally omits rendering-only shading/camera fields and has not yet defined how jobs, equipment, current statuses, weather, and content definitions resolve into an effective movement profile.

## Terrain propagation proof

`TerrainAwareMovementRange` is a deterministic policy using a priority queue to find the least total entry cost to each same-layer cardinal tile. A destination tile's cost is charged when entering it. It returns tiles whose least accumulated cost is no greater than the profile's Move value, excluding the origin. Tile-level Blocked and blocked surface costs prevent propagation; Cannot Stop and Untargetable prevent a tile from appearing as a final destination, while still allowing propagation through it in this limited model. A surface absent from the effective profile is treated as invalid input and raises an error.

The policy is not a line-by-line port of the original frontier routine. It deliberately excludes elevation/layer transitions, slope and jump checks, water/weather/movement-mode resolution, occupants, mounts, and special surfaces. The tests use synthetic maps and check cost-on-entry, an alternate cheaper route, blockers, destination-filter flags, and invalid profile data. They establish this bounded rule only, not whole-game pathfinding equivalence.

## What this does not prove

The new policy reads terrain surfaces, effective profile costs, Blocked, Cannot Stop, and Untargetable fields. The profile's surface costs are supplied as effective values; weather and ability resolution are not implemented. Original movement also uses unit records, mounts/standing heights, water and ceiling checks, slope directions, jump spans, frontier/path records, and additional map flags. Source-faithful pathfinding and a differential reachable-set fixture remain future work.
