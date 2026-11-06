namespace Zodiac.Framework;

/// <summary>Numeric refresh fields, without status/affinity/equipped flags, slot ownership or live adoption.</summary>
public sealed record EquipmentAttributeProjection(Mode1AttributeProjection Attributes, BattleAttributeValues Bonuses,
    BattleAttributeValues ActualAttributes, byte Move, byte Jump);
