using System.Collections.Immutable;
using System.Text.Json;
using Zodiac.AssetTool;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;

namespace Zodiac.AssetTool.Cli;

/// <summary>Offline, opt-in import of the closed SCUS-94221 Accumulate effect; original rows stay in scratch provenance.</summary>
public static class OriginalAccumulateImporter
{
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";
    public const string ConverterVersion = "fft.accumulate-effect/1";
    public const string AbilityId = "fft.ps1:ability/accumulate";

    public static OriginalAccumulateImport ImportVerified(ReadOnlySpan<byte> executable, ContentPackage metadata)
    {
        string hash = OriginalExecutableReader.Verify(executable);
        return DecodeSelection(executable, metadata) with { VerifiedInputSha256 = hash };
    }

    /// <summary>Synthetic layout inspection only. This entry point never certifies an edition/hash.</summary>
    public static OriginalAccumulateImport DecodeSelection(ReadOnlySpan<byte> executable, ContentPackage metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.PackageId != OriginalSwordContentExporter.MetadataPackageId || metadata.ProfileId != OriginalSwordContentExporter.ProfileId ||
            metadata.Kind != ContentPackageKind.Baseline)
            throw new InvalidDataException("Select the original Squire baseline metadata dependency explicitly.");
        var reader = new OriginalExecutableReader(executable);
        var job = OriginalMetadataRecords.DecodeJob(reader.Read("g_job_data", 0x800610b8, 48, 74, "fft.ps1:job/squire"));
        if (job.Skillset != 5) throw new InvalidDataException("Squire does not reference the selected Basic Skill row.");
        var slots = OriginalMetadataRecords.DecodeHumanSkillset(reader.Read("g_ability_human_skillset_abilities", 0x80064a94, 25, 5, "fft.ps1:skillset/basic-skill"));
        if (slots[0] != 146) throw new InvalidDataException("Basic Skill's first action is not the selected Accumulate record.");
        var primary = OriginalMetadataRecords.DecodeAbility(reader.Read("g_ability_data", 0x8005ebf0, 8, 146, AbilityId));
        var secondary = OriginalImmediateAbilityRecords.Decode(reader.Read("g_ability_range_data", 0x8005fbf0, 14, 146, AbilityId));
        OriginalImmediateAbilityRecords.ValidateAccumulate(secondary);
        byte menu = reader.Read("g_command_menu_types_by_skillset", 0x80065cb4, 1, 5, "fft.ps1:skillset/basic-skill")[0];
        var status = reader.Read("g_status_infliction_data", 0x80063fc4, 6, 0, AbilityId);
        if (menu != 0 || status.Any(value => value != 0)) throw new InvalidDataException("Unsupported Accumulate menu or status-infliction record.");
        var declaredJob = metadata.Jobs.SingleOrDefault(value => value.Id.Value == "fft.ps1:job/squire");
        var skillset = metadata.Skillsets.SingleOrDefault(value => value.Id.Value == "fft.ps1:skillset/basic-skill");
        var learning = metadata.Abilities.SingleOrDefault(value => value.Id.Value == AbilityId);
        if (declaredJob?.PrimarySkillsetId.Value != skillset?.Id.Value || skillset is null ||
            !skillset.Slots.Any(slot => slot.Slot == 0 && slot.AbilityId?.Value == AbilityId) || learning is null ||
            learning.JpCost != primary.JpCost || learning.LearnOnHitRateRaw != primary.LearnRate || learning.TypeFlagsRaw != primary.TypeFlags ||
            !learning.AiFlagsRaw.SequenceEqual(primary.AiFlags))
            throw new InvalidDataException("Selected metadata does not match the Accumulate job/slot/learning records.");
        var package = new ImmediateAbilityContentPackage("fft.ps1:package/accumulate-effect", metadata.ProfileId, metadata.PackageId,
            [new(new(AbilityId), ImmediateAbilityEffect.SelfPhysicalAttackIncrease, secondary.Y, secondary.ChargeTicks, secondary.MpCost)]);
        package.ValidateAgainst(metadata);
        return new(executable.Length, null, package, reader.Records.ToImmutableArray());
    }

    public static string ToJson(OriginalAccumulateImport selection) => JsonSerializer.Serialize(new
    {
        format = "fft.original-accumulate-import", schemaVersion = 1,
        targetEdition = selection.VerifiedInputSha256 is null ? null : "SCUS-94221",
        confidence = selection.VerifiedInputSha256 is null ? "unverified-layout-decoding" : "verified-input-source-traced-decoding",
        input = new { fileName = "SCUS_942.21", byteLength = selection.InputByteLength, sha256 = selection.VerifiedInputSha256 },
        referenceRevision = ReferenceRevision, converterVersion = ConverterVersion,
        scope = "immediate-self-pa-plus-one", records = selection.Records,
        limitations = "No learned grant, runtime stats, rewards, AI or original animation is inferred.",
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, NewLine = "\n" }) + "\n";
}

public sealed record OriginalAccumulateImport(int InputByteLength, string? VerifiedInputSha256,
    ImmediateAbilityContentPackage Package, ImmutableArray<OriginalMetadataSourceRecord> Records);
