namespace Zodiac.Framework.Ai;

/// <summary>Immutable supported decision inputs shared by pure decisions, fresh command execution and turn recovery.</summary>
/// <remarks>No live state, RNG, cached prefix or executable plan is stored here.</remarks>
public sealed record AiDecisionConfiguration(RosterAiSimulationProfile Simulation, RosterAiRefreshProfile Refresh,
    AiHumanAbilityLists Lists, RosterAiGeneralProfile General, RosterAiMovementProfile Movement,
    AiAbilityEvaluationProfile Abilities)
{
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Simulation); ArgumentNullException.ThrowIfNull(Refresh);
        ArgumentNullException.ThrowIfNull(Lists); ArgumentNullException.ThrowIfNull(General);
        ArgumentNullException.ThrowIfNull(Movement); ArgumentNullException.ThrowIfNull(Abilities);
    }
}
