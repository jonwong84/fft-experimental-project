using System.Security.Cryptography;
using System.Text;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Samples;
using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

/// <summary>Hash-verified prepared definitions for the separate authored, fixed-map Accumulate scene.</summary>
public sealed class PreparedAccumulateBattle
{
    public static AssetId MetadataId => new("sample:battle/accumulate/metadata");
    public static AssetId EffectsId => new("sample:battle/accumulate/effects");
    public TwoUnitBattleDefinition Definition { get; }
    public AssetId AbilityId { get; }
    public string MetadataSha256 { get; }
    public string EffectsSha256 { get; }

    private PreparedAccumulateBattle(ContentPackage metadata, ImmediateAbilityContentPackage effects, byte[] metadataBytes, byte[] effectBytes)
    {
        Definition = SyntheticAccumulateBattle.Create(metadata, effects);
        AbilityId = new(effects.Abilities[0].Id.Value);
        MetadataSha256 = Convert.ToHexStringLower(SHA256.HashData(metadataBytes));
        EffectsSha256 = Convert.ToHexStringLower(SHA256.HashData(effectBytes));
    }

    public static PreparedAccumulateBattle Load(LooseAssetStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var utf8 = new UTF8Encoding(false, true);
        byte[] metadata = store.ReadAllBytes(MetadataId, ContentPackage.MaxJsonBytes);
        byte[] effects = store.ReadAllBytes(EffectsId, ContentPackage.MaxJsonBytes);
        return new(ContentPackage.ReadJson(utf8.GetString(metadata)), ImmediateAbilityContentPackage.ReadJson(utf8.GetString(effects)), metadata, effects);
    }
}
