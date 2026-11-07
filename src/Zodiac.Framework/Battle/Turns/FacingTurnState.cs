using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Opt-in immutable command boundary. Only Begin and accepted transitions issue production states.</summary>
public sealed class FacingTurnState
{
    public AttackBattleSnapshot Battle { get; }
    public FlatSwordBattleProfile Profile { get; }
    public ulong Revision { get; }
    public FacingCompletionReceipt? PendingFacing { get; }
    internal object Lineage { get; }

    internal FacingTurnState(AttackBattleSnapshot battle, FlatSwordBattleProfile profile, ulong revision,
        FacingCompletionReceipt? pendingFacing, object lineage)
    { Battle = battle; Profile = profile; Revision = revision; PendingFacing = pendingFacing; Lineage = lineage; }
}
