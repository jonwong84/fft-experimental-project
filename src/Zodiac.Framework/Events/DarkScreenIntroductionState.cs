using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>One immutable scene; the battle gate is a pure fact, never a battle command.</summary>
public sealed class DarkScreenIntroductionState
{
    public DarkScreenIntroductionProgram Program { get; }
    public SceneEventState Event { get; }
    public DarkScreenState? Dark { get; }
    public GeneralBannerState? General { get; }
    public ReadyDisplayState? Ready { get; }
    public DarkScreenIntroductionBinding? Binding { get; }
    public bool BattleAvailable => Event.Exited && Dark?.IsTerminal == true && General?.IsTerminal == true &&
        Ready?.IsTerminal == true && Event.Tasks.LiveTasks == 0;

    internal DarkScreenIntroductionState(DarkScreenIntroductionProgram program, SceneEventState sceneEvent,
        DarkScreenState? dark, GeneralBannerState? general, ReadyDisplayState? ready, DarkScreenIntroductionBinding? binding)
        => (Program, Event, Dark, General, Ready, Binding) = (program, sceneEvent, dark, general, ready, binding);
    internal DarkScreenIntroductionState With(SceneEventState sceneEvent, DarkScreenState? dark,
        GeneralBannerState? general, ReadyDisplayState? ready) => new(Program, sceneEvent, dark, general, ready, Binding);
}
