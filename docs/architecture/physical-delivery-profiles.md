# Physical delivery and definition ownership — ZOD-421

The owner selected this consolidation after ZOD-420 made physical formulas
assignable. Managed baseline: `f55a611`. The read-only reference remains clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. This refactor reuses the existing
[formula trace](physical-formula-reassignment.md) and its
[eight-file/sixteen-function source receipt](../evidence/combat/physical-formula-assignment-source-v1.json).
No new gameplay rule, reference C execution or original-game comparison is claimed.

## One definition and explicit delivery

`PhysicalAbilityBattleProfile` owns immutable physical definitions and actual
learned grants for one explicit `PhysicalAbilityDelivery`. Definitions retain
their original object identity through content binding and compatibility views.
The current admitted deliveries are:

- `AdjacentUnit`: range1, non-evadeable, no trajectory.
- `DirectProjectile`: range4, execution evasion and direct trajectory.

These are bounded delivery profiles, not universal descriptions of all adjacent
or projectile abilities. Their current flags/ranges remain admission gates. A
definition using the older explicit range/flag constructor still needs to match
the selected delivery before it can be bound. Unsupported combinations reject.

`TargetedAbilityDefinition` retains one `PhysicalAbilityDefinition`; ID, range
and formula are projections of that definition. Custom callers specify delivery
and formula directly, without selecting a named ability effect:

```csharp
var ability = new TargetedAbilityDefinition(
    new AbilityId("custom:ability/physical-action"),
    PhysicalAbilityDelivery.DirectProjectile,
    new AbilityFormula(AbilityFormulaId.RandomPaPlusHalfPa, new AbilityFormulaParameters(9, 0)));
```

`TargetedAbilityBattleSetup.CreatePhysicalProfile` resolves the selected metadata
instances and explicit learned action slots into the shared profile. Metadata
and AI known slots do not grant actions. Multiple definitions using the same
delivery may select different formulas/operands and arbitrary valid stable IDs.

Schema1 keeps its original effect strings and default wire version. Schema2
keeps `ruleset: custom`, existing delivery strings and all existing fields/order.
`TargetedAbilityEffect` and its constructors remain legacy adapters. The new
delivery enum is not cast to the old effect enum or serialized as its numeric
values. Original imports remain schema1; custom records remain schema2.

## Execution, AI and compatibility

`PhysicalAbilityBattleSimulator` owns flat-roster delivery admission and shared
settlement. It checks actual grants, active turn/ACT availability, target
identity/liveness, range and the selected delivery. Projectiles require explicit
sprite heights and retain first-unit interception. Adjacent actions do not run
trajectory work. Formula selection remains entirely in the shared definition.

The existing `PhysicalDamageRules` still owns evasion, formula RNG, critical and
knockback decisions and caps. Settlement preserves ACT consumption, HP/KO,
occupied/off-map push suppression, facing and both RNG copies. Formula-counter
failure returns the original state and no events. Existing Dash and Throw Stone
event shapes are selected by delivery for compatibility; their names do not
select formulas or require matching ability IDs.

`DashAbilityBattleProfile` and `TargetedAbilityBattleProfile` expose a shared
`PhysicalProfile`. Their old ability dictionaries contain immutable compatibility
views of exactly those definitions. `FromDefinitions` and the old constructors
remain available; `FromPhysicalProfile` adapts an already-bound generic profile
without rebuilding definitions. Cross-delivery adaptation rejects.

Named roster simulator APIs forward to the shared owner. The original two-unit
projectile API retains its separate admission, including its exactly-two-unit
limit, before using shared settlement. Generic roster execution does not widen
that pair API. Actual and AI formula lookups read the authoritative shared
dictionary. Existing ranked AI, command receipts, ACT-first/Move-first decisions
and frozen continuations retain their existing public entry points and authority.

No general AI dispatch interface, mixed-delivery profile, cost/charge support,
new shape/flag/status, equipment effect, modifier/terrain expansion, animation
mapping, editor or namespace migration is introduced. Owner drafts, JobStats,
int MP costs, automatic facing, ZOD-106 and the custom cone remain preserved.

## Validation

Locked restore and Release build pass with zero warnings/errors. All **5,094
.NET tests**, including all1,308 AI cases, and **776 Python tests** pass. Seven
new public cases cover generic execution under both deliveries, exact legacy
state/events, projectile interception, missing actual grants, definition identity,
immutable collection capture, delivery rejection and arbitrary-ID content binding.
The custom imported Dash integration now uses generic delivery binding through
ranked AI and both actual action orders, retaining its formula0x32 expectations.

One fresh programmed native Basic Skill check was selected because consolidation
touches projectile routing and event publication. Its existing finite plan is
bounded to30 seconds/300 inputs; it completes121 steps and two journal replays.
All retained steps, replay/final hashes and state-preserving drawing checks match
ZOD-420. This is a programmed Windows route, not physical input or original-game
fidelity evidence. Fresh original Dash/Stone imports match retained schema1
bytes. All ten owner-file blobs remain unchanged.

The [implementation receipt](../evidence/combat/physical-delivery-implementation-v1.json)
records these checks. Current logs, TRX, imports and native output are under
ignored `scratch/physical-delivery-*`. Next: ZOD-418 ordinary spell aim-search
source study, with no automatic activation of movement-origin search or turns.
