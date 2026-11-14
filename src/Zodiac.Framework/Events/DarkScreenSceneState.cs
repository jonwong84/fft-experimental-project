using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed class DarkScreenSceneState
{
    public DarkScreenProgram Program { get; }
    public SceneEventState Event { get; }
    public DarkScreenState? Worker { get; }
    public ReadyDarkScreenBinding? ReadyBinding { get; }

    internal DarkScreenSceneState(DarkScreenProgram program, SceneEventState sceneEvent,
        DarkScreenState? worker, ReadyDarkScreenBinding? binding)
        => (Program, Event, Worker, ReadyBinding) = (program, sceneEvent, worker, binding);

    internal DarkScreenSceneState With(SceneEventState sceneEvent, DarkScreenState? worker) => new(Program, sceneEvent, worker, ReadyBinding);
}
