namespace Zodiac.Framework;

/// <summary>Selected item numeric records only. Presence and armor classification are independent of numeric bonuses.</summary>
public readonly record struct EquipmentAttributeContribution(BattleAttributeValues Bonuses, byte MoveBonus = 0,
    byte JumpBonus = 0, bool AddsArmorVitals = false, byte HpBonus = 0, byte MpBonus = 0, bool ItemPresent = true);
