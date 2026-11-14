using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record DarkScreenIntroductionResult(DarkScreenIntroductionState State,
    ImmutableArray<DarkScreenIntroductionTrace> Trace, DarkScreenIntroductionRejection Rejection,
    SceneEventRejection EventRejection, SceneTaskRejection TaskRejection, string? Diagnostic,
    int InstructionsExecuted, int AcceptedTaskUpdates, int TicksConsumed, bool BattleBecameAvailable)
{
    public bool Accepted => Rejection == DarkScreenIntroductionRejection.None;
}
