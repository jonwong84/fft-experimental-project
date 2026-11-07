using Zodiac.Content.Events;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

public static class ReadyEventExporter
{
    public static PreparedReadyEvent Create(ScenarioBlockInspection inspection, AssetId id, AssetId artwork, AssetId owner)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        var verified = ScenarioBlockInspector.Inspect(inspection.Bytes.AsSpan(), inspection.Encoding,
            inspection.Encoding == ScenarioBlockEncoding.Processed ? inspection.TextOffset : null);
        if (!verified.Complete) throw new InvalidDataException(string.Join(" ", verified.Issues.Select(i => i.Message)));
        var records = verified.Instructions.Select(r =>
        {
            if (r.Opcode == 0x78) return new PreparedOrderedBannerInstruction(r.Offset, OrderedScenarioOperation.DisplayConditions, r.Operands[0], r.Operands[1]);
            var ordinary = ScenarioEventExporter.Convert(r, PreparedScenarioEvent.TaskBarrierProfile);
            return new PreparedOrderedBannerInstruction(ordinary.Offset, Enum.Parse<OrderedScenarioOperation>(ordinary.Operation.ToString()), ordinary.Operand1, ordinary.Operand2);
        }).ToArray();
        var structural = records.Select(PreparedOrderedBannerEvent.ForValidation).ToArray();
        for (int i = 0; i < records.Length; i++)
            if (records[i].Operation is OrderedScenarioOperation.JumpForward or OrderedScenarioOperation.JumpForwardIfZero)
                records[i] = records[i] with { TargetOffset = PreparedScenarioEvent.FindTarget(structural, i) };
        return new(id, artwork, owner, new("synthetic", "SCUS-94221", ScenarioBlockInspector.ReferenceRevision,
            verified.Sha256, verified.Encoding == ScenarioBlockEncoding.Raw ? "raw" : "processed", verified.TextOffset), records);
    }
}
