namespace Zodiac.Framework.Ai;

/// <summary>Fresh rank_baseline_seed output; decided is known false and proximity is not part of this rank.</summary>
public sealed record OrdinarySpellAiMovementRank(bool Decided, short Priority, byte HitPercent);
