using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

internal static class AttackThrowStoneAiTestData
{
    internal static readonly AssetId Sword = new("sample:item/plain-sword");
    internal static readonly TilePosition Origin = new(1, 1, 0), Far = new(0, 1, 0);
    internal static ThrowStoneAiScanRequest Request(bool second = false) => new(second ? B : A, Stone, second ? A : B);
    internal static ThrowStoneAiScanScenario Scenario() => new([Origin, Far], [Origin, Far], Origin, Origin);
    internal static AttackThrowStoneAiProfile Profile(bool active = true) => new(
        new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]), CandidateProfile(),
        new([new(A, AiScoreFacts.ClosedProfile), new(B, AiScoreFacts.ClosedProfile)]),
        new(active, true, true, true, true), new(active, true, true, true, true, true, true, true), new(true, true, true, true), true, true);
    internal static AttackBattleSnapshot Battle(int hp = 50, int evade = 0, bool second = false, bool reverse = false, ulong ticks = 20)
    {
        var state = State(target: new(2, 1, 0), hp: hp, evade: evade, second: second, reverse: reverse, ticks: ticks);
        var actor = second ? B : A;
        return new(state.TurnBattle, state.Combatants.Select(u => new AttackUnitState(u.UnitId, u.Hp, u.MaxHp, u.Mp, u.MaxMp,
            u.PhysicalAttack, u.ClassPhysicalEvade, u.RightHandEvade, u.LeftHandEvade, u.AccessoryEvade, u.Brave,
            u.ZodiacSign, u.Facing, u.Status, u.KoCount, u.UnitId == actor ? Sword : u.RightHandWeaponId)));
    }
    internal static AttackThrowStoneAiCompositionResult Evaluate(AttackBattleSnapshot? state = null, int power = 2,
        ThrowStoneAiScanScenario? scenario = null, AttackThrowStoneAiProfile? profile = null, FftRandomState? rng = null, bool second = false) =>
        AttackThrowStoneAiComposition.Evaluate(state ?? Battle(second: second), Request(second), scenario ?? Scenario(),
            new(Sword, power), profile ?? Profile(), rng ?? Decision);
}
