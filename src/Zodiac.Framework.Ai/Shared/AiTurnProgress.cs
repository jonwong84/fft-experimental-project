namespace Zodiac.Framework.Ai;

/// <summary>Shared closed-turn routing after a published command; no next-clock or encounter policy.</summary>
internal static class AiTurnProgress
{
    internal static BasicSkillAiTurnStage? Next(FacingTurnState state, BattleUnitId actor, FacingMoveContinuation? continuation) =>
        FromFacts(continuation is not null, state.PendingFacing is not null, state.Battle.TurnBattle.Turns.ActiveUnitId == actor);

    internal static BasicSkillAiTurnStage? Next(RosterFacingTurnState state, BattleUnitId actor, RosterMoveContinuation? continuation) =>
        FromFacts(continuation is not null, state.PendingFacing is not null, state.Battle.TurnBattle.Turns.ActiveUnitId == actor);

    private static BasicSkillAiTurnStage? FromFacts(bool retained, bool facing, bool active) =>
        retained ? BasicSkillAiTurnStage.RetainedCommand : facing ? BasicSkillAiTurnStage.FinishFacing :
        active ? BasicSkillAiTurnStage.PostActCommand : null;
}
