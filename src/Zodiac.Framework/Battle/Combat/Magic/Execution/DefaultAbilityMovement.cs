namespace Zodiac.Framework;

/// <summary>Current cost1 flat range and source backtrace for the owned bare-human ordinary encounter.</summary>
public static class DefaultAbilityMovement
{
    public static FlatMovementQueryResult Query(DefaultAbilityBattleState state, BattleUnitId actorId,
        TilePosition? destination = null) => QueryCore(state, actorId, destination, null);

    /// <summary>Same admitted movement graph, with source target-distance rows for a represented focus.</summary>
    public static FlatMovementQueryResult QueryFocused(DefaultAbilityBattleState state, BattleUnitId actorId,
        BattleUnitId focusId) => QueryCore(state, actorId, null, focusId);

    private static FlatMovementQueryResult QueryCore(DefaultAbilityBattleState state, BattleUnitId actorId,
        TilePosition? destination, BattleUnitId? focusId)
    {
        ArgumentNullException.ThrowIfNull(state);
        FlatMovementQueryResult Reject(FlatMovementRejection reason) => new(false, reason, null, state.Turns.RngState);
        var actor = state.Units.FirstOrDefault(u => u.Id == actorId);
        if (actor is null) return Reject(FlatMovementRejection.UnknownActor);
        var focus = focusId is { } id ? state.Units.FirstOrDefault(u => u.Id == id) : null;
        if (focusId is not null && focus is null) return Reject(FlatMovementRejection.UnsupportedProfile);
        // Definition owns the no-trap/RSM/mount/status profile; these are authored cost1 flat tiles.
        if (state.Finished || state.Definition.MovementGround is null || state.Definition.Map.Width > 16 || state.Definition.Map.Depth > 16 ||
            state.Units.Any(u => u.Current.Statuses.Dead)) return Reject(FlatMovementRejection.UnsupportedProfile);
        var occupied = state.Units.Where(u => u.Id != actorId).Select(u => u.Position).ToHashSet();
        var blocked = state.Units.Where(u => u.Id != actorId && u.Occupant.CurrentTeam != actor.Occupant.CurrentTeam)
            .Select(u => u.Position).ToHashSet();
        var turn = state.Turns.Units.Single(u => u.Id == actorId);
        return FlatMovementKernel.Evaluate(state.Definition.Map, actor.Position, actor.Definition.Move, actor.Definition.Jump,
            turn.MovementTaken, blocked, occupied, focus?.Position, destination, state.Turns.RngState);
    }
}
