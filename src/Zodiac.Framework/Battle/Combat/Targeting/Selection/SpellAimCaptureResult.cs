namespace Zodiac.Framework;

public sealed record SpellAimCaptureResult(bool Accepted, SpellTargetingRejection Rejection, ConfirmedSpellAim? Outcome);
