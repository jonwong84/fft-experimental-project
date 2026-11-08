# Knockback calculation — supplied source cross-check

This shared helper is reached from critical hits and formula0x37's successful
Brave comparison. It stages knockback work; later resolution owns live movement
and any fall damage. This is source evidence, not a general terrain/fall port.

All 137 supplied instruction words at `0x801843ec`–`0x8018460c`, the two calls
and both listed returns (`0x80186558`, `0x801869dc`) match prepared BATTLE.BIN.
The [trace receipt](../evidence/combat/knockback-calculation-trace-v1.json) pins
clean reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, the supplied-text hash,
prepared bytes, field layouts and three relevant source functions.
The original/C code was not executed.

## Eligibility and direction

The helper rejects unless the working target counter is exactly 1. It also
rejects immortal target bit `0x04`, importance bits `0x03` equal to 3, target and
actor pointers being equal, or **any nonzero mount byte**. The importance rule
is generic: there is no Ramza/character-ID test here. Distinct units at the same
X/Y also reject, even if they occupy different layers.

For distinct coordinates, one square is selected away from the actor on the
dominant axis. X wins only when `abs(dx) > abs(dy)`; Y wins ties. Source direction
codes are east 3, west 1, positive-Y/north 2 and negative-Y/south 0. These are
the source's direction encoding, not managed facing enum ordinals. The instruction
at `0x8018446c` loads attacker Y, correcting the accompanying X comment.

All 261,120 nonzero delta pairs possible from byte coordinates were compared
offline with dominant-axis selection and the Y tie rule. Seven literal direction
examples retain cardinal, diagonal and unequal-axis cases.

## Destination and movement dependencies

There are two actual calls. Other hyperlinks describe callers or annotations.

- **Knockback 2**, `0x80184610`, is `knockback_destination_check(direction,x,y)`.
  The call's delay slot supplies destination Y. A negative result rejects;
  otherwise the result is the selected map **layer**, not its absolute height.
  Source study already covers its map bounds, facing-edge heights, lower/upper
  layer selection, headroom, occupancy and fall-height staging within the
  [physical review's admitted domain](physical-pipeline-review.md#verified-behavior-within-the-admitted-domain).
  Equal source/destination edge heights are allowed: the notes' "lower than
  origin" must not become a strict-lower test. Its supplied listing is checked below.
- **Float/Float/Fly movements**, `0x8017f020`, is
  `move_get_effective_flags(target)`. Its source combines movement abilities and
  statuses. The parent tests only effective Fly bit `0x80`; Float bit `0x40`
  alone does not enable the no-fall kind. Its supplied leaf listing is now
  checked below.

After the destination helper succeeds, the parent indexes eight-byte tiles by
`(layer << 8) + y * width + x`. It rejects untargetable bit `0x02` of tile byte 6,
any depth bits `0xe0` of byte 3, half-height `byte3 & 0x1f` of 3 or greater, and
surface `byte0 & 0x3f` equal to lava `0x12` or obstacle `0x1c`. The parent does
not forbid every slope; the child owns direction-dependent edge geometry.

These calls consume no RNG in the pinned source. The parent itself performs no
Brave, hit or HP check; its caller owns the random trigger, and later aftermath
can still suppress a lethal target's displacement.

## Supplied Knockback 2 destination checker

All 178 words at `0x80184610`–`0x801848d4`, two direct calls and its listed return
`0x801844e8` match the prepared file. The
[destination receipt](../evidence/combat/knockback-destination-trace-v1.json)
pins the same clean reference, source, layouts and supplied text. Its two calls
are:

- **Map Location Calculation (`0x801810a0`)**, source symbol
  `battle_map_tile_calculate_unit_index`: calculate the target's source tile
  index from X/Y and layer. This pure shared lookup was already source-traced in
  the physical/action-restriction work; its separate supplied listing is optional.
- **Get ID of Unit if Tile is Targetable (`0x8017dc88`)**, source symbol
  `target_find_unit_at_tile`: delegate to `unit_find_at_tile` at `0x8017dca8` with
  filters 0. Only return **-1** permits the landing. Occupied slots and -2
  (invalid or blocked/untargetable tile) reject. This is not an "any negative
  means empty" test. Its supplied wrapper listing is checked below.

The Slope Type hyperlink describes packed tile data, not another call. No RNG
is consumed along this checked path.

Bounds reject negative X/Y or X/Y at or beyond map width/depth. Valid source
directions 0–3 select exit/entry bit shifts: south 4/6, west 2/0, north 6/4,
east 0/2. The edge formula is `height * 2 + halfHeight * coefficient`, with
halfHeight from byte 3's low five bits and coefficient from the selected two
bits of slope byte 4. The mask allows coefficient 3 too; the notes' 0/1/2 list
does not establish a clamp. These are doubled tile-height units. Invalid
directions fall through without setting the shift registers; the parent only
supplies valid directions, and no fallback for other inputs is claimed.

Lower and upper layers use original plane offset `0x100`. A blocked layer is
assigned numeric height `0xff`. Layers at or below the source exit edge are
reachable. If both qualify, choose the higher entry edge; equal heights choose
layer 0. When exactly one qualifies, the other layer's entry edge minus its
`byte5 & 0x1f` must be at least **source edge + 4**. Equality passes. Preserve
the crossed-layer byte-5 accesses and the source-relative threshold, rather than
substituting destination-floor clearance or a body-height property. This proves
the arithmetic; it does not resolve every physical interpretation of byte 5.

`0xff` is a finite sentinel, not infinity. Raw byte-domain heights can exceed it;
that affects selection before the final collision/occupancy check. No claim is
made that those extreme combinations occur on retail maps. The admitted flat
maps stay below this boundary.

After selecting a layer, the helper tests occupancy **once**. It does not retry
the other layer when the preferred one rejects. Success stores the low byte of
the edge-height difference at `0x801938ee` and copies the working target ID from
`0x801938c1` to `0x801938ef`, then returns layer 0 or 1. Failure returns -1 and
leaves old values alone. These are not pending-flag, live movement or HP writes;
the parent still has its later tile gates.

The source selection matches an independent candidate-layer description over
172,800 representative height/headroom/occupancy combinations. Edge arithmetic
checks cover 32,768 byte-height/half-height/coefficient combinations, and 12
literal descriptions retain equality, layer ties, four-versus-three clearance,
exact -1 occupancy, no retry and byte fall-height narrowing. These are offline
source comparisons, not original/C execution or a managed general-geometry port.

## Supplied zero-filter tile-unit wrapper

All eight words at `0x8017dc88`–`0x8017dca4`, its single call and six listed
caller checks match the prepared file after one return-address correction. The
[wrapper receipt](../evidence/combat/tile-unit-lookup-wrapper-trace-v1.json)
pins the same clean reference, source and layouts. The Reflect lookup in
`reaction_targets_build` calls at `0x8017cb64` and returns at `0x8017cb6c`.
The supplied `0x8017cba8` instead follows a call at `0x8017cba0` to
`target_is_unit_untargetable_and_store_tile`, not this wrapper. The other five
listed return addresses match without correction.

The wrapper forwards X, Y and layer unchanged in r4/r5/r6. Its JAL at
`0x8017dc90` calls **Check if another unit is on the same Tile** at `0x8017dca8`,
source symbol `unit_find_at_tile`. The delay slot sets r7 to zero, selecting no
optional status, Chocobo or dry/low-slope filters. That does not disable the
child's bounds and tile-collision checks or exclude occupied active units by
status.

Nothing after the call changes r2. Child slot/mount-slot results, -1 and -2
are forwarded unchanged. There is no boolean conversion or negative-result
normalization. The wrapper does not validate coordinates itself, determine
general ability target legality, publish movement or draw RNG. Knockback 2's
caller-specific requirement remains exactly -1.

The child's supplied listing at `0x8017dca8` is now checked below. These caller
checks do not imply
Reflect or the other targeting families are implemented; current runtime
admission, fixtures and owner definitions are unchanged.

## Supplied filtered unit-at-tile lookup

All 138 words at `0x8017dca8`–`0x8017decc`, its one call and all five listed
caller returns match the prepared file. The
[lookup receipt](../evidence/combat/unit-at-tile-trace-v1.json) pins the same
clean reference, supplied text, source and field layouts. The duplicate
Find Unit at Specified Coordinates link is labelled alternative formatting,
not an additional call; its unprovided annotations are not compared here.

X/Y must lie within the map; the layer is checked unsigned against 2, so
negative bit patterns also reject. Blocked/untargetable tile bits `0x03` return
-2 regardless of filters. Filter `0x02` additionally requires depth 0 and
half-height below 3. The source scans slots 0–20 with stride `0x1c0`, skipping
active-ID byte `0xff` before applying optional filters. These original bounds
are source encoding, not a new fixed-size authored roster requirement.

The preset is a bitmask: Treasure `0x01`, Transparent `0x10`, Crystal `0x40` are
alternative status matches under mask `0x51`. Requesting Crystal and Treasure
does not require both. With none selected, any occupied active slot can match;
there is no alive-only or team filter. Chocobo bit `0x80` adds a separate
requirement. Only after those checks does the routine compare X/Y/layer.

The first matching scan index is returned, not the unit's active-ID byte or a
campaign identity. If its mount byte is at least `0x80`, return the partner
field `mount & 0x1f` instead. The layer comes from packed position bit 15.
No match returns -1, which means **no unit matching these filters**. It proves
absence of active occupancy for knockback because that caller supplies zero
filters, not for every filtered caller. Invalid tile/coordinates remain -2.

The only direct call is **Chocobo Check (`0x801743c8`)**, source symbol
`unit_mount_get_chocobo_state`. Filter `0x80` gates it after presence/status
checks but before coordinate comparison, so eligible off-coordinate units may
also reach it. Any nonzero return passes, including both Free 1 and Paired 2.
Source study pins its original job-range/unmountable-status classification;
its separate listing is not claimed checked here. The source predicate belongs
to the deferred mounting domain. **Knockback's zero-filter wrapper never calls
it**, so its supplied page is not needed to finish this occupancy path.

All 6,144 byte-filter/status/Chocobo-result combinations agree with an independent
requested-status-set description. Sixteen literal scenarios retain invalid and
empty results, dry/half-height boundaries, slot order, slot 20, partner mapping,
zero-filter call suppression and the optional call's position before coordinates.
These are offline source checks; no general filtered lookup or mounting API is
activated. Runtime, fixtures, owner drafts and the disabled CI gate are unchanged.

## Supplied effective movement-flags leaf

All 51 words at `0x8017f020`–`0x8017f0e8` and all eight listed caller references
match the prepared file. The
[movement-flags receipt](../evidence/combat/effective-movement-flags-trace-v1.json)
pins the same clean reference, source and flag layouts. Seven listed locations
are return addresses. The knockback entry `0x80184584` is the call instruction;
its return address is `0x8018458c`. This helper is a leaf: there are no additional
calls. The bottom links identify callers, not more dependencies to port.

It reads current status byte `0x5a` and movement bytes `0x95`/`0x94`. Float
status `0x40` ORs Float into the local movement copy. Frog or Chicken (`0x06`)
clears local Fly. Neither operation writes the unit's statuses or abilities.

Silent Walk independently contributes return bit `0x02`. Effective Fly wins
over Teleport; either Teleport input bit (`movement2 & 0x0c`) selects the one
return bit `0x08` when Fly is absent. Frog/Chicken can therefore expose the
Teleport fallback by disabling Fly; it does not disable Float or Teleport.

Water input `movement3 & 0x80` selects output `0x20`, taking priority over input
`0x40`, which selects output `0x10`. Preserve these exact selectors. The pinned
decomp labels the first input MOVE_IN_WATER but its output ON_WATER; the wiki
inline ability labels differ. Names alone must not reverse the raw mapping.
Without Float, water can coexist with Fly or Teleport. With Float, output `0x40`
is added and both water outputs are removed, while Silent Walk and the selected
Fly/Teleport output survive.

The result is masked to a byte in the JR delay slot. Return bits `0x01` and
`0x04` are never produced by this leaf: although masks preserve `0x04`
syntactically, the zero-initialized local accumulator never sets it. This does
not assign a broader semantic meaning to those unknown bits.

All 256 values of both movement bytes across the eight combinations of relevant
status bits were compared with an independent compositional description
(524,288 cases). Sixteen literal examples retain priority, status overrides,
water coexistence/clearing, ignored bits and combined movement flags. These are
offline source checks, not original/C execution or new managed movement support.

Knockback's normal input still returns no Fly, selecting pending kind `0x81`.
Its effective-Fly test and extra Frog/Chicken check remain as originally written.
No RNG, stack frame, live movement or unit-data writes occur in this leaf.
Current runtime admission and owner drafts are unchanged; the existing recorded
runtime validation was not repeated for this documentation-only milestone.

## Staged result and failure boundaries

After all parent gates, work byte `0x801938ed` is assigned `0x81` normally.
Effective Fly with neither Frog nor Chicken selects `0x82`. The parent retains
an explicit Frog/Chicken recheck even though the current movement classifier
already clears Fly for those statuses. All 1,024 effective-byte/status-mask
combinations were compared offline; no new runtime capability is inferred.

The parent stores destination X/Y/layer to `0x801938ca`/`cb`/`cc`, then ORs
`0x4000` into the target action's special-effect halfword at offset `0x10`.
Unrelated effects survive. No live-unit coordinate or HP store occurs here.

Early exits do not clear old knockback data. The destination child can stage
fall height and post-action target ID before a later parent tile rejection;
the parent does not roll that child work back. Only the parent's four work-byte
stores and effect update are guarded by all of its gates. Preserve this boundary
when studying broader work, rather than assuming a failed parent changed nothing.
Current managed admission retains fresh, ordinary, flat single-target work.

## Validation and remaining scope

Source/prepared Dash checks and all ten owner draft hashes pass; the committed
disabled CI gate is unchanged. This milestone changes documentation/evidence
only. Runtime tests were not rerun after the recorded passing 186 .NET/two Python
physical validation. Flying, general layers/slopes/headroom, mounts, terrain
and fall damage remain guarded or deferred. Neither source presence nor these
instruction checks establish full implementation of those domains.

This completes the supplied outer-knockback dependency branch for the current
admitted domain. No additional page is required before returning to ZOD-436's
actual/preview/AI shared-consumer review. The conditional Chocobo predicate
remains accounted for but deferred for mounting work. Map Location Calculation
and the formula RNG helper are source-traced; duplicate Pass/Fail or root BIOS
RNG listings are not needed. This does not complete the full AI port or broader
movement/terrain implementation.
