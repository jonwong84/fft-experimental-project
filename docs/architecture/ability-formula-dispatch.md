# Ability formula parameters and evaluation — ZOD-446

The subsequent [ZOD-447 integration](faith-magic-formula-dispatch.md) adds the
existing Faith-magic0x08/0x0C families, typed magic context and raw HP healing
channel to shared dispatch. It supersedes the physical-only context/support
limits below; those descriptions retain the ZOD-446 milestone scope. The owner's
formula0x37 names are now `RandomPowerTimesPa` / `RandomPowerTimesPaDamageFormula`.

Owner-requested refactor from managed baseline `0811c64`, using clean read-only
fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[receipt](../evidence/maintenance/ability-formula-refactor-v1.json) records source
fingerprints, validation and preserved owner files. Neither reference C nor the
original game was executed.

## Entry points

[`AbilityFormula`](../../src/Zodiac.Framework/Formulas/AbilityFormula.cs) replaces
`PhysicalDamageFormula`; `AbilityFormulaId` replaces `PhysicalDamageFormulaId`.
The immutable definition selects a handler and owns
[`AbilityFormulaParameters`](../../src/Zodiac.Framework/Formulas/AbilityFormulaParameters.cs).
X/Y/Z are integer content operands, currently constrained to their source byte
range. Z defaults to zero. The original item formulas obtain Z from item data;
it is not a universal third ability-data field. Framework definitions retain Z;
the currently supported handlers ignore it. Prepared content remains
schema1/schema2 with its existing X/Y fields, so the content package rejects a
nonzero Z instead of losing it on save. Z-based item formulas are not enabled.

Owner-review follow-up: formula0x32 owns `ValidateParameters`.
`AbilityFormula` makes one construction-time call to
[`AbilityFormulaValidation.Validate`](../../src/Zodiac.Framework/Formulas/AbilityFormulaValidation.cs),
which routes to the selected handler's checks. New handler checks extend that
dedicated dispatcher rather than adding conditions to the constructor.
Its signed16-safe envelope is unchanged;
both `Evaluate` and the raw arithmetic adapter therefore receive validated
immutable parameters. Validation is not delayed until execution. The original
refactor receipt describes the earlier checkpoint; the
[follow-up receipt](../evidence/maintenance/ability-formula-validation-v1.json)
records the current Z policy and validation placement.

```csharp
var formula = new AbilityFormula(
    AbilityFormulaId.PummelFormula,
    new AbilityFormulaParameters(x: 9, y: 0));
var physical = new AbilityFormulaPhysicalContext(
    physicalAttack: 5, attackerBrave: 70, targetBrave: 70);
var context = new AbilityFormulaContext(physical, AbilityFormulaMode.Execution);
var result = formula.Evaluate(context, rng);
```

[`AbilityFormulaContext`](../../src/Zodiac.Framework/Formulas/AbilityFormulaContext.cs)
is immutable per-evaluation combat input, separate from content operands. It
currently admits PA1..99, Brave10..100 and execution/preview/AI mode. Faith,
equipment and status inputs should be added with independently traced handlers
and admission rules; this refactor does not accept unused broader state.
`AbilityFormulaMode` replaces the former `PhysicalDamageMode`. RNG remains an
explicit input and returned state, not mutable battle state stored in the context.

## Flow and ownership

1. Existing delivery/profile and command owners validate the actual action.
2. [`PhysicalDamageRules.Calculate`](../../src/Zodiac.Framework/CombatRules/PhysicalDamageRules.cs)
   prepares the supported context and resolves physical evasion first. Its real
   accessory/right/left/class rolls retain their previous order in every mode.
   A miss skips the formula dispatcher.
3. `AbilityFormula.Evaluate(context, rng)` selects the shared handler. Formula0x37
   calls `RandomPaDamageFormula`: random1..Y damage, followed by execution-only
   attacker/target Brave rolls and the strict knockback comparison. Formula0x32
   calls `PummelDamageFormula`: critical check, conditional critical
   bonus, then random1..X damage with YA prepared from original PA and its normal
   zodiac floor. The latter requests knockback on a critical; it does not use
   formula0x37's Brave tail.
4. `AbilityFormulaResult` returns uncapped damage, critical/knockback requests and
   next RNG state. `PhysicalDamageRules` retains the upper999 cap and accepted/
   rejected result contract. Later settlement still owns lethal suppression,
   landing and authoritative HP/position/turn publication.

The private `AbilityFormulaRandom` gives both handlers source midpoint16384 in
preview/AI without formula draws; execution advances the existing BIOS generator.
It does not own evasion or AI decision draws. Counter exhaustion throws from the
standalone dispatcher; the combat-rule caller rejects with the entire incoming
RNG state, including any earlier evasion work. No partial battle work is published.

`CalculateDamage` remains a raw-roll arithmetic adapter for the existing neutral
physical tests and legacy Dash/Throw Stone helpers. It delegates to the same
family arithmetic, consumes no RNG and grants no execution authority. New normal
execution uses `Evaluate`. The signed16-safe definition envelope and unsupported
formula rejection are retained; no new IDs or gameplay inputs are admitted.

## Pummel naming and shared result bounds

The owner selected `AbilityFormulaId.PummelFormula` and `PummelDamageFormula`
for formula0x32, using the relocalized name of Repeating Fist. The numeric ID,
content schema and selected handler behavior are unchanged. This is a formula
label; an ability such as Dash can still select it without changing its identity.
The decompilation symbol remains `formula_repeating_fist` for source comparison.

[`FormulaDamageBounds`](../../src/Zodiac.Framework/Formulas/FormulaDamageBounds.cs)
uses `FitsSigned16Range` to check whether a calculated minimum/maximum damage
range fits signed16 storage.
It neither clamps values nor reproduces overflow. Pummel calculates its own
maximum critical XA, original-PA YA and random factor envelope, then passes
that range to the helper. The existing sword command, two-unit setup and
immediate-ability setup guards share the same helper with their own maxima.
Validation timing, exceptions/rejections, supported domains and the upper999
damage cap remain unchanged. This managed admission guard is not a check made
by the original game; broader signed-result compatibility still needs its own
explicit implementation and validation.

The [maintenance receipt](../evidence/maintenance/pummel-formula-maintenance-v1.json)
records the rename, unchanged source/test vectors and preservation of owner
formatting. No legacy enum alias is retained; callers use the new label while
prepared JSON continues to store numeric formula50.

The [validation-dispatch receipt](../evidence/maintenance/formula-validation-dispatch-v1.json)
records the constructor cleanup and the owner's range-helper rename/comments.
All prior validation, supported IDs, operands, RNG and result contracts remain.

## Source correspondence and verification

The supplied [Pre Formula Setup](ability-effect-families.md#pre-formula-setup-boundary)
maps to `action_calculate_target_result`: effective formula selection, canceled
status setup, evasion reactions, indirect dispatch and finalization. This refactor
extracts a managed dispatcher for the two already supported neutral families;
it does not implement that entire routine or its menu-dependent selection.
Special command paths and the separate Faith-magic implementation remain separate.

The handlers preserve `formula_damage_random_y_times_pa`, `formula_repeating_fist`,
the critical/damage-and-knockback helpers, and `formula_get_random` ordering within
the admitted domain. Existing physical assignment/review fixtures and all prior
test expectations are unchanged. Added dispatcher tests reuse the existing source
vectors and cover missing/invalid context, retained unused Z, prediction at exhausted
RNG and uncapped formula versus capped combat results. Content tests reject
nonzero Z at the current schema boundary; existing definition-time signed16
rejection and maximum admitted raw-damage tests remain unchanged.

Full validation passes: zero-warning/error Release build, 5,306 .NET tests and
810 Python tests. Owner enum/test layout edits are carried into the new API;
owner drafts, immutable JobStats, controller formatting and disabled CI are
preserved. Historical evidence payloads are unchanged. Owner review resumes next;
no full AI review group or new formula family is completed by this maintenance.
