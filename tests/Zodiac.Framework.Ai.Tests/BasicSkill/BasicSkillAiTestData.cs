using Zodiac.Framework.Random;
using Xunit;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

internal static class BasicSkillAiTestData
{
    internal static readonly TilePosition Origin = new(1, 1, 0), Far = new(0, 1, 0);
    internal static BasicSkillAiProfile Profile(SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue) =>
        new(SelfData.Configuration(team), new(true, true, true, true, true),
            new(true, true, true, true, true, true, true, true), new(true, true, true, true), true, true, true);
    internal static ThrowStoneAiScanScenario Scenario(bool emptyDamageMask = false, bool sameD1 = false) =>
        new([Origin, Far], emptyDamageMask ? [] : [Origin, Far], Origin, sameD1 ? Origin : Far);
    internal static AttackBattleSnapshot Battle(bool adjacent = true, int evade = 97, int pa = 5, int enemyHp = 50,
        bool second = false, bool reverse = false, ulong ticks = 20, TilePosition? target = null)
    {
        var state = State(target: target ?? (adjacent ? new(2, 1, 0) : new(7, 7, 0)), evade: evade, pa: pa,
            hp: enemyHp, second: second, reverse: reverse, ticks: ticks);
        return new(state.TurnBattle, state.Combatants.Select(u => new AttackUnitState(u.UnitId, u.Hp, u.MaxHp, u.Mp, u.MaxMp,
            u.PhysicalAttack, u.ClassPhysicalEvade, u.RightHandEvade, u.LeftHandEvade, u.AccessoryEvade, u.Brave,
            u.ZodiacSign, u.Facing, u.Status, u.KoCount, SelfData.SwordId)));
    }
    internal static BasicSkillAiCompositionResult Compose(AttackBattleSnapshot state, BasicSkillAiProfile? profile = null,
        ThrowStoneAiScanScenario? scenario = null, RandomState? rng = null, bool second = false) =>
        BasicSkillAiComposition.Evaluate(state, SelfData.Request(second), scenario ?? Scenario(), profile ?? Profile(), rng ?? Decision);
    internal static FacingTurnState Ready(ulong count = 7, uint word = 1, int pa = 5, int evade = 97,
        TilePosition? target = null, bool second = false, bool reverse = false, int ct = 15, int move = 3)
    {
        var template = Battle(evade: evade, pa: pa, target: target, second: second, reverse: reverse);
        var actor = second ? B : A;
        var rng = new RandomState(unchecked((word - 12345u) * 4005161829u), count - 1);
        var old = template.TurnBattle;
        var bodies = old.Battle.Units.Select(u => new BattleUnitState(u.Id, u.Position,
            u.Id == actor ? new UnitMovementProfile(move, u.MovementProfile.Jump, u.MovementProfile.BodyHeight,
                u.MovementProfile.Mode, u.MovementProfile.Traits, u.MovementProfile.TerrainCosts) : u.MovementProfile));
        var turns = old.Turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed, u.Id == actor ? ct + 95 : 90, u.TieKey));
        var initial = FacingTurnBattle.Begin(new(new(new(old.Battle.Map, bodies, rng), new(turns, rng)), template.Combatants), new(Ground));
        var ready = FacingTurnBattle.Advance(initial); Assert.True(ready.Accepted);
        Assert.Equal(new RandomState(word, count), ready.State.Battle.TurnBattle.Turns.RngState); return ready.State;
    }
}
