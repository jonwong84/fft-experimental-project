using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiListPreparationTests
{
    internal static AccumulateAiPreparationConfiguration Config(AiHumanAbilityKind[] actions, AiHumanAbilityKind[]? peer = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, bool selfGranted = true, bool stoneGranted = true)
    {
        AiHumanSkillset Skill(AiHumanAbilityKind[] kinds) => new(new("sample:skillset/basic"),
            kinds.Select((kind, i) => new AiHumanActionSlot(i, kind == AiHumanAbilityKind.Accumulate ? SelfData.Self : Stone, kind, true, true)));
        var p = SelfData.Configuration(team);
        var self = new AccumulateAiCandidateProfile(new([new(SelfData.Self)],
            new[] { A, B }.Select(id => new ImmediateAbilityUnitGrant(id, selfGranted ? [SelfData.Self] : [], 0))), p.Accumulate.Movement, p.Accumulate.Units);
        var stone = new ThrowStoneAiCandidateProfile(new(Ground, [new(Stone)],
            new[] { A, B }.Select(id => new TargetedAbilityUnitGrant(id, stoneGranted ? [Stone] : [], 0))), p.ThrowStone.Movement, p.ThrowStone.Units);
        return p with { Accumulate = self, ThrowStone = stone,
            AbilityLists = AiHumanAbilityLists.Build([new(A, Skill(actions)), new(B, Skill(peer ?? actions))], new(true, true)),
            Preparation = p.Preparation with { BothInitializedLists = false } };
    }

    [Fact]
    public void Missing_repeated_reordered_and_asymmetric_lists_preserve_occurrences_and_source_summaries()
    {
        var state = Ready();
        foreach (var actions in new[] { Array.Empty<AiHumanAbilityKind>(), [AiHumanAbilityKind.Accumulate],
            new[] { AiHumanAbilityKind.ThrowStone }, [AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.Accumulate],
            new[] { AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.ThrowStone } })
        {
            var p = Config(actions, []); var result = BasicSkillAiListPreparation.Evaluate(state.Battle, SelfData.Request(), p, new(1, 7));
            Assert.True(result.Accepted, result.Failure?.ToString()); var o = result.Outcome!;
            Assert.Equal(actions.Prepend(AiHumanAbilityKind.Attack).Append(AiHumanAbilityKind.FrogAttack), o.ActorEntries.Select(e => e.Source.Kind));
            Assert.Equal(Enumerable.Range(0, actions.Length + 2), o.ActorEntries.Select(e => e.Index));
            Assert.Equal(1, o.EnemyHarmfulCount); Assert.Equal(1, o.EnemyPhysicalCount);
            var actor = o.Units.Single(u => u.UnitId == A); Assert.False(actor.MostlySupport); Assert.Null(o.Units.Single(u => u.UnitId == B).MostlySupport);
            Assert.Equal(actions.Length + 2, actor.Initialization.InitializedEntryCount);
            Assert.Equal((1 + actions.Count(k => k == AiHumanAbilityKind.ThrowStone)) * 4 / (actions.Length + 2), actor.Initialization.EvadeQuarterShare);
            Assert.Equal(actions.Count(k => k == AiHumanAbilityKind.Accumulate) * 128 /
                (1 + actions.Length), actor.HelpfulShare);
            Assert.Equal(4, o.Common.EnemyAbilityReach); Assert.Equal(3, o.Common.AllyAbilityReach); Assert.Equal(new RandomState(1103527590, 8), result.RngState);
        }
    }

    [Fact]
    public void Absent_actions_do_not_require_their_grants_but_listed_actions_do()
    {
        var state = Ready(); var none = Config([], selfGranted: false, stoneGranted: false);
        Assert.True(BasicSkillAiListPreparation.Evaluate(state.Battle, SelfData.Request(), none, new(1, 7)).Accepted);
        foreach (var p in new[] { Config([AiHumanAbilityKind.Accumulate], selfGranted: false), Config([AiHumanAbilityKind.ThrowStone], stoneGranted: false) })
        {
            var result = BasicSkillAiListPreparation.Evaluate(state.Battle, SelfData.Request(), p, new(1, 7));
            Assert.False(result.Accepted); Assert.Equal(AccumulateAiPreparationRejection.AbilityRejected, result.Failure!.Reason);
            Assert.Equal(new RandomState(1, 7), result.RngState); Assert.Null(result.Outcome);
        }
        // Extra executable grants can describe known actions disabled for AI; absence from the AI list is not grant absence.
        Assert.True(BasicSkillAiListPreparation.Evaluate(state.Battle, SelfData.Request(), Config([]), new(1, 7)).Accepted);
    }

    [Fact]
    public void Canonical_preparation_matches_old_fields_and_keeps_the_old_variable_list_guard()
    {
        var state = Ready(); var p = Config([AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.ThrowStone]);
        var result = BasicSkillAiListPreparation.Evaluate(state.Battle, SelfData.Request(), p, new(1, 7));
        var old = AccumulateAiPreparation.Evaluate(state.Battle, SelfData.Request(), p, new(1, 7));
        Assert.True(result.Accepted); Assert.True(old.Accepted); Assert.Equal(Serialize(old.Outcome!.Common), Serialize(result.Outcome!.Common));
        Assert.Equal(old.RngState, result.RngState);
        var rejected = AccumulateAiPreparation.Evaluate(state.Battle, SelfData.Request(), Config([]), new(1, 7));
        Assert.False(rejected.Accepted); Assert.Equal(AccumulateAiPreparationRejection.UnsupportedList, rejected.Failure!.Reason);
    }

    [Fact]
    public void Post_ACT_computes_reaches_and_counts_before_clearing_all_actor_usable_bits()
    {
        var ready = Ready(move: 0); var act = FacingTurnBattle.Attack(ready, new(A, B), SelfData.Weapon);
        Assert.True(act.Accepted); var p = Config([AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.Accumulate]);
        var result = BasicSkillAiListPreparation.EvaluateAfterAct(act.State.Battle, SelfData.Request(), p, act.State.Battle.TurnBattle.Turns.RngState);
        Assert.True(result.Accepted, result.Failure?.ToString()); Assert.All(result.Outcome!.ActorEntries, e => Assert.False(e.Usable));
        Assert.Equal(1, result.Outcome.Common.EnemyAbilityReach); Assert.Equal(0, result.Outcome.Common.AllyAbilityReach);
        Assert.Equal(2, result.Outcome.Units.Single(u => u.UnitId == A).Initialization.HelpfulCount);
        Assert.True(result.Outcome.ActorEntries[2].ConsumeAim);
    }

    [Fact]
    public void Original_list_extent_exact_refresh_budget_and_failure_isolation_are_explicit()
    {
        var state = Ready(); var maximum = Config(Enumerable.Repeat(AiHumanAbilityKind.Accumulate, 32).ToArray());
        var exact = BasicSkillAiListPreparation.Evaluate(state.Battle, SelfData.Request(), maximum, new(1, ulong.MaxValue - 1));
        Assert.True(exact.Accepted); Assert.Equal(34, exact.Outcome!.ActorEntries.Length); Assert.Equal(ulong.MaxValue, exact.RngState.DrawCount);
        foreach (var p in new[] { maximum with { AbilityLists = null }, Config(Enumerable.Repeat(AiHumanAbilityKind.Accumulate, 33).ToArray()) })
        {
            var result = BasicSkillAiListPreparation.Evaluate(state.Battle, SelfData.Request(), p, new(1, 7));
            Assert.False(result.Accepted); Assert.Equal(AccumulateAiPreparationRejection.UnsupportedList, result.Failure!.Reason);
        }
        var failed = BasicSkillAiListPreparation.Evaluate(state.Battle, SelfData.Request(), maximum, new(1, ulong.MaxValue));
        Assert.False(failed.Accepted); Assert.Null(failed.Outcome); Assert.Equal(new RandomState(1, ulong.MaxValue), failed.RngState);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_actor_identities_and_reversed_collections_replay_without_input_mutation()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(second: second); var p = Config([AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.Accumulate], team: team);
            var before = Snapshot(state.Battle); var first = BasicSkillAiListPreparation.Evaluate(state.Battle, SelfData.Request(second), p, new(1, 7));
            var reverse = BasicSkillAiListPreparation.Evaluate(Ready(second: second, reverse: true).Battle, SelfData.Request(second), p, new(1, 7));
            Assert.True(first.Accepted); Assert.Equal(Serialize(first), Serialize(reverse)); Assert.Equal(before, Snapshot(state.Battle));
        }
    }
}
