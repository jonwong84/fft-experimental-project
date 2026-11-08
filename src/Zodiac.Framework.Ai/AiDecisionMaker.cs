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

        var prepared = RosterAiGeneralScenarios.Evaluate(state, request, configuration.Simulation, configuration.Refresh,
            configuration.Lists, configuration.General, configuration.Movement, rng);
        if (!prepared.Accepted)
        {
            return Reject(new(AiDecisionStage.Prefix, prepared.Failure));
        }

        var scenarios = prepared.Outcome!;
        var selection = AiGeneralActionSelection.Select(state, request.ActorId, configuration, scenarios,
            prepared.RngState, workspace);
        if (selection.Failure is { } failure)
        {
            return Reject(failure);
        }

        var successor = workspace.Write(scenarios.Movement.Select(tile =>
            KeyValuePair.Create(tile.Outcome.Destination, tile.TilePriority)));
        return new(true, null, selection.Decision, selection.RngState, successor);
    }
}
