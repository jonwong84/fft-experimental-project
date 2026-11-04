using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record BasicSkillAiScenarioFailure(AccumulateAiPreparationFailure? Preparation = null,
    AttackThrowStoneAiScenarioFailure? Movement = null);
public sealed record BasicSkillAiScenarioOutcome(AccumulateAiPreparationOutcome Preparation, AiDerivedMovementScenario Movement);
public sealed record BasicSkillAiScenarioResult(bool Accepted, BasicSkillAiScenarioFailure? Failure,
    BasicSkillAiScenarioOutcome? Outcome, RandomState RngState);
internal sealed record BasicSkillAiScenarioCore(BasicSkillAiScenarioResult Result, AiMovementPrefixOutcome? Prefix = null);

/// <summary>Own fresh three-entry preparation and derived ordinary movement, preserving unknown cells without reading them.</summary>
/// <remarks>ZOD-249/250. No public cached prefixes, action scans, prior memory, lottery or command authority.</remarks>
public static class BasicSkillAiScenarios
{
    public static BasicSkillAiScenarioResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration configuration, AttackThrowStoneAiScenarioProfile profile, RandomState decisionRng) =>
        EvaluateFresh(state, request, configuration, profile, decisionRng).Result;

    internal static BasicSkillAiScenarioCore EvaluateFresh(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration configuration, AttackThrowStoneAiScenarioProfile profile, RandomState decisionRng)
    {
        AccumulateAiPreparation.ValidateArguments(state, request, configuration); ArgumentNullException.ThrowIfNull(profile);
        BasicSkillAiScenarioCore Reject(BasicSkillAiScenarioFailure failure) => new(new(false, failure, null, decisionRng));
        if (!profile.NormalScoring || !profile.CanonicalCoordinates || !profile.NoOtherScenarioOverrides)
            return Reject(new(Movement: new(AttackThrowStoneAiScenarioStage.Admission, AttackThrowStoneAiScenarioRejection.UnsupportedProfile)));
        var preparation = AccumulateAiPreparation.Evaluate(state, request, configuration, decisionRng);
        if (!preparation.Accepted) return Reject(new(Preparation: preparation.Failure));
        var ready = preparation.Outcome!;
        var result = AiMovementScenarios.Evaluate(state, request.Turn, configuration.Attack, configuration.Accumulate.Units,
            ready.Common, ready.Team, ready.Units.Single(u => u.UnitId == request.Turn.ActorId).HasRangedAbility,
            preparation.RngState, decisionRng, outside: null);
        if (!result.Accepted) return Reject(new(Movement: result.Failure));
        return new(new(true, null, new(ready, result.Outcome!), result.RngState), result.Prefix);
    }
}
