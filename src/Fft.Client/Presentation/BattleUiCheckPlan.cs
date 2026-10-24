using System.Collections.Immutable;
using Fft.Content.Samples;
using Fft.Core;
using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

public sealed record BattleCheckInput(Keys? Key, PixelPoint? Click, string? Capture = null);

/// <summary>Opt-in, programmed UI input sequence. No native input injection or simulation shortcut.</summary>
public static class BattleUiCheckPlan
{
    public static ImmutableArray<BattleCheckInput> Inputs { get; } =
    [
        Button(BattleUiAction.NextTurn),
        new(Keys.N, null, "rejected.png"), // The core rejects a second Advance while a turn is active.
        new(Keys.M, null, "moves.png"),
        Tile(0, 1),
        new(Keys.A, null, "targets.png"),
        Unit(1, 1),
        new(Keys.N, null),
        new(Keys.A, null),
        new(Keys.Left, null),
        new(Keys.Enter, null),
        Button(BattleUiAction.Wait),
        new(Keys.N, null),
        Button(BattleUiAction.Move),
        Tile(1, 0),
        new(Keys.W, null),
        Button(BattleUiAction.NextTurn),
        new(Keys.M, null),
        new(Keys.Up, null),
        new(Keys.Enter, null),
        Button(BattleUiAction.Attack),
        Unit(1, 0) with { Capture = "finished.png" },
        new(Keys.N, null, "terminal-rejected.png"), // Terminal lock: no further tick, HP or RNG effects.
    ];

    public static TwoUnitBattleState Expected()
    {
        var state = SyntheticTwoUnitBattle.Create().Start();
        foreach (var command in SyntheticTwoUnitBattle.Commands)
        {
            var result = TwoUnitBattleScenario.Apply(state, command);
            if (!result.Accepted) throw new InvalidOperationException("Authored headless check command failed.");
            state = result.State;
        }
        return state;
    }

    private static BattleCheckInput Button(BattleUiAction action)
    {
        var b = BattleUiLayout.Buttons.Single(b => b.Action == action).Bounds;
        return new(null, new(b.X + b.Width / 2, b.Y + b.Height / 2));
    }
    private static BattleCheckInput Tile(int x, int y) => new(null, SceneLayout.TileCenter(x, y, 2, 2));
    private static BattleCheckInput Unit(int x, int y)
    {
        var feet = SceneLayout.TileCenter(x, y, 2, 2);
        return new(null, new(feet.X, feet.Y - 36));
    }
}
