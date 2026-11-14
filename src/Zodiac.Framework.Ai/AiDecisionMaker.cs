using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Prepare one fresh decision, select a supported general action and publish successor priority knowledge.</summary>
/// <remarks>ZOD-302/303; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Every hypothesis restores state while decision RNG chains.</remarks>
public static class AiDecisionMaker
{
    public static AiDecisionResult Decide(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        AiDecisionConfiguration configuration, RandomState rng, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        workspace ??= AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map);
        workspace.ValidateMap(state);
        AiDecisionResult Reject(AiDecisionFailure failure) => new(false, failure, null, rng, workspace);

        var profile = configuration.Abilities;
        if (!profile.NoDefendSupport || !profile.FreshOrdinarySelectionDefaults)
        {
            return Reject(new(AiDecisionStage.Admission));
        }

        var prepared = RosterAiGeneralScenarios.Prepare(state, request, configuration.Simulation, configuration.Refresh,
            configuration.Lists, configuration.General, configuration.Movement, rng);
        if (!prepared.Accepted)
        {
            return Reject(new(AiDecisionStage.Prefix, prepared.Failure));
        }

        var selection = ActionSelect(state, request, configuration, prepared.Outcome!,
            prepared.RngState, workspace);
        if (selection.Failure is { } failure)
        {
            return Reject(failure);
        }

        var successor = workspace.Write(selection.Decision!.Scenarios.Movement.Select(tile =>
            KeyValuePair.Create(tile.Outcome.Destination, tile.TilePriority)));
        return new(true, null, selection.Decision, selection.RngState, successor);
    }

    /// <summary>The admitted action_select branch, after fresh roster/target preparation.</summary>
    /// <remarks>
    /// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, src/battle/ai/turn.c::action_select.
    /// Existing admission excludes status-imposed, conditional-retreat and special auto-battle policies.
    /// They must be implemented before adding their branches here; they do not silently fall through to general search.
    /// </remarks>
    private static (AiDecision? Decision, AiDecisionFailure? Failure, RandomState RngState) ActionSelect(
        AttackBattleSnapshot state, RosterAiRefreshRequest request, AiDecisionConfiguration configuration,
        RosterAiGeneralPreparationOutcome preparation, RandomState rng, AiPriorityWorkspace workspace)
    {
        return AiGeneralActionSelection.GeneralSelect(state, request, configuration, preparation, rng, workspace);
    }
}
