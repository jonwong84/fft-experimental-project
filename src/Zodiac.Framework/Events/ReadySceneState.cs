using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed class ReadySceneState
{
    public ReadySceneProgram Program { get; }
    public SceneEventState Event { get; }
    public ReadyDisplayState? Ready { get; }
    public PreloadedBannerBinding? Binding { get; }
    internal ReadySceneState(ReadySceneProgram program, SceneEventState sceneEvent, ReadyDisplayState? ready, PreloadedBannerBinding? binding)
        => (Program, Event, Ready, Binding) = (program, sceneEvent, ready, binding);
    internal ReadySceneState With(SceneEventState sceneEvent, ReadyDisplayState? ready) => new(Program, sceneEvent, ready, Binding);
}
