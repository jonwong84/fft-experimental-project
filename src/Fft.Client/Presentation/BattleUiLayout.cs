using Fft.Core;
using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

public enum BattleUiAction { NextTurn, Move, Attack, Wait, Restart, SelectFirst, SelectSecond, SelectNext, Confirm, Cancel }
public sealed record BattleUiButton(BattleUiAction Action, string Label, PixelRect Bounds);

/// <summary>Authored UI/picking for the flat synthetic canvas. No original camera or movement rules.</summary>
public static class BattleUiLayout
{
    public static IReadOnlyList<BattleUiButton> Buttons { get; } = Array.AsReadOnly(new[]
    {
        new BattleUiButton(BattleUiAction.NextTurn, "N  NEXT TURN", new(728, 230, 208, 40)),
        new BattleUiButton(BattleUiAction.Move, "M  MOVE", new(728, 278, 208, 40)),
        new BattleUiButton(BattleUiAction.Attack, "A  ATTACK", new(728, 326, 208, 40)),
        new BattleUiButton(BattleUiAction.Wait, "W  WAIT", new(728, 374, 208, 40)),
        new BattleUiButton(BattleUiAction.Restart, "R  RESTART", new(728, 438, 208, 40)),
    });

    public static bool Contains(PixelRect r, PixelPoint p) => p.X >= r.X && p.Y >= r.Y &&
        p.X < (long)r.X + r.Width && p.Y < (long)r.Y + r.Height;

    /// <summary>Mouse positions are window coordinates; explicitly account for client/backbuffer scaling and bars.</summary>
    public static PixelPoint? CanvasPoint(PixelPoint mouse, int clientWidth, int clientHeight, int backWidth, int backHeight)
    {
        if (clientWidth <= 0 || clientHeight <= 0 || backWidth <= 0 || backHeight <= 0 ||
            mouse.X < 0 || mouse.Y < 0 || mouse.X >= clientWidth || mouse.Y >= clientHeight) return null;
        long bx = (long)mouse.X * backWidth / clientWidth, by = (long)mouse.Y * backHeight / clientHeight;
        var fit = SceneLayout.Fit(backWidth, backHeight);
        if (bx < fit.X || by < fit.Y || bx >= (long)fit.X + fit.Width || by >= (long)fit.Y + fit.Height) return null;
        return new((int)((bx - fit.X) * SceneLayout.CanvasWidth / fit.Width),
            (int)((by - fit.Y) * SceneLayout.CanvasHeight / fit.Height));
    }

    public static PixelRect UnitBounds(SceneUnit unit) => new(unit.Feet.X - 24, unit.Feet.Y - 72, 48, 72);
    public static PixelRect CardBounds(int index) => new(24, 220 + index * 142, 208, 126);

    public static TilePosition? PickTile(PixelPoint point, BattleMapDefinition map)
    {
        // Frontmost diamond wins shared edges, consistent with terrain painter order.
        for (int depth = map.Width + map.Depth - 2; depth >= 0; depth--)
        for (int x = map.Width - 1; x >= 0; x--)
        {
            int y = depth - x;
            if (y < 0 || y >= map.Depth) continue;
            var c = SceneLayout.TileCenter(x, y, map.Width, map.Depth);
            if (Math.Abs((long)point.X - c.X) * 40 + Math.Abs((long)point.Y - c.Y) * 80 <= 3200)
                return new(x, y, 0);
        }
        return null;
    }
}

/// <summary>Shared semantic routes for native input, headless tests and the finite Windows UI check.</summary>
public sealed class BattleUiRouter(BattleInteraction interaction)
{
    public BattleInteraction Interaction { get; } = interaction ?? throw new ArgumentNullException(nameof(interaction));
    public TilePosition Cursor { get; private set; } = new(0, 0, 0);

    public void Action(BattleUiAction action)
    {
        switch (action)
        {
            case BattleUiAction.NextTurn: Interaction.Advance(); SynchronizeCursor(); break;
            case BattleUiAction.Move: Interaction.SetMode(BattleInteractionMode.Move); SynchronizeCursor(); break;
            case BattleUiAction.Attack: Interaction.SetMode(BattleInteractionMode.Attack); SynchronizeCursor(); break;
            case BattleUiAction.Wait: Interaction.Wait(); break;
            case BattleUiAction.Restart: Interaction.Restart(); Cursor = new(0, 0, 0); break;
            case BattleUiAction.SelectFirst: Select(Interaction.State.Definition.FirstUnitId); break;
            case BattleUiAction.SelectSecond: Select(Interaction.State.Definition.SecondUnitId); break;
            case BattleUiAction.SelectNext:
                Select(Interaction.SelectedUnit == Interaction.State.Definition.FirstUnitId
                    ? Interaction.State.Definition.SecondUnitId : Interaction.State.Definition.FirstUnitId); break;
            case BattleUiAction.Confirm: Interaction.ClickTile(Cursor); break;
            case BattleUiAction.Cancel: Interaction.SetMode(BattleInteractionMode.Inspect); break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    public void MoveCursor(int dx, int dy)
    {
        var map = Interaction.State.Battle.TurnBattle.Battle.Map;
        Cursor = new((int)Math.Clamp((long)Cursor.X + dx, 0, map.Width - 1),
            (int)Math.Clamp((long)Cursor.Y + dy, 0, map.Depth - 1), 0);
    }

    public void Key(Keys key)
    {
        switch (key)
        {
            case Keys.N: case Keys.Space: Action(BattleUiAction.NextTurn); break;
            case Keys.M: Action(BattleUiAction.Move); break;
            case Keys.A: Action(BattleUiAction.Attack); break;
            case Keys.W: Action(BattleUiAction.Wait); break;
            case Keys.R: Action(BattleUiAction.Restart); break;
            case Keys.D1: Action(BattleUiAction.SelectFirst); break;
            case Keys.D2: Action(BattleUiAction.SelectSecond); break;
            case Keys.Tab: Action(BattleUiAction.SelectNext); break;
            case Keys.Enter: Action(BattleUiAction.Confirm); break;
            case Keys.Back: Action(BattleUiAction.Cancel); break;
            case Keys.Left: MoveCursor(-1, 0); break;
            case Keys.Right: MoveCursor(1, 0); break;
            case Keys.Up: MoveCursor(0, -1); break;
            case Keys.Down: MoveCursor(0, 1); break;
        }
    }

    public void Click(PixelPoint point)
    {
        foreach (var button in BattleUiLayout.Buttons)
            if (BattleUiLayout.Contains(button.Bounds, point)) { Action(button.Action); return; }
        for (int i = 0; i < 2; i++)
            if (BattleUiLayout.Contains(BattleUiLayout.CardBounds(i), point))
            { Action(i == 0 ? BattleUiAction.SelectFirst : BattleUiAction.SelectSecond); return; }
        // Sprites extend above their floor diamond. Pick them front-to-back before the floor.
        foreach (var unit in SceneLayout.Units(Interaction.State.Battle).Reverse())
            if (BattleUiLayout.Contains(BattleUiLayout.UnitBounds(unit), point))
            {
                var body = Interaction.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == unit.Id);
                Cursor = body.Position; Interaction.ClickTile(Cursor); return;
            }
        if (BattleUiLayout.PickTile(point, Interaction.State.Battle.TurnBattle.Battle.Map) is { } tile)
        { Cursor = tile; Interaction.ClickTile(tile); }
    }

    private void Select(BattleUnitId id)
    {
        Interaction.SetMode(BattleInteractionMode.Inspect); Interaction.Select(id); SynchronizeCursor();
    }
    private void SynchronizeCursor()
    {
        var body = Interaction.State.Battle.TurnBattle.Battle.Units.FirstOrDefault(u => u.Id == Interaction.SelectedUnit);
        if (body is not null) Cursor = body.Position;
    }
}
