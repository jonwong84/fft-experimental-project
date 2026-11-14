using System.Security.Cryptography;
using System.Text;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Samples;
using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

/// <summary>Hash-verified prepared definitions for the separate authored, fixed-map Basic Skill scene.</summary>
public sealed class PreparedBasicSkillBattle
{
    public static AssetId MetadataId => new("sample:battle/basic-skill/metadata");
    public static AssetId EffectsId => new("sample:battle/basic-skill/effects");
    public static AssetId TargetedEffectsId => new("sample:battle/basic-skill/targeted");
    public AssetId TargetedAbilityId { get; }
    public string TargetedEffectsSha256 { get; }
    public TwoUnitBattleDefinition Definition { get; }
    public AssetId SelfAbilityId { get; }
    public string MetadataSha256 { get; }
    public string EffectsSha256 { get; }

    private PreparedBasicSkillBattle(ContentPackage metadata, ImmediateAbilityContentPackage effects, TargetedAbilityContentPackage targeted, byte[] metadataBytes, byte[] effectBytes, byte[] targetedBytes)
    {
        Definition = SyntheticBasicSkillBattle.Create(metadata, effects, targeted);
        SelfAbilityId = new(effects.Abilities[0].Id.Value);
        TargetedAbilityId = new(targeted.Abilities[0].Id.Value);
        TargetedEffectsSha256 = Convert.ToHexStringLower(SHA256.HashData(targetedBytes));
        MetadataSha256 = Convert.ToHexStringLower(SHA256.HashData(metadataBytes));
        EffectsSha256 = Convert.ToHexStringLower(SHA256.HashData(effectBytes));
    }

    public static PreparedBasicSkillBattle Load(LooseAssetStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var utf8 = new UTF8Encoding(false, true);
        byte[] metadata = store.ReadAllBytes(MetadataId, ContentPackage.MaxJsonBytes);
        byte[] effects = store.ReadAllBytes(EffectsId, ContentPackage.MaxJsonBytes);
        byte[] targeted = store.ReadAllBytes(TargetedEffectsId, ContentPackage.MaxJsonBytes);
        return new(ContentPackage.ReadJson(utf8.GetString(metadata)), ImmediateAbilityContentPackage.ReadJson(utf8.GetString(effects)), TargetedAbilityContentPackage.ReadJson(utf8.GetString(targeted)), metadata, effects, targeted);
    }
}
