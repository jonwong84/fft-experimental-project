using Xunit;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class ThrowStoneAiFormulaProjectionTests
{
    [Fact]
    public void Midpoint_damage_uses_current_PA_keeps_accuracy_separate_and_never_projects_knockback()
    {
        foreach (var (pa, hp, evade, hit, damage, remaining) in new[] {
            (5, 50, 0, 100, 10, 40), (5, 50, 75, 25, 10, 40), (5, 50, 100, 0, 0, 50),
            (6, 11, 0, 100, 12, 0), (99, 200, 0, 100, 198, 2) })
        {
            var state = State(pa: pa, hp: hp, maxHp: Math.Max(50, hp), evade: evade); var before = Snapshot(state);
            var result = Evaluate(state); var o = Success(result);
            Assert.Equal(new(hit, damage, remaining), o); Assert.Equal(hit != 0, o.HasAccuracy);
            Assert.Equal(new FftRandomState(3295386429, 11), result.RngState);
            Assert.Equal(before, Snapshot(state)); Assert.Null(state.Combatants[0].RightHandWeaponId);
        }
    }

    [Fact]
    public void Range_edge_and_diagonal_front_evasion_share_actual_Throw_Stone_geometry()
    {
        foreach (var (target, facing, hit) in new[] {
            (new TilePosition(5, 1, 0), BattleFacing.West, 25), (new TilePosition(2, 1, 0), BattleFacing.North, 100),
            (new TilePosition(3, 3, 0), BattleFacing.West, 25), (new TilePosition(3, 3, 0), BattleFacing.South, 25),
            (new TilePosition(3, 3, 0), BattleFacing.North, 100) })
            Assert.Equal(hit, Success(Evaluate(State(target: target, evade: 75, facing: facing))).HitPercent);
        Atomic(Evaluate(State(target: new(6, 1, 0))), AttackCommandRejection.OutOfRange);
    }

    [Fact]
    public void Exactly_four_draws_fit_at_MAX_and_partial_evasion_failure_is_atomic()
    {
        var state = State(); var before = Snapshot(state); var last = new FftRandomState(1, ulong.MaxValue - 4);
        var fits = Evaluate(state, rng: last); Success(fits); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var shortBudget = last with { DrawCount = ulong.MaxValue - 3 };
        Atomic(Evaluate(state, rng: shortBudget), AttackCommandRejection.RandomDrawCountExhausted, shortBudget);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Shared_current_command_admission_checks_grants_choices_and_supported_profile_before_drawing()
    {
        var state = State();
        Atomic(ThrowStoneAiFormulaProjection.Evaluate(state, new(new("sample:unit/missing"), Stone, B), AbilityProfile(), Decision), AttackCommandRejection.UnknownActor);
        Atomic(ThrowStoneAiFormulaProjection.Evaluate(state, new(A, Stone, new("sample:unit/missing")), AbilityProfile(), Decision), AttackCommandRejection.UnknownTarget);
        Atomic(ThrowStoneAiFormulaProjection.Evaluate(state, new(A, Stone, A), AbilityProfile(), Decision), AttackCommandRejection.SelfTarget);
        Atomic(ThrowStoneAiFormulaProjection.Evaluate(state, new(A, new("sample:ability/unknown"), B), AbilityProfile(), Decision), AttackCommandRejection.UnknownAbility);
        Atomic(Evaluate(state, profile: AbilityProfile(false)), AttackCommandRejection.AbilityNotGranted);
        Atomic(Evaluate(State(acted: true)), AttackCommandRejection.ActionAlreadyTaken);
        Atomic(Evaluate(State(active: false)), AttackCommandRejection.InactiveActor);
        Atomic(Evaluate(State(hp: 0)), AttackCommandRejection.DeadTarget);
        Atomic(Evaluate(State(badGround: true)), AttackCommandRejection.UnsupportedSetup);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Either_actor_collection_order_and_carried_decision_RNG_replay_without_live_mutation()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = State(second: second); var before = Snapshot(state); var result = Evaluate(state, second: second); Success(result);
            Assert.Equal(result, Evaluate(State(second: second, reverse: true), second: second));
            Assert.Equal(result, Evaluate(state, second: second));
            var rng = result.RngState;
            for (int i = 0; i < 4; i++) rng = new FftBiosRandom().Next(rng).State;
            Assert.Equal(rng, Evaluate(state, second: second, rng: result.RngState).RngState);
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static ThrowStoneAiFormulaResult Evaluate(AttackBattleSnapshot state, bool second = false,
        TargetedAbilityBattleProfile? profile = null, FftRandomState? rng = null) =>
        ThrowStoneAiFormulaProjection.Evaluate(state, Command(second), profile ?? AbilityProfile(), rng ?? Decision);
    private static ThrowStoneAiFormulaOutcome Success(ThrowStoneAiFormulaResult result)
    { Assert.True(result.Accepted, Serialize(result)); Assert.Equal(AttackCommandRejection.None, result.Rejection); return Assert.IsType<ThrowStoneAiFormulaOutcome>(result.Outcome); }
    private static void Atomic(ThrowStoneAiFormulaResult result, AttackCommandRejection reason, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState); }
}
