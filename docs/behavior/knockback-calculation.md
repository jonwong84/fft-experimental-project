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
  origin" must not become a strict-lower test. Its supplied listing is next useful.
- **Float/Float/Fly movements**, `0x8017f020`, is
  `move_get_effective_flags(target)`. Its source combines movement abilities and
  statuses. The parent tests only effective Fly bit `0x80`; Float bit `0x40`
  alone does not enable the no-fall kind. Its supplied listing remains useful
  after the destination checker.

After the destination helper succeeds, the parent indexes eight-byte tiles by
`(layer << 8) + y * width + x`. It rejects untargetable bit `0x02` of tile byte 6,
any depth bits `0xe0` of byte 3, half-height `byte3 & 0x1f` of 3 or greater, and
surface `byte0 & 0x3f` equal to lava `0x12` or obstacle `0x1c`. The parent does
not forbid every slope; the child owns direction-dependent edge geometry.

These calls consume no RNG in the pinned source. The parent itself performs no
Brave, hit or HP check; its caller owns the random trigger, and later aftermath
can still suppress a lethal target's displacement.

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

Next useful supplied listing: **Knockback 2 (`0x80184610`)**, followed by the
effective-movement routine if available. No duplicate Pass/Fail or root BIOS RNG
listing is needed.
