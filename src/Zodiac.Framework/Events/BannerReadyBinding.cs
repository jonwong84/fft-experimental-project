using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record BannerReadyBinding(PreloadedBannerBinding General, PreloadedBannerBinding Ready);
