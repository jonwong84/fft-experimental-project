namespace Zodiac.Framework.Ai;

/// <summary>Working entry policy, deliberately independent of the static AI metadata's fourth byte.</summary>
public sealed record DefaultAbilityAiUsage(bool ConsumeAim = false, bool ForceConsumeAim = false,
    bool ExhaustiveOrigins = false, bool RequiresMonsterSkill = false, bool EnemiesOnly = false,
    bool AlliesOnly = false, bool Usable = false, bool ReservedBit4 = false);

public sealed record DefaultAbilityAiSupports(bool HalfMp = false, bool MonsterSkill = false, bool Defend = false,
    bool ShortCharge = false, bool NonCharge = false);

public sealed record DefaultAbilityAiConsidered(DefaultAbilityAiListEntry Entry, AiAbilityBehavior Behavior,
    DefaultAbilityGeometry Geometry, int MpCost, byte ChargeTime, DefaultAbilityAiUsage Usage);

/// <summary>Ordinary default-menu loader and initialization policy, not an executable charge plan.</summary>
public static class DefaultAbilityAiParameters
{
    // Retail SCUS-94221 menu table0x80065CB4. These human skillsets take special menu paths;
    // the ordinary loader cannot replace their weapon, inventory, terrain or calculator rules.
    internal static void RequireDefaultMenu(byte skillset)
    {
        if (skillset is < 5 or >= 176 or 6 or 8 or 17 or 18 or 19 or 20 or 21 or 52 or 56 or 57 or 59 or 62 or 156)
            throw new NotSupportedException("An original default-menu human skillset is required for ordinary spell AI.");
    }

    public static DefaultAbilityAiConsidered Load(DefaultAbilityAiListEntry entry, DefaultAbilityAiSupports supports,
        DefaultAbilityAiUsage retainedUsage)
    {
        ArgumentNullException.ThrowIfNull(entry); ArgumentNullException.ThrowIfNull(entry.Slot);
        ArgumentNullException.ThrowIfNull(supports); ArgumentNullException.ThrowIfNull(retainedUsage);
        RequireDefaultMenu(entry.OriginalSkillsetId);
        var spell = entry.Slot.Spell;
        ArgumentNullException.ThrowIfNull(spell); ArgumentNullException.ThrowIfNull(entry.Slot.Behavior);
        if (spell.OriginalAbilityId is < 1 or >= 368 || spell.Targeting is null || spell.Targeting.Auto || spell.MpCost < 0)
            throw new NotSupportedException("Explicit ordinary default-menu secondary parameters are required.");
        // One shared arithmetic owner preserves the retail AI/actual-charge support-bit distinction.
        byte ct = MagicCharge.CalculateOrdinaryAiDelay(spell.RawChargeTime, supports.MonsterSkill, supports.Defend, spell.Persevere);
        return new(entry, entry.Slot.Behavior, spell.Targeting,
            supports.HalfMp ? spell.MpCost >> 1 : spell.MpCost, ct, retainedUsage);
    }

    public static DefaultAbilityAiUsage InitializeUsage(DefaultAbilityAiConsidered ability)
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
