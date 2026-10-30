using System.Collections.Immutable;
using Zodiac.AssetTool;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;

namespace Zodiac.AssetTool.Cli;

/// <summary>Offline conversion of the selected job and its referenced learning metadata.</summary>
public static class OriginalMetadataImporter
{
    public const string ReferenceRevision = "611b504d25d4a9b03fab8023d7dbea75fe381d5d";
    public const string ExecutableSha256 = "287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562";
    public const int ExecutableLength = 356352;
    public const string ConverterVersion = "fft.selected-metadata/1";

    /// <summary>Requires the exact pinned executable before attributing output to SCUS-94221.</summary>
    public static OriginalMetadataImport ImportVerified(ReadOnlySpan<byte> executable)
    {
        OriginalExecutableReader.Verify(executable);
        return DecodeSelection(executable);
    }

    /// <summary>
    /// Parses the pinned address layout for synthetic tests or inspection. This alone does not verify edition.
    /// Use ImportVerified for an edition-attributed import.
    /// </summary>
    public static OriginalMetadataImport DecodeSelection(ReadOnlySpan<byte> executable)
    {
        var reader = new OriginalExecutableReader(executable);
        var records = reader.Records;
        var diagnostics = new List<string>();
        byte[] Read(string table, uint tableAddress, int stride, int index, string? definitionId) =>
            reader.Read(table, tableAddress, stride, index, definitionId);
        const string jobId = "fft.ps1:job/squire";
        OriginalJobRecord job = OriginalMetadataRecords.DecodeJob(Read("g_job_data", 0x800610b8, 48, 74, jobId));
        if (job.Skillset >= 176)
            throw new InvalidDataException($"Squire skillset {job.Skillset} is outside the selected human-skillset domain.");
        string skillsetId = job.Skillset == 5 ? "fft.ps1:skillset/basic-skill" : $"fft.ps1:skillset/source-{job.Skillset:d4}";
        byte[] skillsetBytes = Read("g_ability_human_skillset_abilities", 0x80064a94, 25, job.Skillset, skillsetId);
        ImmutableArray<ushort> slots = OriginalMetadataRecords.DecodeHumanSkillset(skillsetBytes);
        if (job.Innates.Any(id => id >= 512))
            throw new InvalidDataException("Squire innate ability reference exceeds the primary ability table (0..511).");

        var abilities = new List<AbilityDefinition>();
        foreach (ushort index in slots.Concat(job.Innates).Where(id => id != 0).Distinct().Order())
        {
            string id = AbilityName(index);
            OriginalAbilityRecord ability = OriginalMetadataRecords.DecodeAbility(
                Read("g_ability_data", 0x8005ebf0, 8, index, id));
            abilities.Add(new(new(id), ability.JpCost, ability.LearnRate, ability.TypeFlags, ability.AiFlags));
            // Secondary effects are preserved as opaque records, never interpreted as battle-ready abilities.
            (string table, uint address, int stride, int relativeIndex) = index switch
            {
                < 368 => ("g_ability_range_data", 0x8005fbf0, 14, index),
                < 382 => ("g_item_ability_item_ids", 0x80061010, 1, index - 368),
                < 394 => ("g_ability_throw_item_types", 0x80061020, 1, index - 382),
                < 406 => ("g_ability_jump_data", 0x8006102c, 2, index - 394),
                < 414 => ("g_ability_charge_data", 0x80061044, 2, index - 406),
                _ => ("g_ability_math_rsm_data", 0x80061054, 1, index - 414),
            };
            _ = Read(table, address, stride, relativeIndex, id);
            diagnostics.Add($"{id}: AI flags and secondary record preserved without effect interpretation.");
            if ((ability.TypeFlags & 0x10) != 0)
                diagnostics.Add($"{id}: unknown type flag 0x10 preserved.");
            int serializedClass = ability.TypeFlags & 0x0f;
            if (serializedClass is < 1 or > 9)
                diagnostics.Add($"{id}: unclassified serialized class {serializedClass} preserved.");
        }
        diagnostics.Add($"{jobId}: equipment, growths, class evade, status sets, affinity and presentation fields remain opaque; all 48 source bytes preserved.");
        if ((skillsetBytes[2] & 3) != 0)
            diagnostics.Add($"{skillsetId}: unused low skillset flag bits 0x{skillsetBytes[2] & 3:x2} preserved.");
        var definition = new JobDefinition(new(jobId), new(skillsetId), job.Move, job.JumpRaw & 0x7f,
            (job.JumpRaw & 0x80) != 0, job.Innates.Where(id => id != 0).Select(id => new AbilityId(AbilityName(id))));
        var skillset = new SkillsetDefinition(new(skillsetId), slots.Select((id, slot) =>
            new SkillsetSlot(slot, id == 0 ? null : new AbilityId(AbilityName(id)))));
        var package = new ContentPackage("fft.ps1:package/squire-metadata", "fft.ps1:profile/scus-94221",
            ContentPackageKind.Baseline, null, [definition], [skillset], abilities);
        return new(package, records.ToImmutableArray(), diagnostics.ToImmutableArray());
    }

    private static string AbilityName(ushort index) => index == 146
        ? "fft.ps1:ability/accumulate" : $"fft.ps1:ability/source-{index:d4}";
}

/// <summary>Raw import metadata, separate from runtime definitions. Generated instances belong under scratch.</summary>
public sealed record OriginalMetadataSourceRecord(string Table, int Index, string? DefinitionId,
    uint VirtualAddress, int FileOffset, int ByteLength, string RawHex);

public sealed record OriginalMetadataImport(ContentPackage Package,
    ImmutableArray<OriginalMetadataSourceRecord> Records, ImmutableArray<string> Diagnostics);
