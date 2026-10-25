using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Zodiac.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

/// <summary>Modern depth-buffer/cutout inspection of prepared data; not a PS1 GPU or encounter camera.</summary>
internal sealed class PreparedSceneWindow : Game
{
    private readonly LoadedBattleMap map;
    private readonly LoadedSpritePose pose;
    private readonly PreparedSceneGeometry geometry;
    private readonly string initialInputs;
    private readonly string? checkDirectory;
    private readonly GraphicsDeviceManager graphics;
    private readonly (int Bank, VertexPositionColorTexture[] Vertices)[] mapDraws, poseDraws;
    private readonly Dictionary<int, Texture2D> palettes = [];
    private SpriteBatch? batch;
    private AlphaTestEffect? effect;
    private Texture2D? pixel, sprite;
    private RenderTarget2D? scene;
    private string? firstFrameHash;
    private int frames;
    public bool CheckCompleted { get; private set; }

    public PreparedSceneWindow(LoadedBattleMap map, LoadedSpritePose pose, PreparedSceneGeometry geometry, string? checkDirectory)
    {
        this.map = map; this.pose = pose; this.geometry = geometry; this.checkDirectory = checkDirectory;
        initialInputs = InputDigest();
        mapDraws = geometry.MapDraws.Select(d => (d.PaletteBank, d.Vertices.ToArray())).ToArray();
        poseDraws = geometry.PoseDraws.Select(d => (d.PaletteBank, d.Vertices.ToArray())).ToArray();
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = SceneLayout.CanvasWidth, PreferredBackBufferHeight = SceneLayout.CanvasHeight,
            SynchronizeWithVerticalRetrace = true, GraphicsProfile = GraphicsProfile.Reach,
        };
        IsFixedTimeStep = true; TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60);
        IsMouseVisible = true; Window.AllowUserResizing = true;
        Window.Title = "FFT - MAP003 / frozen Chocobo pose | Esc closes";
        Window.KeyDown += OnKeyDown; graphics.DeviceReset += OnDeviceReset;
    }

    protected override void LoadContent()
    {
        CreateResources();
        Console.WriteLine($"Prepared scene: {geometry.MapDraws.Length} visible map polygons, {geometry.HiddenPolygons} hidden, " +
            $"{geometry.PoseDraws.Length} pose pieces; tile {PreparedSceneGeometry.UnitTile}, anchor {geometry.UnitAnchor}.");
        Console.WriteLine("Fixed authored camera; neutral texture lighting, point sampling, opaque cutout, modern depth. Animation frozen.");
    }

    private void CreateResources()
    {
        DisposeResources();
        batch = new SpriteBatch(GraphicsDevice);
        effect = new AlphaTestEffect(GraphicsDevice)
        {
            World = Matrix.Identity, View = PreparedSceneGeometry.View, Projection = PreparedSceneGeometry.Projection,
            VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 0,
            DiffuseColor = Vector3.One, Alpha = 1, FogEnabled = false,
        };
        pixel = new Texture2D(GraphicsDevice, 1, 1); pixel.SetData(new[] { Color.White });
        foreach (int bank in mapDraws.Select(d => d.Bank).Where(b => b >= 0).Distinct())
        {
            var texture = new Texture2D(GraphicsDevice, 1024, 256, false, SurfaceFormat.Color);
            palettes.Add(bank, texture);
            texture.SetData(PreparedSceneGeometry.PaletteTexture(map, bank));
        }
        sprite = new Texture2D(GraphicsDevice, 256, 256, false, SurfaceFormat.Color);
        var colors = new Color[256 * 256];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = new(pose.Rgba[i * 4], pose.Rgba[i * 4 + 1], pose.Rgba[i * 4 + 2], pose.Rgba[i * 4 + 3]);
        sprite.SetData(colors);
        scene = new RenderTarget2D(GraphicsDevice, SceneLayout.CanvasWidth, SceneLayout.CanvasHeight,
            false, SurfaceFormat.Color, DepthFormat.Depth24);
    }

    protected override void Update(GameTime gameTime)
    {
        // No simulation, camera motion, RNG or animation updates consume the presentation clock.
        base.Update(gameTime);
    }

    private void DrawScene(bool includePose)
    {
        GraphicsDevice.SetRenderTarget(scene);
        GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1, 0);
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
        foreach (var draw in mapDraws) DrawPrimitive(draw);
        // A single billboard reads the map depth but never writes it: parts cannot self-occlude
        // because of floating-point depth rounding, and their prepared painter order is authoritative.
        GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        if (includePose) foreach (var draw in poseDraws) DrawPrimitive(draw);
        batch!.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        Text("MAP003 / FROZEN CHOCOBO POSE", 24, 20);
        Text("FIXED CAMERA / ESC CLOSE", 24, SceneLayout.CanvasHeight - 32);
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
    }

    private void DrawPrimitive((int Bank, VertexPositionColorTexture[] Vertices) draw)
    {
        effect!.Texture = draw.Bank == -2 ? sprite! : draw.Bank == -1 ? pixel! : palettes[draw.Bank];
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, draw.Vertices, 0, draw.Vertices.Length / 3);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        if (Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0) return;
        DrawScene(includePose: true);
        if (checkDirectory is not null && frames == 0) firstFrameHash = PixelDigest(ReadPixels());
        GraphicsDevice.Clear(Color.Black);
        var pp = GraphicsDevice.PresentationParameters;
        var fit = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight);
        batch!.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        batch.Draw(scene!, new Rectangle(fit.X, fit.Y, fit.Width, fit.Height), Color.White);
        batch.End();
        if (++frames == 120 && checkDirectory is not null) { CaptureCheck(); Exit(); }
        base.Draw(gameTime);
    }

    private Color[] ReadPixels()
    {
        var colors = new Color[SceneLayout.CanvasWidth * SceneLayout.CanvasHeight];
        scene!.GetData(colors); return colors;
    }

    private static string PixelDigest(Color[] colors) => Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(colors.AsSpan())));
    private string InputDigest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(map.Definition.ToUtf8()); hash.AppendData(map.TextureIndices.AsSpan());
        hash.AppendData(pose.Definition.ToUtf8()); hash.AppendData(pose.Rgba.AsSpan());
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public void VerifyInputs()
    {
        if (InputDigest() != initialInputs) throw new InvalidOperationException("Rendering changed prepared inputs.");
        Console.WriteLine($"Prepared inputs unchanged after {frames} frames; no simulation or animation advanced.");
    }

    private void CaptureCheck()
    {
        VerifyInputs();
        Color[] combined = ReadPixels(); string hash = PixelDigest(combined);
        if (hash != firstFrameHash) throw new InvalidOperationException("Fixed scene changed between frames 1 and 120.");
        DrawScene(includePose: false);
        Color[] mapOnly = ReadPixels();
        int changedPixels = combined.Where((color, i) => color != mapOnly[i]).Count();
        if (changedPixels == 0) throw new InvalidOperationException("Prepared pose contributed no visible pixels.");
        DrawScene(includePose: true);
        if (PixelDigest(ReadPixels()) != hash) throw new InvalidOperationException("Restored capture changed the fixed scene.");
        using (var png = new FileStream(Path.Combine(checkDirectory!, "scene.png"), FileMode.CreateNew))
            scene!.SaveAsPng(png, scene.Width, scene.Height);
        static float[] Vec(Vector3 v) => [v.X, v.Y, v.Z];
        var pp = GraphicsDevice.PresentationParameters;
        var receipt = new
        {
            evidence = "prepared source-derived inspection; no independent original/emulator capture available (ZOD-026)",
            policy = "neutral-unmodulated/point-clamp/opaque-cutout/modern-depth/frozen-base",
            referenceRevision = map.Definition.Source.ReferenceRevision,
            mapId = map.Definition.Id.Value, poseId = pose.Definition.Id.Value,
            mapSource = map.Definition.Source, poseSource = pose.Definition.Source,
            inputsSha256 = initialInputs, rgbaSha256 = hash, frames, identicalFrames = true, inputsUnchanged = true,
            poseVisiblePixels = changedPixels, visiblePolygons = geometry.MapDraws.Length, geometry.HiddenPolygons,
            unitTile = PreparedSceneGeometry.UnitTile, sourceAnchor = Vec(new(geometry.UnitAnchor.X, -geometry.UnitAnchor.Y, geometry.UnitAnchor.Z)),
            managedAnchor = Vec(geometry.UnitAnchor), screenAnchor = Vec(PreparedSceneGeometry.Project(geometry.UnitAnchor)),
            camera = new { yaw = PreparedSceneGeometry.CameraYaw, eye = Vec(PreparedSceneGeometry.Eye), target = Vec(PreparedSceneGeometry.Target),
                projection = "orthographic", PreparedSceneGeometry.ViewWidth, PreparedSceneGeometry.ViewHeight, near = 1, far = 2000 },
            posePhase = new { frame = pose.Definition.FrameId, sequence = pose.Definition.SequenceId,
                wait = pose.Definition.Wait, palette = pose.Definition.PaletteIndex, held = true,
                orientation = "unmirrored front, camera-independent" },
            canvas = new[] { SceneLayout.CanvasWidth, SceneLayout.CanvasHeight },
            backbuffer = new[] { pp.BackBufferWidth, pp.BackBufferHeight }, viewport = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight),
            runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            adapter = GraphicsAdapter.DefaultAdapter.Description, graphicsProfile = GraphicsDevice.GraphicsProfile.ToString(),
        };
        using (var json = new FileStream(Path.Combine(checkDirectory!, "check.json"), FileMode.CreateNew))
            JsonSerializer.Serialize(json, receipt, new JsonSerializerOptions { WriteIndented = true });
        CheckCompleted = true;
        Console.WriteLine($"Prepared GPU check passed: {hash}; pose contributes {changedPixels} pixels. Capture: {checkDirectory}");
    }

    private void Text(string text, int x, int y)
    {
        foreach (char character in text)
        {
            var rows = TinyFont.Rows(character);
            for (int row = 0; row < rows.Length; row++)
            for (int col = 0; col < 5; col++)
                if ((rows[row] & (1 << (4 - col))) != 0)
                    batch!.Draw(pixel!, new Rectangle(x + col * 2, y + row * 2, 2, 2), Color.White);
            x += 12;
        }
    }

    private void OnKeyDown(object? sender, InputKeyEventArgs e) { if (e.Key == Keys.Escape) Exit(); }
    private void OnDeviceReset(object? sender, EventArgs e) { if (scene is not null) CreateResources(); }
    private void DisposeResources()
    {
        scene?.Dispose(); scene = null; sprite?.Dispose(); sprite = null; pixel?.Dispose(); pixel = null;
        foreach (var texture in palettes.Values) texture.Dispose(); palettes.Clear();
        effect?.Dispose(); effect = null; batch?.Dispose(); batch = null;
    }
    protected override void UnloadContent() => DisposeResources();
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Window.KeyDown -= OnKeyDown; graphics.DeviceReset -= OnDeviceReset; DisposeResources(); }
        base.Dispose(disposing);
    }
}
