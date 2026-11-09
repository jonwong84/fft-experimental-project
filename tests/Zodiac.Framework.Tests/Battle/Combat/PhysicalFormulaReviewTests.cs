using Xunit;
using Zodiac.Framework.Formulas;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

public sealed class PhysicalFormulaReviewTests
{
    [Theory]
    [InlineData(false, 2524885223U, 2UL)]
    [InlineData(true, 2516284547U, 6UL)]
    public void Reassigned_formula_keeps_neutral_YA_floor_in_actual_adjacent_and_projectile_execution(
        bool projectile, uint word, ulong count)
    {
        // Source 11464943bbdbd2a0af022d90cb7f10455cf911df:
        // PA1/Y0 -> YA0 -> normal zodiac floor1 -> base2; seed1's factor2 gives4 damage.
        var s = Make([new("a", new(1, 1, 0)), new("b", new(2, 1, 0), 1), new("c", new(4, 3, 0))], seed: 1, pa: 1);
        var id = new AssetId(projectile ? "test:stone" : "test:dash");
        var delivery = projectile ? PhysicalAbilityDelivery.DirectProjectile : PhysicalAbilityDelivery.AdjacentUnit;
        var profile = new PhysicalAbilityBattleProfile(Ground, delivery,
            [new(id, delivery, new(AbilityFormulaId.RandomPaPlusHalfPa, new(9, 0)))],
            s.State.Combatants.Select(u => new TargetedAbilityUnitGrant(u.UnitId, [id], 0)));
        var before = Snapshot(s.State);
        var result = PhysicalAbilityBattleSimulator.ApplyRoster(s.State, new(A, id, B), profile, s.Profile,
            projectile ? new(s.State.Combatants.Select(u => new FlatRosterSpriteHeight(u.UnitId, 24))) : null);
        Assert.True(result.Accepted);
        Assert.Equal(46, result.State.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(new Random.RandomState(word, count), result.State.TurnBattle.Turns.RngState);
        Assert.Equal(before, Snapshot(s.State));
        if (projectile) Assert.Equal(4, Assert.Single(result.Events.OfType<ThrowStoneResolvedEvent>()).Damage);
        else Assert.Equal(4, Assert.Single(result.Events.OfType<DashResolvedEvent>()).Damage);
    }
}
