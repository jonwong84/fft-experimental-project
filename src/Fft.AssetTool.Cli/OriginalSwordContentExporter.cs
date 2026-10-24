using Fft.Content.ContentPackages;
using Fft.Content.Definitions;

namespace Fft.AssetTool.Cli;

/// <summary>Explicit opt-in mapping of the selected sword to schema 2; raw records stay in the import report.</summary>
public static class OriginalSwordContentExporter
{
    public const string MetadataPackageId = "fft.ps1:package/squire-metadata";
    public const string ProfileId = "fft.ps1:profile/scus-94221";

    public static SwordContentPackage ExportVerified(ReadOnlySpan<byte> executable, ContentPackage metadata) =>
        CreatePackage(OriginalSwordImporter.ImportVerified(executable), metadata);

    /// <summary>
    /// Validates a decoded selection without certifying its input hash. Use ExportVerified for edition verification.
    /// No proc, modifier or capability is silently dropped to fit the supported profile.
    /// </summary>
    public static SwordContentPackage CreatePackage(OriginalSwordImport selection, ContentPackage metadata)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.PackageId != MetadataPackageId || metadata.ProfileId != ProfileId || metadata.Kind != ContentPackageKind.Baseline)
            throw new InvalidDataException("Select the SCUS-94221 Squire baseline metadata dependency explicitly.");
        // Mixed category flags can select additional armor/stat paths even with a zero attribute block.
        if (selection.Item.Type != 3 || selection.Item.TypeFlags != 0x80 || selection.Item.SecondaryDataId != 19 || selection.AttackMenuType != 8)
            throw new InvalidDataException("Unsupported selected sword/Attack mapping.");
        if (selection.Attributes.Length != 25 || selection.Attributes.Any(value => value != 0))
            throw new InvalidDataException("Nonzero or incomplete equipment attribute records are unsupported.");
        var source = selection.Weapon;
        if ((source.Flags & ~0x8e) != 0 || (source.Flags & 0x80) == 0)
            throw new InvalidDataException("Unsupported weapon flag combination.");
        if (selection.Proc.Type is not (0 or 128))
            throw new InvalidDataException("Unsupported proc mode.");
        // Constructors reject unsupported range/formula/element and nonempty/malformed status data.
        var sword = new SwordDefinition(new WeaponId(OriginalSwordImporter.WeaponId), SwordWeaponType.Sword,
            source.Range, source.Power, (SwordDamageFormula)source.Formula, SwordTargetingFlags.Striking,
            source.Evade, (SwordElement)source.Element,
            new EmptySwordAddedEffect((SwordEmptyProcMode)selection.Proc.Type, selection.Proc.Statuses),
            (SwordCapabilityFlags)(source.Flags & 0x0e));
        var package = new SwordContentPackage("fft.ps1:package/broad-sword", ProfileId, MetadataPackageId,
            [sword], SwordContentPackage.CurrentSchemaVersion);
        package.ValidateAgainst(metadata);
        return package;
    }
}
