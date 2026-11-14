using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Fft.Client.Presentation;

/// <summary>Both prepared resources validate before a scene can be started; GPU readiness belongs to the host.</summary>
public sealed class BannerReadySession
{
    public PreparedBannerReadySequence Definition { get; }
    public LoadedGeneralBanner General { get; }
    public LoadedReadyArtwork Ready { get; }
    private readonly BannerReadyProgram program;
    private BannerReadySession(PreparedBannerReadySequence definition, LoadedGeneralBanner general, LoadedReadyArtwork ready)
        => (Definition, General, Ready, program) = (definition, general, ready, definition.ToProgram());
    public static BannerReadySession Load(LooseAssetStore sequences, AssetId id, LooseAssetStore general, LooseAssetStore ready)
    {
        var definition = PreparedBannerReadySequence.Load(sequences, id);
        var g = PreparedGeneralBanner.Load(general, new(definition.GeneralArtworkId));
        var r = PreparedReadyArtwork.Load(ready, new(definition.ReadyArtworkId));
        if (g.Definition.Source.ReferenceRevision != definition.ReferenceRevision || r.Definition.Source.ReferenceRevision != definition.ReferenceRevision)
            throw new InvalidDataException("Both artwork policies must match the sequence's reference revision.");
        return new(definition, g, r);
    }
    public BannerReadyResult Start(AssetId scene, AssetId consumer) => BannerReadyExecutor.RunUntilBlocked(program.Start(scene, consumer,
        new(new(program.General.Definition.Id, program.General.ArtworkId, program.General.OwnerId),
            new(program.Ready.Definition.Id, program.Ready.ArtworkId, program.Ready.OwnerId))));
    public byte[] Compose(BannerReadyState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ReferenceEquals(state.Program, program)) throw new ArgumentException("Snapshot belongs to another sequence session.");
        if (state.Ready is not null) return ReadyPresentation.Compose(Ready, ReadyPresentation.Project(state.Ready, ReadyRasterState.Selected));
        var geometry = state.General is null ? ImmutableArray<BannerDraw>.Empty : GeneralBannerPresentation.Project(state.General);
        return FiveBitGeneralBannerPresentation.Compose(General, [.. geometry.Select(g => new BannerRasterDraw(g, new(false, 0, 0)))]);
    }
}

public sealed record BannerReadyPreviewOptions(string SequenceRoot, AssetId SequenceId, string GeneralRoot, string ReadyRoot,
    string? CheckRoot, ImmutableArray<int> CircleTicks)
{
    public const string Usage = "--banner-ready-sequence --sequence-root <directory> --sequence-id <id> --general-artwork-root <directory> --ready-artwork-root <directory> [--check <fresh-scratch-root> --circle-ticks <comma-separated 1..4096>]";
    public static BannerReadyPreviewOptions Parse(string[] args)
    {
        if (args.Length < 9 || args.Length % 2 != 1 || args[0] != "--banner-ready-sequence") throw new ArgumentException(Usage);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
            if (args[i] is not ("--sequence-root" or "--sequence-id" or "--general-artwork-root" or "--ready-artwork-root" or "--check" or "--circle-ticks") ||
                string.IsNullOrWhiteSpace(args[i + 1]) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Usage);
        foreach (string key in new[] { "--sequence-root", "--sequence-id", "--general-artwork-root", "--ready-artwork-root" })
            if (!options.ContainsKey(key)) throw new ArgumentException(Usage);
        var ticks = ImmutableArray.CreateBuilder<int>();
        if (options.TryGetValue("--circle-ticks", out string? text))
        {
            if (!options.ContainsKey("--check")) throw new ArgumentException(Usage);
            foreach (string part in text.Split(','))
                if (!int.TryParse(part, out int tick) || tick is < 1 or > 4096 || ticks.Contains(tick)) throw new ArgumentException(Usage);
                else ticks.Add(tick);
        }
        return new(options["--sequence-root"], new(options["--sequence-id"]), options["--general-artwork-root"], options["--ready-artwork-root"],
            options.GetValueOrDefault("--check"), [.. ticks.Order()]);
    }
}

public static class BannerReadyPreview
{
    public static int Run(string[] args)
    {
        try
        {
            var options = BannerReadyPreviewOptions.Parse(args);
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The sequence window supports Windows.");
            var session = BannerReadySession.Load(LooseAssetStore.Load(options.SequenceRoot), options.SequenceId,
                LooseAssetStore.Load(options.GeneralRoot), LooseAssetStore.Load(options.ReadyRoot));
            string? check = GeneralBannerPreview.CheckPath(options.CheckRoot);
            using var window = new BannerReadyWindow(session, options, check); window.Run();
            if (check is not null && !window.CheckCompleted) throw new InvalidOperationException("Sequence check did not complete.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("Banner/Ready preview failed: " + e.Message); return 1; }
    }
}

internal sealed class BannerReadyWindow : Game
{
    private readonly BannerReadySession session;
    private readonly BannerReadyPreviewOptions options;
    private readonly string? check;
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch? batch;
    private Texture2D? texture;
    private RenderTarget2D? target;
    private BannerReadyState? state;
    private KeyboardState previous;
    private bool rebuild, rebuilt;
    private long painted = -1;
    private readonly List<BannerReadyTrace> trace = [];
    private readonly List<object> captures = [];
    public bool CheckCompleted { get; private set; }
    internal BannerReadyWindow(BannerReadySession session, BannerReadyPreviewOptions options, string? check)
    {
        this.session = session; this.options = options; this.check = check;
        graphics = new(this) { PreferredBackBufferWidth = 768, PreferredBackBufferHeight = 720, SynchronizeWithVerticalRetrace = true };
        graphics.DeviceReset += (_, _) => rebuild = true;
        Window.Title = "Authored banner → Ready | Space tick, C Circle tick, Esc close";
        Window.AllowUserResizing = true; IsMouseVisible = true;
    }
    protected override void LoadContent()
    {
        Resources(); // Both CPU artwork bindings and every graphics resource exist before logical admission.
        var result = session.Start(new("sample:banner-ready/scene"), new("sample:banner-ready/consumer"));
        if (!result.Accepted) throw new InvalidOperationException(result.Diagnostic);
        state = result.State; trace.AddRange(result.Trace);
        if (check is not null) Directory.CreateDirectory(check);
    }
    private void Resources()
    {
        target?.Dispose(); texture?.Dispose(); batch?.Dispose();
        batch = new(GraphicsDevice); texture = new(GraphicsDevice, 256, 240);
        target = new(GraphicsDevice, 256, 240, false, SurfaceFormat.Color, DepthFormat.None); rebuild = false;
    }
    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState(); if (keys.IsKeyDown(Keys.Escape)) Exit();
        bool advance = check is not null ? state is not null && painted == state.Event.Tick && !CheckCompleted :
            keys.IsKeyDown(Keys.Space) && previous.IsKeyUp(Keys.Space) || keys.IsKeyDown(Keys.C) && previous.IsKeyUp(Keys.C);
        if (advance && !rebuild && state is { Event.Exited: false })
        {
            if (state.Event.Tick >= 4096) throw new InvalidOperationException("Sequence check exceeded its finite budget.");
            bool circle = check is null ? keys.IsKeyDown(Keys.C) : options.CircleTicks.Contains(checked((int)state.Event.Tick + 1));
            var result = BannerReadyExecutor.AdvanceTick(state, circle);
            if (!result.Accepted) throw new InvalidOperationException(result.Diagnostic);
            state = result.State; trace.AddRange(result.Trace);
        }
        previous = keys; base.Update(gameTime);
    }
    protected override void Draw(GameTime gameTime)
    {
        if (state is null || Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0) return;
        if (rebuild) Resources();
        if (check is not null && state.Event.Tick == 5 && !rebuilt) { Resources(); rebuilt = true; }
        byte[] pixels = session.Compose(state); texture!.SetData(pixels);
        GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(Color.Black);
        batch!.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(texture, Vector2.Zero, Color.White); batch.End(); GraphicsDevice.SetRenderTarget(null);
        if (check is not null && painted != state.Event.Tick)
        {
            var readback = new byte[pixels.Length]; target!.GetData(readback);
            if (!pixels.AsSpan().SequenceEqual(readback)) throw new InvalidOperationException("Sequence GPU readback differs from CPU.");
            string file = $"tick-{state.Event.Tick:D3}.png";
            using (var stream = File.Create(Path.Combine(check, file))) target.SaveAsPng(stream, 256, 240);
            captures.Add(new { tick = state.Event.Tick, file, rgbaSha256 = Convert.ToHexStringLower(SHA256.HashData(pixels)),
                stage = state.Ready is null ? "general" : "ready", general = state.General?.Phase.ToString(), ready = state.Ready?.ChildPhase.ToString(),
                generalReveal = state.General?.RevealCompleted, generalHold = state.General?.HoldCompleted, generalHide = state.General?.HideCompleted,
                readyScale = state.Ready?.DrawScale, totalTasks = state.Event.Tasks.TotalStarted, liveTasks = state.Event.Tasks.LiveTasks });
        }
        GraphicsDevice.Clear(Color.Black);
        int scale = Math.Max(1, Math.Min(Window.ClientBounds.Width / 256, Window.ClientBounds.Height / 240));
        batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(target!, new Rectangle((Window.ClientBounds.Width - 256 * scale) / 2, (Window.ClientBounds.Height - 240 * scale) / 2, 256 * scale, 240 * scale), Color.White); batch.End();
        painted = state.Event.Tick;
        if (check is not null && state.Event.Exited && !CheckCompleted)
        {
            File.WriteAllText(Path.Combine(check, "check.json"), JsonSerializer.Serialize(new
            {
                format = "fft.banner-ready-sequence-capture", schemaVersion = 1, definition = session.Definition,
                generalPolicy = FiveBitGeneralBannerPresentation.Policy, readyPolicy = ReadyPresentation.Policy,
                circleTicks = options.CircleTicks, captures, finalState = state,
                traceSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(trace))),
                rendererTaskAcknowledgments = 0, resourcesRebuiltAtTick5 = rebuilt,
                scope = "Authored preloaded sequence; no original loading duration, event execution or combined scanout comparison"
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            CheckCompleted = true; Exit();
        }
        base.Draw(gameTime);
    }
    protected override void UnloadContent() { target?.Dispose(); texture?.Dispose(); batch?.Dispose(); base.UnloadContent(); }
}
