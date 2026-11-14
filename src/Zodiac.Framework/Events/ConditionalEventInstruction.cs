using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Normalized operation with a diagnostic source offset; jump targets are already resolved.</summary>
public sealed record ConditionalEventInstruction(int Offset, ConditionalEventOperation Operation,
    int? Operand1 = null, int? Operand2 = null, int? TargetOffset = null);
