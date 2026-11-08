# Targeting entry points

Start with [SpellTargetChoiceQuery.Evaluate](Selection/SpellTargetChoiceQuery.cs)
when an ordinary ability is selected. It calls
[DefaultAbilityTargeting.Range](DefaultAbilityTargeting.cs) and reports unit-or-tile,
tile-only, automatic or no available targeting. It does not drive the cursor.

Follow the supported cast in this order:

1. [SpellTargetChoiceQuery](Selection/SpellTargetChoiceQuery.cs): selectable aim
   panels and selection mode, corresponding to `target_range_set_for_action`.
2. [SpellAimCapture](Selection/SpellAimCapture.cs): confirm a legal initial aim
   and retain a unit anchor or tile. The confirmed selection is immutable.
3. [OrdinaryMagicPreparation](../../../CombatRules/OrdinaryMagicPreparation.cs):
   shared primary preparation. The actual command owner commits ACT and queues it.
4. [SpellAimResolver](Resolution/SpellAimResolver.cs): resolve the stored aim at
   readiness. A followed unit may have moved; initial range is not rechecked.
5. [DefaultAbilityTargeting.Area](DefaultAbilityTargeting.cs): affected panels
   around that resolved aim, corresponding to `ability_area_set`.
6. [DefaultAbilityTargeting.SelectTargets](DefaultAbilityTargeting.cs): apply
   source-ordered panel/team rules and collect recipients.
7. [MagicCastQuery](../Magic/MagicCastQuery.cs): resources and supplied-order
   formula results. Actual application is owned by the battle command lifecycle.

[DefaultAbilityTargeting](DefaultAbilityTargeting.cs) retains the public API.
Its internal owners are [SpellTargetGeometry](Geometry/SpellTargetGeometry.cs),
[SpellTargetCollection](Resolution/SpellTargetCollection.cs) and
[SpellTargetCastResolution](Resolution/SpellTargetCastResolution.cs).
`ResolveCast` is a pure composition; it does not publish live state or a command.

`Selection/` contains choice/confirmation and stored-aim definitions. `Geometry/`
contains panel geometry and geometry profiles. `Resolution/` contains readiness
aim resolution, roster facts, recipient collection and pure cast composition.
Shared rejection enums stay beside this entry-point file. Namespaces remain
`Zodiac.Framework`; these folders do not add public APIs.

The [pathway guide](../../../../../docs/development/action-pipelines.md) connects
these stages to the player command and AI entry points, records original routine
addresses and explains the existing compatibility limits. To modify range or
height handling, start with `SpellTargetGeometry`; for target following, start
with choice/capture and `SpellAimResolver`. Do not put AI preferences or panel
colors in these rule owners.
