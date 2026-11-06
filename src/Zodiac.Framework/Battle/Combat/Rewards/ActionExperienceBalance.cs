namespace Zodiac.Framework;

/// <summary>Numeric level/EXP projection. RequiresStatGrowth must be resolved before adopting a complete unit.</summary>
public sealed record ActionExperienceBalance(byte Experience, byte Level, bool RequiresStatGrowth);
