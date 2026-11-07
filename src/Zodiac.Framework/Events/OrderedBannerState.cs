using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>One scene retains its banner and binding across consumer lifetimes, including terminal tombstones.</summary>
public sealed class OrderedBannerState
{
    public OrderedBannerProgram Program { get; }
    public SceneEventState Event { get; }
    public GeneralBannerState? Banner { get; }
    public PreloadedBannerBinding? ReadyBinding { get; }
    internal BannerSceneState Scene => new(Event, Banner);

    internal OrderedBannerState(OrderedBannerProgram program, SceneEventState sceneEvent,
        GeneralBannerState? banner, PreloadedBannerBinding? readyBinding)
        => (Program, Event, Banner, ReadyBinding) = (program, sceneEvent, banner, readyBinding);

    internal OrderedBannerState With(SceneEventState sceneEvent, GeneralBannerState? banner)
        => new(Program, sceneEvent, banner, ReadyBinding);
}
