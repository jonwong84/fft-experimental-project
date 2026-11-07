using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Caller-owned proof of prevalidated logical definitions. Device/file readiness belongs to the host.</summary>
public sealed record DarkScreenIntroductionBinding(ReadyDarkScreenBinding Dark,
    PreloadedBannerBinding General, PreloadedBannerBinding Ready);
