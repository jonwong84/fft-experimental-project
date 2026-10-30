namespace Zodiac.Framework.Events;

/// <summary>Authored post-readiness definition, validated by BannerSceneExecutor.AttachBanner.</summary>
public sealed record GeneralBannerDefinition(AssetId Id, int Condition, int HoldFrames)
{
    public const int SupportedCondition = 8;
    public const int RevealIterations = 20;
    public const int HideIterations = 10;
    public const int MaxHoldFrames = byte.MaxValue;
}

public enum GeneralBannerPhase { Revealing, Holding, Hiding, ExitYield, Completed, Cancelled }
public enum GeneralBannerExitCause { Natural, CircleSkipped, OwnerAcknowledgedCancellation }
public sealed record BannerCancellation(SceneTaskId TaskId, long ExpectedRevision);

/// <summary>One ready banner's immutable logical lifetime. No quad, loading, input-device or renderer state.</summary>
public sealed class GeneralBannerState
{
    public GeneralBannerDefinition Definition { get; }
    public SceneTaskId TaskId { get; }
    public AssetId OwnerId { get; }
    public long StartTick { get; }
    public long LastProcessedTick { get; }
    public GeneralBannerPhase Phase { get; }
    public int RevealCompleted { get; }
    public int HoldCompleted { get; }
    public int HideCompleted { get; }
    public GeneralBannerExitCause? ExitCause { get; }
    public bool IsTerminal => Phase is GeneralBannerPhase.Completed or GeneralBannerPhase.Cancelled;

    internal GeneralBannerState(GeneralBannerDefinition definition, SceneTaskId taskId, AssetId ownerId,
        long startTick, long lastProcessedTick, GeneralBannerPhase phase, int reveal, int hold, int hide,
        GeneralBannerExitCause? exitCause)
        => (Definition, TaskId, OwnerId, StartTick, LastProcessedTick, Phase, RevealCompleted, HoldCompleted, HideCompleted, ExitCause) =
            (definition, taskId, ownerId, startTick, lastProcessedTick, phase, reveal, hold, hide, exitCause);

    internal GeneralBannerState Advance(long tick, bool circlePressed, bool cancel)
    {
        if (IsTerminal) return this;
        var phase = Phase;
        var cause = ExitCause;
        int reveal = RevealCompleted, hold = HoldCompleted, hide = HideCompleted;
        if (cancel)
        {
            phase = GeneralBannerPhase.Cancelled;
            cause = GeneralBannerExitCause.OwnerAcknowledgedCancellation;
        }
        else if (circlePressed && phase is GeneralBannerPhase.Revealing or GeneralBannerPhase.Holding)
        {
            phase = GeneralBannerPhase.ExitYield;
            cause = GeneralBannerExitCause.CircleSkipped;
        }
        else
        {
            switch (phase)
            {
                case GeneralBannerPhase.Revealing:
                    if (++reveal == GeneralBannerDefinition.RevealIterations)
                        phase = Definition.HoldFrames == 0 ? GeneralBannerPhase.Hiding : GeneralBannerPhase.Holding;
                    break;
                case GeneralBannerPhase.Holding:
                    if (++hold == Definition.HoldFrames) phase = GeneralBannerPhase.Hiding;
                    break;
                case GeneralBannerPhase.Hiding:
                    if (++hide == GeneralBannerDefinition.HideIterations)
                    {
                        phase = GeneralBannerPhase.ExitYield;
                        cause = GeneralBannerExitCause.Natural;
                    }
                    break;
                case GeneralBannerPhase.ExitYield:
                    phase = GeneralBannerPhase.Completed;
                    break;
            }
        }
        return new(Definition, TaskId, OwnerId, StartTick, tick, phase, reveal, hold, hide, cause);
    }
}

/// <summary>Retains one effect, including its terminal record, across event interpreter lifetimes.</summary>
public sealed class BannerSceneState
{
    public SceneEventState Event { get; }
    public GeneralBannerState? Banner { get; }
    internal BannerSceneState(SceneEventState sceneEvent, GeneralBannerState? banner) => (Event, Banner) = (sceneEvent, banner);
    public static BannerSceneState Create(SceneEventState sceneEvent)
    {
        ArgumentNullException.ThrowIfNull(sceneEvent);
        return new(sceneEvent, null);
    }
}
