using System.Collections.Immutable;

namespace Zodiac.Framework;

public enum TurnCycleRejection
{
    None,
    TurnAlreadyActive,
    UnknownUnit,
    InactiveUnit,
    MovementAlreadyTaken,
    ActionAlreadyTaken,
    UnsupportedCommand,
    TickCountExhausted,
    RandomDrawCountExhausted,
}
