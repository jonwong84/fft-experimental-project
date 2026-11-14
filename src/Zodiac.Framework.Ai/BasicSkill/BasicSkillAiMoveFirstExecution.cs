namespace Zodiac.Framework.Ai;

/// <summary>Execute a fresh different-tile Move and retain the selected Attack, Throw Stone or Wait.</summary>
/// <remarks>ZOD-240. Continue only with the returned exact-state Framework token; selection RNG is adopted once.
/// A rejected follow-up preserves the published Move. The closed self entry always retains ACT-first.</remarks>
internal static class BasicSkillAiMoveFirstExecution
{
    public static BasicSkillAiCommandResult Execute(FacingTurnState state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiProfile profile, BasicSkillAiSelectionProfile selection,
        SwordAiCommandDispatchProfile dispatch) => BasicSkillAiCommandExecution.ExecuteMode(state, request, scenario, profile,
            selection, dispatch, BasicSkillAiDispatchMode.MoveFirst);
}
