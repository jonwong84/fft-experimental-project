# Shared Faith-magic formula dispatch — ZOD-447

Owner-selected next step from `668ef1f`, following the
[formula/context refactor](ability-formula-dispatch.md). Read-only fft_decomp
remains `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source trace](zod-334-faith-magic.md), supplied formula0x08 documentation and
existing magic pipeline establish these families. The
[implementation receipt](../evidence/combat/faith-magic-dispatch-v1.json) records
the renewed source fingerprint check, owner-edit preservation and validation.
No reference C or original game was executed.

## Supported shared entry

`AbilityFormulaId.FaithMagicDamage = 0x08` and
`AbilityFormulaId.FaithMagicHealing = 0x0C` now enter
[`AbilityFormula.Evaluate`](../../src/Zodiac.Framework/Formulas/AbilityFormula.cs).
They reuse `FaithMagicAmountFormula` and `FaithMagicHpRouting`, including their
source-ordered modifiers, elemental work, signed16 stores, single Faith division,
absorption and undead reversal. No alternate magic arithmetic was introduced.
The owner's `RandomPowerTimesPa` / `RandomPowerTimesPaDamageFormula` naming and
mode comments are preserved with the existing physical paths.

[`AbilityFormulaMagicContext`](../../src/Zodiac.Framework/Formulas/AbilityFormulaMagicContext.cs)
holds immutable MA, attacker/target `FaithValue`, target undead state, modifiers
and optional elemental input. `AbilityFormulaContext` exposes `MagicalAttack`,
`AttackerFaith` and `TargetFaith` from that one object; it does not copy those
values into competing fields. Faith/Innocent status overrides remain part of
`FaithValue` rather than being discarded into plain integer Faith.

[`AbilityFormulaPhysicalContext`](../../src/Zodiac.Framework/Formulas/AbilityFormulaPhysicalContext.cs)
now encapsulates PA and both Brave values symmetrically with the magic context.
It retains the previous PA/Brave checks; `AbilityFormulaContext` owns mode and
accepts either `(physical, mode)` or `(magic, mode)`. Its convenience stat properties
forward to the selected object rather than duplicating values. The physical
object and its projected values are absent when a magic context is selected. Validation
rejects using a magic formula with physical inputs or the reverse before any
formula draws. Missing elemental facts for damage still reject under the existing
magic contracts; healing still bypasses that elemental branch.

The [physical-context follow-up receipt](../evidence/maintenance/ability-formula-physical-context-v1.json)
records this owner-requested symmetry change, preserved arithmetic/RNG contracts
and full regression results. Physical handlers consume `context.Physical` just
as the magic handler consumes `context.Magic`.

```csharp
var magic = new AbilityFormulaMagicContext(
    MagicalAttack: 10,
    AttackerFaith: new FaithValue(70),
    TargetFaith: new FaithValue(60),
    TargetIsUndead: false);
var context = new AbilityFormulaContext(magic, AbilityFormulaMode.Preview);
var healing = new AbilityFormula(
    AbilityFormulaId.FaithMagicHealing,
    new AbilityFormulaParameters(x: 0, y: 20));
var result = healing.Evaluate(context, rng); // Healing84, no formula RNG draw.
```

`AbilityFormulaResult` retains the physical damage/critical/knockback/RNG API and
adds `Healing` and an opaque `MagicProjection`. These are raw signed HP channels,
before caps or live application. Nullification/affinity metadata remains in the
projection; an internal HP-routing result preserves the deferred canceled-status
operation for the existing composer. This is not yet a general MP/stat/status or
accuracy result model.

## Existing magic pipeline integration

Both `FaithMagicFormula.Resolve` and `ResolveAfterHit` now project their amount
through the shared dispatcher. Their legacy kind/power inputs bind the effective
formula ID/Y, with an explicit mode mapping: the two enums have different ordinal
orders and are not cast to one another. No duplicate RNG stream is introduced.

The certified entry still owns evasion, source mode-specific rolls and whole-call
rollback. The existing amount projection before evasion is private and consumes
no RNG; a miss publishes none of that work. HP routing is calculated privately
once, then consumed only after admission/hit. The composer retains proc decisions,
empty-status behavior, flags, upper999 finalization and rejection semantics. The
projection-based compatibility API retains its original tail owner too. Existing
live casting and AI magic simulation therefore reach the shared amount dispatcher
without gaining new command or turn authority.

Standalone shared magic amount evaluation consumes no RNG in any mode. It does
not replace the certified full resolver: it does not perform evasion, the0x08
conditional proc, status composition, final caps or live mutation. Those existing
owners are still required for a complete supported action. Physical delivery
definitions explicitly reject magic IDs, so the broader formula enum cannot
silently expand the Dash/Throw Stone content/profile domain.

## Validation and next work

Release build: zero warnings/errors. All **5,346 .NET tests** and **810 Python
tests** pass. Forty added shared-entry cases reuse the unchanged source amount/
routing literals and exercise effective Faith, signed stores, modifiers, HP
channels, all modes, assignment, input mismatch and physical-delivery rejection.
Existing live cast/evasion/proc/finalization regressions preserve their literal
expectations and RNG states. Historical receipts/fixtures and owner drafts,
JobStats, controller layout and disabled CI remain intact.

This integrates two existing magic families into shared dispatch; it does not
port the rest of the formula table. Next, trace and select a bounded Faith-based
accuracy/percentage/status family, such as0x09, before adding its handler and
result channels. Formulas0x0A/0x0B/0x0D/0x0E and the other families remain
unsupported by `AbilityFormula`. Generic status application, revive/equipment/
inventory/menu branches, cone targeting, full AI list dispatch and deferred
domains retain their separate gates. Prepared schemas and live spell kind/power
bindings are unchanged; arbitrary prepared magic formula reassignment is not
activated by this dispatcher consolidation.
