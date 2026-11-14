namespace Zodiac.Framework;

/// <summary>An owned single-action offer. Only battle execution creates reports.</summary>
public sealed class DefaultAbilityLearningReport
{
    public ulong Id { get; }
    public BattleUnitId LearnerId { get; }
    public AssetId AbilityId { get; }
    public AssetId JobId { get; }

    internal DefaultAbilityLearningReport(ulong id, BattleUnitId learnerId, AssetId abilityId, AssetId jobId)
    {
        Id = id;
        LearnerId = learnerId;
        AbilityId = abilityId;
        JobId = jobId;
    }
}
