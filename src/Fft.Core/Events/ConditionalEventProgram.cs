using System.Collections.Immutable;

namespace Fft.Core.Events;

public enum ConditionalEventOperation
{
    Nop, ZeroVariable, AddImmediate, AddVariable, CompareEqual,
    JumpForwardIfZero, JumpForward, Target, Exit,
}

/// <summary>Normalized operation with a diagnostic source offset; jump targets are already resolved.</summary>
public sealed record ConditionalEventInstruction(int Offset, ConditionalEventOperation Operation,
    int? Operand1 = null, int? Operand2 = null, int? TargetOffset = null);

/// <summary>FFT's closed conditional-event proof. No bytecode, I/O, RNG, waits or scene lifecycle.</summary>
public sealed class ConditionalEventProgram
{
    // Proof resource/domain limits, not original table sizes or original variable arithmetic widths.
    public const int MaxInstructions = 8192;
    public const int MaxVariableValue = ushort.MaxValue;
    public AssetId Id { get; }
    public ImmutableArray<ConditionalEventInstruction> Instructions { get; }
    private readonly ImmutableDictionary<int, int> indices;

    public ConditionalEventProgram(AssetId id, IEnumerable<ConditionalEventInstruction> instructions)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("Event ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(instructions);
        var records = instructions.Take(MaxInstructions + 1).ToImmutableArray();
        if (records.Length is < 1 or > MaxInstructions)
            throw new ArgumentException($"Event requires 1..{MaxInstructions} operations.", nameof(instructions));
        int previous = -1;
        foreach (var record in records)
        {
            if (record is null || !Enum.IsDefined(record.Operation) || record.Offset <= previous)
                throw new ArgumentException("Operations must be known, nonnull and have increasing nonnegative source offsets.", nameof(instructions));
            ValidateOperands(record);
            previous = record.Offset;
        }
        var lookup = records.Select((record, index) => (record.Offset, index)).ToImmutableDictionary(pair => pair.Offset, pair => pair.index);
        foreach (var record in records.Where(record => record.TargetOffset is not null))
            if (record.TargetOffset <= record.Offset || !lookup.ContainsKey(record.TargetOffset!.Value))
                throw new ArgumentException($"Jump at {record.Offset} must land on a later operation.", nameof(instructions));

        // Check both conditional edges. Reaching an omitted SearchEnd or falling off the program is unsupported.
        var pending = new Queue<int>();
        var seen = new HashSet<int>();
        pending.Enqueue(0);
        while (pending.TryDequeue(out int index))
        {
            if (!seen.Add(index)) continue;
            if (index >= records.Length) throw new ArgumentException("A reachable path has no explicit thread exit.", nameof(instructions));
            var record = records[index];
            if (record.Operation == ConditionalEventOperation.Exit) continue;
            if (record.TargetOffset is { } target) pending.Enqueue(lookup[target]);
            if (record.Operation != ConditionalEventOperation.JumpForward) pending.Enqueue(index + 1);
        }
        (Id, Instructions, indices) = (id, records, lookup);
    }

    public ConditionalEventState Start(ConditionalEventVariables variables)
    {
        if (!variables.InDomain)
            throw new ArgumentOutOfRangeException(nameof(variables), $"Every proof variable must be in 0..{MaxVariableValue}.");
        return new(this, Instructions[0].Offset, variables, exited: false);
    }

    internal int IndexOf(int offset) => indices[offset];

    private static void ValidateOperands(ConditionalEventInstruction record)
    {
        bool variable = record.Operand1 is >= 0 and <= 3;
        bool valid = record.Operation switch
        {
            ConditionalEventOperation.ZeroVariable => variable && record.Operand2 is null,
            ConditionalEventOperation.AddImmediate => variable && record.Operand2 is >= 0 and <= MaxVariableValue,
            ConditionalEventOperation.AddVariable => variable && record.Operand2 is >= 0 and <= 3,
            _ => record.Operand1 is null && record.Operand2 is null,
        };
        bool jump = record.Operation is ConditionalEventOperation.JumpForward or ConditionalEventOperation.JumpForwardIfZero;
        if (!valid || jump != (record.TargetOffset is not null))
            throw new ArgumentException($"Unsupported operands or target at {record.Offset}.", nameof(record));
    }
}
