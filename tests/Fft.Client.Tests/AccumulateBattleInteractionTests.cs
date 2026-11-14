using System.Text.Json;
using Fft.Client.Presentation;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Xunit;

namespace Fft.Client.Tests;

public sealed class AccumulateBattleInteractionTests
{
    private static AssetId Ability => new(SyntheticAccumulateBattle.AccumulateId.Value);
    private static BattleUnitId A => SyntheticTwoUnitBattle.FirstUnitId;
    private static BattleUnitId B => SyntheticTwoUnitBattle.SecondUnitId;

    [Fact]
    public void Direct_self_action_consumes_Act_without_target_mode_and_reports_PA_change()
    {
        var ui = new BattleInteraction(SyntheticAccumulateBattle.Create(), Ability);
        var router = new BattleUiRouter(ui);
        Assert.False(ui.CanUseSelfAbility);
        router.Key(Keys.C); Assert.Empty(ui.Journal); Assert.True(ui.StatusIsError);
        router.Key(Keys.N); Assert.True(ui.CanUseSelfAbility);
        string before = Snapshot(ui.State); int count = ui.Journal.Length;
        for (int i = 0; i < 5; i++) Assert.True(ui.CanUseSelfAbility);
        Assert.Equal(before, Snapshot(ui.State)); Assert.Equal(count, ui.Journal.Length);
        router.Key(Keys.M);
        Click(router, BattleUiAction.Accumulate);
        Assert.Equal(BattleInteractionMode.Inspect, ui.Mode);
        Assert.Equal(6, ui.State.Battle.Combatants[0].PhysicalAttack);
        Assert.Contains("PA 5 TO 6", ui.Status); Assert.False(ui.StatusIsError);
        Assert.False(ui.CanUseSelfAbility);
        var state = ui.State;
        router.Key(Keys.C);
        Assert.Same(state, ui.State); Assert.Equal(AttackCommandRejection.ActionAlreadyTaken, ui.LastResult!.AttackRejection);
        Assert.True(ui.StatusIsError);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Inactive_and_ungranted_selection_never_publishes_an_ability(bool activeB)
    {
        var ui = new BattleInteraction(SyntheticAccumulateBattle.Create(), Ability);
        var router = new BattleUiRouter(ui); router.Key(Keys.N);
        if (activeB) { router.Key(Keys.W); router.Key(Keys.N); }
        router.Key(Keys.D2);
        Assert.False(ui.CanUseSelfAbility);
        var state = ui.State; router.Key(Keys.C);
        Assert.Same(state, ui.State); Assert.Empty(ui.LastResult!.Events);
        Assert.Equal(activeB ? AttackCommandRejection.AbilityNotGranted : AttackCommandRejection.InactiveActor, ui.LastResult.AttackRejection);
    }

    [Fact]
    public void At_cap_is_usable_and_consumed_and_restart_restores_starting_PA()
    {
        var ui = new BattleInteraction(SyntheticAccumulateBattle.Create(99), Ability);
        var router = new BattleUiRouter(ui); router.Key(Keys.N); Assert.True(ui.CanUseSelfAbility);
        router.Key(Keys.C);
        Assert.True(ui.LastResult!.Accepted); Assert.Contains("PA 99 MAX", ui.Status);
        Assert.False(ui.CanUseSelfAbility); Assert.Equal(99, ui.State.Battle.Combatants[0].PhysicalAttack);
        Assert.Empty(ui.LastResult.Events.OfType<TwoUnitBattleTransitionEvent>().Select(ev => ev.Event).OfType<AttackPhysicalAttackChangedEvent>());
        router.Key(Keys.R);
        Assert.Empty(ui.Journal); Assert.Null(ui.SelectedUnit); Assert.Equal(99, ui.State.Battle.Combatants[0].PhysicalAttack);
        // Restart derives setup PA rather than subtracting an assumed number of boosts.
        var normal = new BattleInteraction(SyntheticAccumulateBattle.Create(), Ability);
        var normalRouter = new BattleUiRouter(normal); normalRouter.Key(Keys.N); normalRouter.Key(Keys.C); normalRouter.Key(Keys.R);
        Assert.Equal(5, normal.State.Battle.Combatants[0].PhysicalAttack);
    }

    [Fact]
    public void Legacy_layout_and_C_route_remain_unchanged_without_the_selected_effect()
    {
        Assert.Same(BattleUiLayout.Buttons, BattleUiLayout.ButtonsFor(false));
        Assert.DoesNotContain(BattleUiLayout.Buttons, button => button.Action == BattleUiAction.Accumulate);
        var old = new BattleInteraction(SyntheticTwoUnitBattle.Create()); var router = new BattleUiRouter(old);
        var state = old.State; router.Key(Keys.C);
        Assert.Same(state, old.State); Assert.Empty(old.Journal); Assert.Equal("ADVANCE TO START", old.Status);
        Assert.Throws<ArgumentException>(() => new BattleInteraction(SyntheticTwoUnitBattle.Create(), Ability));
    }

    [Fact]
    public void Held_C_presses_and_focus_baselines_do_not_repeat_an_action()
    {
        var input = new BattleInputBuffer();
        input.KeyDown(Keys.C); input.KeyDown(Keys.C);
        Assert.True(input.TryTake(out var key)); Assert.Equal(Keys.C, key); Assert.False(input.TryTake(out _));
        input.Baseline([Keys.C], false); input.KeyDown(Keys.C); Assert.False(input.TryTake(out _));
        input.KeyUp(Keys.C); input.KeyDown(Keys.C); Assert.True(input.TryTake(out _));
        input.KeyUp(Keys.C); input.KeyDown(Keys.C); input.ClearQueued(); Assert.False(input.TryTake(out _));
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Invalid_host_options_fail_before_any_output_or_native_launch(int kind)
    {
        string[] args = kind switch
        {
            0 => ["--accumulate-battle"],
            1 => ["--accumulate-battle", "--wrong", "unused"],
            _ => ["--accumulate-battle", "--content-root", "unused", "--wrong", "unused"],
        };
        Assert.Equal(2, AccumulateBattleHost.Run(args));
    }

    [Fact]
    public void Missing_prepared_input_fails_before_creating_the_check_directory()
    {
        string root = Path.Combine(Path.GetTempPath(), "fft-accumulate-missing-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(root, "output");
        Assert.Equal(1, AccumulateBattleHost.Run(["--accumulate-battle", "--content-root", root, "--check", output]));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Ability_buttons_are_distinct_inside_the_canvas_and_all_routes_match_the_headless_plan()
    {
        var buttons = BattleUiLayout.ButtonsFor(true);
        foreach (var left in buttons)
        {
            Assert.InRange(left.Bounds.X + left.Bounds.Width, 0, SceneLayout.CanvasWidth);
            Assert.InRange(left.Bounds.Y + left.Bounds.Height, 0, SceneLayout.CanvasHeight);
            foreach (var right in buttons.Where(button => button != left))
                Assert.False(left.Bounds.X < right.Bounds.X + right.Bounds.Width && right.Bounds.X < left.Bounds.X + left.Bounds.Width &&
                    left.Bounds.Y < right.Bounds.Y + right.Bounds.Height && right.Bounds.Y < left.Bounds.Y + left.Bounds.Height);
        }
        var definition = SyntheticAccumulateBattle.Create();
        var ui = new BattleInteraction(definition, Ability); var router = new BattleUiRouter(ui);
        var expected = definition.Start(); var events = new List<TwoUnitBattleEvent>();
        int accepted = 0, rejected = 0; bool capReady = false, capUsed = false, damage = false, replayed = false;
        var plan = AccumulateUiCheckPlan.Create(definition, Ability);
        Assert.InRange(plan.Length, 100, 1200);
        foreach (var step in plan)
        {
            TwoUnitBattleResult? result = null;
            if (step.Restart)
            {
                var replay = definition.Start(); var replayEvents = new List<TwoUnitBattleEvent>();
                foreach (var command in ui.Journal)
                { var replayResult = TwoUnitBattleScenario.Apply(replay, command); replay = replayResult.State; replayEvents.AddRange(replayResult.Events); }
                Assert.Equal(Snapshot(ui.State), Snapshot(replay)); Assert.Equal(events.ToArray(), replayEvents.ToArray());
                replayed = true; expected = definition.Start();
            }
            if (step.Command is not null)
            {
                result = TwoUnitBattleScenario.Apply(expected, step.Command); expected = result.State;
                if (result.Accepted) accepted++; else rejected++;
                events.AddRange(result.Events);
            }
            if (step.Input.Key is { } key) router.Key(key);
            if (step.Input.Click is { } click) router.Click(click);
            Assert.Equal(Snapshot(expected), Snapshot(ui.State));
            if (result is not null)
            {
                Assert.Equal(step.Command, ui.Journal[^1]); Assert.Equal(result.Accepted, ui.LastResult!.Accepted);
                Assert.Equal(result.Events.ToArray(), ui.LastResult.Events.ToArray());
            }
            if (step.Input.Capture == "cap-ready.png") { Assert.True(ui.CanUseSelfAbility); capReady = true; }
            if (step.Input.Capture == "cap-used.png") { Assert.False(ui.CanUseSelfAbility); Assert.Contains("PA 99 MAX", ui.Status); capUsed = true; }
            if (step.Input.Capture == "damage.png") { Assert.Contains("HIT 24", ui.Status); damage = true; }
        }
        Assert.True(capReady && capUsed && damage && replayed);
        Assert.Equal(4, rejected); Assert.True(accepted > 100);
        Assert.Equal(Snapshot(definition.Start()), Snapshot(ui.State)); Assert.Empty(ui.Journal);
        Assert.Equal(14, plan.Count(step => step.Input.Capture is not null)); // Plus the window's initial start capture.
    }

    private static void Click(BattleUiRouter router, BattleUiAction action)
    { var bounds = BattleUiLayout.ButtonsFor(true).Single(button => button.Action == action).Bounds; router.Click(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)); }
    private static string Snapshot(TwoUnitBattleState state) => JsonSerializer.Serialize(new
    { state.Outcome, state.WinnerId, state.Battle.Combatants, state.Battle.TurnBattle.Turns, units = state.Battle.TurnBattle.Battle.Units.Select(unit => new { unit.Id, unit.Position, unit.HasMoved }) });
}
