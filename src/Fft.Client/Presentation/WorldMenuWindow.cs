using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Fft.Client.Navigation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;

namespace Fft.Client.Presentation;

internal sealed class WorldMenuWindow : Game
{
    private readonly GraphicsDeviceManager graphics;
    private readonly string? directory;
    private readonly bool slotCheck;
    private readonly WorldSlotCheckFiles? slotFiles;
    private IReadOnlyList<WorldMenuCheckStep> CheckSteps => slotCheck ? WorldSlotsCheckPlan.Steps : WorldMenuCheckPlan.Steps;
    private readonly WorldMenuSession model, replay;
    private readonly WorldMenuInputRouter input = new();
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private readonly List<object> trace = [];
    private readonly List<object> captures = [];
    private Texture2D? texture;
    private RenderTarget2D? target;
    private SpriteBatch? batch;
    private int frames, step;
    private string? capture;
    private bool permitExit, failed, closeQueued, waitingForDraw;
    public bool CheckCompleted { get; private set; }
    public WorldMenuWindow(string? checkDirectory, bool slotCheck = false)
    {
        directory = checkDirectory;
        this.slotCheck = slotCheck;
        var profile = SyntheticWorldCheckpoint.CreateProfile();
        var store = new WorldSaveSlotStore(profile, directory is null ? null : Path.Combine(directory, "saves"));
        model = new(store, enableSaves: directory is null || slotCheck);
        var fresh = SyntheticWorldCheckpoint.CreateProfile();
        var replayStore = new WorldSaveSlotStore(fresh, directory is null ? null : Path.Combine(directory, "replay-saves"));
        replay = new(replayStore, enableSaves: directory is null || slotCheck);
        if (slotCheck) slotFiles = new(store, replayStore);
        graphics = new(this) { PreferredBackBufferWidth = 960, PreferredBackBufferHeight = 640 };
        Window.Title = "FFT / Authored world navigation"; Window.AllowUserResizing = true; IsMouseVisible = true;
        Activated += ActivatedWindow; Deactivated += DeactivatedWindow;
        Exiting += ClosingWindow; graphics.DeviceReset += ResetDevice;
    }
    protected override void LoadContent() { CreateResources(); input.Baseline(directory is null ? ReadSample() : new()); }
    private void CreateResources()
    {
        DisposeResources(); batch = new(GraphicsDevice);
        texture = new(GraphicsDevice, 960, 640);
        target = new(GraphicsDevice, 960, 640, false, SurfaceFormat.Color, DepthFormat.None);
    }
    protected override void Update(GameTime gameTime)
    {
        try
        {
            if (directory is not null)
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(slotCheck ? 90 : 60) || frames >= (slotCheck ? 1024 : 600)) throw new InvalidOperationException("Menu check exceeded its finite budget.");
                if (!waitingForDraw && step < CheckSteps.Count)
                {
                    var current = CheckSteps[step++];
                    if (slotCheck) slotFiles!.Apply(current.Mutation);
                    if (current.Width > 0)
                    {
                        string before = Snapshot(); graphics.PreferredBackBufferWidth = current.Width; graphics.PreferredBackBufferHeight = current.Height;
                        graphics.ApplyChanges(); Require(Snapshot() == before, "Resize changed model.");
                        Require(BackSize().SequenceEqual(new[] { current.Width, current.Height }), "Resize did not produce the requested backbuffer.");
                    }
                    if (current.Recreate) { string before = Snapshot(); CreateResources(); Require(Snapshot() == before, "Recreation changed model."); }
                    var raw = current.Sample;
                    if (raw.Left) raw = raw with { Pointer = ClientPoint(raw.Pointer) };
                    WorldMenuDispatch? command; WorldMenuResult? result = null;
                    if (current.Close)
                    {
                        var args = new ExitingEventArgs(); OnExiting(this, args);
                        Require(args.Cancel, "Close callback bypassed confirmation."); command = new(Close: true);
                        // The callback applied the live command; replay it below only.
                        var replayResult = command.Apply(replay); Require(replayResult.Handled, "Close replay failed.");
                        result = replayResult;
                    }
                    else
                    {
                        command = Poll(raw);
                        if (command is not null) result = Apply(command);
                    }
                    Require(Snapshot() == JsonSerializer.Serialize(replay.View), "Headless view replay differs.");
                    if (slotCheck) trace.Add(new { current.Name, mutation = current.Mutation, sample = raw, command, result, view = model.View,
                        client = new[] { Window.ClientBounds.Width, Window.ClientBounds.Height }, backbuffer = BackSize() });
                    else trace.Add(new { current.Name, sample = raw, command, result, view = model.View,
                        client = new[] { Window.ClientBounds.Width, Window.ClientBounds.Height }, backbuffer = BackSize() });
                    if (current.Capture) capture = current.Name;
                    waitingForDraw = true;
                }
            }
            else
            {
                var sample = ReadSample();
                if (closeQueued) { closeQueued = false; model.RequestExit(); input.Baseline(sample); }
                else if (Poll(sample) is { } command) command.Apply(model);
                if (model.View.Screen == WorldMenuScreen.Closed) { permitExit = true; Exit(); }
            }
        }
        catch { failed = true; throw; }
        base.Update(gameTime);
    }
    private WorldMenuDispatch? Poll(WorldMenuSample sample)
    {
        var pp = GraphicsDevice.PresentationParameters;
        return input.Poll(sample, model.View, Window.ClientBounds.Width, Window.ClientBounds.Height, pp.BackBufferWidth, pp.BackBufferHeight);
    }
    private WorldMenuResult Apply(WorldMenuDispatch command)
    {
        var left = command.Apply(model); var right = command.Apply(replay);
        Require(JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right), "Headless transition/events differ.");
        return left;
    }
    protected override void Draw(GameTime gameTime)
    {
        try { DrawFrame(gameTime); }
        catch { failed = true; throw; }
    }
    private void DrawFrame(GameTime gameTime)
    {
        if (Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0) return;
        string before = Snapshot(); uint[] pixels = WorldMenuPresentation.Render(model.View);
        texture!.SetData(pixels); GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(Color.Black);
        batch!.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        batch.Draw(texture, Vector2.Zero, Color.White); batch.End();
        GraphicsDevice.SetRenderTarget(null); GraphicsDevice.Clear(Color.Black);
        var pp = GraphicsDevice.PresentationParameters; var fit = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight);
        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        batch.Draw(target!, new Rectangle(fit.X, fit.Y, fit.Width, fit.Height), Color.White); batch.End();
        Require(Snapshot() == before, "Drawing changed model."); frames++; waitingForDraw = false;
        if (capture is not null)
        {
            var gpu = new uint[pixels.Length]; target!.GetData(gpu);
            Require(pixels.SequenceEqual(gpu), "GPU target differs from authored CPU pixels.");
            using var png = new FileStream(Path.Combine(directory!, capture + ".png"), FileMode.CreateNew);
            target.SaveAsPng(png, 960, 640);
            captures.Add(new { name = capture, sha256 = Convert.ToHexStringLower(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan()))),
                viewport = fit, backbuffer = BackSize(), view = model.View }); capture = null;
        }
        if (directory is not null && step == CheckSteps.Count)
        {
            Require(model.View.Screen == WorldMenuScreen.Closed && model.View.Checkpoint!.Funds == 93 && model.View.Checkpoint.ElapsedSeconds == 53, "Unexpected final checkpoint.");
            if (!slotCheck) Require(!Directory.Exists(Path.Combine(directory, "saves")) && !Directory.Exists(Path.Combine(directory, "replay-saves")), "Navigation wrote saves.");
            else
            {
                Require(File.ReadAllText(Path.Combine(directory, "saves", "slot-01.json")) == "{\"corrupt\":true}", "Recovery repaired the damaged primary.");
                Require(File.Exists(Path.Combine(directory, "saves", "slot-02.json")) && !File.Exists(Path.Combine(directory, "saves", "slot-03.json")) && !File.Exists(Path.Combine(directory, "saves", "slot-04.json")), "Unexpected check saves.");
            }
            File.WriteAllText(Path.Combine(directory, "trace.json"), JsonSerializer.Serialize(trace, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(directory, "captures.json"), JsonSerializer.Serialize(captures, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(directory, "check.json"), JsonSerializer.Serialize(new { format = slotCheck ? "fft.windows-world-slots-check" : "fft.windows-world-navigation-check", schemaVersion = 1,
                passed = true, frames, steps = step, captures = captures.Count, cpuGpuMatched = true, headlessMatched = true, statePreservedByRendering = true,
                adapter = GraphicsAdapter.DefaultAdapter.Description, final = model.View, physicalInputVerified = false }, new JsonSerializerOptions { WriteIndented = true }));
            CheckCompleted = true; permitExit = true; Exit();
        }
        base.Draw(gameTime);
    }
    private int[] BackSize() { var pp = GraphicsDevice.PresentationParameters; return [pp.BackBufferWidth, pp.BackBufferHeight]; }
    private PixelPoint ClientPoint(PixelPoint canvas)
    {
        var size = BackSize(); var fit = SceneLayout.Fit(size[0], size[1]);
        return new((int)(((long)fit.X + (long)canvas.X * fit.Width / 960) * Window.ClientBounds.Width / size[0]),
            (int)(((long)fit.Y + (long)canvas.Y * fit.Height / 640) * Window.ClientBounds.Height / size[1]));
    }
    private WorldMenuSample ReadSample()
    {
        var keys = Keyboard.GetState(); var pad = GamePad.GetState(PlayerIndex.One); var mouse = Mouse.GetState(Window);
        WorldMenuButtons keyboard = 0, controller = 0;
        if (keys.IsKeyDown(Keys.Up)) keyboard |= WorldMenuButtons.Up;
        if (keys.IsKeyDown(Keys.Down)) keyboard |= WorldMenuButtons.Down;
        if (keys.IsKeyDown(Keys.Enter) || keys.IsKeyDown(Keys.Space)) keyboard |= WorldMenuButtons.Confirm;
        if (keys.IsKeyDown(Keys.Escape) || keys.IsKeyDown(Keys.Back)) keyboard |= WorldMenuButtons.Back;
        if (pad.IsButtonDown(Buttons.DPadUp)) controller |= WorldMenuButtons.Up;
        if (pad.IsButtonDown(Buttons.DPadDown)) controller |= WorldMenuButtons.Down;
        if (pad.IsButtonDown(Buttons.A)) controller |= WorldMenuButtons.Confirm;
        if (pad.IsButtonDown(Buttons.B)) controller |= WorldMenuButtons.Back;
        return new(keyboard, controller, pad.IsConnected, IsActive, mouse.LeftButton == ButtonState.Pressed, new(mouse.X, mouse.Y));
    }
    private string Snapshot() => JsonSerializer.Serialize(model.View);
    private void ActivatedWindow(object? sender, EventArgs e) { if (directory is null) input.Baseline(ReadSample()); }
    private void DeactivatedWindow(object? sender, EventArgs e) { if (directory is null) input.Baseline(ReadSample() with { Active = false }); }
    private void ClosingWindow(object? sender, ExitingEventArgs e)
    {
        if (permitExit || failed) return;
        e.Cancel = true;
        if (directory is null) closeQueued = true;
        else model.RequestExit();
    }
    private void ResetDevice(object? sender, EventArgs e) { if (texture is not null) CreateResources(); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private void DisposeResources() { texture?.Dispose(); texture = null; target?.Dispose(); target = null; batch?.Dispose(); batch = null; }
    protected override void UnloadContent() => DisposeResources();
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Activated -= ActivatedWindow; Deactivated -= DeactivatedWindow; Exiting -= ClosingWindow;
            graphics.DeviceReset -= ResetDevice; slotFiles?.Dispose(); DisposeResources();
        }
        base.Dispose(disposing);
    }
}
