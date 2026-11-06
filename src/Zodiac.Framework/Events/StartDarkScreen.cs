using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record StartDarkScreen(DarkScreenDefinition Definition, AssetId OwnerId);
