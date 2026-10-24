using System.Collections.Immutable;
using System.Text.Json;
using Fft.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

/// <summary>Presentation-only view of one immutable, invented battle. No simulation transitions run here.</summary>
internal sealed class SyntheticBattleWindow : Game
{
    private static readonly Color Background = new(14, 21, 34);
    private static readonly Color FirstColor = new(82, 202, 232);
    private static readonly Color SecondColor = new(241, 172, 86);
    private readonly AttackBattleSnapshot snapshot;
    private readonly ImmutableArray<SceneUnit> units;
    private readonly string initialState;
    private readonly string? checkDirectory;
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch? batch;
    private Texture2D? pixel, diamond, unitTexture;
    private RenderTarget2D? scene;
    private long frames;
    public bool CheckCompleted { get; private set; }

    public SyntheticBattleWindow(AttackBattleSnapshot snapshot, string? checkDirectory)
    {
        this.snapshot = snapshot;
        this.checkDirectory = checkDirectory;
        units = SceneLayout.Units(snapshot);
        initialState = SimulationState();
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = SceneLayout.CanvasWidth,
            PreferredBackBufferHeight = SceneLayout.CanvasHeight,
            SynchronizeWithVerticalRetrace = true,
            GraphicsProfile = GraphicsProfile.Reach,
        };
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60);
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "FFT - Synthetic battle view | snapshot only | Esc closes";
        Window.ClientSizeChanged += OnClientSizeChanged;
        Window.KeyDown += OnKeyDown;
        graphics.DeviceReset += OnDeviceReset;
    }

    protected override void LoadContent()
    {
        CreateResources();
        Console.WriteLine($"Graphics ready: {GraphicsAdapter.DefaultAdapter.Description}; {GraphicsDevice.GraphicsProfile}.");
        OnClientSizeChanged(this, EventArgs.Empty);
    }

    private void CreateResources()
    {
        DisposeResources();
        batch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        diamond = new Texture2D(GraphicsDevice, 32, 16);
        var tilePixels = new Color[32 * 16];
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 32; x++)
        {
            int distance = Math.Abs(2 * x - 31) + 2 * Math.Abs(2 * y - 15);
            tilePixels[y * 32 + x] = distance <= 32 ? distance >= 28 ? new Color(50, 75, 89) : Color.White : Color.Transparent;
        }
        diamond.SetData(tilePixels);
        unitTexture = new Texture2D(GraphicsDevice, 16, 24);
        // Authored four-entry palette. Index zero is transparent; no original sprite data is used.
        Color[] palette = [Color.Transparent, new(29, 39, 57), Color.White, new(174, 192, 206)];
        var spritePixels = new Color[16 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 16; x++)
        {
            int index = 0;
            if (y is >= 1 and <= 8 && x is >= 4 and <= 11)
                index = y is 1 or 8 || x is 4 or 11 ? 1 : 2;
            if (y is >= 9 and <= 17 && x is >= 2 and <= 13)
                index = y is 9 or 17 || x is 2 or 13 ? 1 : x > 9 ? 3 : 2;
            if (y is >= 18 and <= 23 && (x is >= 3 and <= 6 || x is >= 9 and <= 12))
                index = y == 23 ? 1 : 3;
            spritePixels[y * 16 + x] = palette[index];
        }
        unitTexture.SetData(spritePixels);
        scene = new RenderTarget2D(GraphicsDevice, SceneLayout.CanvasWidth, SceneLayout.CanvasHeight,
            false, SurfaceFormat.Color, DepthFormat.None);
    }

    protected override void Update(GameTime gameTime)
    {
        // Frame time is deliberately never sent to CT, movement, combat or RNG.
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0) return;
        GraphicsDevice.SetRenderTarget(scene);
        GraphicsDevice.Clear(Background);
        batch!.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        Text("SYNTHETIC BATTLE", 48, 42, 4, Color.White);
        Text("IMMUTABLE SNAPSHOT", 50, 88, 2, new Color(145, 162, 184));
        Text($"A  HP {units[0].Hp}/{units[0].MaxHp}", 50, 158, 2, FirstColor);
        Text($"B  HP {units[1].Hp}/{units[1].MaxHp}", 700, 158, 2, SecondColor);
        var map = snapshot.TurnBattle.Battle.Map;
        for (int depth = 0; depth < map.Width + map.Depth - 1; depth++)
        for (int x = 0; x < map.Width; x++)
        {
            int y = depth - x;
            if (y < 0 || y >= map.Depth) continue;
            var center = SceneLayout.TileCenter(x, y, map.Width, map.Depth);
            batch.Draw(diamond!, new Rectangle(center.X - 80, center.Y - 32, 160, 80), new Color(28, 41, 53));
            batch.Draw(diamond!, new Rectangle(center.X - 80, center.Y - 40, 160, 80),
                (x + y) % 2 == 0 ? new Color(92, 130, 133) : new Color(113, 149, 139));
        }
        // Terrain first, then stable back-to-front unit order. Only this flat synthetic scene is supported.
        for (int i = 0; i < units.Length; i++)
        {
            var feet = units[i].Feet;
            batch.Draw(unitTexture!, new Rectangle(feet.X - 24, feet.Y - 72, 48, 72), i == 0 ? FirstColor : SecondColor);
        }
        Text("ESC CLOSE / RESIZE THE WINDOW", 50, 540, 2, new Color(145, 162, 184));
        var turns = snapshot.TurnBattle.Turns;
        Text($"SIM TICKS {turns.Ticks}  RNG {turns.RngState.Value}/{turns.RngState.DrawCount}", 50, 578, 2, new Color(145, 162, 184));
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        var pp = GraphicsDevice.PresentationParameters;
        var fit = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight);
        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        batch.Draw(scene!, new Rectangle(fit.X, fit.Y, fit.Width, fit.Height), Color.White);
        batch.End();
        frames++;
        if (checkDirectory is not null && frames == 120)
        {
            CaptureCheck();
            Exit();
        }
        base.Draw(gameTime);
    }

    private void CaptureCheck()
    {
        VerifySnapshot();
        var colors = new Color[SceneLayout.CanvasWidth * SceneLayout.CanvasHeight];
        scene!.GetData(colors);
        var feet = units[0].Feet;
        bool transparentCorner = colors[(feet.Y - 72) * SceneLayout.CanvasWidth + feet.X - 24] == Background;
        bool opaquePalette = colors[(feet.Y - 72 + 7) * SceneLayout.CanvasWidth + feet.X - 24 + 25] == FirstColor;
        if (!transparentCorner || !opaquePalette)
            throw new InvalidOperationException($"Synthetic GPU pixel checks failed: transparent={transparentCorner}, opaque={opaquePalette}.");
        using (var png = new FileStream(Path.Combine(checkDirectory!, "scene.png"), FileMode.CreateNew))
            scene.SaveAsPng(png, scene.Width, scene.Height);
        var pp = GraphicsDevice.PresentationParameters;
        var receipt = new
        {
            evidence = "synthetic GPU expectation; no original-game comparison",
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            adapter = GraphicsAdapter.DefaultAdapter.Description,
            frames, snapshotUnchanged = true, turns = snapshot.TurnBattle.Turns,
            transparentCorner, opaquePalette,
            clientWidth = Window.ClientBounds.Width, clientHeight = Window.ClientBounds.Height,
            backBufferWidth = pp.BackBufferWidth, backBufferHeight = pp.BackBufferHeight,
            viewport = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight),
        };
        using (var json = new FileStream(Path.Combine(checkDirectory!, "check.json"), FileMode.CreateNew))
            JsonSerializer.Serialize(json, receipt, new JsonSerializerOptions { WriteIndented = true });
        CheckCompleted = true;
        Console.WriteLine($"GPU check passed; capture and receipt: {checkDirectory}");
    }

    // Runtime diagnostic only, not a save format. All nested simulation types are immutable.
    private string SimulationState() => JsonSerializer.Serialize(new
    {
        snapshot.Combatants, snapshot.TurnBattle.Turns, snapshot.TurnBattle.Battle.RngState,
        positions = snapshot.TurnBattle.Battle.Units.Select(unit => new { unit.Id, unit.Position, unit.HasMoved }),
    });

    public void VerifySnapshot()
    {
        if (SimulationState() != initialState) throw new InvalidOperationException("Rendering changed the simulation snapshot.");
        Console.WriteLine($"Snapshot unchanged after {frames} rendered frames; ticks={snapshot.TurnBattle.Turns.Ticks}, " +
            $"RNG={snapshot.TurnBattle.Turns.RngState.Value}/{snapshot.TurnBattle.Turns.RngState.DrawCount}.");
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        if (scene is null) return;
        var pp = GraphicsDevice.PresentationParameters;
        Console.WriteLine($"Resize: client={Window.ClientBounds.Width}x{Window.ClientBounds.Height}, " +
            $"backbuffer={pp.BackBufferWidth}x{pp.BackBufferHeight}, viewport={SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight)}.");
    }

    private void OnKeyDown(object? sender, InputKeyEventArgs e)
    {
        // Consume the edge event: a short press may begin and end between presentation updates.
        if (e.Key == Keys.Escape) Exit();
    }

    private void OnDeviceReset(object? sender, EventArgs e)
    {
        if (scene is not null) CreateResources();
    }

    private void Text(string text, int x, int y, int scale, Color color)
    {
        foreach (char character in text)
        {
            var rows = TinyFont.Rows(character);
            for (int row = 0; row < rows.Length; row++)
            for (int column = 0; column < 5; column++)
                if ((rows[row] & (1 << (4 - column))) != 0)
                    batch!.Draw(pixel!, new Rectangle(x + column * scale, y + row * scale, scale, scale), color);
            x += 6 * scale;
        }
    }

    private void DisposeResources()
    {
        scene?.Dispose(); scene = null;
        unitTexture?.Dispose(); unitTexture = null;
        diamond?.Dispose(); diamond = null;
        pixel?.Dispose(); pixel = null;
        batch?.Dispose(); batch = null;
    }

    protected override void UnloadContent() => DisposeResources();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Window.ClientSizeChanged -= OnClientSizeChanged;
            Window.KeyDown -= OnKeyDown;
            graphics.DeviceReset -= OnDeviceReset;
            DisposeResources();
        }
        base.Dispose(disposing);
    }
}
