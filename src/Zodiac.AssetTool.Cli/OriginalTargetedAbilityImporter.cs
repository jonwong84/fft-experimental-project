using System.Collections.Immutable;
using System.Text.Json;
using Zodiac.AssetTool;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;

namespace Zodiac.AssetTool.Cli;

/// <summary>Offline closed Stone/Dash import sharing canonical metadata and source provenance validation.</summary>
public static class OriginalTargetedAbilityImporter
{
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";
    private static (int Number, int Slot, string Name, string Slug) Selection(TargetedAbilityEffect effect) => effect switch
    {
        TargetedAbilityEffect.ThrowStoneFlat => (148, 2, "Throw Stone", "throw-stone"),
        TargetedAbilityEffect.DashFlat => (147, 1, "Dash", "dash"),
        _ => throw new NotSupportedException("Select a traced original targeted effect."),
    };

    public static OriginalTargetedAbilityImport ImportVerified(ReadOnlySpan<byte> executable, ContentPackage metadata, TargetedAbilityEffect effect)
    {
        string hash = OriginalExecutableReader.Verify(executable);
        return DecodeSelection(executable, metadata, effect) with { VerifiedInputSha256 = hash };
    }

    /// <summary>Synthetic layout decoding never certifies an edition/hash or infers a learned grant.</summary>
    public static OriginalTargetedAbilityImport DecodeSelection(ReadOnlySpan<byte> executable, ContentPackage metadata, TargetedAbilityEffect effect)
    {
        ArgumentNullException.ThrowIfNull(metadata); var selection = Selection(effect);
        string id = $"fft.ps1:ability/source-{selection.Number:D4}";
        if (metadata.PackageId != OriginalSwordContentExporter.MetadataPackageId || metadata.ProfileId != OriginalSwordContentExporter.ProfileId ||
            metadata.Kind != ContentPackageKind.Baseline)
            throw new InvalidDataException("Select the original Squire baseline metadata dependency explicitly.");
        var reader = new OriginalExecutableReader(executable);
        var job = OriginalMetadataRecords.DecodeJob(reader.Read("g_job_data", 0x800610b8, 48, 74, "fft.ps1:job/squire"));
        if (job.Skillset != 5) throw new InvalidDataException("Squire does not reference Basic Skill.");
        var slots = OriginalMetadataRecords.DecodeHumanSkillset(reader.Read("g_ability_human_skillset_abilities", 0x80064a94, 25, 5, "fft.ps1:skillset/basic-skill"));
        if (slots[selection.Slot] != selection.Number) throw new InvalidDataException($"Basic Skill does not select {selection.Name} in the traced slot.");
        var primary = OriginalMetadataRecords.DecodeAbility(reader.Read("g_ability_data", 0x8005ebf0, 8, selection.Number, id));
        var secondary = OriginalImmediateAbilityRecords.Decode(reader.Read("g_ability_range_data", 0x8005fbf0, 14, selection.Number, id));
        if (effect == TargetedAbilityEffect.DashFlat) OriginalImmediateAbilityRecords.ValidateDash(secondary);
        else OriginalThrowStoneRecords.Validate(secondary);
        byte menu = reader.Read("g_command_menu_types_by_skillset", 0x80065cb4, 1, 5, "fft.ps1:skillset/basic-skill")[0];
        var status = reader.Read("g_status_infliction_data", 0x80063fc4, 6, 0, id);
        if (menu != 0 || status.Any(value => value != 0)) throw new InvalidDataException("Unsupported menu or status-infliction record.");
        var skillset = metadata.Skillsets.SingleOrDefault(value => value.Id.Value == "fft.ps1:skillset/basic-skill");
        var learning = metadata.Abilities.SingleOrDefault(value => value.Id.Value == id);
        if (metadata.Jobs.SingleOrDefault(value => value.Id.Value == "fft.ps1:job/squire")?.PrimarySkillsetId.Value != skillset?.Id.Value || skillset is null ||
            !skillset.Slots.Any(slot => slot.Slot == selection.Slot && slot.AbilityId?.Value == id) || learning is null ||
            learning.JpCost != primary.JpCost || learning.LearnOnHitRateRaw != primary.LearnRate ||
            OriginalAbilityMetadataAdapter.EncodeTypeFlags(learning) != primary.TypeFlags || !learning.AiFlagsRaw.SequenceEqual(primary.AiFlags))
            throw new InvalidDataException($"Selected metadata differs from the {selection.Name} job/slot/learning records.");
        if (metadata.ToJson() != OriginalMetadataImporter.DecodeSelection(executable).Package.ToJson())
            throw new InvalidDataException("Metadata is not the original selected Squire dependency.");
        var package = new TargetedAbilityContentPackage($"fft.ps1:package/{selection.Slug}-effect", metadata.ProfileId, metadata.PackageId,
            [new(new(id), effect, secondary.Range, secondary.Y, secondary.ChargeTicks, secondary.MpCost)]);
        package.ValidateAgainst(metadata);
        return new(executable.Length, null, effect, package, reader.Records.ToImmutableArray());
    }

    public static string ToJson(OriginalTargetedAbilityImport selection)
    {
        var kind = Selection(selection.Effect);
        return JsonSerializer.Serialize(new
        {
            format = $"fft.original-{kind.Slug}-import", schemaVersion = 1,
            targetEdition = selection.VerifiedInputSha256 is null ? null : "SCUS-94221",
            confidence = selection.VerifiedInputSha256 is null ? "unverified-layout-decoding" : "verified-input-source-traced-decoding",
            input = new { fileName = "SCUS_942.21", byteLength = selection.InputByteLength, sha256 = selection.VerifiedInputSha256 },
            referenceRevision = ReferenceRevision, converterVersion = $"fft.{kind.Slug}-effect/1",
            scope = "immediate-occupied-target-physical-damage-flat-knockback", records = selection.Records,
            limitations = "No learned grant, original encounter/terrain, runtime stats, rewards, AI or animation is inferred.",
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, NewLine = "\n" }) + "\n";
    }
}

public sealed record OriginalTargetedAbilityImport(int InputByteLength, string? VerifiedInputSha256, TargetedAbilityEffect Effect,
    TargetedAbilityContentPackage Package, ImmutableArray<OriginalMetadataSourceRecord> Records);
