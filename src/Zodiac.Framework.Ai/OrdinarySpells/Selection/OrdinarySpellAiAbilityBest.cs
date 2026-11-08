namespace Zodiac.Framework.Ai;

/// <summary>The retained best and the caller pass that produced it; exact ties keep their earlier owner.</summary>
public sealed record OrdinarySpellAiAbilityBest(OrdinarySpellAiAimPassKind Pass, OrdinarySpellAiPassBest Candidate);
