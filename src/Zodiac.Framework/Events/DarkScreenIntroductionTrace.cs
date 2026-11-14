using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record DarkScreenIntroductionTrace(DarkScreenIntroductionTraceKind Kind, long Tick,
    SceneEventTrace? Event = null, SceneTaskTrace? Task = null, DarkScreenState? Dark = null,
    GeneralBannerState? General = null, ReadyDisplayState? Ready = null, bool? Circle = null);
