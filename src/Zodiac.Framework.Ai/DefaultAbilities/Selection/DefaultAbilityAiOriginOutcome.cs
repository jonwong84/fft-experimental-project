using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiOriginOutcome(DefaultAbilityAiMovementOutcome Movement,
    DefaultAbilityAiAimOutcome Potential, DefaultAbilityAiOriginScenario Scenario, ImmutableArray<DefaultAbilityAiOriginPass> Passes);
