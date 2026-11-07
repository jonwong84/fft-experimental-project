namespace Zodiac.Framework;

public sealed record MagicLivingApplicationResult(bool Accepted, MagicLivingApplicationRejection Rejection, MagicLivingApplicationOutcome? Outcome);
