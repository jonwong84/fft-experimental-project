# Assignable physical formulas — ZOD-420

The later [ZOD-427 review](../behavior/physical-pipeline-review.md) corrects
formula0x32's PA1/Y0 boundary: the source zodiac helper floors YA to1 even in
the admitted neutral profile. Current verification uses the explicit v2 fixture;
the original v1 fixture and this milestone's receipts remain historical.

Subsequent [ZOD-421 delivery/profile consolidation](physical-delivery-profiles.md)
now owns shared binding and settlement. The implementation and validation below
record the original ZOD-420 checkpoint; its schemas and formula contracts remain.

Owner-authorized bounded refactor, managed baseline
`c34d6c5590f0e9b7336947d6f3a4c05b17bb083e`; clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

## Source contract before translation

`formula_damage_random_y_times_pa` (0x37) calls physical evasion, PA/Y setup,
Attack Up/Martial Arts, physical modifiers and damage/Brave knockback. In the
existing bare-human neutral profile, supports/status/equipment/zodiac modifiers
are absent. Actual damage is `PA * (1 + floor(raw * Y / 32768))`; it then draws
attacker Brave and target Brave, with strict target-scaled < attacker-scaled
comparison. Both draws precede lethal/landing suppression. AI/preview formula
randomness is midpoint16384; execution-only Brave/knockback is skipped.

`formula_repeating_fist` (0x32) calls physical evasion, then
`formula_store_pa_and_pa_plus_y_divided_by_two`: XA=PA and YA=floor((PA+Y)/2).
After supports/physical modifiers it calls the critical step: the first formula
roll scaled to100 must be below4; a successful actual critical consumes another
roll, increases XA by floor(raw*originalXA/32768), and requests knockback.
YA retains its pre-critical value. `formula_store_xa_plus_ya` adds the two terms;
the next formula roll supplies `1 + floor(raw * X / 32768)`. The factor is u8;
the prior hp_damage is read signed16 and the product is stored signed16.
This path never calls0x37's two-Brave-roll tail. Its preview/AI midpoint never
crits and consumes no formula randomness.

`formula_evade_calculate_final_hit_percent` bypasses the four evade sources for
non-Attack abilities without the secondary Evadeable flag. With that flag it
computes accuracy and draws accessory/right/left/class in order; execution stops
at the first actual evade, while AI/preview keep all four draws and only use the
computed accuracy. The admitted existing profile has zero equipment evasion;
class evade applies only from the front, including either axis on a diagonal.
Primary AI Evadeable is not the execution flag.

`action_finalize_target_result` clears hit at zero accuracy, caps positive
damage at999 and cancels knockback on lethal damage. Existing flat settlement
then suppresses blocked/off-map landings, retains facing/choices and increments
the byte KO count. The query's knockback attempt is diagnostic, not proof of
displacement. `knockback_calculate` has target-count and broader state/terrain
guards; this first profile remains one ordinary living target on dry flat ground.

Managed operand fields are ints, but the initial supported source family keeps
X/Y within0..255. For0x32, the worst-case admitted PA99 critical XA197 plus
floor((99+Y)/2), multiplied by max(1,X), must fit signed16. Reject wider contexts
instead of silently replacing signed overflow with unbounded arithmetic. HP
damage then receives the source upper999 cap. X/Y0 remain valid source operands.
Broader supports/status/element/compatibility, signed-negative results, multiple
targets, reactions and terminal/frame RNG are not activated.

The existing [source inventory](../evidence/combat/ability-formula-source-v1.json)
pins the shared source files; the new implementation receipt will pin the focused
functions and vectors. No reference C or original media is used at runtime.

## Implemented boundary (ZOD-420)

`PhysicalDamageFormula` in `Zodiac.Framework.Formulas` owns immutable formula
ID/X/Y and raw arithmetic. `PhysicalDamageRules` in `Zodiac.Framework.CombatRules`
owns fresh execution/preview/AI RNG, physical evasion, critical/Brave knockback
decisions and the post-formula upper999 cap. `PhysicalAbilityDefinition` supplies
the selected formula and admitted targeting/attributes. Delivery admission,
formula work and live settlement remain distinct. Original Dash/Stone adapters preserve their current
range, evasion, trajectory, grants and event contracts while selecting generic
physical definitions. Their named formula helpers become compatibility forwards,
not executable dispatch owners. No new ability-specific formula class is added.

Schema1 output remains byte-identical for original imported companions. Schema2
is explicit custom content for formula reassignment; changing a formula does not
not copy another ability's targeting, metadata, grants or presentation. Reuse
existing metadata as the sole learning/AI source. Do not activate owner draft
flags, equipment policies, a general status catalog or host/editor work here.

Execution, preview and AI consume the same selected formula, including
ranked candidate evaluation and frozen Move continuations. Source-derived
literal vectors cover critical/no-critical, cap, operand edges and mode/RNG
boundaries; custom Dash-as0x32 integration preserves original regressions.

The selected data flows through `TargetedAbilityBattleSetup` and the profiles'
`FromDefinitions` factories. New callers author shared physical definitions;
the old BoundedDash/BoundedThrowStone and named formula APIs remain compatibility
adapters. The `legacyDashEvents` branch preserves existing event shapes only;
it never selects the calculation or knockback policy. Broader profile/name
consolidation is the next refactor child, not a prerequisite for assignment.

## Editing formula assignments

Create a custom companion from existing definitions and matching metadata IDs:

```csharp
var original = effects.Abilities.Single();
var edited = new TargetedAbilityDefinition(original.Id, original.Effect,
    new PhysicalDamageFormula(PhysicalDamageFormulaId.RandomPaPlusHalfPa, 9, 0));
var custom = new TargetedAbilityContentPackage(effects.PackageId,
    effects.ProfileId, effects.RequiredMetadataPackageId, [edited]);
string json = custom.ToJson();
```

Schema2 emits `ruleset: "custom"` and separates `delivery`, `formula`, `x`, `y`,
range, execution evasion, target, charge and MP. Formula values are decimal JSON
integers:50 selects0x32,55 selects0x37. Its current deliveries are `adjacent-unit`
(range1, no evasion/trajectory) and `direct-projectile` (range4, physical evasion
and direct trajectory). Those delivery attributes remain fixed admission gates
for this first slice. Charge and MP remain zero. Unsupported handlers, flags,
geometry, operands or signed16 envelopes reject; no original-formula fallback.
The parser does not impose the original100-slot table size on future custom IDs.

A formula-only edit retains existing X/Y. Dash's X0/Y4 under0x32 yields neutral
PA5 midpoint9, whereas explicitly choosing X9/Y0 yields35. The verified local
SCUS-94221 input confirms original Repeating Fist101 uses0x32/X9/Y0 and has
execution Evadeable set. Assigning that formula to Dash147 retains Dash's clear
execution evasion flag. Its primary AI traits stay with the same metadata, and
actual learned grants remain separate from AI known slots.

Schema1 retains its legacy field names and default wire version. The package's
`Version` reports the selected schema; `SchemaVersion` remains1 for old callers,
and `CustomSchemaVersion` is2. Mixed definitions serialize through explicit
custom schema2, which requires a nonempty ability list. Original import routines
still validate original source records and emit schema1; custom edits happen
after import. The existing hash-verified loading boundary remains in force.

## Validation

Locked restore and a zero-warning/error Release build pass. All **5,080 .NET
tests** and **776 Python tests** pass. Eighteen independent source vectors cover
both formulas, critical/noncritical order, original-PA YA, actual/preview/AI
draws, zero operands, caps and late counter rollback. Twenty-three managed
physical cases also cover actual critical/KO/push settlement, replay, generic
binding admission and frozen Move continuation. Eleven Content cases cover
custom round-trips, formula-only edits, explicit custom mode, operands and
unsupported combinations. Two added CLI/import cases carry a custom JSON Dash
through normal metadata binding, ranked AI, actual execution and complete turns
in both action orders. All1,305 existing AI regressions pass.

Fresh original Dash/Stone imports match retained schema1 package bytes. The
native Basic Skill scenario matches all121 retained baseline steps, two journal
replays, state-preserving drawing and final hash. The four native magic scenarios
retain eighteen full replay checkpoints and their previous final hashes. These
are programmed routes, not physical input or original-game fidelity evidence.

All ten owner-file blobs, JobStats, int MP costs and the approved formatting edit
remain preserved. All474 public Framework declarations have matching files and
retain existing partial parts. No owner draft activation, general status catalog,
equipment effect, broader modifier/shape, editor, automatic facing, ZOD-106 or
general magic AI work is included. The named adapters remain; this is the first
bounded formula/data milestone, not a whole-framework namespace migration.

The [source receipt](../evidence/combat/physical-formula-assignment-source-v1.json)
pins eight files/sixteen functions. The
[implementation receipt](../evidence/combat/physical-formula-assignment-implementation-v1.json)
records validation, preserved imports and native baselines. No original or C
execution is claimed, and no original media/reference is required at runtime.
