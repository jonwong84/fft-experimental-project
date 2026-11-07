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

/// <summary>Validated CPU content; the window creates every GPU resource before calling Start.</summary>
public sealed class ReadySession
{
    public PreparedReadyEvent Definition { get; }
    public LoadedReadyArtwork Artwork { get; }
    private readonly ReadySceneProgram program;
    private ReadySession(PreparedReadyEvent definition, LoadedReadyArtwork artwork)
    { Definition = definition; Artwork = artwork; program = definition.ToExecutionDefinition(); }
    public static ReadySession Load(LooseAssetStore events, AssetId eventId, LooseAssetStore artwork)
    {
        var definition = PreparedReadyEvent.Load(events, eventId);
        return new(definition, PreparedReadyArtwork.Load(artwork, definition.ArtworkId));
    }
    public ReadySceneResult Start(AssetId scene, AssetId consumer, ConditionalEventVariables variables = default)
        => ReadySceneExecutor.RunUntilBlocked(program.Start(variables, SceneTaskState.Create(scene), consumer,
            new(Definition.DefinitionId, new(Artwork.Definition.Id), Definition.OwnerId)), ConditionalEventProgram.MaxInstructions);
}

public sealed record ReadyPreviewOptions(string EventRoot, AssetId EventId, string ArtworkRoot, string? CheckRoot, int? CircleTick)
{
    public const string Usage = "--ready --event-root <directory> --event-id <id> --artwork-root <directory> [--check <fresh-scratch-directory> --circle-tick <1..4096>]";
    public static ReadyPreviewOptions Parse(string[] args)
    {
        if (args.Length < 7 || args.Length % 2 != 1 || args[0] != "--ready") throw new ArgumentException(Usage);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
            if (args[i] is not ("--event-root" or "--event-id" or "--artwork-root" or "--check" or "--circle-tick") ||
                string.IsNullOrWhiteSpace(args[i + 1]) || !values.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Usage);
        if (!values.ContainsKey("--event-root") || !values.ContainsKey("--event-id") || !values.ContainsKey("--artwork-root")) throw new ArgumentException(Usage);
        int? circle = null;
        if (values.TryGetValue("--circle-tick", out var text))
        {
            if (!values.ContainsKey("--check") || !int.TryParse(text, out int tick) || tick is < 1 or > 4096) throw new ArgumentException(Usage);
            circle = tick;
        }
        return new(values["--event-root"], new(values["--event-id"]), values["--artwork-root"], values.GetValueOrDefault("--check"), circle);
    }
}
public static class ReadyPreview
{
    public static int Run(string[] args)
    {
        try
        {
            var options = ReadyPreviewOptions.Parse(args);
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Ready window supports Windows only.");
            var session = ReadySession.Load(LooseAssetStore.Load(options.EventRoot), options.EventId, LooseAssetStore.Load(options.ArtworkRoot));
            string? check = GeneralBannerPreview.CheckPath(options.CheckRoot);
            using var window = new ReadyWindow(session, options, check);
            window.Run();
            if (check is not null && !window.CheckCompleted) throw new InvalidOperationException("Ready check did not finish.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("Ready preview failed: " + e.Message); return 1; }
    }
}
internal sealed class ReadyWindow : Game
{
    private readonly ReadySession session;
    private readonly ReadyPreviewOptions options;
    private readonly string? check;
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch? batch;
    private Texture2D? texture;
    private RenderTarget2D? target;
    private ReadySceneState? state;
    private KeyboardState previous;
    private bool rebuild;
    private bool checkedResourceRebuild;
    private long painted = -1;
    private long? childClearTick, dispatcherClearTick, eventExitTick;
    private readonly List<ReadySceneTrace> trace = [];
    private readonly List<object> captures = [];
    public bool CheckCompleted { get; private set; }
    private bool Finished => state!.Event.Exited && (state.Ready is null || state.Ready.IsTerminal);
    internal ReadyWindow(ReadySession session, ReadyPreviewOptions options, string? check)
    {
        this.session = session; this.options = options; this.check = check;
        graphics = new(this) { PreferredBackBufferWidth = 768, PreferredBackBufferHeight = 720, SynchronizeWithVerticalRetrace = true };
        graphics.DeviceReset += (_, _) => rebuild = true;
        Window.Title = "Authored Ready | RGB5 glyph preview | Space tick, C Circle tick, Esc close";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }
    protected override void LoadContent()
    {
        CreateResources();
        // No scene exists until prepared content and all GPU resources have succeeded.
        var result = session.Start(new("sample:ready-preview/scene"), new("sample:ready-preview/consumer"));
        if (!result.Accepted) throw new InvalidOperationException(result.Diagnostic);
        state = result.State; trace.AddRange(result.Trace); RecordTerminalTicks();
        if (check is not null) Directory.CreateDirectory(check);
    }
    private void CreateResources()
    {
        target?.Dispose(); texture?.Dispose(); batch?.Dispose();
        batch = new(GraphicsDevice); texture = new(GraphicsDevice, 256, 240);
        target = new(GraphicsDevice, 256, 240, false, SurfaceFormat.Color, DepthFormat.None);
        rebuild = false;
    }
    private void RecordTerminalTicks()
    {
        if (state!.Ready?.ChildIsTerminal == true) childClearTick ??= state.Event.Tick;
        if (state.Ready?.IsTerminal == true) dispatcherClearTick ??= state.Event.Tick;
        if (state.Event.Exited) eventExitTick ??= state.Event.Tick;
    }
    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        if (keys.IsKeyDown(Keys.Escape)) Exit();
        bool advance = check is not null ? state is not null && painted == state.Event.Tick && !CheckCompleted :
            keys.IsKeyDown(Keys.Space) && previous.IsKeyUp(Keys.Space) || keys.IsKeyDown(Keys.C) && previous.IsKeyUp(Keys.C);
        if (advance && !rebuild && state is not null && !Finished)
        {
            if (check is not null && state.Event.Tick >= 4096) throw new InvalidOperationException("Ready check exceeded 4096 ticks.");
            bool circle = check is null ? keys.IsKeyDown(Keys.C) : state.Event.Tick + 1 == options.CircleTick;
            var result = ReadySceneExecutor.AdvanceTick(state, circle, [], ConditionalEventProgram.MaxInstructions);
            if (!result.Accepted) throw new InvalidOperationException(result.Diagnostic);
            state = result.State; trace.AddRange(result.Trace); RecordTerminalTicks();
        }
        previous = keys;
        base.Update(gameTime);
    }
    protected override void Draw(GameTime gameTime)
    {
        if (state is null || Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0) return;
        if (rebuild) CreateResources();
        if (check is not null && state.Event.Tick == 5 && !checkedResourceRebuild)
        {
            // Exercise resource recreation in finite checks without changing the accepted snapshot.
            CreateResources();
            checkedResourceRebuild = true;
            Console.WriteLine("Ready check rebuilt resources at accepted tick 5.");
        }
        var draws = ReadyPresentation.Project(state.Ready, ReadyRasterState.Selected);
        var rgba = ReadyPresentation.Compose(session.Artwork, draws);
        texture!.SetData(rgba);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Black);
        batch!.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(texture, Vector2.Zero, Color.White); batch.End();
        GraphicsDevice.SetRenderTarget(null);
        if (check is not null && painted != state.Event.Tick)
        {
            var readback = new byte[rgba.Length]; target!.GetData(readback);
            if (!rgba.AsSpan().SequenceEqual(readback)) throw new InvalidOperationException("Ready GPU readback differs from CPU.");
            // A finite check retains each accepted tick, including empty extra yield and parent/child gaps.
            string file = $"tick-{state.Event.Tick:D3}.png";
            using (var stream = File.Create(Path.Combine(check, file))) target.SaveAsPng(stream, 256, 240);
            captures.Add(new
            {
                tick = state.Event.Tick,
                file,
                rgbaSha256 = Convert.ToHexStringLower(SHA256.HashData(rgba)),
                dispatcher = state.Ready?.DispatcherPhase.ToString(),
                child = state.Ready?.ChildPhase.ToString(),
                draws
            });
        }
        GraphicsDevice.Clear(Color.Black);
        int scale = Math.Max(1, Math.Min(Window.ClientBounds.Width / 256, Window.ClientBounds.Height / 240));
        batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(target!, new Rectangle((Window.ClientBounds.Width - 256 * scale) / 2, (Window.ClientBounds.Height - 240 * scale) / 2, 256 * scale, 240 * scale), Color.White);
        batch.End(); painted = state.Event.Tick;
        if (check is not null && Finished && !CheckCompleted)
        {
            var receipt = new
            {
                format = "fft.ready-capture",
                schemaVersion = 1,
                profile = PreparedReadyEvent.Profile,
                policy = ReadyPresentation.Policy,
                eventId = session.Definition.Id.Value,
                session.Definition.Source,
                artworkId = session.Artwork.Definition.Id,
                artworkSource = session.Artwork.Definition.Source,
                circleTick = options.CircleTick,
                childClearTick,
                dispatcherClearTick,
                eventExitTick,
                completionTick = state.Event.Tick,
                captures,
                finalState = state,
                traceSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(trace))),
                rendererTaskAcknowledgments = 0,
                originalComparison = "not performed",
                domain = "authored preloaded; no original startup or display latency claim"
            };
            File.WriteAllText(Path.Combine(check, "check.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            CheckCompleted = true; Exit();
        }
        base.Draw(gameTime);
    }
    protected override void UnloadContent()
    { target?.Dispose(); texture?.Dispose(); batch?.Dispose(); base.UnloadContent(); }
}
