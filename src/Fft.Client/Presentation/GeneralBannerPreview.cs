using System.Security.Cryptography;
using System.Text.Json;
using Fft.Content;
using Fft.Core;
using Fft.Core.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

public static class GeneralBannerPreview
{
    public static readonly AssetId BannerId = new("fft.ps1-us:banner/general-0");
    public static BannerSceneState Start(LoadedGeneralBanner artwork, int hold = 60)
    {
        artwork.Definition.Validate();
        var definition = artwork.Definition.ToLogicalDefinition(hold);
        var program = new SceneEventProgram(new("sample:banner-preview/program"),
            [new(4, SceneTaskClass.ConditionDisplay), new(new ConditionalEventInstruction(7, ConditionalEventOperation.Exit))]);
        var scene = BannerSceneState.Create(program.Start(new(0, 0, 0, 0), SceneTaskState.Create(new("sample:banner-preview/scene")), new("sample:banner-preview/consumer")));
        var attached = BannerSceneExecutor.AttachBanner(scene, definition, new("sample:banner-preview/owner"));
        if (!attached.Accepted) throw new InvalidOperationException(attached.Diagnostic);
        var run = BannerSceneExecutor.RunUntilBlocked(attached.State, 1);
        if (!run.Accepted) throw new InvalidOperationException(run.Diagnostic);
        return run.State;
    }

    public static int Run(string[] args)
    {
        try
        {
            if (args.Length is not (3 or 5) || args[1] != "--content-root" || (args.Length == 5 && args[3] != "--check"))
                throw new ArgumentException("Usage: --general-banner --content-root <prepared-directory> [--check <fresh-scratch-directory>]");
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Banner window currently supports Windows only.");
            // No scene or task exists until both prepared files and GPU resources have loaded successfully.
            var banner = PreparedGeneralBanner.Load(LooseAssetStore.Load(args[2]), BannerId);
            string? check = args.Length == 5 ? Path.GetFullPath(args[4]) : null;
            if (check is not null)
            {
                string scratch = Path.GetFullPath("scratch") + Path.DirectorySeparatorChar;
                if (!check.StartsWith(scratch, StringComparison.OrdinalIgnoreCase) || Directory.Exists(check) || File.Exists(check))
                    throw new IOException("Check output must be a fresh directory beneath repository scratch/.");
                for (string? at = check; at is not null; at = Path.GetDirectoryName(at))
                    if (Directory.Exists(at) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Reparse-point capture output is unsupported.");
            }
            using var window = new GeneralBannerWindow(banner, check);
            window.Run();
            if (check is not null && !window.CheckCompleted) throw new InvalidOperationException("Banner check did not finish.");
            return 0;
        }
        catch (Exception e)
        { Console.Error.WriteLine("Banner preview failed: " + e.Message); return 1; }
    }
}

/// <summary>The host samples explicit logical commands. Draw/rebuild only repaint the accepted snapshot.</summary>
internal sealed class GeneralBannerWindow : Game
{
    private readonly LoadedGeneralBanner banner;
    private readonly string? check;
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch? batch;
    private Texture2D? texture;
    private RenderTarget2D? target;
    private BannerSceneState? state;
    private KeyboardState previous;
    private long painted = -1;
    private bool rebuild;
    private readonly List<object> captures = [];
    private static readonly HashSet<long> CaptureTicks = [0, 1, 2, 5, 9, 10, 11, 14, 20, 80, 81, 82, 85, 90, 91];
    public bool CheckCompleted { get; private set; }

    internal GeneralBannerWindow(LoadedGeneralBanner banner, string? check)
    {
        this.banner = banner; this.check = check;
        graphics = new(this) { PreferredBackBufferWidth = 768, PreferredBackBufferHeight = 720, SynchronizeWithVerticalRetrace = true };
        Window.Title = "General banner | modern RGB8 preview | Space tick, C Circle tick, Esc close";
        Window.AllowUserResizing = true;
        graphics.DeviceReset += (_, _) => rebuild = true;
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        CreateResources();
        if (check is not null) Directory.CreateDirectory(check);
        state = GeneralBannerPreview.Start(banner);
        Console.WriteLine(GeneralBannerPresentation.Policy);
    }

    private void CreateResources()
    {
        target?.Dispose(); texture?.Dispose(); batch?.Dispose();
        batch = new(GraphicsDevice); texture = new(GraphicsDevice, 256, 240);
        target = new(GraphicsDevice, 256, 240, false, SurfaceFormat.Color, DepthFormat.None);
        rebuild = false;
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        if (keys.IsKeyDown(Keys.Escape)) Exit();
        bool advance = check is not null ? state is not null && painted == state.Event.Tick && !CheckCompleted :
            (keys.IsKeyDown(Keys.Space) && previous.IsKeyUp(Keys.Space)) || (keys.IsKeyDown(Keys.C) && previous.IsKeyUp(Keys.C));
        if (advance && !rebuild && state is { Banner.IsTerminal: false })
        {
            var result = BannerSceneExecutor.AdvanceTick(state, check is null && keys.IsKeyDown(Keys.C), [], 1);
            if (!result.Accepted) throw new InvalidOperationException(result.Diagnostic);
            state = result.State;
        }
        previous = keys;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (state is null || Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0) return;
        if (rebuild) CreateResources(); // Rebuild never changes state or supplies terminal facts.
        var projection = GeneralBannerPresentation.Project(state.Banner!);
        byte[] rgba = GeneralBannerPresentation.Compose(banner, projection);
        texture!.SetData(rgba);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Black);
        batch!.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(texture, Vector2.Zero, Color.White);
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
        if (check is not null && painted != state.Event.Tick && CaptureTicks.Contains(state.Event.Tick))
        {
            var readback = new byte[rgba.Length]; target!.GetData(readback);
            if (!rgba.AsSpan().SequenceEqual(readback)) throw new InvalidOperationException("GPU point/opaque output differs from CPU canvas.");
            string file = $"tick-{state.Event.Tick:D3}.png";
            using (var stream = File.Create(Path.Combine(check, file))) target.SaveAsPng(stream, 256, 240);
            captures.Add(new { tick = state.Event.Tick, phase = state.Banner!.Phase.ToString(), file,
                rgbaSha256 = Convert.ToHexStringLower(SHA256.HashData(rgba)), pieces = projection });
        }
        GraphicsDevice.Clear(Color.Black);
        int scale = Math.Max(1, Math.Min(Window.ClientBounds.Width / 256, Window.ClientBounds.Height / 240));
        batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(target!, new Rectangle((Window.ClientBounds.Width - 256 * scale) / 2, (Window.ClientBounds.Height - 240 * scale) / 2, 256 * scale, 240 * scale), Color.White);
        batch.End();
        painted = state.Event.Tick;
        if (check is not null && state.Banner!.IsTerminal && !CheckCompleted)
        {
            File.WriteAllText(Path.Combine(check, "check.json"), JsonSerializer.Serialize(new
            {
                policy = GeneralBannerPresentation.Policy, referenceRevision = banner.Definition.Source.ReferenceRevision,
                completionTick = state.Event.Tick, phase = state.Banner.Phase.ToString(), captures,
                originalComparison = "pending", rendererTaskAcknowledgments = 0,
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            CheckCompleted = true; Exit();
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    { target?.Dispose(); texture?.Dispose(); batch?.Dispose(); base.UnloadContent(); }
}
