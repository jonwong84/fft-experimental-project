using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed partial record RosterDecisionResult<T>
{
    public RosterMoveContinuation? Continuation { get; init; }
}
