using System.Collections.ObjectModel;

namespace Zodiac.Framework;

public enum BattleTransitionRejection
{
    None,
    UnknownUnit,
    UnitHasAlreadyMoved,
    DestinationOccupied,
    DestinationNotReachable,
}
