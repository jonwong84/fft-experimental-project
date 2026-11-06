namespace Zodiac.Framework;

/// <summary>Selected numeric fields only, without status reconciliation or permission to adopt a complete unit.</summary>
public sealed record Mode1AttributeProjection(ushort Hp, ushort MaxHp, ushort Mp, ushort MaxMp,
    BattleAttributeValues BaseAttributes);
