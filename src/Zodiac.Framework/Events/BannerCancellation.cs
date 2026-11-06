namespace Zodiac.Framework.Events;

public sealed record BannerCancellation(SceneTaskId TaskId, long ExpectedRevision);
