using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;

namespace Zodiac.Framework.Ai.Tests;

// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-209/211.
[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiPostActPreparationTests
{
    [Fact]
    public void Actual_hit_miss_and_push_feed_one_refresh_while_reach_and_first_pass_counts_survive_the_ACT_clear()
    {
        // ActRng is the expected checkpoint after the real command. RefreshRng is one further hypothetical upkeep draw.
        // Counts are cumulative; words are the full internal RNG state, not individual random rolls.
        var cases = new[] {
            new { Name = "Attack hit", Move = 3, Power = 2, Evade = 0, Seed = 1u, Hp = 40, Target = new TilePosition(2, 1, 0),
                ActRng = new FftRandomState(995405759, 49), RefreshRng = new FftRandomState(2102736524, 50) },
            new { Name = "Stone hit", Move = 0, Power = 1, Evade = 0, Seed = 1u, Hp = 40, Target = new TilePosition(2, 1, 0),
                ActRng = new FftRandomState(3823029677, 27), RefreshRng = new FftRandomState(3914758370, 28) },
            new { Name = "Stone miss", Move = 0, Power = 1, Evade = 75, Seed = 1u, Hp = 50, Target = new TilePosition(2, 1, 0),
                ActRng = new FftRandomState(675780521, 31), RefreshRng = new FftRandomState(744964398, 32) },
            new { Name = "Stone push", Move = 0, Power = 1, Evade = 0, Seed = 20u, Hp = 40, Target = new TilePosition(3, 1, 0),
                ActRng = new FftRandomState(2928661168, 27), RefreshRng = new FftRandomState(3381560617, 28) } };
        foreach (var example in cases)
        {
            var state = Act(Ready(move: example.Move, evade: example.Evade, entryWord: example.Seed), example.Power).Battle;
            string before = Snapshot(state);
            Assert.Equal(example.ActRng, state.TurnBattle.Turns.RngState);
            Assert.Equal(example.Hp, state.Combatants.Single(u => u.UnitId == B).Hp);
            var result = Prepare(state, weaponPower: example.Power); var o = Success(result); var common = o.Common;
            Assert.Equal(example.RefreshRng, result.RngState);
            Assert.Equal((35, 1ul, B), (common.Refresh.TurnEndCt, common.Refresh.ElapsedTicks, common.Refresh.MoveOpportunityUnitId));
            Assert.Equal(40, common.Refresh.Units.Single(u => u.UnitId == A).Ct);
            Assert.Equal(0, common.Refresh.Units.Single(u => u.UnitId == B).Ct);
            Assert.Single(common.Refresh.UpkeepPicks);
            Assert.Equal((17, 1, example.Move + 1, example.Move), (common.ActorDelay, common.TargetDelay, common.EnemyAbilityReach, common.AllyAbilityReach));
            Assert.Equal(Origin, common.Origin); Assert.Equal(example.Target, common.FocusPosition);
            Assert.Contains(Origin, common.ReachableOrigins); Assert.DoesNotContain(example.Target, common.ReachableOrigins);
            Assert.False(common.ActorAttackUsable); Assert.All(o.ActorEntries, e => Assert.False(e.Usable));
            Assert.All(o.Units, u => {
                Assert.Equal(new AttackThrowStoneAiInitializedStatistics(3, 0, 0, 2, 0, null), u.Initialized);
                Assert.Equal((2, 0, true, false), (u.UsableHarmfulAbilities, u.UsableHelpfulAbilities, u.HasRangedAbility, u.CanHealAllies));
            });
            Assert.Equal((2, 2), (o.EnemyHarmfulAbilityCount, o.EnemyPhysicalAbilityCount));
            var config = SelectionConfiguration(new());
            var refresh = SwordAiRefresh.EvaluateAfterAct(state, FreshRequest(), config.Attack, config.Scoring, config.Refresh, example.ActRng);
            Assert.True(refresh.Accepted); Assert.Equal(Serialize(refresh.Outcome), Serialize(common.Refresh));
            Assert.Equal(example.RefreshRng, refresh.RngState); Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Actual_Critical_and_displacement_change_restored_flags_and_occupancy_without_reusing_pre_ACT_geometry()
    {
        var critical = Act(Ready(), power: 8).Battle; string before = Snapshot(critical);
        Assert.Equal(10, critical.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(AttackUnitStatus.Critical, critical.Combatants.Single(u => u.UnitId == B).Status);
        var prepared = Success(Prepare(critical, weaponPower: 8));
        Assert.True(prepared.Common.Refresh.RefreshedUnits.Single(u => u.UnitId == B).HpBelowHalf);
        Assert.True(prepared.Common.TargetActive); Assert.False(prepared.Common.ActorAttackUsable);
        Assert.Equal(before, Snapshot(critical));

        // Actual seven-draw Stone pushes from a supplied checkpoint. The farther push also leaves current Stone range.
        var config = SelectionConfiguration(new());
        foreach (int originalX in new[] { 2, 5 })
        {
            var movedTarget = FacingTurnBattle.UseTargetedAbility(Ready(target: new(originalX, 1, 0), entryWord: 2711922929, count: 20),
                new(A, Stone, B), config.ThrowStone.Ability);
            Assert.True(movedTarget.Accepted); Assert.Single(movedTarget.Events.OfType<ThrowStoneKnockbackEvent>());
            before = Snapshot(movedTarget.State.Battle);
            var pushed = Success(Prepare(movedTarget.State.Battle));
            Assert.Equal(new TilePosition(originalX + 1, 1, 0), pushed.Common.FocusPosition);
            if (originalX == 2) Assert.Contains(new TilePosition(2, 1, 0), pushed.Common.ReachableOrigins);
            Assert.DoesNotContain(pushed.Common.FocusPosition, pushed.Common.ReachableOrigins);
            Assert.Equal((byte)(10 - originalX), pushed.Common.FoeProximity); Assert.Equal(4, pushed.Common.EnemyAbilityReach);
            Assert.Equal(before, Snapshot(movedTarget.State.Battle));
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Spent_ACT_does_not_bypass_grants_setup_or_certificates_and_never_becomes_live_command_authority()
    {
        var acted = Act(Ready()); var state = acted.Battle; var config = SelectionConfiguration(new());
        var command = new UseTargetedAbilityCommand(A, Stone, B);
        Assert.Equal(AttackCommandRejection.None, TargetedAbilityBattleSimulator.ValidateAbilitySetup(state, A, Stone, config.ThrowStone.Ability));
        Assert.Equal(AttackCommandRejection.ActionAlreadyTaken, TargetedAbilityBattleSimulator.ValidateCommand(state, command, config.ThrowStone.Ability));
        foreach (var missing in new[] { A, B })
        {
            var grants = new TargetedAbilityBattleProfile(Ground, [new(Stone)], [new(A, missing == A ? [] : [Stone], 0), new(B, missing == B ? [] : [Stone], 0)]);
            var changed = config with { ThrowStone = new(grants, config.ThrowStone.Movement, config.ThrowStone.Units) };
            Assert.Equal(AttackCommandRejection.AbilityNotGranted,
                Atomic(state, Prepare(state, configuration: changed), AttackThrowStoneAiPreparationRejection.AbilityRejected).AbilityRejection);
            // Real ACT admission still rejects the spent choice first, even with invalid grants.
            Assert.Equal(AttackCommandRejection.ActionAlreadyTaken, TargetedAbilityBattleSimulator.ValidateCommand(state, command, grants));
        }
        foreach (var id in new[] { A, B })
            Assert.Equal(AttackCommandRejection.UnsupportedSetup,
                Atomic(WithPa(state, id, 100), Prepare(WithPa(state, id, 100)), AttackThrowStoneAiPreparationRejection.AbilityRejected).AbilityRejection);
        var unknown = new AttackThrowStoneAiSelectionRequest(FreshRequest(), new("sample:ability/unknown"), new(Sword, 2));
        Assert.Equal(AttackCommandRejection.UnknownAbility,
            Atomic(state, Prepare(state, request: unknown), AttackThrowStoneAiPreparationRejection.AbilityRejected).AbilityRejection);
        foreach (var preparation in new[] { config.Preparation with { BothInitializedAttackThrowStoneLists = false },
            config.Preparation with { NoSpecialBehavior = false }, config.Preparation with { CleanActorDelayCache = false } })
            Atomic(state, Prepare(state, configuration: config with { Preparation = preparation }), AttackThrowStoneAiPreparationRejection.UnsupportedProfile);
        Atomic(state, Prepare(state, configuration: config with { Team = SwordAiScenarioTeam.Blue }), AttackThrowStoneAiPreparationRejection.UnsupportedTeam);
        Atomic(state, Prepare(state, configuration: config with { ThrowStone = new(config.ThrowStone.Ability, config.ThrowStone.Movement, [new(A, 255), new(B, 20)]) }),
            AttackThrowStoneAiPreparationRejection.MismatchedProfiles);
        var unavailable = config with { Refresh = config.Refresh with { FreshRefreshEntry = false } };
        Assert.Equal(SwordAiRefreshRejection.UnsupportedRefreshProfile,
            Atomic(state, Prepare(state, configuration: unavailable), AttackThrowStoneAiPreparationRejection.RefreshRejected).RefreshRejection);
        // Spend the remaining Move through Framework: a still-active unit with both choices spent is invalid state.
        var ended = FacingTurnPathMovement.MoveAlongPath(acted, new(A, new(0, 1, 0)), config.Attack.Movement);
        Assert.True(ended.Accepted);
        foreach (var invalid in new[] { Battle(), Battle(moved: true), ended.State.Battle })
            Assert.Equal(SwordAiRefreshRejection.UnavailableAction,
                Atomic(invalid, Prepare(invalid), AttackThrowStoneAiPreparationRejection.RefreshRejected).RefreshRejection);
        var terminal = Act(Ready(hp: 10, move: 0), power: 1);
        Assert.Null(terminal.PendingFacing); Assert.Equal(0, terminal.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(SwordAiRefreshRejection.TerminalInput,
            Atomic(terminal.Battle, Prepare(terminal.Battle, weaponPower: 1), AttackThrowStoneAiPreparationRejection.RefreshRejected).RefreshRejection);
    }

    [Fact]
    public void One_draw_and_tick_boundaries_preserve_the_published_ACT_and_hide_all_failed_preparation()
    {
        var state = Act(Ready()).Battle; string before = Snapshot(state);
        var exact = new FftRandomState(1, ulong.MaxValue - 1);
        Assert.Equal(new FftRandomState(1103527590, ulong.MaxValue), Prepare(state, rng: exact).RngState);
        Success(Prepare(state, rng: exact));
        var exhausted = exact with { DrawCount = ulong.MaxValue };
        Assert.Equal(SwordAiRefreshRejection.RandomDrawCountExhausted,
            Atomic(state, Prepare(state, rng: exhausted), AttackThrowStoneAiPreparationRejection.RefreshRejected, exhausted).RefreshRejection);
        Assert.Equal(before, Snapshot(state));
        var overflow = Battle(acted: true, ticks: ulong.MaxValue);
        Assert.Equal(SwordAiRefreshRejection.TickCountExhausted,
            Atomic(overflow, Prepare(overflow), AttackThrowStoneAiPreparationRejection.RefreshRejected).RefreshRejection);
        var noTick = Battle(acted: true, ticks: ulong.MaxValue, targetCt: 100, ct: 99, move: 0);
        var capped = Success(Prepare(noTick));
        Assert.Equal((60, 0ul, ulong.MaxValue), (capped.Common.Refresh.TurnEndCt, capped.Common.Refresh.ElapsedTicks, capped.Common.Refresh.Ticks));
        Assert.Equal(Origin, Assert.Single(capped.Common.ReachableOrigins)); Assert.False(capped.Common.ActorAttackUsable);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_reordered_inputs_replay_the_same_preparation_without_live_mutation()
    {
        foreach (bool second in new[] { false, true })
        {
            var request = FreshRequest(second);
            var state = Act(Ready(second: second), request: request).Battle; string before = Snapshot(state);
            var first = Prepare(state, request: new(request, Stone, new(Sword, 2))); Success(first);
            var replay = Prepare(state, request: new(request, Stone, new(Sword, 2))); Success(replay);
            var reversedRequest = request with { Policies = request.Policies.Reverse().ToImmutableArray() };
            var reversed = Act(Ready(second: second, reverse: true), request: reversedRequest).Battle;
            var reordered = Prepare(reversed, request: new(reversedRequest, Stone, new(Sword, 2))); Success(reordered);
            Assert.Equal(Serialize(first), Serialize(replay)); Assert.Equal(Serialize(first), Serialize(reordered));
            Assert.Equal(before, Snapshot(state));
        }
    }

    internal static FacingTurnState Act(FacingTurnState state, int power = 2, SwordAiRefreshRequest? request = null)
    {
        var result = AttackThrowStoneAiActFirstExecution.StartInReach(state, new(request ?? FreshRequest(), Stone, new(Sword, power)),
            SelectionConfiguration(new(true, true, true, true, true)), new(true, true, true, true));
        Assert.True(result.Accepted, Serialize(result.Failure)); return result.State;
    }
    private static AttackThrowStoneAiPreparationResult Prepare(AttackBattleSnapshot state, int weaponPower = 2,
        AttackThrowStoneAiSelectionRequest? request = null, AttackThrowStoneAiSelectionConfiguration? configuration = null, FftRandomState? rng = null) =>
        AttackThrowStoneAiPreparation.EvaluateAfterAct(state, request ?? new(FreshRequest(), Stone, new(Sword, weaponPower)),
            configuration ?? SelectionConfiguration(new()), rng ?? state.TurnBattle.Turns.RngState);
    private static AttackThrowStoneAiPreparationOutcome Success(AttackThrowStoneAiPreparationResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiPreparationOutcome>(result.Outcome); }
    private static AttackThrowStoneAiPreparationFailure Atomic(AttackBattleSnapshot state, AttackThrowStoneAiPreparationResult result,
        AttackThrowStoneAiPreparationRejection reason, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? state.TurnBattle.Turns.RngState, result.RngState);
        Assert.Equal(reason, result.Failure!.Reason); return result.Failure; }
    private static AttackBattleSnapshot WithPa(AttackBattleSnapshot state, BattleUnitId id, int pa) => new(state.TurnBattle,
        state.Combatants.Select(u => new AttackUnitState(u.UnitId, u.Hp, u.MaxHp, u.Mp, u.MaxMp, u.UnitId == id ? pa : u.PhysicalAttack,
            u.ClassPhysicalEvade, u.RightHandEvade, u.LeftHandEvade, u.AccessoryEvade, u.Brave, u.ZodiacSign, u.Facing, u.Status, u.KoCount, u.RightHandWeaponId)));
}
