namespace Zodiac.Framework.Ai;

/// <summary>Working entry policy, deliberately independent of the static AI metadata's fourth byte.</summary>
public sealed record OrdinarySpellAiUsage(bool ConsumeAim = false, bool ForceConsumeAim = false,
    bool ExhaustiveOrigins = false, bool RequiresMonsterSkill = false, bool EnemiesOnly = false,
    bool AlliesOnly = false, bool Usable = false, bool ReservedBit4 = false);

public sealed record OrdinarySpellAiSupports(bool HalfMp = false, bool MonsterSkill = false, bool Defend = false,
    bool ShortCharge = false, bool NonCharge = false);

public sealed record OrdinarySpellAiConsidered(OrdinarySpellAiListEntry Entry, AiAbilityBehavior Behavior,
    OrdinarySpellGeometry Geometry, int MpCost, byte ChargeTime, OrdinarySpellAiUsage Usage);

/// <summary>Ordinary default-menu loader and initialization policy, not an executable charge plan.</summary>
public static class OrdinarySpellAiParameters
{
    public static OrdinarySpellAiConsidered Load(OrdinarySpellAiListEntry entry, OrdinarySpellAiSupports supports,
        OrdinarySpellAiUsage retainedUsage)
    {
        ArgumentNullException.ThrowIfNull(entry); ArgumentNullException.ThrowIfNull(entry.Slot);
        ArgumentNullException.ThrowIfNull(supports); ArgumentNullException.ThrowIfNull(retainedUsage);
        var spell = entry.Slot.Spell;
        ArgumentNullException.ThrowIfNull(spell); ArgumentNullException.ThrowIfNull(entry.Slot.Behavior);
        if (spell.OriginalAbilityId is < 1 or >= 368 || spell.Targeting is null || spell.Targeting.Auto || spell.MpCost < 0)
            throw new NotSupportedException("Explicit ordinary default-menu secondary parameters are required.");
        // One shared arithmetic owner preserves the retail AI/actual-charge support-bit distinction.
        byte ct = MagicCharge.CalculateOrdinaryAiDelay(spell.RawChargeTime, supports.MonsterSkill, supports.Defend, spell.Persevere);
        return new(entry, entry.Slot.Behavior, spell.Targeting,
            supports.HalfMp ? spell.MpCost >> 1 : spell.MpCost, ct, retainedUsage);
    }

    public static OrdinarySpellAiUsage InitializeUsage(OrdinarySpellAiConsidered ability)
    {
        ArgumentNullException.ThrowIfNull(ability);
        var usage = ability.Usage with { ConsumeAim = false, ForceConsumeAim = false, ExhaustiveOrigins = false };
        var b = ability.Behavior; var g = ability.Geometry;
        if (b.DirectTrajectory || b.ArcTrajectory) return usage;
        if (b.HasExhaustiveAreaShape) return usage with { ExhaustiveOrigins = true };
        if (g.Range + g.Area < 3 && ability.Entry.Slot.Spell.Spell.SecondaryEvadeable)
            return usage with { ExhaustiveOrigins = true };
        if (g.CannotHitCaster || g.CanHitEnemies || g.Area == 0)
            return usage with { ForceConsumeAim = true };
        return usage;
    }
}
