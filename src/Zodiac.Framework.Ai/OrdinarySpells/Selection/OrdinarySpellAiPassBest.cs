namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiPassBest(OrdinarySpellAiOrigin Origin, byte DestinationProximity,
    OrdinarySpellAiAimEvaluation Evaluation);
