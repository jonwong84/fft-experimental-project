using System.Collections.Immutable;
using Microsoft.Xna.Framework.Input;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

public sealed record AccumulateCheckStep(BattleCheckInput Input, TwoUnitBattleCommand? Command = null, bool Restart = false, bool Resize = false);

/// <summary>Finite authored UI routes with a separately executed typed-command expectation for every step.</summary>
public static class AccumulateUiCheckPlan
{
    public static ImmutableArray<AccumulateCheckStep> Create(TwoUnitBattleDefinition definition, AssetId ability)
    {
        var steps = ImmutableArray.CreateBuilder<AccumulateCheckStep>();
        var state = definition.Start();
        var a = definition.FirstUnitId; var b = definition.SecondUnitId;
        Add(Keys.N, new TwoUnitBattleCommand.Advance(), "ready.png");
        AddButton(BattleUiAction.Accumulate, new TwoUnitBattleCommand.UseAbility(a, ability), "boosted.png");
        Add(Keys.C, new TwoUnitBattleCommand.UseAbility(a, ability), "duplicate-rejected.png");
        Add(Keys.D2, capture: "inactive-selection.png");
        Add(Keys.C, new TwoUnitBattleCommand.UseAbility(b, ability), "inactive-rejected.png");
        Add(Keys.D1);
        Add(Keys.M);
        Add(Keys.Down);
        Add(Keys.Enter, new TwoUnitBattleCommand.Move(a, new(0, 1, 0)), "moved.png");
        Add(Keys.N, new TwoUnitBattleCommand.Advance());
        Add(Keys.C, new TwoUnitBattleCommand.UseAbility(b, ability), "no-grant.png");
        Add(Keys.W, new TwoUnitBattleCommand.Wait(b));
        Add(Keys.N, new TwoUnitBattleCommand.Advance());
        Add(Keys.W, new TwoUnitBattleCommand.Wait(b));
        Add(Keys.N, new TwoUnitBattleCommand.Advance());
        Add(Keys.A);
        Attack("damage.png");
        Add(Keys.W, new TwoUnitBattleCommand.Wait(a));
        while (state.Battle.Combatants.Single(unit => unit.UnitId == a).PhysicalAttack < 99)
        {
            UntilA();
            bool last = state.Battle.Combatants.Single(unit => unit.UnitId == a).PhysicalAttack == 98;
            Add(Keys.C, new TwoUnitBattleCommand.UseAbility(a, ability), last ? "cap-reached.png" : null);
            Add(Keys.W, new TwoUnitBattleCommand.Wait(a));
        }
        UntilA();
        Add(Keys.D1, capture: "cap-ready.png", resize: true);
        AddButton(BattleUiAction.Accumulate, new TwoUnitBattleCommand.UseAbility(a, ability), "cap-used.png");
        Add(Keys.W, new TwoUnitBattleCommand.Wait(a));
        UntilA();
        Add(Keys.A);
        Attack("finished.png");
        Add(Keys.C, new TwoUnitBattleCommand.UseAbility(a, ability), "terminal-rejected.png");
        steps.Add(new(new(Keys.R, null, "restarted.png"), Restart: true));
        if (steps.Count > 1200) throw new InvalidOperationException("Authored ability check exceeded its input budget.");
        return steps.ToImmutable();

        void UntilA()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Add(Keys.N, new TwoUnitBattleCommand.Advance());
                if (state.Battle.TurnBattle.Turns.ActiveUnitId == a) return;
                Add(Keys.W, new TwoUnitBattleCommand.Wait(b));
            }
            throw new InvalidOperationException("Authored check did not reach A's next turn.");
        }
        void Add(Keys key, TwoUnitBattleCommand? command = null, string? capture = null, bool resize = false)
        { steps.Add(new(new(key, null, capture), command, Resize: resize)); Apply(command); }
        void AddButton(BattleUiAction action, TwoUnitBattleCommand command, string capture)
        {
            var bounds = BattleUiLayout.ButtonsFor(true).Single(button => button.Action == action).Bounds;
            steps.Add(new(new(null, new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), capture), command));
            Apply(command);
        }
        void Attack(string capture)
        {
            var feet = SceneLayout.TileCenter(1, 1, 2, 2);
            var command = new TwoUnitBattleCommand.Attack(a, b);
            steps.Add(new(new(null, new(feet.X, feet.Y - 36), capture), command)); Apply(command);
        }
        void Apply(TwoUnitBattleCommand? command)
        { if (command is not null) state = TwoUnitBattleScenario.Apply(state, command).State; }
    }
}
