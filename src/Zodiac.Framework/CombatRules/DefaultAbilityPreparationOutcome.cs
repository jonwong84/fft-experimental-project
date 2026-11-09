namespace Zodiac.Framework.CombatRules;

/// <summary>Prepared command and proposed primary-action status changes. Turn usage is committed by the caller.</summary>
public sealed record DefaultAbilityPreparationOutcome(PendingMagicCast Plan, MagicCurrentUnit Caster)
{
    public bool Immediate => Plan.Counter == 0;
}
