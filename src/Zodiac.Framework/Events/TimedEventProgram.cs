using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Separate timed proof profile. Conditional validation/execution remains closed and unchanged.</summary>
public sealed class TimedEventProgram
{
    public const int MaxWaitFrames = short.MaxValue;
    public const int MaxTicksPerCall = 32767;
    public AssetId Id => Body.Id;
    public ImmutableArray<TimedEventInstruction> Instructions { get; }
    internal ConditionalEventProgram Body { get; }

    public TimedEventProgram(AssetId id, IEnumerable<TimedEventInstruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        Instructions = instructions.Take(ConditionalEventProgram.MaxInstructions + 1).ToImmutableArray();
        if (Instructions.Any(instruction => instruction is null))
            throw new ArgumentException("Timed operations must be nonnull.", nameof(instructions));
        // A wait has normal fallthrough control flow. The private body supplies admission and ordinary
        // operation transitions; its placeholder NOP is never exposed as a timed instruction trace.
        Body = new(id, Instructions.Select(instruction => instruction.Operation ??
            new ConditionalEventInstruction(instruction.Offset, ConditionalEventOperation.Nop)));
    }

    public TimedEventState Start(ConditionalEventVariables variables) => new(this, Body.Start(variables), 0, null, 0);
}
