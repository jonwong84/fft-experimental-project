using System.Collections.Immutable;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Fft.Client.Presentation;

/// <summary>Authored ownership adapter. Battle rules and command history remain in the existing controller.</summary>
public sealed class IntroductionBattleSession
{
    private readonly BannerReadySession resources;
    internal BattleInteraction Interaction { get; }
    internal BattleUiRouter Router { get; }
    public BattleIntroductionState Gate { get; private set; }
    public TwoUnitBattleState Battle => Interaction.State;
    public BattleUnitId? SelectedUnit => Interaction.SelectedUnit;
    public BattleInteractionMode Mode => Interaction.Mode;
    public TilePosition Cursor => Router.Cursor;
    public ImmutableArray<TwoUnitBattleCommand> Journal => Interaction.Journal;
    public ImmutableArray<BannerReadyTrace> IntroductionTrace { get; private set; }
    public int AvailabilityPublications { get; private set; }
    public long? AvailabilityTick { get; private set; }

    private IntroductionBattleSession(BannerReadySession resources, BannerReadyResult admitted)
    {
        this.resources = resources;
        Gate = BattleIntroductionState.Begin(admitted.State); IntroductionTrace = admitted.Trace;
        Interaction = new(SyntheticTwoUnitBattle.Create()); Router = new(Interaction);
    }
    /// <summary>Call after graphics readiness. Prepared inputs have already been validated by BannerReadySession.Load.</summary>
    public static IntroductionBattleSession Start(BannerReadySession resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var admitted = resources.Start(new("sample:intro-battle/scene"), new("sample:intro-battle/consumer"));
        if (!admitted.Accepted) throw new InvalidOperationException(admitted.Diagnostic);
        return new(resources, admitted);
    }
    public BattleIntroductionResult AdvanceIntroduction(bool? circle, int instructionBudget = 8192, int updateBudget = 64)
    {
        var result = Gate.AdvanceTick(circle, instructionBudget, updateBudget);
        if (!result.Accepted) return result;
        Gate = result.State; IntroductionTrace = IntroductionTrace.AddRange(result.IntroductionResult!.Trace);
        if (result.BattleBecameAvailable) { AvailabilityPublications++; AvailabilityTick = Gate.Introduction.Event.Tick; }
        return result; // No battle input is accepted by this transaction.
    }
    // A true return means the ownership gate admitted routing, not that a battle command was legal.
    public bool Key(Keys key) { if (!Gate.BattleAvailable) return false; Router.Key(key); return true; }
    public bool Action(BattleUiAction action) { if (!Gate.BattleAvailable) return false; Router.Action(action); return true; }
    public bool Click(PixelPoint point) { if (!Gate.BattleAvailable) return false; Router.Click(point); return true; }
    public byte[] ComposeIntroduction() => resources.Compose(Gate.Introduction);
}

public static class IntroductionBattleHost
{
    public const string Usage = "--intro-battle --sequence-root <directory> --sequence-id <id> --general-artwork-root <directory> --ready-artwork-root <directory> [--check <fresh-scratch-root> --circle-ticks <CSV>]";
    public static BannerReadyPreviewOptions Parse(string[] args)
    {
        if (args.Length == 0 || args[0] != "--intro-battle") throw new ArgumentException(Usage);
        string[] forwarded = [.. args]; forwarded[0] = "--banner-ready-sequence";
        try { return BannerReadyPreviewOptions.Parse(forwarded); }
        catch (ArgumentException e) { throw new ArgumentException(Usage, e); }
    }
    public static int Run(string[] args)
    {
        try
        {
            var options = Parse(args);
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The introduction/battle window supports Windows.");
            var prepared = BannerReadySession.Load(LooseAssetStore.Load(options.SequenceRoot), options.SequenceId,
                LooseAssetStore.Load(options.GeneralRoot), LooseAssetStore.Load(options.ReadyRoot));
            string? check = GeneralBannerPreview.CheckPath(options.CheckRoot);
            Environment.SetEnvironmentVariable("SDL_WINDOWS_DPI_AWARENESS", "permonitorv2");
            using var window = new PlayableBattleWindow(check, prepared, options); window.Run();
            if (check is not null && !window.CheckCompleted) throw new InvalidOperationException("Introduction/battle check did not complete.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("Introduction/battle failed: " + e.Message); return 1; }
    }
}
