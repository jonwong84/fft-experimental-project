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
high sheet and leave no result. TopDown replaces the physically lower sheet's
comparison height with -255 (lower wins height ties), without clearing that
panel. It still applies the sentinel/distance procedure: at extreme heights the
substituted sentinel can win, retaining the physically lower unblocked sheet.

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

## ZOD-360

`Range` and `Area` produce opaque immutable panel sets from a managed map and
positive ordinary/no-special/cross-section/padding certificates. The managed
implementation uses distance enumeration independently of Python's frontier
spread model. Twenty-six literal panel descriptions and four focused managed
tests passed, covering both sheets, vertical ties, whole map, self flags,
collision, propagation through obstacles and rejection boundaries. No unit list
or command is inferred yet. Panel order is managed layer/Y/X, not target order.

## ZOD-361

`SelectTargets` derives a stable source-order unit list from opaque green panels
and a certified complete roster. It preserves per-panel marks, initial/current
team semantics, caster ally override and stable nearest-distance ties. Inactive
positions are ignored; active positions, IDs, teams and unique source ordinals
are validated. Crystal/Treasure/Jump/mount are excluded, while unsupported formula
statuses remain for the calculation owner to reject. Twelve literal roster
scenarios and six focused managed tests passed. No HP/status eligibility shortcuts
or live target lists were introduced.

## ZOD-362

The ready cast definition optionally binds `OrdinarySpellGeometry`; prior supplied-
order APIs and calculation bodies are unchanged. `ResolveCast` requires certified
previous aim validation, complete source roster and no earlier target effects.
It builds green panels, derives default source order, then delegates to
`MagicCastQuery.ResolveTargets`. MP is proposed once by preparation; no blue range
recheck, cost reevaluation, formula duplication or live publication occurs.
Selected unsupported targets and late counter failures discard all candidate
MP/results/RNG. Special ability329 is explicitly excluded. Missing selected
current inputs and conflicting metadata reject before calculation.

The [13 composition descriptions](../fixtures/behavior/spell-geometry-cast-contract-v1.json)
reuse prior literal magic frame/RNG expectations and independently describe map
selection. They include team filtering, source order, moved/out-of-current-range
aim, Preview/AI, self-healing, empty targets and whole-cast rollback. Nine new
managed cases cover geometry/casts, guards and isolation; the focused magic suite
passes99 cases. Fourteen new Python cases pass independently. A final reread added
two columns and one area description for TopDown's comparison-only substitution;
the source model and managed primitive now both preserve this extreme-height
quirk. Final targeting descriptions total58 (4 references,15 columns,27 panels,
12 rosters), plus13 casts. The earlier milestone counts record the smaller set
validated at those commits; they are not full equivalence claims.

[ZOD-363](spell-follow-and-stored-aim.md) now traces unit-follow and stored-aim
behavior. [ZOD-364–369](stored-spell-aim-pipeline.md) now add the certified aim
primitive, bridge and pure confirmation/charge/readiness phases. Next ZOD-370
traces publication. Future custom formulas and the cone remain independent.

Final validation: locked restore and Release build passed with zero warnings/errors;
all **4,661 .NET tests** and **687 Python tests** passed. Source fingerprints,
prior magic/prepared-data, algorithm guide, coverage and preservation audits
passed. [Implementation receipt](../evidence/combat/spell-targeting-implementation-v1.json)
records milestone bases and ignored report locations. All nine owner drafts and
JobStats are unchanged. Existing C# calculation bodies/tests/fixtures are unchanged;
only the cast definition gains its optional geometry parameter. No project,
package, host/editor or AI activation changes; no original/C execution or UI review.
