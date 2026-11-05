using System.Collections.Immutable;
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

    public static OriginalThrowStoneImport ImportVerified(ReadOnlySpan<byte> executable, ContentPackage metadata) =>
        Convert(OriginalTargetedAbilityImporter.ImportVerified(executable, metadata, TargetedAbilityEffect.ThrowStoneFlat));

    public static OriginalThrowStoneImport DecodeSelection(ReadOnlySpan<byte> executable, ContentPackage metadata) =>
        Convert(OriginalTargetedAbilityImporter.DecodeSelection(executable, metadata, TargetedAbilityEffect.ThrowStoneFlat));

    private static OriginalThrowStoneImport Convert(OriginalTargetedAbilityImport value) =>
        new(value.InputByteLength, value.VerifiedInputSha256, value.Package, value.Records);

    public static string ToJson(OriginalThrowStoneImport selection) => OriginalTargetedAbilityImporter.ToJson(
        new(selection.InputByteLength, selection.VerifiedInputSha256, TargetedAbilityEffect.ThrowStoneFlat, selection.Package, selection.Records));
}

public sealed record OriginalThrowStoneImport(int InputByteLength, string? VerifiedInputSha256,
    TargetedAbilityContentPackage Package, ImmutableArray<OriginalMetadataSourceRecord> Records);
