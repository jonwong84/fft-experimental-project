namespace Zodiac.Framework;

/// <summary>Pure original-compatible living-roster movement: ally transit, empty endpoints and enemy blockers.</summary>
/// <remarks>ZOD-279; shares source range/backtrace with the existing two-opponent query. No choice, facing or RNG publication.</remarks>
public static class FlatRosterMovement
{
    public static FlatMovementQueryResult Evaluate(AttackBattleSnapshot state, BattleUnitId actorId,
        FlatRosterBattleProfile profile, TilePosition? destination = null, BattleUnitId? focusId = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(profile);
        var battle = state.TurnBattle.Battle;
        FlatMovementQueryResult Reject(FlatMovementRejection reason) => new(false, reason, null, state.TurnBattle.Turns.RngState);
        if (!battle.TryGetUnit(actorId, out var actor)) return Reject(FlatMovementRejection.UnknownActor);
        if (!profile.SupportsMovement(state)) return Reject(FlatMovementRejection.UnsupportedProfile);
        TilePosition? focus = null;
        if (focusId is { } id)
        {
            if (!battle.TryGetUnit(id, out var target)) return Reject(FlatMovementRejection.UnknownFocus);
            focus = target!.Position;
        }
        var occupied = battle.Units.Where(u => u.Id != actorId).Select(u => u.Position).ToHashSet();
        int team = profile.Units[actorId].StableTeam;
        var blocked = battle.Units.Where(u => u.Id != actorId && profile.Units[u.Id].StableTeam != team).Select(u => u.Position).ToHashSet();
        return FlatMovementKernel.Evaluate(state, actor!, blocked, occupied, focus, destination);
    }
}
