using Fft.Client.Presentation;

namespace Fft.Client.Navigation;

[Flags]
public enum WorldMenuButtons { None = 0, Up = 1, Down = 2, Confirm = 4, Back = 8 }
public sealed record WorldMenuSample(WorldMenuButtons Keyboard = 0, WorldMenuButtons Controller = 0,
    bool Connected = false, bool Active = true, bool Left = false, PixelPoint Pointer = default);
public sealed record WorldMenuDispatch(WorldMenuInput? Input = null, WorldMenuAction? Action = null, bool Close = false)
{
    public WorldMenuResult Apply(WorldMenuSession session)
    {
        if ((Input.HasValue ? 1 : 0) + (Action.HasValue ? 1 : 0) + (Close ? 1 : 0) != 1)
            throw new ArgumentException("Exactly one menu action is required.");
        return Close ? session.RequestExit() : Input is { } input ? session.Handle(input) : session.Activate(Action!.Value);
    }
}

/// <summary>Presentation-only press edges. A single sample can dispatch at most one action.</summary>
public sealed class WorldMenuInputRouter
{
    private WorldMenuSample previous = new(Active: false);
    public void Baseline(WorldMenuSample sample) => previous = sample;
    public WorldMenuDispatch? Poll(WorldMenuSample sample, WorldMenuView view,
        int clientWidth, int clientHeight, int backWidth, int backHeight)
    {
        var before = previous; previous = sample;
        if (!sample.Active || !before.Active) return null;
        var fresh = sample.Keyboard & ~before.Keyboard;
        if (sample.Connected && before.Connected) fresh |= sample.Controller & ~before.Controller;
        if (fresh.HasFlag(WorldMenuButtons.Back)) return new(WorldMenuInput.Back);
        if (fresh.HasFlag(WorldMenuButtons.Up)) return new(WorldMenuInput.Up);
        if (fresh.HasFlag(WorldMenuButtons.Down)) return new(WorldMenuInput.Down);
        if (fresh.HasFlag(WorldMenuButtons.Confirm)) return new(WorldMenuInput.Confirm);
        if (sample.Left && !before.Left && BattleUiLayout.CanvasPoint(sample.Pointer, clientWidth, clientHeight, backWidth, backHeight) is { } canvas)
        {
            for (int i = 0; i < view.Entries.Length; i++)
                if (view.Entries[i].Enabled && BattleUiLayout.Contains(WorldMenuPresentation.Button(i, view), canvas))
                    return new(Action: view.Entries[i].Action);
        }
        return null;
    }
}
