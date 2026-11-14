using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record TurnClockAdvancedEvent(ulong Tick) : TurnCycleEvent;
