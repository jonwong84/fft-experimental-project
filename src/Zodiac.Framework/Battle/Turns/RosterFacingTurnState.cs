using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Opaque ordinary roster session. Only this command owner creates production instances.</summary>
public sealed class RosterFacingTurnState
{
    public AttackBattleSnapshot Battle { get; }
    public FlatRosterBattleProfile Profile { get; }
    public ulong Revision { get; }
    public FacingCompletionReceipt? PendingFacing { get; }
    internal object Lineage { get; }
    internal RosterFacingTurnState(AttackBattleSnapshot battle, FlatRosterBattleProfile profile, ulong revision,
        FacingCompletionReceipt? pending, object lineage)
    { Battle = battle; Profile = profile; Revision = revision; PendingFacing = pending; Lineage = lineage; }
}
