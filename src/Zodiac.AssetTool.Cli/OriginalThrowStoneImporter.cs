using System.Collections.Immutable;
using System.Text.Json;
using Zodiac.AssetTool;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;

namespace Zodiac.AssetTool.Cli;

/// <summary>Offline verified Throw Stone conversion. Preserve the historical learning metadata stable ID.</summary>
public static class OriginalThrowStoneImporter
{
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";
    public const string ConverterVersion = "fft.throw-stone-effect/1";
    public const string AbilityId = "fft.ps1:ability/source-0148";

    public static OriginalThrowStoneImport ImportVerified(ReadOnlySpan<byte> executable, ContentPackage metadata)
    {
        string hash = OriginalExecutableReader.Verify(executable);
        return DecodeSelection(executable, metadata) with { VerifiedInputSha256 = hash };
    }

    /// <summary>Synthetic layout inspection only; never certifies an edition/hash.</summary>
    public static OriginalThrowStoneImport DecodeSelection(ReadOnlySpan<byte> executable, ContentPackage metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.PackageId != OriginalSwordContentExporter.MetadataPackageId || metadata.ProfileId != OriginalSwordContentExporter.ProfileId ||
            metadata.Kind != ContentPackageKind.Baseline)
            throw new InvalidDataException("Select the original Squire baseline metadata dependency explicitly.");
        var reader = new OriginalExecutableReader(executable);
        var job = OriginalMetadataRecords.DecodeJob(reader.Read("g_job_data", 0x800610b8, 48, 74, "fft.ps1:job/squire"));
        if (job.Skillset != 5) throw new InvalidDataException("Squire does not reference Basic Skill.");
        var slots = OriginalMetadataRecords.DecodeHumanSkillset(reader.Read("g_ability_human_skillset_abilities", 0x80064a94, 25, 5, "fft.ps1:skillset/basic-skill"));
        if (slots[2] != 148) throw new InvalidDataException("Basic Skill slot 2 does not select Throw Stone.");
        var primary = OriginalMetadataRecords.DecodeAbility(reader.Read("g_ability_data", 0x8005ebf0, 8, 148, AbilityId));
        var secondary = OriginalImmediateAbilityRecords.Decode(reader.Read("g_ability_range_data", 0x8005fbf0, 14, 148, AbilityId));
        OriginalThrowStoneRecords.Validate(secondary);
        byte menu = reader.Read("g_command_menu_types_by_skillset", 0x80065cb4, 1, 5, "fft.ps1:skillset/basic-skill")[0];
        var status = reader.Read("g_status_infliction_data", 0x80063fc4, 6, 0, AbilityId);
        if (menu != 0 || status.Any(value => value != 0)) throw new InvalidDataException("Unsupported menu or status-infliction record.");
        var declaredJob = metadata.Jobs.SingleOrDefault(value => value.Id.Value == "fft.ps1:job/squire");
        var skillset = metadata.Skillsets.SingleOrDefault(value => value.Id.Value == "fft.ps1:skillset/basic-skill");
        var learning = metadata.Abilities.SingleOrDefault(value => value.Id.Value == AbilityId);
        if (declaredJob?.PrimarySkillsetId.Value != skillset?.Id.Value || skillset is null ||
            !skillset.Slots.Any(slot => slot.Slot == 2 && slot.AbilityId?.Value == AbilityId) || learning is null ||
            learning.JpCost != primary.JpCost || learning.LearnOnHitRateRaw != primary.LearnRate ||
            learning.TypeFlagsRaw != primary.TypeFlags || !learning.AiFlagsRaw.SequenceEqual(primary.AiFlags))
            throw new InvalidDataException("Selected metadata differs from the Throw Stone job/slot/learning records.");
        // Compare the whole canonical dependency, including job fields and other slots; never rewrite it.
        if (metadata.ToJson() != OriginalMetadataImporter.DecodeSelection(executable).Package.ToJson())
            throw new InvalidDataException("Metadata is not the original selected Squire dependency.");
        var package = new TargetedAbilityContentPackage("fft.ps1:package/throw-stone-effect", metadata.ProfileId, metadata.PackageId,
            [new(new(AbilityId), TargetedAbilityEffect.ThrowStoneFlat, secondary.Range, secondary.Y, secondary.ChargeTicks, secondary.MpCost)]);
        package.ValidateAgainst(metadata);
        return new(executable.Length, null, package, reader.Records.ToImmutableArray());
    }

    public static string ToJson(OriginalThrowStoneImport selection) => JsonSerializer.Serialize(new
    {
        format = "fft.original-throw-stone-import", schemaVersion = 1,
        targetEdition = selection.VerifiedInputSha256 is null ? null : "SCUS-94221",
        confidence = selection.VerifiedInputSha256 is null ? "unverified-layout-decoding" : "verified-input-source-traced-decoding",
        input = new { fileName = "SCUS_942.21", byteLength = selection.InputByteLength, sha256 = selection.VerifiedInputSha256 },
        referenceRevision = ReferenceRevision, converterVersion = ConverterVersion,
        scope = "immediate-occupied-target-physical-damage-flat-knockback", records = selection.Records,
        limitations = "No learned grant, original encounter/terrain, runtime stats, rewards, AI or animation is inferred.",
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, NewLine = "\n" }) + "\n";
}

public sealed record OriginalThrowStoneImport(int InputByteLength, string? VerifiedInputSha256,
    TargetedAbilityContentPackage Package, ImmutableArray<OriginalMetadataSourceRecord> Records);
