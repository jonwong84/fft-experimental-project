using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record BannerReadyTrace(BannerReadyTraceKind Kind, long Tick, SceneEventTrace? Event = null,
    SceneTaskTrace? Task = null, GeneralBannerState? General = null, ReadyDisplayState? Ready = null, bool? Circle = null);
