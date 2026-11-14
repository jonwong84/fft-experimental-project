using System.Collections.Immutable;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Fft.Client.Presentation;

/// <summary>Prepared resources only. Loading performs no scene admission or graphics work.</summary>
public sealed class DarkScreenIntroductionSession
{
    public PreparedDarkScreenIntroduction Definition { get; }
    public PreparedDarkScreen Dark { get; }
    public LoadedGeneralBanner General { get; }
    public LoadedReadyArtwork Ready { get; }
    private readonly DarkScreenIntroductionProgram program;
    private readonly byte[] background = DarkScreenPresentation.CheckerBackground();

    private DarkScreenIntroductionSession(PreparedDarkScreenIntroduction definition, PreparedDarkScreen dark,
        LoadedGeneralBanner general, LoadedReadyArtwork ready, DarkScreenIntroductionProgram program)
        => (Definition, Dark, General, Ready, this.program) = (definition, dark, general, ready, program);

    public static DarkScreenIntroductionSession Load(LooseAssetStore descriptors, AssetId id, LooseAssetStore dark,
        LooseAssetStore general, LooseAssetStore ready)
    {
        var definition = PreparedDarkScreenIntroduction.Load(descriptors, id);
        var d = PreparedDarkScreen.Load(dark, new(definition.DarkScreenId));
        var g = PreparedGeneralBanner.Load(general, new(definition.GeneralArtworkId));
        var r = PreparedReadyArtwork.Load(ready, new(definition.ReadyArtworkId));
        var program = definition.ToProgram(d);
        if (g.Definition.Source.ReferenceRevision != definition.ReferenceRevision || r.Definition.Source.ReferenceRevision != definition.ReferenceRevision)
            throw new InvalidDataException("Both selected artwork policies must match the introduction reference revision.");
        return new(definition, d, g, r, program);
    }

    public DarkScreenIntroductionResult Start(AssetId scene, AssetId consumer) => DarkScreenIntroductionExecutor.RunUntilBlocked(
        program.Start(scene, consumer, new(new(program.Dark.Definition.Id, program.Dark.OwnerId),
            new(program.General.Definition.Id, program.General.ArtworkId, program.General.OwnerId),
            new(program.Ready.Definition.Id, program.Ready.ArtworkId, program.Ready.OwnerId))));

    public byte[] Compose(DarkScreenIntroductionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ReferenceEquals(state.Program, program)) throw new ArgumentException("Snapshot belongs to another prepared introduction.", nameof(state));
        byte[] pixels = DarkScreenPresentation.Compose(DarkScreenPresentation.Project(Dark, state.Dark), background);
        if (state.Ready is { } ready)
            return ReadyPresentation.ComposeOver(Ready, ReadyPresentation.Project(ready, ReadyRasterState.Selected), pixels);
        var geometry = state.General is null ? ImmutableArray<BannerDraw>.Empty : GeneralBannerPresentation.Project(state.General);
        return FiveBitGeneralBannerPresentation.ComposeOver(General, [.. geometry.Select(g => new BannerRasterDraw(g, new(false, 0, 0)))], pixels);
    }
}

/// <summary>The existing battle controller is frozen until the three-worker aggregate publishes availability.</summary>
public sealed class DarkScreenIntroductionBattleSession
{
    private readonly DarkScreenIntroductionSession resources;
    internal BattleInteraction Interaction { get; }
    internal BattleUiRouter Router { get; }
    public DarkScreenIntroductionState State { get; private set; }
    public ImmutableArray<DarkScreenIntroductionTrace> Trace { get; private set; }
    public int AvailabilityPublications { get; private set; }
    public long? AvailabilityTick { get; private set; }
    public TwoUnitBattleState Battle => Interaction.State;
    public ImmutableArray<TwoUnitBattleCommand> Journal => Interaction.Journal;
    public BattleUnitId? SelectedUnit => Interaction.SelectedUnit;
    public BattleInteractionMode Mode => Interaction.Mode;
    public TilePosition Cursor => Router.Cursor;

    private DarkScreenIntroductionBattleSession(DarkScreenIntroductionSession resources, DarkScreenIntroductionResult admitted)
    {
        this.resources = resources; State = admitted.State; Trace = admitted.Trace;
        Interaction = new(SyntheticTwoUnitBattle.Create()); Router = new(Interaction);
    }
    public static DarkScreenIntroductionBattleSession Start(DarkScreenIntroductionSession resources, Action createGraphicsResources)
    {
        ArgumentNullException.ThrowIfNull(resources); ArgumentNullException.ThrowIfNull(createGraphicsResources);
        createGraphicsResources(); // Failure cannot construct a scene, battle controller or input consumer.
        var admitted = resources.Start(new("sample:dark-intro-battle/scene"), new("sample:dark-intro-battle/consumer"));
        if (!admitted.Accepted) throw new InvalidOperationException(admitted.Diagnostic);
        return new(resources, admitted);
    }
    public DarkScreenIntroductionResult AdvanceIntroduction(bool? circle, int instructionBudget = 8192, int updateBudget = 64)
    {
        var result = DarkScreenIntroductionExecutor.AdvanceTick(State, circle, instructionBudget, updateBudget);
        if (!result.Accepted) return result;
        State = result.State; Trace = Trace.AddRange(result.Trace);
        if (result.BattleBecameAvailable) { AvailabilityPublications++; AvailabilityTick = State.Event.Tick; }
        return result; // The availability transaction executes no battle command.
    }
    public bool Key(Keys key) { if (!State.BattleAvailable) return false; Router.Key(key); return true; }
    public bool Action(BattleUiAction action) { if (!State.BattleAvailable) return false; Router.Action(action); return true; }
    public bool Click(PixelPoint point) { if (!State.BattleAvailable) return false; Router.Click(point); return true; }
    public byte[] ComposeIntroduction() => resources.Compose(State);
    public void RecreateGraphics(Action createGraphicsResources)
    {
        ArgumentNullException.ThrowIfNull(createGraphicsResources);
        createGraphicsResources(); // Presentation-only; success or failure never advances the accepted state.
    }
}

public sealed record DarkScreenIntroductionOptions(string SequenceRoot, AssetId SequenceId, string DarkRoot,
    string GeneralRoot, string ReadyRoot, string? CheckRoot, ImmutableArray<int> CircleTicks)
{
    public const string Usage = "--dark-intro-battle --sequence-root <directory> --sequence-id <id> --dark-screen-root <directory> --general-artwork-root <directory> --ready-artwork-root <directory> [--check <fresh-scratch-root> --circle-ticks <CSV>]";
    public static DarkScreenIntroductionOptions Parse(string[] args)
    {
        if (args.Length < 11 || args.Length % 2 != 1 || args[0] != "--dark-intro-battle") throw new ArgumentException(Usage);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
            if (args[i] is not ("--sequence-root" or "--sequence-id" or "--dark-screen-root" or "--general-artwork-root" or "--ready-artwork-root" or "--check" or "--circle-ticks") ||
                string.IsNullOrWhiteSpace(args[i + 1]) || !values.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Usage);
        foreach (string key in new[] { "--sequence-root", "--sequence-id", "--dark-screen-root", "--general-artwork-root", "--ready-artwork-root" })
            if (!values.ContainsKey(key)) throw new ArgumentException(Usage);
        var ticks = ImmutableArray.CreateBuilder<int>();
        if (values.TryGetValue("--circle-ticks", out string? csv))
        {
            if (!values.ContainsKey("--check")) throw new ArgumentException(Usage);
            foreach (string part in csv.Split(','))
                if (!int.TryParse(part, out int tick) || tick is < 1 or > 4096 || ticks.Contains(tick)) throw new ArgumentException(Usage);
                else ticks.Add(tick);
        }
        return new(values["--sequence-root"], new(values["--sequence-id"]), values["--dark-screen-root"],
            values["--general-artwork-root"], values["--ready-artwork-root"], values.GetValueOrDefault("--check"), [.. ticks.Order()]);
    }
}

public static class DarkScreenIntroductionHost
{
    public static int Run(string[] args)
    {
        try
        {
            var options = DarkScreenIntroductionOptions.Parse(args);
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The combined introduction window supports Windows.");
            var prepared = DarkScreenIntroductionSession.Load(LooseAssetStore.Load(options.SequenceRoot), options.SequenceId,
                LooseAssetStore.Load(options.DarkRoot), LooseAssetStore.Load(options.GeneralRoot), LooseAssetStore.Load(options.ReadyRoot));
            string? check = GeneralBannerPreview.CheckPath(options.CheckRoot);
            Environment.SetEnvironmentVariable("SDL_WINDOWS_DPI_AWARENESS", "permonitorv2");
            using var window = new PlayableBattleWindow(check, prepared, options); window.Run();
            if (check is not null && !window.CheckCompleted) throw new InvalidOperationException("Combined introduction check did not finish.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("Combined introduction/battle failed: " + e.Message); return 1; }
    }
}
