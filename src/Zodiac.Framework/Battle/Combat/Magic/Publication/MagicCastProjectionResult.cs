namespace Zodiac.Framework;

public sealed record MagicCastProjectionResult(bool Accepted, MagicProjectionRejection Rejection,
    MagicProjectionSnapshot? Candidate, MagicLivingApplicationRejection? ApplicationRejection = null);
