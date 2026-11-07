using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Useful membership in source slot order, and potential aims in source layer/Y/X order.</summary>
public sealed record OrdinarySpellAiAimPass(OrdinarySpellAiAimPassKind Kind, bool ConsumeEvaluatedAims,
    ImmutableArray<BattleUnitId> UsefulTargets, ImmutableArray<OrdinarySpellAiAim> Aims);
