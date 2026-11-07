namespace Zodiac.Framework.Events;

/// <summary>Authored post-readiness definition, validated by BannerSceneExecutor.AttachBanner.</summary>
public sealed record GeneralBannerDefinition(AssetId Id, int Condition, int HoldFrames)
{
    public const int SupportedCondition = 8;
    public const int RevealIterations = 20;
    public const int HideIterations = 10;
    public const int MaxHoldFrames = byte.MaxValue;
}
