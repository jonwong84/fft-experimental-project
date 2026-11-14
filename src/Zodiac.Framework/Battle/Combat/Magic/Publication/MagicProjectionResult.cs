using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicProjectionResult(bool Accepted, MagicProjectionRejection Rejection, MagicProjectionProposal? Proposal,
    RandomState RngState, MagicLivingApplicationRejection? ApplicationRejection = null, MagicMpBlock? ResourceBlock = null,
    FaithMagicResolutionRejection? CalculationRejection = null);
