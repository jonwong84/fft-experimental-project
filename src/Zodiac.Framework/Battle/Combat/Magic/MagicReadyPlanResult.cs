namespace Zodiac.Framework;

/// <summary>Resource block diagnostic or atomic cast proposal; no separate partially payable MP proposal.</summary>
public sealed record MagicReadyPlanResult(MagicMpBlock? ResourceBlock, MagicCastResolutionResult Resolution)
{
    public bool Accepted => Resolution.Accepted;
}
