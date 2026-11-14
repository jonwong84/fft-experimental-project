using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed class BannerReadyState
{
    public BannerReadyProgram Program { get; }
    public SceneEventState Event { get; }
    public GeneralBannerState? General { get; }
    public ReadyDisplayState? Ready { get; }
    public BannerReadyBinding? Binding { get; }
    internal BannerReadyState(BannerReadyProgram program, SceneEventState sceneEvent, GeneralBannerState? general,
        ReadyDisplayState? ready, BannerReadyBinding? binding) => (Program, Event, General, Ready, Binding) = (program, sceneEvent, general, ready, binding);
    internal BannerReadyState With(SceneEventState sceneEvent, GeneralBannerState? general, ReadyDisplayState? ready)
        => new(Program, sceneEvent, general, ready, Binding);
}
