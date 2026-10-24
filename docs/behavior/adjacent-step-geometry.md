# Ordinary adjacent-step geometry (ZOD-004)

Date: 2026-10-22. Target: North American PS1 `SCUS-94221`. Reference checkout: `C:\repositories\fft_decomp`, HEAD observed with `git rev-parse HEAD`: `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. Every C symbol/path below refers to that revision; line numbers are navigation aids only. This is static, source-derived evidence with `source-traced` confidence under [fixture format v1](fixture-format.md), not an executing C oracle or an original-game observation. No BIN, emulator, capture, or C harness was used.

## Selected rule and implementation recommendation

For an ordinary same-layer cardinal ground-to-ground step, compare the height of the source's exit edge with the destination's facing entry edge. With no water, special movement or steep flag, the geometry gate accepts exactly when their absolute difference in half-height units is at most twice the unit's capped Jump. Equality is accepted in either direction. Slope affects this rule through the facing edge samples, even when whole tile heights are equal; a comparison of whole heights alone is insufficient.

Implement this bounded gate in the FFT-owned terrain policy for ZOD-010, after ZOD-012 establishes explicit policy selection. Keep surface costs, FFT slope encoding, capped Jump and compatibility scope in `Fft.*`. The selected generic flat-grid extraction does not acquire these FFT rules. Add the source-derived vectors below as tests then; this discovery changes no production code or tests.

The bounded supported inputs should be explicit: original flat `0x00` and edge slope encodings `0x25`, `0x52`, `0x58`, `0x85`, half-height 0..3, and effective ordinary Jump 0..7, with half-height no greater than capped Jump for an edge. Flat inputs use half-height zero. Require nonnegative base heights, every sampled edge <=121, unit size 6, and both resolved ceilings `0x7f`; thus all clearance sums fit under the sentinel and no byte overflow occurs. Corner and steep slopes remain unsupported, rather than silently treated as ordinary flat steps. This is a deliberately limited compatibility slice, not a replacement for all original movement.

## Callers and ordering

In `src/battle/action_menus.c`, `state_enter_display_move_area` calls `move_range_build` at approximately line 512 and `state_enter_select_move_tile` calls it at 611. `src/battle/ai_commands.c` also calls it at 498. The authoritative bounded flow is in `src/battle/move_range.c`:

1. `move_range_build` (573) resolves the acting unit via `move_spread_store_unit_to_scratchpad`, builds frontier flags with `move_spread_set_frontier_flags`, and visits queued panels on both layers. The whole original routine is not same-layer only.
2. `move_spread_check_one_tile_around` (1116) loads source height and tries presets in index order east, north, west, south. Ground source indices are 0..3. `move_spread_set_tile_coordinates` and `move_spread_check_tile_coordinates` reject out-of-bounds candidates before source geometry. The source initializer must return zero before destinations are tried.
3. `move_spread_init_source_panel` (953) computes source geometry and clearance; `move_spread_propagate_destination` (1007) is called with `extra_span = 0`. Destination candidate `j & 1` selects layer; `j / 2` selects terrain versus a unit record. Select only `j = source layer` (0 or 1) for this investigation.
4. Destination enterability, destination steep-slope gate and three clearance checks precede the Jump comparison. After it, maximum height delta is merged with the source panel's prior maximum; terrain cost is charged, residual and tie-breaker checks run, then the panel/frontier are updated. A passed geometry gate alone does not imply a returned zero or a legal move endpoint.
5. `move_range_set_tile_flags` (779) filters final destinations separately: among other conditions it excludes high-half-height corners, half-height >=4, Cannot Stop, obstacles, water restrictions, lava restrictions, Untargetable and occupied destinations. A nonzero propagation panel is not sufficient by itself.

The alternate path reconstruction flow uses `move_path_init_source_geometry`, `move_path_init_destination_geometry` and `move_path_update_candidate` (1705 onward). Its candidate checks run in a different order, and its slope sign conventions differ. `move_path_update_candidate` also uses a doubled-Jump limit but returns 7 for that rejection, whereas range propagation returns 9. Do not interchange the two numeric reason sets or generalize their horizontal-span restrictions. The present vectors specify the forward range propagation path.

## Units, widths and effective unit assumptions

`include/fft/battle_map.h`, `battle_map_tile_t` and `battle_map_tile_depth_t` (279, 323): base `height` is `u8` in whole units; `half_height` is a five-bit unsigned slope amplitude, not a boolean half-step flag; depth is three unsigned bits. `slope_type` is `u8`, with two-bit unsigned multipliers for east at shift 0, west at 2, south at 4, north at 6. Named shapes use multipliers 0, 1, 2; arbitrary multiplier 3 is representable but outside this selected slice.

In `move_spread_store_unit_to_scratchpad` (434), a `u8 value` receives `unit->jump`; values >=8 become 7. `jump_half` is computed first for horizontal spans. Fly or Ignore Height subsequently changes value to `0x1f`. `jump_times_two` is `u8 value * 2` (ordinary maximum 14). Thus an effective managed Jump >7 must not silently grant extra ordinary compatibility height. Jump 0 is a valid arithmetic boundary, irrespective of whether ordinary content supplies it.

`move_spread_state_t` (78 onward) stores doubled base heights, source exit/opposite, destination entry/opposite, ceilings and final absolute `height_delta` as `u8`; side differences are `s16`. Coordinates and candidate residual range are `s16`. On the PS1 C model these byte values promote to signed 32-bit `int` for multiplication, subtraction and comparisons, then narrow on byte assignment. The edge formula is assigned to byte storage before computing signed side deltas; absolute differences of the resulting unsigned bytes are 0..255. Threshold comparison is unsigned-byte values promoted to `int`, using `>` to reject. The raw `height * 2` also narrows to a byte before the edge addition, so replacing this with unlimited managed integers changes overflow behavior outside our bounded inputs. These notes describe the source C representation, not independent machine-code validation.

Selected profile: active, ordinary unmounted unit, normal movement class; no Fly, Ignore Height, Teleport, Float ability/status, Chicken/Frog size changes, water movement or other special movement adjustments. `move_spread_store_unit_to_scratchpad` (340..355) sets ordinary `unit_size` to 6 half-height units (three whole units); Chicken/Frog instead uses 4 and Float can add 2. Both chosen tiles are unoccupied terrain; no source/destination unit-record paths are selected. This does not require a particular job, gender or equipment definition: effective Jump is the already resolved unit stat. Deriving it from job/equipment is separate work.

For an ordinary step there is no single tile-center standing height in this gate. Effective source standing height is the sampled exit edge, and effective destination standing height is its facing entry edge. The other-unit compact record `standing_height` and `top_height` fields belong to mounts/standing-on-unit paths and are not used here. Do not substitute `unit_calculate_height_data` output or a tile-center height for these edge samples.

## Formula, orientation and clearance

For tile T and encoded edge multiplier m, define its bounded edge sample `E(T,m) = 2 * T.height + T.half_height * m`, in half-height units. The original stores the intermediate doubled base and result as bytes. Let `hs` be the source exit sample and `hd` the destination entry sample. With depth zero, `delta = abs(hd - hs)` and the selected Jump gate is `delta <= 2 * min(rawJump, 7)`.

Preset shifts are exact source assignments in `move_spread_preset_east/north/west/south` (853..889):

- +X/east: source East (0), destination West (2).
- -X/west: source West (2), destination East (0).
- +Y/north: source North (6), destination South (4).
- -Y/south: source South (4), destination North (6).

These are the decomp's grid names, not a claim about screen up/down. `TileSlope` currently preserves the same packed-byte mapping. When reversing a physical step, swap the tile records AND choose the reversed preset; reusing the former exit edge is incorrect.

`move_spread_calculate_tile_ceiling` (1211) inspects both terrain layers at that coordinate, computes each nonblocked tile's sampled edge minus twice its ceiling depth, and chooses a ceiling strictly above the requested edge height, starting with sentinel `0x7f`. Before Jump, `move_spread_propagate_destination` rejects if destination ceiling < hs + size, source ceiling < hd + size, or destination ceiling < hd + size. Equality is sufficient clearance. A source surface can itself find another slope-side ceiling sample; the vectors below use single supported terrain surfaces, with the other layer blocked, ceiling depth zero and both computed ceilings `0x7f`. Heights are well below that sentinel, and size is 6, so these checks pass. Same-layer movement does not imply that the other layer can be ignored generally.

## Source-derived vectors

All vectors use dry, unoccupied, in-bounds tiles, normal unit size 6, no special flags, no water, no unit record candidates, and `extra_span = 0`. Flat means `(height, half_height, slope)`, slope in hex. Source panel residual is 3, prior maximum height delta 0; destination residual 0 and maximum delta `0xff`; entered surface cost 1. Both ceilings are `0x7f`. Under these initialized scratch preconditions a geometry pass reaches propagation return 0 with destination residual 2; a Jump failure returns 9 before updating that destination. This is a direct routine expectation, not an expected `BattleSimulator` result or event trace. Other candidates/paths in a full map may still reach a tile whose direct step fails.

- `flat-up-at-limit`: east from A=(10,0,00) to B=(13,0,00), Jump 3. hs=20, hd=26, delta=6, threshold=6: accept/return 0.
- `flat-down-at-limit`: reverse the same physical pair, west B to A. hs=26, hd=20, delta=6: accept/0. Drops have the same bound here.
- `flat-up-above-limit`: east A=(10,0,00) to C=(14,0,00), Jump 3. hs=20, hd=28, delta=8: reject/9.
- `flat-down-above-limit`: reverse west C to A. hs=28, hd=20, delta=8: reject/9.
- `half-step-above-limit`: east A=(10,0,00) to D=(13,1,25), Jump 3. D's West multiplier=1, hd=27, hs=20, delta=7: reject/9. Reverse west D to A gives hs=27, hd=20: reject/9. D is non-steep because half-height 1 <= Jump 3.
- `slope-at-limit`: east S=(10,3,52) to A=(10,0,00), Jump 3. S's East multiplier=2, hs=26, hd=20, delta=6: accept/0. Reverse west A to S uses S's East entry=26: accept/0. Equal whole heights do not imply delta zero.
- `slope-entry-at-limit`: east A=(10,0,00) to W=(10,3,58), Jump 3. W's West multiplier=2, hd=26, delta=6: accept/0; reverse west W to A also accepts. The previous example samples a source slope; this samples a destination slope.
- `north-slope-at-limit`: north N=(10,3,85) to A=(10,0,00), Jump 3, hs=26, hd=20: accept/0. Reverse south A to N samples N's North entry=26: accept/0.
- `south-slope-at-limit`: south Q=(10,3,25) to A=(10,0,00), Jump 3, hs=26, hd=20: accept/0. Reverse north A to Q samples Q's South entry=26: accept/0.
- `zero-jump-level`: east A=(10,0,00) to a second flat tile of height 10, Jump 0: delta=0, accept/0. Changing destination height to 11 gives delta=2: reject/9; reversing also rejects.
- `jump-cap`: east (10,0,00) to (17,0,00), raw Jump 8, resolved Jump 7: delta=14, accept/0. Destination height 18 gives delta=16: reject/9; reverse cases respectively accept/reject. Raw Jump 255 also caps to 7, rather than overflowing the doubled threshold.

These expectations are hand-derived from the pinned functions. They have not been run by a fixture runner or executing reference. The future runner's current v1 flat prototype payload does not implement this policy; do not add a fixture claiming it does. A future source-description record can encode named direct inputs and these returns, with source-derived evidence and explicit scratch preconditions.

## Steep/corner evidence and limits

`move_spread_set_frontier_flags` (683..695) identifies corners by `(slope & 5) == 1 || == 4`. Corners are steep when half-height >=3; noncorner slopes are steep when half-height > capped Jump. Before the Jump gate, source geometry rejects when `source_exit - source_opposite >= 0 && STEEP`, while destination geometry rejects when `destination_opposite - destination_entry > 0 && STEEP`. The equality difference is observable source behavior: even a sideways equal-height exit can fail on a steep source, while an equal-height entry to a steep destination passes that particular gate. Do not normalize these predicates into a symmetric generic slope rule.

For example an east-rising edge `(10,4,52)` at Jump 3 is steep: an east exit rejects in source initialization (return 3), and a west exit toward its low side passes that source predicate. Its north/south samples are equal (24), yet either such source exit rejects because equality satisfies `>=0`. Entering this steep tile sideways passes the destination steep predicate (equal opposite and entry samples). This is evidence about intermediate gates only: half-height 4 also fails the final destination filter, and a complete step may fail other gates. These cases are excluded from the recommended first implementation.

## Remaining ambiguity and next validation

- No executing oracle or edition-verified original observation validates these findings independently. A viable next check is a separately recorded source-derived harness with initialized scratch/map data and raw routine outputs, or a verified original/emulator setup with controlled adjacent tiles. The harness must identify its revision/compiler/options; a C# transcription alone is not independent evidence. The game runtime must never depend on it.
- Artificial scratch vectors are not claims about map records present on the user-provided disc. The exact original content distribution and malformed/overflow heights have not been inventoried. Keep this slice's safe numeric envelope explicit; trace byte-wrap cases separately if original content needs them.
- Path reconstruction and AI propagation share data but do not have interchangeable predicates, ordering or return codes. Full route reachability, tie-breaking and reconstruction equivalence remain open.
- Horizontal jump spans, water/depth and submerged/Float standing height, mounts, stepping stones/occupied tiles, corner/steep slopes, interlayer transitions, special movement modes, weather/cost resolution, ceilings as a general mechanic, and endpoint restrictions beyond existing cost/flag policy are excluded. If ZOD-010 needs any of them to support its chosen scenario, expand the trace before implementing, rather than infer a rule.

ZOD-010 can now implement the bounded non-steep dry adjacent edge gate with threshold, reverse-direction, slope-facing and Jump-cap tests, while retaining the exclusions. Source tracing resolves that gate; it does not establish original-game equivalence for the complete movement system.

Implementation update: [ZOD-010's explicit policy](adjacent-step-implementation.md) now implements this bounded gate with source-derived tests and conservative domain validation. The source investigation above remains the provenance for the geometry rule; synthetic transition fixtures do not upgrade it to executable-reference or original-observation evidence.
