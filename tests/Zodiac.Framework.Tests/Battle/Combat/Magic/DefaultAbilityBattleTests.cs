using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class DefaultAbilityBattleTests
{
    private static DefaultAbilityUnitState Mage(DefaultAbilityBattleState s) => s.Units.Single(u => u.Id == DefaultAbilityBattleTestData.Mage);
    private static DefaultAbilityUnitState Target(DefaultAbilityBattleState s) => s.Units.Single(u => u.Id == DefaultAbilityBattleTestData.Target);

    [Fact]
    public void Confirmation_charges_without_paying_then_readiness_publishes_hp_mp_rng_and_career()
    {
        var definition = DefaultAbilityBattleTestData.Definition();
        var queued = DefaultAbilityBattleTestData.Queued(definition);
        Assert.Equal((ushort)75, Mage(queued).Current.Mp);
        Assert.True(Mage(queued).Current.Statuses.Charging);
        Assert.Equal((byte)4, queued.Pending[DefaultAbilityBattleTestData.Mage].Counter);
        Assert.Equal(10ul, queued.Turns.Ticks);
        var before = DefaultAbilityBattleTestData.Apply(queued, new DefaultAbilityCommand.Advance());
        Assert.Equal(DefaultAbilityBattleTestData.Target, before.Turns.ActiveUnitId);
        Assert.Equal(13ul, before.Turns.Ticks);
        Assert.Equal((byte)1, before.Pending[DefaultAbilityBattleTestData.Mage].Counter);
        before = DefaultAbilityBattleTestData.Apply(before, new DefaultAbilityCommand.Wait(DefaultAbilityBattleTestData.Target));
        var result = DefaultAbilityBattle.Apply(before, new DefaultAbilityCommand.Advance());
        Assert.True(result.Accepted, result.Rejection);
        var after = result.State;
        Assert.Equal((ushort)69, Mage(after).Current.Mp);
        Assert.Equal((ushort)146, Target(after).Vitals.Hp);
        Assert.Equal((byte)10, Mage(after).Experience);
        Assert.Equal(new JobPointBalance(62, 112, 1), Mage(after).JobPoints[new("fft.ps1:job/wizard")]);
        Assert.Equal(16ul, after.Turns.RngState.DrawCount);
        Assert.False(Mage(after).Current.Statuses.Charging);
        Assert.Empty(after.Pending);
        Assert.Equal(13ul, after.Turns.Ticks);
        Assert.Equal(AbilityOutcomeMarker.Targeted, Mage(after).OutcomeMarker);
        Assert.Equal(AbilityOutcomeMarker.Affected, Target(after).OutcomeMarker);
        Assert.Single(result.Events.OfType<DefaultAbilityEvent.Resolved>());
        Assert.Equal((ushort)200, Target(before).Vitals.Hp);
        Assert.Equal((ushort)75, Mage(before).Current.Mp);
    }

    [Fact]
    public void Follow_uses_current_anchor_outside_old_blue_range_while_tile_can_fizzle_after_movement()
    {
        var definition = DefaultAbilityBattleTestData.Definition();
        var follow = DefaultAbilityBattleTestData.Apply(DefaultAbilityBattleTestData.Queued(definition), new DefaultAbilityCommand.Advance());
        follow = DefaultAbilityBattleTestData.Apply(follow, new DefaultAbilityCommand.Step(DefaultAbilityBattleTestData.Target, new(5, 2, 0)));
        follow = DefaultAbilityBattleTestData.Apply(follow, new DefaultAbilityCommand.Wait(DefaultAbilityBattleTestData.Target));
        var followed = DefaultAbilityBattle.Apply(follow, new DefaultAbilityCommand.Advance());
        Assert.True(followed.Accepted, followed.Rejection);
        Assert.Equal(new TilePosition(5, 2, 0), Assert.Single(followed.Events.OfType<DefaultAbilityEvent.Resolved>()).Center);
        Assert.Equal((ushort)146, Target(followed.State).Vitals.Hp);

        var tile = DefaultAbilityBattleTestData.Apply(DefaultAbilityBattleTestData.Queued(definition, false, new(3, 2, 0)), new DefaultAbilityCommand.Advance());
        tile = DefaultAbilityBattleTestData.Apply(tile, new DefaultAbilityCommand.Step(DefaultAbilityBattleTestData.Target, new(5, 2, 0)));
        tile = DefaultAbilityBattleTestData.Apply(tile, new DefaultAbilityCommand.Wait(DefaultAbilityBattleTestData.Target));
        var empty = DefaultAbilityBattle.Apply(tile, new DefaultAbilityCommand.Advance());
        Assert.True(empty.Accepted, empty.Rejection);
        Assert.Equal(6, Assert.Single(empty.Events.OfType<DefaultAbilityEvent.Fizzled>()).MpCost);
        Assert.Equal((ushort)69, Mage(empty.State).Current.Mp);
        Assert.Equal((ushort)200, Target(empty.State).Vitals.Hp);
        Assert.Equal((byte)0, Mage(empty.State).Experience);
        Assert.Equal(11ul, empty.State.Turns.RngState.DrawCount);
    }

    [Fact]
    public void All_charges_tick_before_first_resolution_and_other_zero_counters_do_not_tick_twice()
    {
        var d = DefaultAbilityBattleTestData.Definition(enemySpell: true, enemySpeed: 10);
        var state = DefaultAbilityBattleTestData.Queued(d);
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.Advance());
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.Cast(DefaultAbilityBattleTestData.Target, DefaultAbilityBattleTestData.Fire, new(0, 2, 0)));
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.Wait(DefaultAbilityBattleTestData.Target));
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.Advance());
        Assert.Equal(13ul, state.Turns.Ticks);
        Assert.Equal((byte)0, state.Pending[DefaultAbilityBattleTestData.Target].Counter);
        var afterFirst = state.Turns.RngState;
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.Advance());
        Assert.Equal(13ul, state.Turns.Ticks);
        Assert.Empty(state.Pending);
        Assert.Equal(afterFirst.DrawCount + 5, state.Turns.RngState.DrawCount);
        Assert.Equal((ushort)69, Mage(state).Current.Mp);
        Assert.Equal((ushort)44, Target(state).Current.Mp);
    }

    [Fact]
    public void Immediate_ko_publishes_dead_count_source_marker_rewards_and_demo_completion()
    {
        var d = DefaultAbilityBattleTestData.Definition(targetHp: 30, charge: 0);
        var state = DefaultAbilityBattleTestData.Apply(d.Start(), new DefaultAbilityCommand.Advance());
        state = DefaultAbilityBattleTestData.Apply(state, new DefaultAbilityCommand.Cast(DefaultAbilityBattleTestData.Mage, DefaultAbilityBattleTestData.Fire, new(4, 2, 0)));
        Assert.True(state.Finished);
        Assert.True(Target(state).Current.Statuses.Dead);
        Assert.False(Target(state).Vitals.Critical);
        Assert.Equal((byte)1, Target(state).KoCount);
        Assert.Equal((byte)3, Target(state).DeathCounter);
        Assert.Equal(AbilityOutcomeMarker.Targeted, Target(state).OutcomeMarker);
        Assert.Equal((byte)20, Mage(state).Experience);
        Assert.Null(state.Turns.ActiveUnitId);
        Assert.Empty(state.Pending);
        Assert.False(DefaultAbilityBattle.Apply(state, new DefaultAbilityCommand.Advance()).Accepted);
        Assert.False(d.Start().Finished);
    }

    [Fact]
    public void Level_up_adopts_raw_growth_five_draws_new_vitals_and_full_job_unlock_recalculation()
    {
        var d = DefaultAbilityBattleTestData.Definition(experience: 95);
        var before = DefaultAbilityBattleTestData.BeforeResolution(d);
        var state = DefaultAbilityBattleTestData.Apply(before, new DefaultAbilityCommand.Advance());
        Assert.Equal((byte)11, Mage(state).Level);
        Assert.Equal((byte)0, Mage(state).Experience);
        Assert.True(Mage(state).RawStats.Hp > Mage(before).RawStats.Hp);
        Assert.True(Mage(state).Vitals.MaxHp > 100);
        Assert.True(Mage(state).Vitals.MaxMp > 75);
        Assert.Equal((ushort)100, Mage(state).Vitals.Hp);
        Assert.Equal((ushort)69, Mage(state).Current.Mp);
        Assert.Equal((byte)8, Mage(state).Current.MagicAttack);
        Assert.Equal(21ul, state.Turns.RngState.DrawCount);
        var expected = d.Jobs.Calculate(Mage(state).JobPoints.ToImmutableDictionary(p => p.Key, p => (byte)p.Value.JobLevel), true);
        Assert.Equal(expected.ToArray(), Mage(state).UnlockedJobs.ToArray());
    }

    [Fact]
    public void Late_formula_or_growth_counter_exhaustion_rejects_the_whole_unpublished_command()
    {
        foreach (var (experience, remaining) in new (byte, ulong)[] { (0, 6), (95, 11) })
        {
            var d = DefaultAbilityBattleTestData.Definition(experience: experience, rng: new(57, ulong.MaxValue - remaining));
            var before = DefaultAbilityBattleTestData.BeforeResolution(d);
            var r = DefaultAbilityBattle.Apply(before, new DefaultAbilityCommand.Advance());
            Assert.False(r.Accepted);
            Assert.Same(before, r.State);
            Assert.Empty(r.Events);
            Assert.Equal((ushort)75, Mage(before).Current.Mp);
            Assert.Equal((ushort)200, Target(before).Vitals.Hp);
            Assert.Single(before.Pending);
        }
    }

    [Fact]
    public void Admission_rejects_unlearned_out_of_range_inactive_and_repeat_actions_without_publication()
    {
        var d = DefaultAbilityBattleTestData.Definition();
        var started = d.Start();
        Assert.False(DefaultAbilityBattle.Apply(started, new DefaultAbilityCommand.Cast(DefaultAbilityBattleTestData.Mage, DefaultAbilityBattleTestData.Fire, new(4, 2, 0))).Accepted);
        var active = DefaultAbilityBattleTestData.Apply(started, new DefaultAbilityCommand.Advance());
        foreach (var cmd in new DefaultAbilityCommand[]
        {
            new DefaultAbilityCommand.Cast(DefaultAbilityBattleTestData.Mage, new("unknown:spell/a"), new(4, 2, 0)),
            new DefaultAbilityCommand.Cast(DefaultAbilityBattleTestData.Mage, DefaultAbilityBattleTestData.Fire, new(6, 4, 0)),
            new DefaultAbilityCommand.Cast(DefaultAbilityBattleTestData.Target, DefaultAbilityBattleTestData.Fire, new(0, 2, 0))
        })
        {
            var r = DefaultAbilityBattle.Apply(active, cmd);
            Assert.False(r.Accepted);
            Assert.Same(active, r.State);
            Assert.Empty(r.Events);
        }
        var queued = DefaultAbilityBattleTestData.Apply(active, new DefaultAbilityCommand.Cast(DefaultAbilityBattleTestData.Mage, DefaultAbilityBattleTestData.Fire, new(4, 2, 0)));
        var repeated = DefaultAbilityBattle.Apply(queued, new DefaultAbilityCommand.Cast(DefaultAbilityBattleTestData.Mage, DefaultAbilityBattleTestData.Fire, new(4, 2, 0)));
        Assert.False(repeated.Accepted);
        Assert.Same(queued, repeated.State);
    }

    [Fact]
    public void Resource_failure_at_readiness_is_distinct_from_paid_empty_target_fizzle()
    {
        var state = DefaultAbilityBattleTestData.BeforeResolution(DefaultAbilityBattleTestData.Definition());
        int i = Enumerable.Range(0, state.Units.Length).Single(i => state.Units[i].Id == DefaultAbilityBattleTestData.Mage);
        // Test-only external resource change; the current encounter has no MP-drain command.
        var before = state.With(units: state.Units.SetItem(i, Mage(state) with { Current = Mage(state).Current with { Mp = 0 } }));
        var r = DefaultAbilityBattle.Apply(before, new DefaultAbilityCommand.Advance());
        Assert.True(r.Accepted, r.Rejection);
        Assert.Equal(0, Assert.Single(r.Events.OfType<DefaultAbilityEvent.Fizzled>()).MpCost);
        Assert.Equal((ushort)0, Mage(r.State).Current.Mp);
        Assert.Equal((ushort)200, Target(r.State).Vitals.Hp);
        Assert.False(Mage(r.State).Current.Statuses.Charging);
        Assert.Empty(r.State.Pending);
        Assert.Empty(r.Events.OfType<DefaultAbilityEvent.Rewarded>());
    }

    [Fact]
    public void Command_replay_and_restart_reproduce_the_complete_owned_state_and_event_tape()
    {
        var d = DefaultAbilityBattleTestData.Definition();
        DefaultAbilityCommand[] tape = [new DefaultAbilityCommand.Advance(),
            new DefaultAbilityCommand.Cast(DefaultAbilityBattleTestData.Mage, DefaultAbilityBattleTestData.Fire, new(4, 2, 0)),
            new DefaultAbilityCommand.Wait(DefaultAbilityBattleTestData.Mage), new DefaultAbilityCommand.Advance(),
            new DefaultAbilityCommand.Wait(DefaultAbilityBattleTestData.Target), new DefaultAbilityCommand.Advance()];
        var a = d.Start(); var b = d.Start();
        foreach (var command in tape)
        {
            var ar = DefaultAbilityBattle.Apply(a, command); var br = DefaultAbilityBattle.Apply(b, command);
            Assert.True(ar.Accepted, ar.Rejection); Assert.True(br.Accepted, br.Rejection);
            Assert.Equal(ar.State.Turns.RngState, br.State.Turns.RngState);
            Assert.Equal(ar.State.Units.Select(u => (u.Id, u.Vitals, u.Current, u.Level, u.Experience)),
                br.State.Units.Select(u => (u.Id, u.Vitals, u.Current, u.Level, u.Experience)));
            Assert.Equal(ar.Events.Select(e => e.GetType()), br.Events.Select(e => e.GetType()));
            a = ar.State; b = br.State;
        }
        Assert.Equal(d.InitialRng, d.Start().Turns.RngState);
        Assert.Equal((ushort)200, Target(d.Start()).Vitals.Hp);
    }
}
