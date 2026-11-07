using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AccumulateAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AccumulateAiPreparationTests
{
    [Theory]
    [InlineData(SwordAiScenarioTeam.NonBlue)] [InlineData(SwordAiScenarioTeam.Blue)]
    public void Fresh_shared_refresh_derives_four_entry_statistics_and_usage_without_a_usefulness_coin(SwordAiScenarioTeam team)
    {
        var state = Battle(); var before = Snapshot(state); var result = Prepare(state, Configuration(team)); var o = Success(result);
        Assert.Equal(new RandomState(1103527590, 8), result.RngState);
        Assert.Equal((55, 1ul, 21ul), (o.Common.Refresh.TurnEndCt, o.Common.Refresh.ElapsedTicks, o.Common.Refresh.Ticks));
        Assert.Equal((4, 3, 17, 1, 0), (o.Common.EnemyAbilityReach, o.Common.AllyAbilityReach,
            o.Common.ActorDelay, o.Common.TargetDelay, o.Common.RandomUseThreshold));
        Assert.Equal(team, o.Team); Assert.True(o.Common.TargetActive); Assert.True(o.Common.ManualAutobattle);
        Assert.All(o.Units, u => {
            Assert.Equal((4, 2, 0, 0, 2, 1), (u.InitializedEntryCount, u.EvadeQuarterShare, u.SilenceQuarterShare,
                u.MpQuarterShare, u.HarmfulCount, u.HelpfulCount));
            Assert.True(u.HasRangedAbility); Assert.False(u.CanHealAllies);
            Assert.Equal(u.UnitId == A ? false : (bool?)null, u.MostlySupport);
        });
        Assert.Equal((2, 2), (o.EnemyHarmfulCount, o.EnemyPhysicalCount));
        Assert.Collection(o.ActorEntries,
            e => Assert.Equal(new(AccumulateAiEntryKind.Attack, 1, true, true, false, false), e),
            e => Assert.Equal(new(AccumulateAiEntryKind.Accumulate, 0, true, false, true, true), e),
            e => Assert.Equal(new(AccumulateAiEntryKind.ThrowStone, 4, true, false, false, true), e));
        Assert.Equal(team == SwordAiScenarioTeam.NonBlue, o.Common.Policies.Single(u => u.UnitId == A).TeamSupportUnavailable);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void List_context_peer_grants_teams_and_reachable_PA_envelope_are_admitted_before_refresh()
    {
        var c = Configuration();
        foreach (var flags in new[] { c.Preparation with { BothInitializedLists = false },
            c.Preparation with { CleanActorDelayCache = false }, c.Preparation with { NoSpecialBehavior = false } })
            Reject(Prepare(Battle(), c with { Preparation = flags }));
        Reject(Prepare(Battle(), c with { Team = SwordAiScenarioTeam.Unknown }));
        Reject(Prepare(Battle(), c with { Team = SwordAiScenarioTeam.Blue, Preparation = c.Preparation with { BlueAiControlled = false } }));
        Reject(Prepare(Battle(actorHp: 17), Configuration(SwordAiScenarioTeam.Blue)));
        Success(Prepare(Battle(actorHp: 18), Configuration(SwordAiScenarioTeam.Blue)));
        Success(Prepare(Battle(actorHp: 10))); // NonBlue has no Blue healer threshold.
        var noPeer = new AccumulateAiCandidateProfile(new([new(Self)], [new(A, [Self], 0), new(B, [], 0)]), c.Accumulate.Movement, c.Accumulate.Units);
        var rejected = Prepare(Battle(), c with { Accumulate = noPeer }); Reject(rejected);
        Assert.Equal(AccumulateAiCandidateRejection.AbilityNotGranted, rejected.Failure!.AccumulateReason);
        Reject(Prepare(Battle(), c, Request() with { Weapon = new(SwordId, 167) }));
        Success(Prepare(Battle(), c, Request() with { Weapon = new(SwordId, 166) }));
        Reject(Prepare(Battle(), c, Request() with { ThrowStoneAbilityId = Self }));
        Reject(Prepare(Battle(move: 125))); Reject(Prepare(Battle(moved: true))); Reject(Prepare(Battle(acted: true)));
        var mismatch = new AccumulateAiCandidateProfile(c.Accumulate.Ability, c.Accumulate.Movement, [new(A, 1), new(B, 19)]);
        Reject(Prepare(Battle(), c with { Accumulate = mismatch }));
    }

    [Fact]
    public void Fresh_preparation_failure_and_exact_budgets_are_atomic()
    {
        var last = new RandomState(1, ulong.MaxValue - 1);
        Assert.Equal(ulong.MaxValue, Prepare(Battle(ticks: ulong.MaxValue - 1), rng: last).RngState.DrawCount);
        var exhausted = last with { DrawCount = ulong.MaxValue };
        Reject(Prepare(Battle(), rng: exhausted), exhausted);
        var result = Prepare(Battle(ticks: ulong.MaxValue)); Reject(result);
        Assert.Equal(SwordAiRefreshRejection.TickCountExhausted, result.Failure!.RefreshReason);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_identities_and_input_orders_replay_and_preserve_retained_policy()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(second: second); var before = Snapshot(state); var request = Request(second);
            var result = Prepare(state, request: request); Success(result);
            Assert.Equal(Serialize(result), Serialize(Prepare(state, request: request)));
            Assert.Equal(Serialize(result), Serialize(Prepare(Battle(second: second, reverse: true), request: request)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    internal static AccumulateAiPreparationResult Prepare(AttackBattleSnapshot state,
        AccumulateAiPreparationConfiguration? configuration = null, AccumulateAiPreparationRequest? request = null, RandomState? rng = null) =>
        AccumulateAiPreparation.Evaluate(state, request ?? Request(), configuration ?? Configuration(), rng ?? Decision);
    private static AccumulateAiPreparationOutcome Success(AccumulateAiPreparationResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); return Assert.IsType<AccumulateAiPreparationOutcome>(result.Outcome); }
    private static void Reject(AccumulateAiPreparationResult result, RandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState); }
}
