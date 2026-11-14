using System.Collections.Immutable;
using System.Numerics;

namespace Zodiac.Framework;

public sealed record FlatRosterDirectTrajectoryResult(bool Accepted, FlatRosterDirectTrajectoryRejection Rejection,
    FlatRosterDirectTrajectoryOutcome? Outcome);
