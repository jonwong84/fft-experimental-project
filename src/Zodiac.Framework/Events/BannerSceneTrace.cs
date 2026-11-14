using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Ordered event/task facts plus explicit effect inputs and before/after snapshots. Terminal observations never advance the effect.</summary>
public sealed record BannerSceneTrace(BannerSceneTraceKind Kind, long Tick,
    SceneEventTrace? EventTrace = null, SceneTaskTrace? TaskTrace = null,
    GeneralBannerState? Before = null, GeneralBannerState? After = null, bool? CirclePressed = null,
    BannerCancellation? Cancellation = null, int? InstructionBudget = null, int? UpdateBudget = null,
    SceneEventState? StartedEvent = null);
