using System.Collections.Immutable;

namespace Zodiac.Framework;

public static class EquipmentAttributeCalculation
{
    /// <summary>Move/Jump are already job/R/S/M-prepared. Contributions arrive in explicit source equipment order.</summary>
    public static EquipmentAttributeProjection CalculateMode1(BattleRawStats raw, BattleStatMultipliers multipliers,
        ushort hp, ushort mp, BattleAttributeValues baseAttributes, byte move, byte jump,
        ImmutableArray<EquipmentAttributeContribution> contributions, bool monster = false, bool egg = false)
    {
        var maxima = Mode1BattleAttributes.DeriveMaxima(raw, multipliers, egg);
        int maxHp = maxima.Hp, maxMp = maxima.Mp, pa = 0, ma = 0, speed = 0;
        if (!monster)
        {
            if (contributions.IsDefault) throw new ArgumentException("Explicit ordered equipment contributions required.", nameof(contributions));
            foreach (var item in contributions)
            {
                if (!item.ItemPresent) continue;
                pa = Math.Min(255, pa + item.Bonuses.PhysicalAttack);
                ma = Math.Min(255, ma + item.Bonuses.MagicAttack);
                speed = Math.Min(255, speed + item.Bonuses.Speed);
                move = (byte)Math.Min(253, move + item.MoveBonus);
                jump = (byte)Math.Min(7, jump + item.JumpBonus);
                if (item.AddsArmorVitals)
                {
                    maxHp = Math.Min(999, maxHp + item.HpBonus);
                    maxMp = Math.Min(999, maxMp + item.MpBonus);
                }
            }
        }
        // Preserve incoming current fields until every armor store has run.
        var attributes = new Mode1AttributeProjection((ushort)Math.Min(hp, maxHp), (ushort)maxHp,
            (ushort)Math.Min(mp, maxMp), (ushort)maxMp, baseAttributes);
        var bonuses = new BattleAttributeValues((byte)pa, (byte)ma, (byte)speed);
        var actual = new BattleAttributeValues((byte)Math.Min(99, baseAttributes.PhysicalAttack + pa),
            (byte)Math.Min(99, baseAttributes.MagicAttack + ma), (byte)Math.Min(50, baseAttributes.Speed + speed));
        return new(attributes, bonuses, actual, move, jump);
    }
}
