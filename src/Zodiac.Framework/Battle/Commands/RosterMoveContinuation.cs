using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Opaque frozen follow-up bound to one exact published roster Move. No save format or replacement payload.</summary>
public sealed class RosterMoveContinuation
{
    internal RosterFacingTurnState ExpectedState { get; }
    internal AttackBattleSnapshot ExpectedBattle { get; }
    internal BattleUnitId ActorId { get; }
    internal ulong Revision { get; }
    internal object Lineage { get; }
    internal Func<RosterFacingTurnState, RosterFacingResult> Followup { get; }
    internal RosterMoveContinuation(RosterFacingTurnState state, BattleUnitId actor,
        Func<RosterFacingTurnState, RosterFacingResult> followup)
    { ExpectedState = state; ExpectedBattle = state.Battle; ActorId = actor; Revision = state.Revision; Lineage = state.Lineage; Followup = followup; }
}
