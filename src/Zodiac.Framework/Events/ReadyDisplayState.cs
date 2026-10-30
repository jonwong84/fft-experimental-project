namespace Zodiac.Framework.Events;

public sealed record ReadyDisplayDefinition(AssetId Id, int Condition, int HoldFrames);
public sealed record DisplayReady(ReadyDisplayDefinition Definition, AssetId ArtworkId, AssetId OwnerId);
public sealed record ReadyCancellation(SceneTaskId TaskId, long ExpectedRevision);
public readonly record struct ReadyChildId(SceneTaskId DispatcherTaskId);
public enum ReadyDispatcherPhase { WaitingInput, WaitingChild, Completed, Cancelled }
public enum ReadyChildPhase { FadeIn, Holding, FadeOut, ExitYield, Completed, Cancelled }
public enum ReadyExitCause { Natural, CircleSkipped, OwnerAcknowledgedCancellation }

/// <summary>One task-56 dispatcher owns a child with no separate ledger task. DrawScale is logical submission data, not a resource.</summary>
public sealed record ReadyDisplayState
{
    public ReadyDisplayDefinition Definition { get; private init; }
    public SceneTaskId TaskId { get; private init; }
    public ReadyChildId ChildId => new(TaskId);
    public AssetId OwnerId { get; private init; }
    public long StartTick { get; private init; }
    public long LastProcessedTick { get; private init; }
    public ReadyDispatcherPhase DispatcherPhase { get; private init; }
    public ReadyChildPhase ChildPhase { get; private init; }
    public int WaitSamplesTaken { get; private init; }
    public long? ReleaseTick { get; private init; }
    public ReadyExitCause? ExitCause { get; private init; }
    public int? DrawScale { get; private init; }
    public int FadeOutDraws { get; private init; }
    public bool ChildIsTerminal => ChildPhase is ReadyChildPhase.Completed or ReadyChildPhase.Cancelled;
    public bool IsTerminal => DispatcherPhase is ReadyDispatcherPhase.Completed or ReadyDispatcherPhase.Cancelled;

    private ReadyDisplayState(ReadyDisplayDefinition definition, SceneTaskId taskId, AssetId ownerId, long tick)
    {
        Definition = definition; TaskId = taskId; OwnerId = ownerId; StartTick = LastProcessedTick = tick;
        bool release = definition.HoldFrames == 0;
        DispatcherPhase = release ? ReadyDispatcherPhase.WaitingChild : ReadyDispatcherPhase.WaitingInput;
        ChildPhase = release ? ReadyChildPhase.FadeOut : ReadyChildPhase.FadeIn;
        DrawScale = release ? 128 : 48;
        ReleaseTick = release ? tick : null;
        ExitCause = release ? ReadyExitCause.Natural : null;
        FadeOutDraws = release ? 1 : 0;
    }

    // Initialization includes the draw preceding the first child yield; it is not an extra resume/tick.
    internal static ReadyDisplayState Initialize(ReadyDisplayDefinition definition, SceneTaskId id, AssetId owner, long tick)
        => new(definition, id, owner, tick);

    internal ReadyDisplayState ResumeDispatcher(long tick, bool circle)
    {
        if (IsTerminal) return this;
        if (DispatcherPhase == ReadyDispatcherPhase.WaitingChild)
            return ChildIsTerminal ? this with { DispatcherPhase = ReadyDispatcherPhase.Completed } : this;
        int samples = checked(WaitSamplesTaken + 1);
        return circle || samples == Definition.HoldFrames
            ? this with { WaitSamplesTaken = samples, DispatcherPhase = ReadyDispatcherPhase.WaitingChild,
                ReleaseTick = tick, ExitCause = circle ? ReadyExitCause.CircleSkipped : ReadyExitCause.Natural }
            : this with { WaitSamplesTaken = samples };
    }

    internal ReadyDisplayState ResumeChild(long tick)
    {
        var current = this with { LastProcessedTick = tick };
        if (ChildIsTerminal) return current;
        if (ReleaseTick == tick)
            return current with { ChildPhase = ReadyChildPhase.FadeOut, DrawScale = 128, FadeOutDraws = 1 };
        return ChildPhase switch
        {
            ReadyChildPhase.FadeIn => current with { DrawScale = DrawScale!.Value + 8,
                ChildPhase = DrawScale == 120 ? ReadyChildPhase.Holding : ReadyChildPhase.FadeIn },
            ReadyChildPhase.Holding => current with { DrawScale = 128 },
            ReadyChildPhase.FadeOut when FadeOutDraws < 7 => current with { DrawScale = 128 - 12 * FadeOutDraws, FadeOutDraws = FadeOutDraws + 1 },
            ReadyChildPhase.FadeOut => current with { ChildPhase = ReadyChildPhase.ExitYield, DrawScale = null },
            ReadyChildPhase.ExitYield => current with { ChildPhase = ReadyChildPhase.Completed, DrawScale = null },
            _ => current,
        };
    }

    // Explicit scene-owner policy; deliberately not the native child's parameter-3 value-1 path.
    internal ReadyDisplayState Cancel(long tick) => this with
    {
        LastProcessedTick = tick, DispatcherPhase = ReadyDispatcherPhase.Cancelled,
        ChildPhase = ChildIsTerminal ? ChildPhase : ReadyChildPhase.Cancelled,
        DrawScale = null, ExitCause = ReadyExitCause.OwnerAcknowledgedCancellation,
    };
}
