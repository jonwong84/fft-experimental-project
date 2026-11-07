namespace Zodiac.Framework;

public sealed record SpellTargetSelectionResult(bool Accepted, SpellTargetingRejection Rejection, SpellTargetSelection? Outcome);
