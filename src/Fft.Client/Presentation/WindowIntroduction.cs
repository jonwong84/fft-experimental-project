using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

// Internal reuse for two existing typed sessions, not an engine/framework replacement contract.
internal interface IWindowIntroduction
{
    long Tick { get; }
    bool BattleAvailable { get; }
    int AvailabilityPublications { get; }
    long? AvailabilityTick { get; }
    BattleInteraction Interaction { get; }
    BattleUiRouter Router { get; }
    object Ownership { get; }
    object Trace { get; }
    object TraceMarker { get; }
    bool Advance(bool circle);
    bool Key(Keys key);
    bool Click(PixelPoint point);
    byte[] Compose();
    void Recreate(Action allocate);
    object Capture(string file, string pixelHash, string battleHash);
}

internal sealed class BannerWindowIntroduction(IntroductionBattleSession session) : IWindowIntroduction
{
    public long Tick => session.Gate.Introduction.Event.Tick;
    public bool BattleAvailable => session.Gate.BattleAvailable;
    public int AvailabilityPublications => session.AvailabilityPublications;
    public long? AvailabilityTick => session.AvailabilityTick;
    public BattleInteraction Interaction => session.Interaction;
    public BattleUiRouter Router => session.Router;
    public object Ownership => session.Gate;
    public object Trace => session.IntroductionTrace;
    public object TraceMarker => session.IntroductionTrace;
    public bool Advance(bool circle)
    {
        var result = session.AdvanceIntroduction(circle);
        if (!result.Accepted) throw new InvalidOperationException("Introduction rejected: " + result.Rejection);
        return result.BattleBecameAvailable;
    }
    public bool Key(Keys key) => session.Key(key);
    public bool Click(PixelPoint point) => session.Click(point);
    public byte[] Compose() => session.ComposeIntroduction();
    public void Recreate(Action allocate) => allocate();
    public object Capture(string file, string pixelHash, string battleHash)
    {
        var state = session.Gate.Introduction;
        return new
        {
            tick = Tick, file, rgbaSha256 = pixelHash,
            stage = state.Ready is null ? "general" : "ready", general = state.General?.Phase.ToString(), ready = state.Ready?.ChildPhase.ToString(),
            generalReveal = state.General?.RevealCompleted, generalHold = state.General?.HoldCompleted, generalHide = state.General?.HideCompleted,
            readyScale = state.Ready?.DrawScale, totalTasks = state.Event.Tasks.TotalStarted, liveTasks = state.Event.Tasks.LiveTasks,
            state.Event.Exited, battleAvailable = BattleAvailable, battleSha256 = battleHash, journalEntries = Interaction.Journal.Length,
        };
    }
}

internal sealed class DarkWindowIntroduction(DarkScreenIntroductionBattleSession session) : IWindowIntroduction
{
    public long Tick => session.State.Event.Tick;
    public bool BattleAvailable => session.State.BattleAvailable;
    public int AvailabilityPublications => session.AvailabilityPublications;
    public long? AvailabilityTick => session.AvailabilityTick;
    public BattleInteraction Interaction => session.Interaction;
    public BattleUiRouter Router => session.Router;
    public object Ownership => new { Introduction = session.State, BattleAvailable };
    public object Trace => session.Trace;
    public object TraceMarker => session.Trace.Length;
    public bool Advance(bool circle)
    {
        var result = session.AdvanceIntroduction(circle);
        if (!result.Accepted) throw new InvalidOperationException("Combined introduction rejected: " + result.Rejection);
        return result.BattleBecameAvailable;
    }
    public bool Key(Keys key) => session.Key(key);
    public bool Click(PixelPoint point) => session.Click(point);
    public byte[] Compose() => session.ComposeIntroduction();
    public void Recreate(Action allocate) => session.RecreateGraphics(allocate);
    public object Capture(string file, string pixelHash, string battleHash)
    {
        var state = session.State;
        return new
        {
            tick = Tick, file, rgbaSha256 = pixelHash,
            stage = state.Ready is not null ? "ready" : state.General is not null ? "general" : "dark",
            dark = state.Dark?.Phase.ToString(), darkDraw = state.Dark?.Draw.Kind.ToString(),
            darkDrawCount = state.Dark?.Draw.Tiles.Length, darkRevision = state.Event.Tasks.Tasks[0].Revision,
            general = state.General?.Phase.ToString(), ready = state.Ready?.ChildPhase.ToString(),
            generalReveal = state.General?.RevealCompleted, generalHold = state.General?.HoldCompleted, generalHide = state.General?.HideCompleted,
            readyScale = state.Ready?.DrawScale, totalTasks = state.Event.Tasks.TotalStarted, liveTasks = state.Event.Tasks.LiveTasks,
            state.Event.Exited, battleAvailable = BattleAvailable, battleSha256 = battleHash, journalEntries = Interaction.Journal.Length,
        };
    }
}
