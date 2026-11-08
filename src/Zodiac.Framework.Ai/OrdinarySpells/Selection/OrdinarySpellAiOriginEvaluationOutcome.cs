using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>One isolated origin's aim scan; baseline admission is distinct from best selection and grants no authority.</summary>
public sealed record OrdinarySpellAiOriginEvaluationOutcome(OrdinarySpellAiOriginOutcome Origins,
    OrdinarySpellAiOriginPass Pass, OrdinarySpellAiOrigin Origin, byte DestinationProximity,
    ImmutableArray<OrdinarySpellAiAimEvaluation> Evaluations, OrdinarySpellAiAimEvaluation? Best,
    bool OriginStoppedUnresolved, bool PassesBaseline);
