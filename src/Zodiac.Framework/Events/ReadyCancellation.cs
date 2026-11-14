namespace Zodiac.Framework.Events;

public sealed record ReadyCancellation(SceneTaskId TaskId, long ExpectedRevision);
