namespace Zodiac.Framework.Events;

/// <summary>Ownership of one authored introduction. This does not implement retail EventEnd.</summary>
public sealed class BattleIntroductionState
{
    public BannerReadyState Introduction { get; }
    public bool BattleAvailable { get; }
    private BattleIntroductionState(BannerReadyState introduction, bool available)
        => (Introduction, BattleAvailable) = (introduction, available);

    /// <summary>Attach only to a freshly admitted, preloaded sequence; no battle command runs here.</summary>
    public static BattleIntroductionState Begin(BannerReadyState admitted)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        if (admitted.Event.Tick != 0 || admitted.Event.Exited || admitted.General is null || admitted.Ready is not null ||
            admitted.Event.Tasks.TotalStarted != 1 || admitted.Event.Tasks.LiveTasks != 1)
            throw new ArgumentException("A fresh admitted introduction is required.", nameof(admitted));
        return new(admitted, false);
    }

    public BattleIntroductionResult AdvanceTick(bool? circle, int instructionBudget = 8192, int updateBudget = 64)
    {
        if (BattleAvailable) return new(this, false, null, BattleIntroductionRejection.AlreadyAvailable);
        var result = BannerReadyExecutor.AdvanceTick(Introduction, circle, instructionBudget, updateBudget);
        if (!result.Accepted) return new(this, false, result, BattleIntroductionRejection.IntroductionRejected);
        bool available = result.State.Event.Exited;
        if (available && (result.State.General?.IsTerminal != true || result.State.Ready?.IsTerminal != true ||
                          result.State.Event.Tasks.LiveTasks != 0))
            return new(this, false, null, BattleIntroductionRejection.InvalidCompletion);
        return new(new(result.State, available), available, result, BattleIntroductionRejection.None);
    }
}

public enum BattleIntroductionRejection { None, AlreadyAvailable, IntroductionRejected, InvalidCompletion }
public sealed record BattleIntroductionResult(BattleIntroductionState State, bool BattleBecameAvailable,
    BannerReadyResult? IntroductionResult, BattleIntroductionRejection Rejection)
{
    public bool Accepted => Rejection == BattleIntroductionRejection.None;
}
