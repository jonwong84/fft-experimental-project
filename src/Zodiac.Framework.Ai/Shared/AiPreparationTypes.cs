namespace Zodiac.Framework.Ai;

/// <summary>Known retained unit policies; never inferred from unit IDs.</summary>
public sealed record AiUnitPolicy(BattleUnitId UnitId, bool ConserveCt, bool TeamSupportUnavailable);

public sealed record AiPreparedAbility(int Index, AiHumanInitializedEntry Source, bool Usable, bool ConsumeAim);

public sealed record AiReachAdjustment(uint Raw, int Modulo, int Before, int After)
{
    public bool Halved => Modulo == 0;
}
