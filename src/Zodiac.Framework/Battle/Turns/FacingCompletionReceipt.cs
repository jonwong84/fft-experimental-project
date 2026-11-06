using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Opaque in-memory authorization for a fully settled, automatically ended turn; not a save token.</summary>
public sealed class FacingCompletionReceipt
{
    public BattleUnitId ActorId { get; }
    public ulong Revision { get; }
    internal AttackBattleSnapshot ExpectedBattle { get; }
    internal object Lineage { get; }

    internal FacingCompletionReceipt(BattleUnitId actorId, ulong revision, AttackBattleSnapshot battle, object lineage)
    { ActorId = actorId; Revision = revision; ExpectedBattle = battle; Lineage = lineage; }
}
