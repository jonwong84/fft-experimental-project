using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Typed facing intent. Choosing an enemy explicitly certifies its opposing role in this closed profile.</summary>
public abstract record FacingChoice
{
    private FacingChoice() { }
    public sealed record Fixed(BattleFacing Facing) : FacingChoice;
    public sealed record KeepCurrent : FacingChoice;
    public sealed record ChooseClosedEnemy(BattleUnitId EnemyId) : FacingChoice;
}
