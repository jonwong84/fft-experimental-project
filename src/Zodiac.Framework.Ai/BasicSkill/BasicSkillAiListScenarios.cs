using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Remaining ordinary source context; no supplied scenarios or fixed entry-order assertion.</summary>
public sealed record BasicSkillAiListProfile(AccumulateAiPreparationConfiguration Preparation,
    AttackThrowStoneAiScenarioProfile Scenarios, bool SourceGeneralEntry = false,
    bool FreshEmptyRankedList = false, bool ClearDirectTrajectories = false);
public sealed record BasicSkillAiListScenarioOutcome(BasicSkillAiListPreparationOutcome Preparation, AiDerivedMovementScenario Movement);
public sealed record BasicSkillAiListScenarioResult(bool Accepted, BasicSkillAiScenarioFailure? Failure,
    BasicSkillAiListScenarioOutcome? Outcome, RandomState RngState);
internal sealed record BasicSkillAiListScenarioCore(BasicSkillAiListScenarioResult Result, AiMovementPrefixOutcome? Prefix = null);

/// <summary>One fresh variable-list preparation and shared source movement evaluation; no action search.</summary>
internal static class BasicSkillAiListScenarios
{
    public static BasicSkillAiListScenarioResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiListProfile profile, RandomState rng) => EvaluateFresh(state, request, profile, rng, false).Result;

    internal static BasicSkillAiListScenarioCore EvaluateFresh(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiListProfile profile, RandomState rng, bool afterAct)
    {
        ValidateArguments(state, request, profile);
        BasicSkillAiListScenarioCore Reject(BasicSkillAiScenarioFailure reason) => new(new(false, reason, null, rng));
        if (!profile.Scenarios.NormalScoring || !profile.Scenarios.CanonicalCoordinates || !profile.Scenarios.NoOtherScenarioOverrides)
            return Reject(new(Movement: new(AttackThrowStoneAiScenarioStage.Admission, AttackThrowStoneAiScenarioRejection.UnsupportedProfile)));
        var p = profile.Preparation;
        var prepared = afterAct ? BasicSkillAiListPreparation.EvaluateAfterAct(state, request, p, rng)
            : BasicSkillAiListPreparation.Evaluate(state, request, p, rng);
        if (!prepared.Accepted) return Reject(new(Preparation: prepared.Failure));
        var ready = prepared.Outcome!;
        var movement = AiMovementScenarios.Evaluate(state, request.Turn, p.Attack, p.Accumulate.Units, ready.Common,
            ready.Team, ready.Units.Single(u => u.UnitId == request.Turn.ActorId).Initialization.HasRangedAbility,
            prepared.RngState, rng, outside: null);
        if (!movement.Accepted) return Reject(new(Movement: movement.Failure));
        return new(new(true, null, new(ready, movement.Outcome!), movement.RngState), movement.Prefix);
    }

    internal static void ValidateArguments(AttackBattleSnapshot state, AccumulateAiPreparationRequest request, BasicSkillAiListProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(profile.Scenarios);
        AccumulateAiPreparation.ValidateArguments(state, request, profile.Preparation);
    }
}
