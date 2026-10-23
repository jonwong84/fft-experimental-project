using System.Collections.Immutable;
using System.Text.Json;
using Fft.AssetTool;

namespace Fft.AssetTool.Cli;

/// <summary>Offline, lossless selected-record import; never substitutes a supported synthetic sword.</summary>
public static class OriginalSwordImporter
{
    public const string ConverterVersion = "fft.selected-sword/2";
    public const string WeaponId = "fft.ps1:item/broad-sword";

    public static OriginalSwordImport ImportVerified(ReadOnlySpan<byte> executable)
    {
        string hash = OriginalExecutableReader.Verify(executable);
        return DecodeSelection(executable) with { VerifiedInputSha256 = hash };
    }

    /// <summary>Unverified layout decoding for synthetic tests; edition attribution requires ImportVerified.</summary>
    public static OriginalSwordImport DecodeSelection(ReadOnlySpan<byte> executable)
    {
        var reader = new OriginalExecutableReader(executable);
        var item = OriginalSwordRecords.DecodeItem(reader.Read("g_item_primary_data", 0x80062eb8, 12, 19, WeaponId));
        if ((item.TypeFlags & 0x80) == 0 || item.Type != 3)
            throw new InvalidDataException("Selected item 19 must be a weapon of type Sword (3).");
        // Battle targeting/formula paths index by item ID; some equipment helpers use secondary_data_id.
        // Verify their equality for this selected record instead of guessing which row to substitute.
        if (item.SecondaryDataId != 19)
            throw new InvalidDataException("Selected sword secondary index differs from battle's direct item-ID index 19; unsupported mapping.");
        var weapon = OriginalSwordRecords.DecodeWeapon(reader.Read("g_item_weapon_data", 0x80063ab8, 8, 19, WeaponId));
        if (weapon.ProcId >= 128) throw new InvalidDataException("Weapon proc index exceeds the pinned 128-record status table.");
        var proc = OriginalSwordRecords.DecodeProc(reader.Read("g_status_infliction_data", 0x80063fc4, 6, weapon.ProcId, WeaponId));
        if (item.Attributes >= 80) throw new InvalidDataException("Item attribute index exceeds the pinned 80-record table.");
        var attributes = reader.Read("g_item_attributes", 0x800642c4, 25, item.Attributes, WeaponId).ToImmutableArray();
        var ability = OriginalMetadataRecords.DecodeAbility(reader.Read("g_ability_data", 0x8005ebf0, 8, 0, "fft.ps1:ability/attack-selector"));
        var range = reader.Read("g_ability_range_data", 0x8005fbf0, 14, 0, "fft.ps1:ability/attack-selector").ToImmutableArray();
        byte menu = reader.Read("g_command_menu_types_by_skillset", 0x80065cb4, 1, 1, "fft.ps1:skillset/attack")[0];
        if (menu != 8) throw new InvalidDataException("Skillset 1 does not select the pinned Attack menu type 8.");
        var diagnostics = new List<string>();
        Compare("range", weapon.Range, 1); Compare("weaponFlagsRaw", weapon.Flags, 0x80);
        Compare("formula", weapon.Formula, 1); Compare("power", weapon.Power, 5); Compare("evade", weapon.Evade, 0);
        Compare("element", weapon.Element, 0); Compare("procTypeRaw", proc.Type, 0x80);
        if (proc.Statuses.Any(value => value != 0)) diagnostics.Add("Proc has nonempty status bytes; the synthetic profile has five zero bytes.");
        diagnostics.Add("All source bytes preserved, including item unused/padding, weapon unused03, attributes, ability AI and ability-0 secondary data.");
        diagnostics.Add("Attack uses skillset1/menu8 and resolves ability0, but selects the weapon formula; ability-0 range formula is not the Attack formula.");
        diagnostics.Add("This report preserves source records only. Optional schema2 sword export requires explicit metadata selection and supported fields; it does not execute combat.");
        return new(executable.Length, null, item, weapon, proc, attributes, ability, range, menu,
            reader.Records.ToImmutableArray(), diagnostics.ToImmutableArray());

        void Compare(string field, int actual, int synthetic)
        {
            if (actual != synthetic) diagnostics.Add($"{field}: imported {actual}, synthetic ZOD-021 value {synthetic}; preserved without substitution.");
        }
    }

    public static string ToJson(OriginalSwordImport result) => JsonSerializer.Serialize(new
    {
        format = "fft.original-sword-import", schemaVersion = 1, scope = "selected-retail-records",
        targetEdition = result.VerifiedInputSha256 is null ? null : "SCUS-94221",
        confidence = result.VerifiedInputSha256 is null ? "unverified-layout-decoding" : "verified-input-source-traced-decoding",
        input = new { fileName = "SCUS_942.21", byteLength = result.InputByteLength, sha256 = result.VerifiedInputSha256 },
        referenceRevision = OriginalMetadataImporter.ReferenceRevision, converterVersion = ConverterVersion,
        weaponId = WeaponId, runtimeReadiness = "metadata-only",
        itemIndex = 19, weaponIndex = 19, procIndex = result.Weapon.ProcId, attributeIndex = result.Item.Attributes,
        attack = new { skillsetIndex = 1, menuType = result.AttackMenuType, resolvedAbilityIndex = 0,
            effectiveFormula = result.Weapon.Formula, ability = result.AttackAbility, secondaryRaw = result.AttackSecondary },
        item = result.Item, weapon = result.Weapon, proc = result.Proc, attributesRaw = result.Attributes,
        records = result.Records, diagnostics = result.Diagnostics,
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, NewLine = "\n" }) + "\n";
}

public sealed record OriginalSwordImport(int InputByteLength, string? VerifiedInputSha256,
    OriginalItemRecord Item, OriginalWeaponRecord Weapon, OriginalProcRecord Proc, ImmutableArray<byte> Attributes,
    OriginalAbilityRecord AttackAbility, ImmutableArray<byte> AttackSecondary, byte AttackMenuType,
    ImmutableArray<OriginalMetadataSourceRecord> Records, ImmutableArray<string> Diagnostics);
