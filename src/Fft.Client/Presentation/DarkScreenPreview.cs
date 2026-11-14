using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Fft.Client.Presentation;

public sealed class DarkScreenSession
{
    public PreparedDarkScreen Definition { get; }
    private readonly DarkScreenProgram program;
    private DarkScreenSession(PreparedDarkScreen definition) { Definition = definition; program = definition.ToExecutionDefinition(); }
    public static DarkScreenSession Load(LooseAssetStore store, AssetId id) => new(PreparedDarkScreen.Load(store, id));
    public DarkScreenResult Start() => DarkScreenExecutor.RunUntilBlocked(program.StartScene(default,
        SceneTaskState.Create(new("sample:dark-screen-preview/scene")), new("sample:dark-screen-preview/consumer"),
        new(new(Definition.Id), new(Definition.OwnerId))), 8);
}
public sealed record DarkScreenPreviewOptions(string ContentRoot, AssetId Id, string? CheckRoot)
{
    public const string Usage = "--dark-screen --content-root <prepared-directory> --id <id> [--check <fresh-scratch-directory>]";
    public static DarkScreenPreviewOptions Parse(string[] args)
    {
        if (args.Length is not (5 or 7) || args[0] != "--dark-screen") throw new ArgumentException(Usage);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
            if (args[i] is not ("--content-root" or "--id" or "--check") || string.IsNullOrWhiteSpace(args[i + 1]) || !values.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Usage);
        if (!values.ContainsKey("--content-root") || !values.ContainsKey("--id")) throw new ArgumentException(Usage);
        return new(values["--content-root"], new(values["--id"]), values.GetValueOrDefault("--check"));
    }
}
public static class DarkScreenPreview
{
    public static int Run(string[] args)
    {
        try
        {
            var options = DarkScreenPreviewOptions.Parse(args);
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Dark-screen window supports Windows only.");
            var session = DarkScreenSession.Load(LooseAssetStore.Load(options.ContentRoot), options.Id);
            string? check = GeneralBannerPreview.CheckPath(options.CheckRoot);
            using var window = new DarkScreenWindow(session, check);
            window.Run();
            if (check is not null && !window.CheckCompleted) throw new InvalidOperationException("Dark-screen check did not finish.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("Dark-screen preview failed: " + e.Message); return 1; }
    }
}
internal sealed class DarkScreenWindow : Game
{
    private readonly DarkScreenSession session;
    private readonly string? check;
    private readonly GraphicsDeviceManager graphics;
    private readonly byte[] background = DarkScreenPresentation.CheckerBackground();
    private SpriteBatch? batch;
    private Texture2D? texture;
    private RenderTarget2D? target;
    private DarkScreenSceneState? state;
    private KeyboardState previous;
    private bool rebuild, rebuilt;
    private long painted = -1;
    private readonly List<DarkScreenTrace> trace = [];
    private readonly List<object> captures = [];
    public bool CheckCompleted { get; private set; }
    private bool Finished => state!.Event.Exited && state.Worker!.IsTerminal;
    internal DarkScreenWindow(DarkScreenSession session, string? check)
    {
        this.session = session; this.check = check;
        graphics = new(this) { PreferredBackBufferWidth = 768, PreferredBackBufferHeight = 720, SynchronizeWithVerticalRetrace = true };
        graphics.DeviceReset += (_, _) => rebuild = true;
        Window.Title = "Authored DarkScreen | flat RGB5 preview | Space tick, Esc close";
        Window.AllowUserResizing = true; IsMouseVisible = true;
    }
    protected override void LoadContent()
    {
        CreateResources();
        var started = session.Start(); // No logical worker or check output until all content/resources are ready.
        if (!started.Accepted) throw new InvalidOperationException(started.Diagnostic);
        state = started.State; trace.AddRange(started.Trace);
        if (check is not null) Directory.CreateDirectory(check);
    }
    private void CreateResources()
    {
        target?.Dispose(); texture?.Dispose(); batch?.Dispose();
        batch = new(GraphicsDevice); texture = new(GraphicsDevice, 256, 240);
        target = new(GraphicsDevice, 256, 240, false, SurfaceFormat.Color, DepthFormat.None); rebuild = false;
    }
    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState(); if (keys.IsKeyDown(Keys.Escape)) Exit();
        bool advance = check is not null ? state is not null && painted == state.Event.Tick && !CheckCompleted : keys.IsKeyDown(Keys.Space) && previous.IsKeyUp(Keys.Space);
        if (advance && !rebuild && state is not null && !Finished)
        {
            if (state.Event.Tick >= 1024) throw new InvalidOperationException("Finite dark-screen bound exceeded.");
            var next = DarkScreenExecutor.AdvanceTick(state, [], 8);
            if (!next.Accepted) throw new InvalidOperationException(next.Diagnostic);
            state = next.State; trace.AddRange(next.Trace);
        }
        previous = keys; base.Update(gameTime);
    }
    protected override void Draw(GameTime gameTime)
    {
        if (state is null || Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0) return;
        if (rebuild) CreateResources();
        if (check is not null && state.Event.Tick == 5 && !rebuilt) { CreateResources(); rebuilt = true; }
        var draws = DarkScreenPresentation.Project(session.Definition, state.Worker);
        var rgba = DarkScreenPresentation.Compose(draws, background);
        texture!.SetData(rgba); GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(Color.Black);
        batch!.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(texture, Vector2.Zero, Color.White); batch.End(); GraphicsDevice.SetRenderTarget(null);
        if (check is not null && painted != state.Event.Tick)
        {
            var readback = new byte[rgba.Length]; target!.GetData(readback);
            if (!rgba.AsSpan().SequenceEqual(readback)) throw new InvalidOperationException("Dark-screen CPU/GPU mismatch.");
            string file = $"tick-{state.Event.Tick:D3}.png";
            using (var stream = File.Create(Path.Combine(check, file))) target.SaveAsPng(stream, 256, 240);
            captures.Add(new { tick = state.Event.Tick, file, rgbaSha256 = Convert.ToHexStringLower(SHA256.HashData(rgba)),
                phase = state.Worker!.Phase.ToString(), drawKind = state.Worker.Draw.Kind.ToString(), drawCount = draws.Length,
                taskRevision = state.Event.Tasks.Tasks.Single(t => t.Id == state.Worker.TaskId).Revision });
        }
        GraphicsDevice.Clear(Color.Black);
        int scale = Math.Max(1, Math.Min(Window.ClientBounds.Width / 256, Window.ClientBounds.Height / 240));
        batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(target!, new Rectangle((Window.ClientBounds.Width - 256 * scale) / 2, (Window.ClientBounds.Height - 240 * scale) / 2, 256 * scale, 240 * scale), Color.White);
        batch.End(); painted = state.Event.Tick;
        if (check is not null && Finished && !CheckCompleted)
        {
            var receipt = new { format = "fft.dark-screen-capture", schemaVersion = 1, profile = PreparedDarkScreen.Profile,
                policy = DarkScreenPresentation.Policy, definitionId = session.Definition.Id, session.Definition.Source,
                holdFrames = session.Definition.HoldFrames, completionTick = state.Event.Tick, resourceRebuiltAtTick = rebuilt ? 5 : -1,
                backgroundSha256 = Convert.ToHexStringLower(SHA256.HashData(background)), captures,
                finalStateSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state))),
                finalEventExited = state.Event.Exited, finalWorkerPhase = state.Worker!.Phase.ToString(),
                projections = Enumerable.Range(0, 9).SelectMany(a => Enumerable.Range(0, 13).Select(r =>
                    new { angle = a * 64, radius = r * 4, quad = DarkScreenPresentation.Quad(session.Definition, new(0, a * 64, r * 4)) })).ToArray(),
                gpuReadbackPerformed = true,
                traceSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(trace))),
                rendererTaskAcknowledgments = 0, originalComparison = "not performed", domain = "authored prepared; post-readiness source-derived ticks; provisional coverage policy" };
            File.WriteAllText(Path.Combine(check, "check.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            CheckCompleted = true; Exit();
        }
        base.Draw(gameTime);
    }
    protected override void UnloadContent() { target?.Dispose(); texture?.Dispose(); batch?.Dispose(); base.UnloadContent(); }
}
