namespace Zodiac.Framework.Ai;

/// <summary>Shared closed-turn routing after a published command; no next-clock or encounter policy.</summary>
internal static class AiTurnProgress
{
    internal static AiTurnStage? Next(FacingTurnState state, BattleUnitId actor, FacingMoveContinuation? continuation) =>
        FromFacts(continuation is not null, state.PendingFacing is not null, state.Battle.TurnBattle.Turns.ActiveUnitId == actor);

    internal static AiTurnStage? Next(RosterFacingTurnState state, BattleUnitId actor, RosterMoveContinuation? continuation) =>
        FromFacts(continuation is not null, state.PendingFacing is not null, state.Battle.TurnBattle.Turns.ActiveUnitId == actor);

    private static AiTurnStage? FromFacts(bool retained, bool facing, bool active) =>
        retained ? AiTurnStage.RetainedCommand : facing ? AiTurnStage.FinishFacing :
        active ? AiTurnStage.PostActCommand : null;
}
