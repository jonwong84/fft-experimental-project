namespace Zodiac.Framework;

/// <summary>Current identity mapping and classified turn-block facts, without slot reuse or clock inference.</summary>
public sealed record MagicReadyPlanProfile(bool CurrentIdentityMappingKnown = false, bool CurrentTurnBlockKnown = false,
    bool TurnStatusBlocksWait = false);
