using System.Buffers.Binary;
using Fft.Content.Events;
using Fft.Core;

namespace Fft.AssetTool.Cli;

/// <summary>Explicit bridge from offline inspection to the closed synthetic prepared-event capability set.</summary>
public static class ScenarioEventExporter
{
    public static PreparedScenarioEvent CreateSynthetic(ScenarioBlockInspection inspection, AssetId id, ScenarioEventScene scene)
        => Create(inspection, id, scene, PreparedScenarioEvent.Profile);

    public static PreparedScenarioEvent CreateSyntheticTimed(ScenarioBlockInspection inspection, AssetId id)
        => Create(inspection, id, ScenarioEventScene.Battle, PreparedScenarioEvent.TimedProfile);

    private static PreparedScenarioEvent Create(ScenarioBlockInspection inspection, AssetId id, ScenarioEventScene scene, string profile)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        // Re-inspect retained bytes: a caller-forged record or diagnostics list cannot bypass the offline gate.
        var verified = ScenarioBlockInspector.Inspect(inspection.Bytes.AsSpan(), inspection.Encoding,
            inspection.Encoding == ScenarioBlockEncoding.Processed ? inspection.TextOffset : null);
        if (!verified.Complete) throw new InvalidDataException(string.Join(" ", verified.Issues.Select(issue => $"{issue.Offset}: {issue.Message}")));
        var records = verified.Instructions.Select(record => Convert(record, profile == PreparedScenarioEvent.TimedProfile)).ToArray();
        for (int index = 0; index < records.Length; index++)
            if (records[index].Operation is ScenarioOperation.JumpForward or ScenarioOperation.JumpForwardIfZero)
                records[index] = records[index] with { TargetOffset = PreparedScenarioEvent.FindTarget(records, index) };
        return new(id, scene, new("synthetic", "SCUS-94221", ScenarioBlockInspector.ReferenceRevision, verified.Sha256,
            verified.Encoding == ScenarioBlockEncoding.Raw ? "raw" : "processed", verified.TextOffset), records, profile);
    }

    private static PreparedScenarioInstruction Convert(ScenarioInstructionRecord r, bool allowWait)
    {
        var operation = r.Opcode switch
        {
            0xf2 => ScenarioOperation.Nop, 0xbe => ScenarioOperation.ZeroVariable,
            0xb0 => ScenarioOperation.AddImmediate, 0xb1 => ScenarioOperation.AddVariable, 0xa2 => ScenarioOperation.CompareEqual,
            0xd0 => ScenarioOperation.JumpForwardIfZero, 0xd1 => ScenarioOperation.JumpForward,
            0xd2 => ScenarioOperation.ForwardTarget, 0xd4 => ScenarioOperation.ForwardTargetExit, 0xdb => ScenarioOperation.SearchEnd,
            0xf1 when allowWait => ScenarioOperation.WaitFrames,
            _ => throw new InvalidDataException($"Unsupported executable opcode 0x{r.Opcode:x2} at {r.Offset}; inspection remains available."),
        };
        int? first = r.Operands.Length switch { 1 => r.Operands[0], >= 2 => BinaryPrimitives.ReadUInt16LittleEndian(r.Operands.AsSpan()), _ => null };
        if (operation == ScenarioOperation.WaitFrames) first = BinaryPrimitives.ReadInt16LittleEndian(r.Operands.AsSpan());
        int? second = r.Operands.Length == 4 ? BinaryPrimitives.ReadUInt16LittleEndian(r.Operands.AsSpan()[2..]) : null;
        return new(r.Offset, operation, first, second);
    }
}
