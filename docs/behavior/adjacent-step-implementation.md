# Bounded adjacent-step implementation (ZOD-010)

Date: 2026-10-22. Managed baseline `7af9f8e`. Read-only reference HEAD rechecked before implementation: `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. The [ZOD-004 trace](adjacent-step-geometry.md) identifies source callers, byte widths, edge formulas, orientation, thresholds, steep asymmetry and exclusions. Re-read `move_spread_preset_east/north/west/south`, `move_spread_init_source_panel`, `move_spread_propagate_destination` and Jump preparation at that revision. Evidence is source-derived, not an executing oracle or original observation.

## Selected policy and behavior

`BattleMovementPolicy.OrdinaryAdjacentStepPrototype` explicitly selects the new FFT-owned `OrdinaryAdjacentStepRange`. Existing flat and terrain-cost policies retain their behavior; no existing caller silently gains geometry. The new range uses the same terrain-cost propagation, stopping flags and synthetic occupancy blockers selected in ZOD-012, adding a facing-edge eligibility check to each cardinal edge before charging entry cost. No rule enters Zodiac and no project is renamed.

Facing samples are `2*height + halfHeight*edgeMultiplier`. East uses source East/destination West; west uses source West/destination East; +Y/north uses source North/destination South; -Y/south uses source South/destination North. These names describe source grid axes, not screen direction. The absolute difference must be `<= 2*min(Jump,7)`; equality passes both uphill and downhill. Each adjacent edge is checked, so several individually legal climbs can compose a longer route. No path is reconstructed and no horizontal-span jump is inferred.

The managed implementation uses signed integers only inside a validated domain where all relevant source byte values and clearance sums are representable. It does not replace source byte wrap outside that domain with a claimed generic compatibility rule.

## Explicit supported domain

Before calculation, validate Normal mode, no movement traits, body height 6 and Jump 0..255 (the original effective byte, subsequently capped to 7). Every unblocked tile on the origin layer must be dry with zero ceiling depth; its packed slope must be flat 00 or an edge 25/52/58/85, half-height at most 3 and at most capped Jump, and every edge sample at most 121. Flat slope requires half-height zero. A long intermediate validates large managed heights safely before bounded integer arithmetic.

Every tile on other layers must be blocked. A single-layer managed map represents no overhead terrain; in either case the source ceiling samples resolve to sentinel 0x7f, and sample+size <=127 guarantees the traced clearance gates pass. This validates the whole supplied map, including unreachable unblocked tiles, so unsupported data is deterministic input failure rather than a path-order-dependent approximation. Blocked tile geometry is unused. The origin must be unblocked and in bounds.

Unsupported water, ceilings, corner/steep slopes, other-layer surfaces, special movement, different body size and overflow heights raise `NotSupportedException` naming the unsupported domain rather than being treated as flat or silently unreachable. Occupied endpoints retain the existing early rejection; intermediate occupants are conservative traversal blockers, not original ally/mount/standing-height behavior. The source's steep equality asymmetry is preserved as an explicit exclusion, not flattened into this symmetric non-steep rule. Source unit records, mounts, water standing heights, weather/cost resolution and final original endpoint filters remain unsupported.

## Validation

Source-derived theory vectors cover flat threshold/above-threshold uphill/downhill, the half-step boundary, source/destination facing slopes on all four directions, Jump zero, and raw Jump8/255 capping. Every vector is exercised in both physical directions by swapping records and reversing the neighbor direction. These test the bounded geometry expectation; the surrounding managed state/event contract is synthetic.

Integration cases combine geometry with entry costs/Move and blocked terrain, verify rejected state identity and empty events, preserve legacy modes, test explicit unsupported errors without mutation, and retain occupancy blockers with a blocked second layer. Separate synthetic JSON fixtures replay accepted/rejected transition composition through the fixture runner using policy ID `fft:movement/ordinary-adjacent-step-prototype`. No serialized field shape changes; this is another explicitly supported prototype policy capability in fixture v1.

Targeted geometry tests passed 29 cases on Windows SDK10.0.402. Following review, the runner also handles unsupported-policy exceptions as invalid fixtures, covered by a regression. Final integrated validation passed locked restore, Release build with zero warnings/errors, and all 281 solution tests (123 Core, 60 Content, 22 engine, 76 AssetTool/CLI). No BIN, C runtime, network, emulator or assets are required. Passing source-derived vectors does not establish complete original-game reachable sets or route/tie-break equivalence. Linux CI was not executed locally.
