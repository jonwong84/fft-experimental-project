using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

internal static class AccumulateAiTestData
{
    internal static readonly AssetId Self = new("sample:ability/accumulate"), SwordId = new("sample:weapon/sword");
    internal static readonly TilePosition Origin = new(1, 1, 0), Destination = new(2, 1, 0);
    internal static readonly BoundedSwordWeapon Weapon = new(SwordId, 2);
    internal static AccumulateAiCandidateProfile Profile(bool granted = true) => new(
        new([new(Self)], [new(A, granted ? [Self] : [], 0), new(B, [Self], 0)]),
        new(Ground, A, B), [new(A, 255), new(B, 19)]);
    internal static AiScoreProfile Scores() => new([new(A, AiScoreFacts.ClosedProfile), new(B, AiScoreFacts.ClosedProfile)]);
    internal static SwordAiCandidateProfile Attack() => new(new(Ground), new(Ground, A, B), Profile().Units);
    internal static AccumulateAiCandidate SelfCandidate(bool move = false, bool actFirst = true, bool second = false) =>
        new(second ? B : A, Self, second ? A : B, move ? Destination : Origin, actFirst, true);
    internal static AttackBattleSnapshot Battle(int pa = 5, int actorHp = 50, int enemyHp = 50,
        bool moved = false, bool acted = false, bool second = false, bool reverse = false,
        ulong ticks = 20, int targetCt = 95, int move = 3)
    {
        var state = State(pa: pa, hp: enemyHp, moved: moved, acted: acted, second: second, reverse: reverse,
            ticks: ticks, targetCt: targetCt, move: move);
        var actor = second ? B : A;
        return new(state.TurnBattle, state.Combatants.Select(u => Copy(u, u.UnitId == actor ? actorHp : u.Hp)));
    }
    private static AttackUnitState Copy(AttackUnitState u, int hp) => new(u.UnitId, hp, u.MaxHp, u.Mp, u.MaxMp,
        u.PhysicalAttack, u.ClassPhysicalEvade, u.RightHandEvade, u.LeftHandEvade, u.AccessoryEvade, u.Brave,
        u.ZodiacSign, u.Facing, hp == 0 ? AttackUnitStatus.Dead : hp <= u.MaxHp / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None,
        u.KoCount, SwordId);
    internal static SwordAiRefreshRequest RefreshRequest(bool second = false) =>
        new(second ? B : A, second ? A : B, [new(A, false, false), new(B, false, false)]);
    internal static AccumulateAiPreparationRequest Request(bool second = false) => new(RefreshRequest(second), Self, Stone, Weapon);
    internal static AccumulateAiPreparationConfiguration Configuration(SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue) =>
        new(Attack(), CandidateProfile(), Profile(), Scores(), new(true, true, true), new(true, true, true, true), team);
}
