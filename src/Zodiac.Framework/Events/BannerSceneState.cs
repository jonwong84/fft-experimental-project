namespace Zodiac.Framework.Events;

/// <summary>Retains one effect, including its terminal record, across event interpreter lifetimes.</summary>
public sealed class BannerSceneState
{
    public SceneEventState Event { get; }
    public GeneralBannerState? Banner { get; }
    internal BannerSceneState(SceneEventState sceneEvent, GeneralBannerState? banner) => (Event, Banner) = (sceneEvent, banner);
    public static BannerSceneState Create(SceneEventState sceneEvent)
    {
        ArgumentNullException.ThrowIfNull(sceneEvent);
        return new(sceneEvent, null);
    }
}
