# Ordinary spell targeting

## ZOD-358

Source study is pinned to clean read-only `fft_decomp` revision
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/combat/spell-targeting-source-v1.json) fingerprints
11 files and 21 functions. The [55 descriptions](../fixtures/behavior/spell-targeting-contract-v1.json)
and Python verifier are normalized independent descriptions, not execution of
the original, C or managed implementation.

`target_range_set_for_action` / `ability_range_set` build blue selectable aim
panels; `target_area_set_for_action` / `ability_area_set` build green affected
panels at resolution. `action_resolve_strike` resets nearest-first sorting before
calling `units_on_panels_list`. Resolution does not repeat the caster's current
blue range check. Earlier aim validation must therefore be certified separately.

### Heights and spread

Physical half-step height is `Height*2 + HalfHeight + WaterDepth*2`. The reference
height narrows to unsigned byte after addition; candidate heights do not.
`vertical_tolerance_apply` uses -255 for blocked/missing sheets. It compares both
distances, including that sentinel, chooses lower on ties, then validates the
chosen sheet and tolerance (`vertical*2`). A blocked sentinel can beat a valid
high sheet and leave no result. TopDown first retains the physically higher
sheet (lower wins ties), then still applies the same sentinel/distance procedure.

Finite `panels_spread` residual budgets describe an orthogonal Manhattan diamond
including its centre. Spread ignores obstacles and untargetability; both sheets
start with the same footprint. Radius255 means whole map. Blue removes the
caster's exact panel before optional vertical selection. A nearer excluded panel
does not fall back to a farther sheet. Blue clears untargetable tiles only for
area0, then clears blocked/cross-section panels. Green always applies vertical
selection for finite area, removes CannotHitCaster afterward, then clears both
blocked and untargetable tiles. Whole-map green skips vertical and finite-only
hooks. CannotTargetSelf belongs to blue, not green.

### Teams and stored order

`area_unit_team_eligibility_apply` compares caster **current** team against each
target's **initial** team; caster is always allied to itself. Neither team flag
means no filtering. Otherwise source-slot order mutates per-panel remaining
budget and persistent marks. An allowed occupant marks its panel; a subsequent
disallowed occupant clears remaining budget without erasing the mark. Reversing
those occupants can remove both from the result. Do not refilter individual
units afterward. Finite areas mark remaining empty panels; whole-map areas with
team flags mark only occupied allowed panels.

Target eligibility excludes inactive, Crystal, Treasure, Jump and mount units.
It does not exclude Dead, Petrify or Wall; unsupported formula contexts must
reject later, atomically. Default iteration uses source-slot order, never IDs or
input array order. Explicit nearest-first sorting uses Manhattan distance and
preserves source order on ties; ordinary resolution resets this option false.

### Supported slice and remaining work

Implementation children admit normalized map-aim coordinates, original-width
terrain fields, one/two managed sheets and at most256 logical panels per sheet.
These are compatibility-query limits, not global map or roster limits. Explicit
certificates exclude selectable cross-sections and unused source padding, whose
original marks are not represented by the managed map. Missing upper sheets are
blocked. A complete roster provides unique source ordinals and stable IDs.

Unit-follow, raw command narrowing, weapon/direct/auto/cardinal/linear/directional
shapes, RandomFire, Calculator, Moldball Virus329 water-depth hooks and the blue
unit-marker pointer quirk remain unimplemented. Do not silently repair them.
Geometry owns no MP, formula arithmetic, mutable map marks, RNG or live state.
The future custom cone remains separate: forward rows1/3/5, excluding the actor,
rotated with attack direction. Owner drafts, JobStats, host/editor, automatic
facing, full corpse/Golem/terminal/status/frame RNG, broad interfaces and ZOD-106
remain deferred. No runtime C, BIN, command, content grant or magic AI activation.

## ZOD-359

`OrdinarySpellTargeting.ReferenceHeight` and `SelectLayer` implement the traced
height primitive without map marks, source execution or global map constraints.
Original field-width violations throw at this direct primitive boundary. Seventeen
literal reference/column descriptions cover narrowing, water/half height, tolerance,
lower ties, TopDown and blocked sentinels. Two managed tests passed; Python's eight
checks remain the independently described baseline.
