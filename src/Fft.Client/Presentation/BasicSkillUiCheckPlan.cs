using System.Collections.Immutable;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content.Samples;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

public sealed record BasicSkillCheckStep(BattleCheckInput Input, TwoUnitBattleCommand? Command = null,
    bool Restart = false, bool Resize = false, bool Focus = false);

/// <summary>Finite ordinary UI routes paired with separately executed typed expectations; no state injection.</summary>
public static class BasicSkillUiCheckPlan
{
    public static ImmutableArray<BasicSkillCheckStep> Create(TwoUnitBattleDefinition definition, AssetId self, AssetId targeted)
    {
        var steps = ImmutableArray.CreateBuilder<BasicSkillCheckStep>();
        var state = definition.Start();
        var a = definition.FirstUnitId; var b = definition.SecondUnitId;
        Add(Keys.D1);
        Aim(a, b, "not-ready.png");
        Command(new TwoUnitBattleCommand.Advance(), "ready.png");
        Add(Keys.T, capture: "range.png", resize: true, focus: true);
        Add(Keys.Back, capture: "cancelled.png");
        Add(Keys.T); Add(Keys.Enter, new TwoUnitBattleCommand.UseTargetedAbility(a, targeted, a), "self-rejected.png");
        Add(Keys.D2); Aim(b, a, "inactive-rejected.png");
        Add(Keys.D1); Add(Keys.T); Add(Keys.Up); Add(Keys.Enter, capture: "empty-target.png");
        Command(new TwoUnitBattleCommand.Move(a, new(1, 0, 0)));
        Command(new TwoUnitBattleCommand.Wait(a)); UntilA();
        Command(new TwoUnitBattleCommand.Move(a, new(0, 0, 0)));
        Aim(a, b, "out-of-range.png");
        Restart("range-restarted.png");

        int hits = 0, terminal = 0;
        foreach (var command in SyntheticBasicSkillBattle.Journal())
        {
            string? capture = command switch
            {
                TwoUnitBattleCommand.UseAbility => state.Battle.Combatants[0].PhysicalAttack == 5 ? "boosted.png" : "act-used.png",
                TwoUnitBattleCommand.Move when state.Outcome == TwoUnitBattleOutcome.InProgress => "moved.png",
                TwoUnitBattleCommand.UseTargetedAbility stone when stone.ActorId == b => "no-grant.png",
                TwoUnitBattleCommand.UseTargetedAbility when state.Outcome == TwoUnitBattleOutcome.InProgress => $"stone-{++hits}.png",
                _ => null,
            };
            if (state.Outcome != TwoUnitBattleOutcome.InProgress) capture = $"terminal-{++terminal}.png";
            Command(command, capture, pointer: hits % 2 == 1);
            if (command is TwoUnitBattleCommand.UseAbility && capture == "boosted.png") Aim(a, b, "stone-act-used.png");
        }
        Restart("restarted.png");
        if (steps.Count > 300) throw new InvalidOperationException("Basic Skill UI input budget exceeded.");
        return steps.ToImmutable();

        void UntilA()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Command(new TwoUnitBattleCommand.Advance());
                if (state.Battle.TurnBattle.Turns.ActiveUnitId == a) return;
                Command(new TwoUnitBattleCommand.Wait(b));
            }
            throw new InvalidOperationException("Basic Skill check did not reach A's turn.");
        }
        void Restart(string capture)
        { steps.Add(new(new(Keys.R, null, capture), Restart: true)); state = definition.Start(); }
        void Aim(BattleUnitId actor, BattleUnitId target, string? capture, bool pointer = false)
        {
            if (pointer)
            {
                var button = BattleUiLayout.ButtonsFor(true, true).Single(button => button.Action == BattleUiAction.ThrowStone).Bounds;
                steps.Add(new(new(null, new(button.X + 100, button.Y + 20), null)));
                var body = state.Battle.TurnBattle.Battle.Units.Single(unit => unit.Id == target);
                var feet = SceneLayout.TileCenter(body.Position.X, body.Position.Y, 7, 5, true);
                var command = new TwoUnitBattleCommand.UseTargetedAbility(actor, targeted, target);
                steps.Add(new(new(null, new(feet.X, feet.Y - 18), capture), command)); Apply(command);
            }
            else
            {
                Add(Keys.T);
                Navigate(state.Battle.TurnBattle.Battle.Units.Single(unit => unit.Id == actor).Position,
                    state.Battle.TurnBattle.Battle.Units.Single(unit => unit.Id == target).Position);
                Add(Keys.Enter, new TwoUnitBattleCommand.UseTargetedAbility(actor, targeted, target), capture);
            }
        }
        void Command(TwoUnitBattleCommand command, string? capture = null, bool pointer = false)
        {
            switch (command)
            {
                case TwoUnitBattleCommand.Advance: Add(Keys.N, command, capture); break;
                case TwoUnitBattleCommand.Wait: Add(Keys.W, command, capture); break;
                case TwoUnitBattleCommand.UseAbility: Add(Keys.C, command, capture); break;
                case TwoUnitBattleCommand.UseTargetedAbility stone: Aim(stone.ActorId, stone.TargetId, capture, pointer); break;
                case TwoUnitBattleCommand.Move move:
                    Add(Keys.M); Navigate(state.Battle.TurnBattle.Battle.Units.Single(unit => unit.Id == move.UnitId).Position, move.Destination);
                    Add(Keys.Enter, command, capture); break;
                case TwoUnitBattleCommand.Attack attack:
                    Add(Keys.A); Navigate(state.Battle.TurnBattle.Battle.Units.Single(unit => unit.Id == attack.ActorId).Position,
                        state.Battle.TurnBattle.Battle.Units.Single(unit => unit.Id == attack.TargetId).Position);
                    Add(Keys.Enter, command, capture); break;
                default: throw new InvalidOperationException("Unknown finite-plan command.");
            }
        }
        void Navigate(TilePosition from, TilePosition to)
        {
            for (int x = from.X; x != to.X; x += Math.Sign(to.X - from.X)) Add(to.X > from.X ? Keys.Right : Keys.Left);
            for (int y = from.Y; y != to.Y; y += Math.Sign(to.Y - from.Y)) Add(to.Y > from.Y ? Keys.Down : Keys.Up);
        }
        void Add(Keys key, TwoUnitBattleCommand? command = null, string? capture = null, bool resize = false, bool focus = false)
        { steps.Add(new(new(key, null, capture), command, Resize: resize, Focus: focus)); Apply(command); }
        void Apply(TwoUnitBattleCommand? command)
        { if (command is not null) state = TwoUnitBattleScenario.Apply(state, command).State; }
    }
}
