namespace Zodiac.Framework.Ai;

/// <summary>Named original area-shape traits. Combinations are preserved even when their geometry is unsupported.</summary>
[Flags]
public enum AiAbilityAreaShape
{
    None = 0,
    TripleBracelet = 1,
    TripleAttack = 2,
    Cone = 4,
    Linear = 8
}

/// <summary>Immutable source-verified AI preferences and search traits. These do not define an executable effect or working usage flags.</summary>
/// <remarks>Original packing belongs to the content adapter; combat evasion/trajectory flags remain separate.</remarks>
public sealed record AiAbilityBehavior(
    bool TargetsAllies = false,
    bool TargetsEnemies = false,
    bool Unequips = false,
    bool AffectsStats = false,
    bool AddsStatus = false,
    bool CancelsStatus = false,
    bool AffectsMp = false,
    bool AffectsHp = false,
    bool AffectedBySilence = false,
    bool Evadeable = false,
    bool AffectedByFaith = false,
    bool RandomUse = false,
    bool FollowTarget = false,
    bool UndeadReverse = false,
    bool Reflectable = false,
    bool TargetsMapTiles = false,
    bool PhysicalAttack = false,
    bool MagicalAttack = false,
    AiAbilityAreaShape AreaShape = AiAbilityAreaShape.None,
    bool DirectTrajectory = false,
    bool ArcTrajectory = false)
{
    public bool HasExhaustiveAreaShape => AreaShape != AiAbilityAreaShape.None;
}

// Compatibility defaults for existing explicitly authored semantic slots. Imported content supplies its decoded metadata.
internal static class AiAbilityBehaviorDefaults
{
    internal static AiAbilityBehavior ForKind(AiHumanAbilityKind kind) => kind switch
    {
        AiHumanAbilityKind.Attack => new(TargetsEnemies: true, AffectsHp: true, Evadeable: true, PhysicalAttack: true),
        AiHumanAbilityKind.Accumulate => new(TargetsAllies: true, AffectsStats: true, TargetsMapTiles: true),
        AiHumanAbilityKind.ThrowStone => new(TargetsEnemies: true, AffectsHp: true, Evadeable: true, PhysicalAttack: true, DirectTrajectory: true),
        AiHumanAbilityKind.Dash => new(TargetsEnemies: true, AffectsHp: true, PhysicalAttack: true),
        // Disabled Frog Attack is a structural sentinel in the supported non-Frog domain, not an executable effect.
        _ => new() };
}
