using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiOriginOutcome(OrdinarySpellAiMovementOutcome Movement,
    OrdinarySpellAiAimOutcome Potential, OrdinarySpellAiOriginScenario Scenario, ImmutableArray<OrdinarySpellAiOriginPass> Passes);
