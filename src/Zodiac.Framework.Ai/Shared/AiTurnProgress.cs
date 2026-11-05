namespace Zodiac.Framework.Ai;

/// <summary>Shared closed-turn routing after a published command; no next-clock or encounter policy.</summary>
internal static class AiTurnProgress
{
    internal static BasicSkillAiTurnStage? Next(FacingTurnState state, BattleUnitId actor, FacingMoveContinuation? continuation) =>
        continuation is not null ? BasicSkillAiTurnStage.RetainedCommand :
        state.PendingFacing is not null ? BasicSkillAiTurnStage.FinishFacing :
        state.Battle.TurnBattle.Turns.ActiveUnitId == actor ? BasicSkillAiTurnStage.PostActCommand : null;
}
