using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AccumulateAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AccumulateAiUsefulnessTests
{
    [Theory]
    [InlineData(1u, 16838u, 1103527590u, true)] [InlineData(3u, 17747u, 3310558080u, false)]
    public void Literal_parity_coin_is_per_entry_even_at_PA99_and_enemy_never_draws(uint word, uint raw, uint after, bool useful)
    {
        var state = Battle(pa: 99); var before = Snapshot(state); var rng = new RandomState(word, 7);
        var result = AccumulateAiUsefulness.Evaluate(state, SelfCandidate(), Profile(), new(true, true, true), rng);
        Assert.True(result.Accepted); Assert.Equal(useful, result.Outcome!.SelfUseful);
        Assert.Equal(new RandomState(after, 8), result.RngState);
        Assert.Collection(result.Outcome.Targets,
            t => Assert.Equal(new(A, useful ? AccumulateAiEffect.Useful : AccumulateAiEffect.None, raw), t),
            t => Assert.Equal(new(B, AccumulateAiEffect.Adverse, null), t));
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Inactive_self_takes_no_coin_even_with_exhausted_stream_and_no_targets_is_empty()
    {
        var rng = new RandomState(1, ulong.MaxValue);
        var result = AccumulateAiUsefulness.Evaluate(Battle(), SelfCandidate(), Profile(), new(true, false, true), rng);
        Assert.True(result.Accepted); Assert.False(result.Outcome!.SelfUseful); Assert.Equal(rng, result.RngState);
        Assert.Equal(new(B, AccumulateAiEffect.Adverse, null), Assert.Single(result.Outcome.Targets));
        var empty = AccumulateAiUsefulness.Evaluate(Battle(), SelfCandidate(), Profile(), new(true, false, false), rng);
        Assert.True(empty.Accepted); Assert.Empty(empty.Outcome!.Targets); Assert.Equal(rng, empty.RngState);
    }

    [Fact]
    public void Current_mask_source_order_and_late_coin_failure_publish_no_partial_filter()
    {
        var state = Battle(); var turns = state.TurnBattle.Turns;
        var reordered = new AttackBattleSnapshot(new(state.TurnBattle.Battle,
            new(turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed, u.Ct, u.Id == A ? 17 : 2,
                u.HasTurn, u.MovementTaken, u.ActionTaken)), turns.RngState, turns.Ticks, turns.Phase)), state.Combatants);
        var result = AccumulateAiUsefulness.Evaluate(reordered, SelfCandidate(), Profile(), new(true, true, true), Decision);
        Assert.Equal(new[] { B, A }, result.Outcome!.Targets.Select(t => t.UnitId));
        var exhausted = new RandomState(1, ulong.MaxValue);
        var failure = AccumulateAiUsefulness.Evaluate(reordered, SelfCandidate(), Profile(), new(true, true, true), exhausted);
        Assert.False(failure.Accepted); Assert.Null(failure.Outcome); Assert.Equal(exhausted, failure.RngState);
        Assert.Equal(AccumulateAiUsefulnessRejection.RandomDrawCountExhausted, failure.Rejection);
        var unsupported = AccumulateAiUsefulness.Evaluate(state, SelfCandidate(), Profile(), new(), Decision);
        Assert.False(unsupported.Accepted); Assert.Equal(Decision, unsupported.RngState);
    }
}
