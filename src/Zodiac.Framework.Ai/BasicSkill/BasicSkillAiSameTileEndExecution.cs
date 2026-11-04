namespace Zodiac.Framework.Ai;

/// <summary>Fresh empty ranking at the actual origin dispatches END with deferred ChooseClosedEnemy facing.</summary>
/// <remarks>ZOD-241. Uses actual Wait/refund/facing rules, with no extra ACT, Move, token, draw or clock advance.</remarks>
public static class BasicSkillAiSameTileEndExecution
{
    public static BasicSkillAiCommandResult Execute(FacingTurnState state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiProfile profile, BasicSkillAiSelectionProfile selection,
        SwordAiCommandDispatchProfile dispatch) => BasicSkillAiCommandExecution.ExecuteMode(state, request, scenario, profile,
            selection, dispatch, BasicSkillAiDispatchMode.SameTileEnd);
}
