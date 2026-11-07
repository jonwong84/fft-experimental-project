namespace Zodiac.Framework;

public sealed record SpellTargetChoiceResult(bool Accepted, SpellTargetingRejection Rejection, SpellTargetChoice? Outcome);
