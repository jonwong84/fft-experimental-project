using Xunit;
using Zodiac.Framework.Formulas;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

public sealed class PhysicalFormulaReviewAiTests
{
    [Theory]
    [InlineData(false, 1103527590U, 1UL)]
    [InlineData(true, 4182499122U, 5UL)]
    public void Reassigned_formula_AI_uses_the_neutral_YA_floor_before_shared_clock_and_absolute_score(
        bool projectile, uint word, ulong count)
    {
        var s = Make([new("a", 0, 0, 15, Pa: 1), new("b", 1, 1, 95), new("c", 2, 0)], rng: new(1, 0));
        var id = new AssetId(projectile ? "test:stone" : "test:dash");
        var delivery = projectile ? PhysicalAbilityDelivery.DirectProjectile : PhysicalAbilityDelivery.AdjacentUnit;
        var definition = new PhysicalAbilityDefinition(id, delivery, new(AbilityFormulaId.PummelFormula, new(9, 0)));
        var grants = s.State.Combatants.Select(u => new TargetedAbilityUnitGrant(u.UnitId, [id], 0));
        var origin = s.State.TurnBattle.Battle.Units.Single(u => u.Id == A).Position;
        var before = Snapshot(s.State);
        if (projectile)
        {
            var profile = TargetedAbilityBattleProfile.FromDefinitions(Ground, [definition], grants);
            var result = RosterThrowStoneAiCandidate.Evaluate(s.State, new(A, id, B, origin, true), profile,
                s.Simulation, new(true, true, true, true), new(1, 0));
            Assert.Equal(AiScoringDisposition.Scored, result.Disposition);
            Assert.Equal(10, result.Outcome!.NormalDamage);
            Assert.Equal((short)511, result.Score!.Priority);
            Assert.Equal(new RandomState(word, count), result.RngState);
        }
        else
        {
            var profile = DashAbilityBattleProfile.FromDefinitions(Ground, [definition], grants);
            var result = RosterDashAiCandidate.Evaluate(s.State, new(A, id, B, origin, true), s.Simulation,
                profile, new(true, true, true), new(1, 0));
            Assert.Equal(AiScoringDisposition.Scored, result.Disposition);
            Assert.Equal(10, result.Outcome!.NormalDamage);
            Assert.Equal((short)511, result.Score!.Priority);
            Assert.Equal(new RandomState(word, count), result.RngState);
        }
        Assert.Equal(before, Snapshot(s.State));
    }
}
