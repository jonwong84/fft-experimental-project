using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>The bounded clock distinguishes an initial tick from rechecking already-ready peers.</summary>
public enum TurnCyclePhase
{
    AddCt,
    PickUnit,
    UnitTurn,
}
