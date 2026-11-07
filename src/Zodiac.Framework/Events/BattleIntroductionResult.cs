namespace Zodiac.Framework.Events;

public sealed record BattleIntroductionResult(BattleIntroductionState State, bool BattleBecameAvailable,
    BannerReadyResult? IntroductionResult, BattleIntroductionRejection Rejection)
{
    public bool Accepted => Rejection == BattleIntroductionRejection.None;
}
