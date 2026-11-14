using Fft.Client.Presentation;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Xunit;

namespace Fft.Client.Tests;

public sealed class BasicSkillBattleInteractionTests
{
    private static AssetId Self => new(SyntheticBasicSkillBattle.Accumulate.Value);
    private static AssetId Stone => new(SyntheticBasicSkillBattle.ThrowStone.Value);
    private static BattleUnitId A => SyntheticBasicSkillBattle.First;
    private static BattleUnitId B => SyntheticBasicSkillBattle.Second;
    private static BattleInteraction Create() => new(SyntheticBasicSkillBattle.Create(), Self, Stone);

    [Fact]
    public void Targeting_cancel_and_repeated_probes_preserve_state_history_and_both_RNG_copies()
    {
        var ui = Create(); var router = new BattleUiRouter(ui); router.Key(Keys.N);
        var state = ui.State; string before = BasicSkillBattleCheck.Snapshot(state); int history = ui.Journal.Length;
        for (int i = 0; i < 5; i++)
        {
            Assert.True(ui.CanUseTargetedAbility); Assert.Equal(B, Assert.Single(ui.TargetedTargets));
            Assert.Contains(new TilePosition(5, 2, 0), ui.TargetedRange); Assert.DoesNotContain(new TilePosition(6, 2, 0), ui.TargetedRange);
            router.Key(Keys.T); Assert.Equal(BattleInteractionMode.ThrowStone, ui.Mode);
            router.Key(Keys.Right); router.Key(Keys.Back); Assert.Equal(BattleInteractionMode.Inspect, ui.Mode);
        }
        Assert.Same(state, ui.State); Assert.Equal(before, BasicSkillBattleCheck.Snapshot(ui.State)); Assert.Equal(history, ui.Journal.Length);
        router.Key(Keys.T); router.Key(Keys.Right); router.Key(Keys.Right); router.Key(Keys.Enter);
        Assert.True(ui.LastResult!.Accepted); Assert.Equal(BattleInteractionMode.Inspect, ui.Mode);
        Assert.Contains("STONE HIT", ui.Status); Assert.False(ui.CanUseTargetedAbility); Assert.False(ui.CanUseSelfAbility);
    }

    [Fact]
    public void Empty_self_inactive_and_used_targets_have_atomic_feedback()
    {
        var ui = Create(); var router = new BattleUiRouter(ui); router.Key(Keys.N);
        var state = ui.State; int history = ui.Journal.Length;
        router.Key(Keys.T); router.Key(Keys.Up); router.Key(Keys.Enter);
        Assert.Contains("NO TARGET", ui.Status); Assert.Equal(history, ui.Journal.Length); Assert.Same(state, ui.State);
        router.Key(Keys.T); router.Key(Keys.Enter);
        Assert.False(ui.LastResult!.Accepted); Assert.Same(state, ui.State); Assert.Empty(ui.LastResult.Events);
        router.Key(Keys.D2); router.Key(Keys.T); router.Key(Keys.Left); router.Key(Keys.Left); router.Key(Keys.Enter);
        Assert.Equal(AttackCommandRejection.InactiveActor, ui.LastResult!.AttackRejection); Assert.Same(state, ui.State);
        router.Key(Keys.D1); router.Key(Keys.C); state = ui.State;
        router.Key(Keys.T); router.Key(Keys.Right); router.Key(Keys.Right); router.Key(Keys.Enter);
        Assert.Equal(AttackCommandRejection.ActionAlreadyTaken, ui.LastResult!.AttackRejection); Assert.Same(state, ui.State);
        Assert.Contains("ACTION ALREADY TAKEN", ui.Status);
    }

    [Fact]
    public void Legacy_T_route_is_inert_and_explicit_unresolved_effect_is_rejected()
    {
        var ui = new BattleInteraction(SyntheticTwoUnitBattle.Create()); var router = new BattleUiRouter(ui);
        router.Key(Keys.T); Assert.Equal(BattleInteractionMode.Inspect, ui.Mode); Assert.Empty(ui.Journal);
        Assert.Equal("ADVANCE TO START", ui.Status);
        Assert.Same(BattleUiLayout.ButtonsFor(true), BattleUiLayout.ButtonsFor(true, false));
        Assert.Throws<ArgumentException>(() => new BattleInteraction(SyntheticTwoUnitBattle.Create(), targetedAbilityId: Stone));
    }

    [Fact]
    public void Compact_projection_and_picking_keep_all_35_diamonds_between_cards_and_actions()
    {
        var map = Create().State.Battle.TurnBattle.Battle.Map;
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var center = SceneLayout.TileCenter(x, y, map.Width, map.Depth, true);
            Assert.InRange(center.X - 32, 240, 720); Assert.InRange(center.X + 32, 240, 720);
            Assert.InRange(center.Y - 16, 210, 450); Assert.InRange(center.Y + 16, 210, 450);
            Assert.Equal(new TilePosition(x, y, 0), BattleUiLayout.PickTile(center, map, true));
            var pp = SceneLayout.Fit(1280, 720);
            var window = new PixelPoint(pp.X + center.X * pp.Width / 960, pp.Y + center.Y * pp.Height / 640);
            var canvas = BattleUiLayout.CanvasPoint(window, 1280, 720, 1280, 720);
            Assert.NotNull(canvas); Assert.Equal(new TilePosition(x, y, 0), BattleUiLayout.PickTile(canvas.Value, map, true));
        }
        foreach (var left in BattleUiLayout.ButtonsFor(true, true))
        {
            Assert.InRange(left.Bounds.Y + left.Bounds.Height, 0, 560);
            foreach (var right in BattleUiLayout.ButtonsFor(true, true).Where(button => button != left))
                Assert.False(left.Bounds.Y < right.Bounds.Y + right.Bounds.Height && right.Bounds.Y < left.Bounds.Y + left.Bounds.Height);
        }
    }

    [Fact]
    public void Finite_keyboard_pointer_plan_matches_each_headless_state_events_and_both_journal_replays()
    {
        var ui = Create(); var router = new BattleUiRouter(ui); var definition = ui.State.Definition;
        var expected = definition.Start(); var events = new List<TwoUnitBattleEvent>();
        var captures = new HashSet<string>(); int replays = 0, hits = 0, pushes = 0, rejected = 0;
        var plan = BasicSkillUiCheckPlan.Create(definition, Self, Stone);
        Assert.InRange(plan.Length, 50, 300); Assert.Single(plan.Where(step => step.Resize)); Assert.Single(plan.Where(step => step.Focus));
        foreach (var step in plan)
        {
            int count = ui.Journal.Length;
            if (step.Restart)
            {
                var replay = definition.Start(); var replayEvents = new List<TwoUnitBattleEvent>();
                foreach (var command in ui.Journal)
                { var result = TwoUnitBattleScenario.Apply(replay, command); replay = result.State; replayEvents.AddRange(result.Events); }
                Assert.Equal(BasicSkillBattleCheck.Snapshot(ui.State), BasicSkillBattleCheck.Snapshot(replay));
                Assert.Equal(events.ToArray(), replayEvents.ToArray()); replays++; events.Clear(); expected = definition.Start();
            }
            TwoUnitBattleResult? resultExpected = null;
            if (step.Command is not null)
            {
                resultExpected = TwoUnitBattleScenario.Apply(expected, step.Command); expected = resultExpected.State;
                events.AddRange(resultExpected.Events);
                if (!resultExpected.Accepted) rejected++;
                hits += resultExpected.Events.OfType<TwoUnitBattleTransitionEvent>().Count(ev => ev.Event is ThrowStoneResolvedEvent);
                pushes += resultExpected.Events.OfType<TwoUnitBattleTransitionEvent>().Count(ev => ev.Event is ThrowStoneKnockbackEvent);
            }
            if (step.Input.Key is { } key) router.Key(key);
            if (step.Input.Click is { } click) router.Click(click);
            Assert.Equal(BasicSkillBattleCheck.Snapshot(expected), BasicSkillBattleCheck.Snapshot(ui.State));
            if (resultExpected is not null)
            {
                Assert.Equal(count + 1, ui.Journal.Length); Assert.Equal(step.Command, ui.Journal[^1]);
                Assert.Equal(resultExpected.Accepted, ui.LastResult!.Accepted); Assert.Equal(resultExpected.Events.ToArray(), ui.LastResult.Events.ToArray());
            }
            else if (!step.Restart) Assert.Equal(count, ui.Journal.Length);
            if (step.Input.Capture is { } capture)
            {
                Assert.True(captures.Add(capture));
                if (capture == "out-of-range.png") Assert.Equal(AttackCommandRejection.OutOfRange, ui.LastResult!.AttackRejection);
                if (capture == "no-grant.png") Assert.Equal(AttackCommandRejection.AbilityNotGranted, ui.LastResult!.AttackRejection);
                if (capture == "cancelled.png") Assert.True(ui.CanUseTargetedAbility);
                if (capture == "stone-6.png") Assert.Equal(TwoUnitBattleOutcome.FirstUnitWon, ui.State.Outcome);
            }
        }
        Assert.Equal(2, replays); Assert.Equal(6, hits); Assert.Equal(3, pushes); Assert.Equal(13, rejected);
        Assert.Empty(ui.Journal); Assert.Null(ui.SelectedUnit); Assert.Equal(BasicSkillBattleCheck.Snapshot(definition.Start()), BasicSkillBattleCheck.Snapshot(ui.State));
    }

    [Fact]
    public void Held_T_and_pointer_are_baselined_across_focus_boundaries()
    {
        var input = new BattleInputBuffer(); input.KeyDown(Keys.T); input.KeyDown(Keys.T);
        Assert.True(input.TryTake(out var key)); Assert.Equal(Keys.T, key); Assert.False(input.TryTake(out _));
        input.KeyUp(Keys.T); input.KeyDown(Keys.T); input.ClearQueued(); Assert.False(input.TryTake(out _));
        input.Baseline([Keys.T], true); input.KeyDown(Keys.T); Assert.False(input.TryTake(out _)); Assert.False(input.LeftPress(true));
        input.KeyUp(Keys.T); input.KeyDown(Keys.T); Assert.True(input.TryTake(out _));
        Assert.False(input.LeftPress(false)); Assert.True(input.LeftPress(true));
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Host_rejects_invalid_syntax_without_launch(int kind)
    {
        string[] args = kind switch
        {
            0 => ["--basic-skill-battle"],
            1 => ["--basic-skill-battle", "--wrong", "unused"],
            _ => ["--basic-skill-battle", "--content-root", "unused", "--wrong", "unused"],
        };
        Assert.Equal(2, BasicSkillBattleHost.Run(args));
    }

    [Fact]
    public void Missing_prepared_data_fails_before_output_creation()
    {
        string root = Path.Combine(Path.GetTempPath(), "fft-basic-skill-missing-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(1, BasicSkillBattleHost.Run(["--basic-skill-battle", "--content-root", root, "--check", Path.Combine(root, "out")]));
        Assert.False(Directory.Exists(root));
    }
}
