# Movement input model note

## Scope

The managed core now has data shapes for the terrain and effective unit values that the original movement code reads. This is input modeling only: `FlatGridMovementRange` and `BattleSimulator` do not yet consume those fields for terrain-aware pathfinding.

## Source trace

Reference snapshot: `adamrt/fft_decomp` commit `611b504d25d4a9b03fab8023d7dbea75fe381d5d`.

- `include/fft/battle_map.h:323-335` describes the eight-byte terrain record. Movement-relevant fields include surface type, whole-unit height, half-height/water depth, slope byte, ceiling depth, and blocked/untargetable/cannot-stop flags.
- `include/fft/unit.h:152-185` defines active movement ability bits in three groups, including ignore-height, Any Ground, Any Weather, water restrictions, fly, float, lava, and water modes.
- `src/battle/move_range.c:290-431` resolves movement inputs, selects a movement/cost-table mode, and creates the per-surface costs. Any Ground changes every non-blocked terrain cost to one; blocked remains blocked.
- `src/battle/move_range.c:434-444` prepares jump-height values and raises the effective jump for flying or ignore-height movement.
- `include/fft/move.h:84-99` gives the semantic movement-class choices; `move_range.c:367-396` shows their precedence (Fly, Teleport, Float, Lava, Water Surface, Water Depth One, Underwater, Normal).

## Managed representation

- `BattleTile` stores a stable `TerrainSurfaceId`, height, half-height, water depth, four edge slope values, ceiling depth, and movement flags. `TileSlope` can round-trip the source's packed two-bit-per-edge byte.
- `UnitMovementProfile` captures effective Move, Jump, body height, normalized movement mode, traits, and a per-surface movement-cost map. It copies and exposes that map read-only, and it has no fixed 64-surface capacity.
- `BattleUnitState` can carry the effective profile. Its existing integer-Move constructor creates a clearly named flat-grid prototype profile for the transition proof.

The domain values do not impose the original tile field widths on custom content. An importer for original files must still validate source ranges. The model intentionally omits rendering-only shading/camera fields and has not yet defined how jobs, equipment, current statuses, weather, and content definitions resolve into an effective movement profile.

## What this does not prove

No C# movement rule currently reads the new terrain/profile fields. The profile's surface costs are supplied as effective values; weather and ability resolution are not implemented. Original movement also uses unit records, mounts/standing heights, water and ceiling checks, slope directions, jump spans, frontier/path records, and map flags. Source-faithful pathfinding and a source-derived reachable-set fixture remain future work.
