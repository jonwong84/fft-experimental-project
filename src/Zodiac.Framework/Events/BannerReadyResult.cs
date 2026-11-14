using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record BannerReadyResult(BannerReadyState State, ImmutableArray<BannerReadyTrace> Trace,
    BannerSceneRejection Rejection, SceneTaskRejection TaskRejection, string? Diagnostic, int InstructionsExecuted, int AcceptedTaskUpdates)
{
    public bool Accepted => Rejection == BannerSceneRejection.None;
}
