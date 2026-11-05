# ZOD-317: Dash from actual formula to roster AI search

Source study 2026-11-05, managed baseline `720966b`; read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, clean. Reuse the
[83-function/27-file trace](../evidence/ai/roster-authority-source-v1.json),
including `ability_considerations_set`, `ai_ability_outcome_evaluate`,
`origin_reachable_evaluate`, `origin_aim_tiles_evaluate`, target preparation,
formula dispatch, modifiers, results and actual turn/receipt owners.

Prepared SCUS_942.21 is verified by edition/size/SHA-256 before checking ability
147's primary at file 0x4f888, secondary at 0x50bfa and Squire action slot1.
The offline verifier keeps raw bytes and original media out of runtime/content.
Dash has JP80, learn rate90, formula0x37, X0/Y4, range1, area0, vertical1 with
vertical tolerance enabled, no MP/charge/status/element. Cannot Target Self,
Cannot Hit Caster and No Follow Target are set. Evadeable and Direct Targeting
are absent. Primary AI flags describe harmful physical enemy targeting,
non-exhaustive origins, no trajectory. Unknown flags keep existing provenance;
this work does not consolidate AbilityToggles or reinterpret unknown type bits.

`formula_damage_random_y_times_pa` calls physical evasion then PA/Y setup,
Attack Up/Martial Arts, physical modifiers and damage/Brave knockback. The
closed neutral profile excludes these unrepresented modifiers. Crucially,
`formula_evade_calculate_final_hit_percent` returns before all four equipment/
class rolls when the ability lacks Evadeable. Actual Dash therefore consumes
three draws: scaled damage `PA * (1 + floor(raw*4/32768))`, attacker Brave,
target Brave. Strict target-roll < actor-roll attempts a push. HP clamps to0;
KO increments a byte and suppresses displacement; every occupant blocks landing.
No original critical roll, projectile ray, equipment evade or weapon damage.
Preserve the existing result ordering and actual choice/receipt settlement.

In AI, `formula_get_random` uses midpoint16384, giving 3*PA, and execution-only
knockback is skipped. With no evasion flag, this effect consumes no formula RNG
and has hit100 even against class evade100. Whole-roster hypothetical clock and
scoring still consume their own draws and preserve every peer. Restored initial
roster owns legal movement; projected damage never becomes actual authority.

The prepared entry is harmful/physical, non-ranged and non-exhaustive. General
preparation therefore sets consume-evaluated-aim; `origin_reachable_evaluate`
uses ranked origin keys (including its unconditional ranked-scan assignment).
Origins in scenario1 are union range1 masks about useful enemies, sorted by
signed priority/proximity with map-order ties. Aim panels are visited in map
order. An evaluated aim is consumed, no trajectory test occurs, and actor origin
substitutes D1/ACT-first. Duplicate occurrences remain separate, entries share
one prefix/RNG/ranks and the existing eight-roll lottery. Unknown priority is
not zero; failed entry/actual command publishes none of its private progress.

Select independently checkable children: ZOD-318 formula boundaries/shared
arithmetic; ZOD-319 actual roster Dash/grants/results; ZOD-320 verified proposals
and frozen Move continuation; ZOD-321 semantic list/preparation; ZOD-322 one
isolated hypothetical candidate; ZOD-323 one fresh ranked occurrence; ZOD-324
full supported list/dispatch integration; ZOD-325 complete-turn/replay/recovery
proof. Existing Stone/pair domains remain intact. Flat living compact16 query
and slot21 actual bounds, explicit fixed/kept facing, no host/editor activation,
full corpse/terminal/frame RNG, auto-facing, broad interfaces and ZOD-106 remain.
