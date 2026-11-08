using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>One isolated origin's aim scan; baseline admission is distinct from best selection and grants no authority.</summary>
public sealed record DefaultAbilityAiOriginEvaluationOutcome(DefaultAbilityAiOriginOutcome Origins,
    DefaultAbilityAiOriginPass Pass, DefaultAbilityAiOrigin Origin, byte DestinationProximity,
    ImmutableArray<DefaultAbilityAiAimEvaluation> Evaluations, DefaultAbilityAiAimEvaluation? Best,
    bool OriginStoppedUnresolved, bool PassesBaseline);
