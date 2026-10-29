using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiPreparationTests
{
    private static readonly AttackThrowStoneAiPreparationProfile Preparation = new(true, true, true);
    private static readonly SwordAiRefreshProfile Refresh = new(true, true, true);
    private static SwordAiRefreshRequest RefreshRequest(bool second = false) =>
        new(second ? B : A, second ? A : B, [new(A, false, false), new(B, false, false)]);

    [Fact]
    public void Three_initialized_entries_two_usable_actions_and_actor_usage_follow_one_fresh_refresh()
    {
        var state = Battle(); var before = Snapshot(state); var result = Prepare(state); var outcome = Success(result);
        var common = outcome.Common;
        Assert.Equal((55, 1ul, 21ul, B), (common.Refresh.TurnEndCt, common.Refresh.ElapsedTicks, common.Refresh.Ticks, common.Refresh.MoveOpportunityUnitId));
        Assert.Equal(60, common.Refresh.Units.Single(u => u.UnitId == A).Ct);
        Assert.Equal((17, 1, 4, 3, (byte)9), (common.ActorDelay, common.TargetDelay, common.EnemyAbilityReach, common.AllyAbilityReach, common.FoeProximity));
        Assert.Equal(new FftRandomState(1103527590, 8), result.RngState);
        Assert.All(outcome.Units, unit => {
            Assert.Equal(new AttackThrowStoneAiInitializedStatistics(3, 0, 0, 2, 0, null), unit.Initialized);
            Assert.Equal((2, 0, true, false), (unit.UsableHarmfulAbilities, unit.UsableHelpfulAbilities, unit.HasRangedAbility, unit.CanHealAllies));
        });
        Assert.Equal((2, 2), (outcome.EnemyHarmfulAbilityCount, outcome.EnemyPhysicalAbilityCount));
        Assert.Collection(outcome.ActorEntries,
            e => Assert.Equal(new(AttackThrowStoneAiEntryKind.Attack, 1, 0, 0, 0, true, true, false, false), e),
            e => Assert.Equal(new(AttackThrowStoneAiEntryKind.ThrowStone, 4, 0, 0, 0, true, false, false, true), e));
        Assert.True(common.TargetActive); Assert.True(common.ManualAutobattle); Assert.True(common.ActorAttackUsable);
        Assert.False(common.RetreatSupportAvailable); Assert.False(common.SingleEnemyRemaining); Assert.Equal(0, common.RandomUseThreshold);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Restored_focus_and_full_reachability_do_not_use_temporary_clock_or_Throw_Stones_maximum_range()
    {
        var target = new TilePosition(6, 1, 0); var state = Battle(target: target);
        var outcome = Success(Prepare(state)); var common = outcome.Common;
        Assert.Equal(Origin, common.Origin); Assert.Equal(target, common.FocusPosition);
        Assert.Equal(4, common.EnemyAbilityReach); Assert.True(common.TargetActive); // Original target is five away, still considered.
        Assert.Contains(Origin, common.ReachableOrigins); Assert.Contains(Far, common.ReachableOrigins);
        Assert.Contains(new TilePosition(2, 1, 0), common.ReachableOrigins); Assert.DoesNotContain(target, common.ReachableOrigins);
        Assert.DoesNotContain(new TilePosition(7, 7, 0), common.ReachableOrigins);
        var immobile = Success(Prepare(Battle(move: 0, ct: 99, targetCt: 100)));
        Assert.Equal((1, 0, 1, 0), (immobile.Common.EnemyAbilityReach, immobile.Common.AllyAbilityReach, immobile.Common.ActorDelay, immobile.Common.TargetDelay));
        Assert.Equal(Origin, Assert.Single(immobile.Common.ReachableOrigins)); Assert.Equal(60, immobile.Common.Refresh.TurnEndCt);
        var upper = Success(Prepare(Battle(move: 124))); Assert.Equal(125, upper.Common.EnemyAbilityReach);
    }

    [Fact]
    public void NonBlue_team_policy_retains_cached_flags_and_sets_only_the_actors_support_unavailable_policy()
    {
        var request = new SwordAiRefreshRequest(A, B, [new(B, true, false), new(A, true, false)]);
        var outcome = Success(Prepare(Battle(), request: request));
        Assert.All(outcome.Common.Refresh.RefreshedUnits, u => { Assert.True(u.ConserveCt); Assert.False(u.TeamSupportUnavailable); });
        Assert.Equal(new(A, true, true), outcome.Common.Policies.Single(u => u.UnitId == A));
        Assert.Equal(new(B, true, false), outcome.Common.Policies.Single(u => u.UnitId == B));
        Assert.False(outcome.Common.RetreatSupportAvailable); Assert.True(outcome.Common.ManualAutobattle);
    }

    [Fact]
    public void One_draw_exact_budget_and_tick_exhaustion_publish_no_partial_preparation_or_progressed_RNG()
    {
        var last = new FftRandomState(1, ulong.MaxValue - 1); var fits = Prepare(Battle(), rng: last); Success(fits);
        Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var exhausted = last with { DrawCount = ulong.MaxValue };
        Assert.Equal(SwordAiRefreshRejection.RandomDrawCountExhausted,
            Atomic(Prepare(Battle(), rng: exhausted), AttackThrowStoneAiPreparationRejection.RefreshRejected, exhausted).RefreshRejection);
        var state = Battle(ticks: ulong.MaxValue); var before = Snapshot(state);
        Assert.Equal(SwordAiRefreshRejection.TickCountExhausted,
            Atomic(Prepare(state), AttackThrowStoneAiPreparationRejection.RefreshRejected).RefreshRejection);
        Assert.Equal(before, Snapshot(state));
        var ready = Success(Prepare(Battle(ticks: ulong.MaxValue, targetCt: 100)));
        Assert.Equal((0ul, ulong.MaxValue), (ready.Common.Refresh.ElapsedTicks, ready.Common.Refresh.Ticks));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Lists_cache_team_grants_choice_and_family_admission_remain_explicit_before_owned_refresh()
    {
        foreach (var certificate in new[] { Preparation with { BothInitializedAttackThrowStoneLists = false },
            Preparation with { NoSpecialBehavior = false }, Preparation with { CleanActorDelayCache = false } })
            Atomic(Prepare(Battle(), preparation: certificate), AttackThrowStoneAiPreparationRejection.UnsupportedProfile);
        foreach (var team in new[] { SwordAiScenarioTeam.Blue, SwordAiScenarioTeam.Unknown })
            Atomic(Prepare(Battle(), team: team), AttackThrowStoneAiPreparationRejection.UnsupportedTeam);
        var p = Profile();
        var counters = new ThrowStoneAiCandidateProfile(p.ThrowStone.Ability, p.ThrowStone.Movement, [new(A, 255), new(B, 20)]);
        Atomic(Prepare(Battle(), stone: counters), AttackThrowStoneAiPreparationRejection.MismatchedProfiles);
        var noPeerGrant = new ThrowStoneAiCandidateProfile(new(Ground, [new(Stone)], [new(A, [Stone], 0), new(B, [], 0)]),
            p.ThrowStone.Movement, p.ThrowStone.Units);
        foreach (var profile in new[] { CandidateProfile(granted: false), noPeerGrant })
            Assert.Equal(AttackCommandRejection.AbilityNotGranted,
                Atomic(Prepare(Battle(), stone: profile), AttackThrowStoneAiPreparationRejection.AbilityRejected).AbilityRejection);
        Atomic(Prepare(State()), AttackThrowStoneAiPreparationRejection.UnsupportedAttack);
        Assert.Equal(SwordAiRefreshRejection.UnavailableAction,
            Atomic(Prepare(Battle(moved: true)), AttackThrowStoneAiPreparationRejection.RefreshRejected).RefreshRejection);
        Assert.Equal(AttackCommandRejection.ActionAlreadyTaken,
            Atomic(Prepare(Battle(acted: true)), AttackThrowStoneAiPreparationRejection.AbilityRejected).AbilityRejection);
        Atomic(Prepare(Battle(move: 125)), AttackThrowStoneAiPreparationRejection.UnsupportedProfile);
        Assert.Equal(SwordAiRefreshRejection.UnsupportedRefreshProfile,
            Atomic(Prepare(Battle(), refresh: Refresh with { ZeroCachedMpAbilityModifiers = false }), AttackThrowStoneAiPreparationRejection.RefreshRejected).RefreshRejection);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_policy_orders_replay_without_live_changes()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(hp: 10, second: second); var before = Snapshot(state); var request = RefreshRequest(second);
            var result = Prepare(state, request: request); var outcome = Success(result);
            Assert.Equal(second ? A : B, outcome.Common.Refresh.TargetId);
            Assert.True(outcome.Common.Refresh.RefreshedUnits.Single(u => u.UnitId == request.TargetId).HpBelowHalf);
            Assert.Equal(Serialize(result), Serialize(Prepare(state, request: request)));
            Assert.Equal(Serialize(result), Serialize(Prepare(Battle(hp: 10, second: second, reverse: true),
                request: request with { Policies = request.Policies.Reverse().ToImmutableArray() })));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static AttackThrowStoneAiPreparationResult Prepare(AttackBattleSnapshot state, SwordAiRefreshRequest? request = null,
        AttackThrowStoneAiPreparationProfile? preparation = null, SwordAiRefreshProfile? refresh = null,
        ThrowStoneAiCandidateProfile? stone = null, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null)
    {
        var p = Profile(); return AttackThrowStoneAiPreparation.Evaluate(state, request ?? RefreshRequest(), Stone, new(Sword, 2),
            p.Attack, stone ?? p.ThrowStone, p.Score, refresh ?? Refresh, preparation ?? Preparation, team, rng ?? Decision);
    }
    private static AttackThrowStoneAiPreparationOutcome Success(AttackThrowStoneAiPreparationResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiPreparationOutcome>(result.Outcome); }
    private static AttackThrowStoneAiPreparationFailure Atomic(AttackThrowStoneAiPreparationResult result,
        AttackThrowStoneAiPreparationRejection reason, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
        var failure = Assert.IsType<AttackThrowStoneAiPreparationFailure>(result.Failure); Assert.Equal(reason, failure.Reason); return failure; }
}
