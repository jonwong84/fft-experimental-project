namespace Zodiac.Framework.Events;

public enum SceneEventSuspension { Ready, Frames, Barrier }

/// <summary>One immutable VM continuation and its scene-owned task ledger; the scene clock is authoritative.</summary>
public sealed class SceneEventState
{
    public SceneEventProgram Program { get; }
    internal ConditionalEventState Inner { get; }
    public SceneTaskState Tasks { get; }
    public AssetId ConsumerId { get; }
    public int Offset => Inner.Offset;
    public ConditionalEventVariables Variables => Inner.Variables;
    public bool Exited => Inner.Exited;
    public long Tick => Tasks.Tick;
    public SceneEventSuspension Suspension { get; }
    public int RemainingWaitTicks { get; }
    public int? WaitingAtOffset { get; }

    internal SceneEventState(SceneEventProgram program, ConditionalEventState inner, SceneTaskState tasks,
        AssetId consumerId, SceneEventSuspension suspension, int remainingWaitTicks, int? waitingAtOffset)
        => (Program, Inner, Tasks, ConsumerId, Suspension, RemainingWaitTicks, WaitingAtOffset) =
            (program, inner, tasks, consumerId, suspension, remainingWaitTicks, waitingAtOffset);
}
