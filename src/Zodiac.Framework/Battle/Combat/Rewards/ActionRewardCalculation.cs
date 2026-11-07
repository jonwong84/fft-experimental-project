namespace Zodiac.Framework;

/// <summary>Pure reward arithmetic, separate from admission, stat growth and career storage.</summary>
public static class ActionRewardCalculation
{
    public static ActionRewardAmounts Calculate(byte oldLevel, byte oldJobLevel, byte earnedExperience,
        bool isMonster, bool gainedJpUp)
    {
        if (isMonster) return new(earnedExperience, 0, 0, 0);
        if (oldJobLevel > 15) throw new ArgumentOutOfRangeException(nameof(oldJobLevel));
        int basis = earnedExperience == 0 ? 0 : 8 + 2 * oldJobLevel + (oldLevel >> 2);
        return new(earnedExperience, basis, gainedJpUp ? basis * 3 / 2 : basis, basis / 4);
    }

    public static ActionExperienceBalance ProjectExperience(byte storedExperience, byte earnedExperience, byte level)
    {
        int experience = Math.Min(255, storedExperience + earnedExperience);
        if (experience < 100) return new((byte)experience, level, false);
        return level >= 99 ? new(99, level, false) : new(0, (byte)(level + 1), true);
    }
}
