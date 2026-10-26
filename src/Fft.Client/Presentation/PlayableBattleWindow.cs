using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Zodiac.Content.Samples;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

/// <summary>Local two-player UI for the supported M1 battle. Only the controller publishes simulation results.</summary>
internal sealed partial class PlayableBattleWindow : Game
{
    private static readonly Color Background = new(14, 21, 34);
    private static readonly Color Cyan = new(82, 202, 232), Gold = new(241, 172, 86), Muted = new(145, 162, 184);
    private BattleInteraction interaction = null!;
    private BattleUiRouter router = null!;
    private readonly BattleInputBuffer input = new();
    private readonly GraphicsDeviceManager graphics;
    private readonly string? checkDirectory;
    private SpriteBatch? batch;
    private Texture2D? pixel, diamond, pawn;
    private RenderTarget2D? scene;
    private int frames, checkStep, nextStepFrame = 16;
    private bool resizeChecked;
    private string? pendingCapture;
    public bool CheckCompleted { get; private set; }

    public PlayableBattleWindow(string? checkDirectory, BannerReadySession? preparedIntroduction = null, BannerReadyPreviewOptions? introductionOptions = null,
        PreparedAccumulateBattle? abilityBattle = null)
    {
        if ((preparedIntroduction is null) != (introductionOptions is null)) throw new ArgumentException("Introduction resources and options must be supplied together.");
        if (abilityBattle is not null && preparedIntroduction is not null) throw new ArgumentException("Select either the authored ability battle or introduction.");
        this.abilityBattle = abilityBattle;
        this.checkDirectory = checkDirectory; this.preparedIntroduction = preparedIntroduction; this.introductionOptions = introductionOptions;
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = SceneLayout.CanvasWidth, PreferredBackBufferHeight = SceneLayout.CanvasHeight,
            SynchronizeWithVerticalRetrace = true, GraphicsProfile = GraphicsProfile.Reach,
        };
        IsFixedTimeStep = true; TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60);
        IsMouseVisible = true; Window.AllowUserResizing = true;
        Window.Title = preparedIntroduction is null ? "FFT - Bounded two-unit battle | local two player | Esc closes" :
            "FFT - Authored introduction to bounded battle | C skips / N next turn / Esc closes";
        if (abilityBattle is not null) Window.Title = "FFT - Accumulate battle | C Accumulate / N next turn / Esc closes";
        Window.KeyDown += OnKeyDown; Window.KeyUp += OnKeyUp;
        Activated += HandleActivated; Deactivated += HandleDeactivated; graphics.DeviceReset += OnDeviceReset;
    }

    public PlayableBattleWindow(string? checkDirectory, DarkScreenIntroductionSession prepared, DarkScreenIntroductionOptions options)
        : this(checkDirectory)
    {
        preparedDarkIntroduction = prepared; darkIntroductionOptions = options;
        Window.Title = "FFT - Authored dark-screen introduction to bounded battle | C skips / Esc closes";
    }

    protected override void LoadContent()
    {
        if (preparedDarkIntroduction is not null)
            introduction = new DarkWindowIntroduction(DarkScreenIntroductionBattleSession.Start(preparedDarkIntroduction, CreateResources));
        else
        {
            CreateResources(); // Every graphics resource exists before the introduction or battle is admitted.
            introduction = preparedIntroduction is null ? null : new BannerWindowIntroduction(IntroductionBattleSession.Start(preparedIntroduction));
        }
        interaction = introduction?.Interaction ?? (abilityBattle is null ? new(SyntheticTwoUnitBattle.Create()) :
            new BattleInteraction(abilityBattle.Definition, abilityBattle.AbilityId));
        router = introduction?.Router ?? new(interaction);
        initialBattle = Snapshot(interaction.State); BaselineInput();
        if (abilityBattle is not null && checkDirectory is not null) InitializeAbilityCheck();
        if (checkDirectory is not null)
        {
            Directory.CreateDirectory(checkDirectory);
            if (introduction is null) pendingCapture = "start.png";
            else Directory.CreateDirectory(Path.Combine(checkDirectory, "intro"));
        }
    }

    private void CreateResources()
    {
        DisposeResources(); batch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1); pixel.SetData(new[] { Color.White });
        diamond = new Texture2D(GraphicsDevice, 32, 16);
        var terrain = new Color[32 * 16];
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 32; x++)
            terrain[y * 32 + x] = Math.Abs(2 * x - 31) + 2 * Math.Abs(2 * y - 15) <= 32 ? Color.White : Color.Transparent;
        diamond.SetData(terrain);
        pawn = new Texture2D(GraphicsDevice, 16, 24);
        var unit = new Color[16 * 24];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 16; x++)
        {
            bool visible = y is >= 1 and <= 7 && x is >= 5 and <= 10 ||
                y is >= 8 and <= 16 && x is >= 3 and <= 12 ||
                y is >= 17 and <= 23 && (x is >= 3 and <= 6 || x is >= 9 and <= 12);
            unit[y * 16 + x] = visible ? Color.White : Color.Transparent;
        }
        pawn.SetData(unit);
        scene = new RenderTarget2D(GraphicsDevice, SceneLayout.CanvasWidth, SceneLayout.CanvasHeight, false, SurfaceFormat.Color, DepthFormat.None);
        if (preparedIntroduction is not null || preparedDarkIntroduction is not null)
        {
            introTexture = new(GraphicsDevice, 256, 240);
            introTarget = new(GraphicsDevice, 256, 240, false, SurfaceFormat.Color, DepthFormat.None);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (UpdateIntroduction()) { base.Update(gameTime); return; }
        if (checkDirectory is not null)
        {
            if (abilityBattle is not null)
            { UpdateAbilityCheck(); base.Update(gameTime); return; }
            if (preparedDarkIntroduction is not null && checkWatch.Elapsed > TimeSpan.FromSeconds(120))
                throw new InvalidOperationException("Combined introduction check exceeded 120 seconds.");
            if (checkStep < BattleUiCheckPlan.Inputs.Length && frames >= nextStepFrame)
            {
                if (checkStep == 12)
                {
                    string beforeResize = Snapshot(interaction.State);
                    graphics.PreferredBackBufferWidth = 1280; graphics.PreferredBackBufferHeight = 720;
                    graphics.ApplyChanges();
                    if (Snapshot(interaction.State) != beforeResize) throw new InvalidOperationException("Resize changed battle state.");
                    resizeChecked = true;
                }
                var step = BattleUiCheckPlan.Inputs[checkStep++];
                if (step.Key is { } key) DispatchKey(key);
                if (step.Click is { } click) InjectCanvasClick(click);
                pendingCapture = step.Capture; nextStepFrame = frames + 12;
            }
        }
        else if (IsActive)
        {
            while (input.TryTake(out var key)) DispatchKey(key);
            var mouse = Mouse.GetState(Window);
            if (input.LeftPress(mouse.LeftButton == ButtonState.Pressed)) RoutePointer(new(mouse.X, mouse.Y));
        }
        else input.ClearQueued();
        // CT advances only through an explicit NextTurn command. Presentation time is never passed to simulation.
        base.Update(gameTime);
    }

    private void RoutePointer(PixelPoint mouse)
    {
        var pp = GraphicsDevice.PresentationParameters;
        if (BattleUiLayout.CanvasPoint(mouse, Window.ClientBounds.Width, Window.ClientBounds.Height,
            pp.BackBufferWidth, pp.BackBufferHeight) is { } canvas)
        {
            if (introduction is null) router.Click(canvas); else introduction.Click(canvas);
        }
    }

    private void InjectCanvasClick(PixelPoint canvas)
    {
        // Programmed input still follows the same client/backbuffer/letterbox route as the native mouse.
        var pp = GraphicsDevice.PresentationParameters;
        var fit = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight);
        var mouse = new PixelPoint((int)(((long)fit.X + (long)canvas.X * fit.Width / SceneLayout.CanvasWidth) * Window.ClientBounds.Width / pp.BackBufferWidth),
            (int)(((long)fit.Y + (long)canvas.Y * fit.Height / SceneLayout.CanvasHeight) * Window.ClientBounds.Height / pp.BackBufferHeight));
        RoutePointer(mouse);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0) return;
        if (DrawIntroduction()) { frames++; base.Draw(gameTime); return; }
        string before = Snapshot(interaction.State);
        GraphicsDevice.SetRenderTarget(scene); GraphicsDevice.Clear(Background);
        batch!.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        DrawInterface(); batch.End();
        GraphicsDevice.SetRenderTarget(null); GraphicsDevice.Clear(Color.Black);
        var pp = GraphicsDevice.PresentationParameters; var fit = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight);
        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        batch.Draw(scene!, new Rectangle(fit.X, fit.Y, fit.Width, fit.Height), Color.White); batch.End();
        if (Snapshot(interaction.State) != before) throw new InvalidOperationException("Drawing changed the battle state.");
        frames++;
        if (pendingCapture is not null) { Capture(pendingCapture); pendingCapture = null; }
        if (abilityBattle is not null && checkDirectory is not null && checkStep == abilitySteps.Length && frames >= nextStepFrame)
        { FinishAbilityCheck(); Exit(); }
        else if (abilityBattle is null && checkDirectory is not null && checkStep == BattleUiCheckPlan.Inputs.Length && frames >= nextStepFrame)
        { FinishCheck(); Exit(); }
        base.Draw(gameTime);
    }

    private void DrawInterface()
    {
        var state = interaction.State; var turns = state.Battle.TurnBattle.Turns;
        Text(abilityBattle is null ? "BOUNDED BATTLE" : "ACCUMULATE BATTLE", 24, 24, 3, Color.White);
        Text("CONTROL BOTH UNITS / FIRST KO WINS", 24, 64, 2, Muted);
        string status = state.WinnerId is { } winner ? $"{Name(winner)} WINS / FIRST KO" : interaction.Status;
        Text(status, 24, 110, 2, interaction.StatusIsError && state.WinnerId is null ? Gold : Color.White);
        if (state.WinnerId is not null && interaction.Status != status) Text(interaction.Status, 24, 144, 2, Gold);
        Text(state.WinnerId is not null ? $"BATTLE COMPLETE / TICK {turns.Ticks}" :
            turns.ActiveUnitId is { } active ? $"TURN {Name(active)} / TICK {turns.Ticks}" : $"NEXT TURN / TICK {turns.Ticks}", 24, 174, 2, Cyan);
        var map = state.Battle.TurnBattle.Battle.Map;
        var moves = interaction.Moves; var targets = interaction.Targets;
        for (int depth = 0; depth < map.Width + map.Depth - 1; depth++)
        for (int x = 0; x < map.Width; x++)
        {
            int y = depth - x; if (y < 0 || y >= map.Depth) continue;
            var tile = new TilePosition(x, y, 0); var c = SceneLayout.TileCenter(x, y, map.Width, map.Depth);
            var target = state.Battle.TurnBattle.Battle.Units.FirstOrDefault(u => u.Position == tile);
            Color fill = interaction.Mode == BattleInteractionMode.Move && moves.Contains(tile) ? new(51, 130, 178)
                : interaction.Mode == BattleInteractionMode.Attack && target is not null && targets.Contains(target.Id) ? new(168, 84, 53)
                : (x + y) % 2 == 0 ? new(84, 120, 124) : new(103, 139, 129);
            batch!.Draw(diamond!, new Rectangle(c.X - 80, c.Y - 32, 160, 80), new Color(28, 41, 53));
            batch.Draw(diamond!, new Rectangle(c.X - 80, c.Y - 40, 160, 80), fill);
            OutlineDiamond(c, new Color(40, 64, 74));
            if (router.Cursor == tile) OutlineDiamond(c, Color.White);
        }
        foreach (var unit in SceneLayout.Units(state.Battle))
        {
            Color color = unit.Hp == 0 ? new(65, 73, 86) : unit.Id == state.Definition.FirstUnitId ? Cyan : Gold;
            var rect = BattleUiLayout.UnitBounds(unit);
            if (interaction.SelectedUnit == unit.Id)
                OutlineDiamond(unit.Feet, Color.White);
            batch!.Draw(pawn!, new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), color);
            Text(unit.Hp == 0 ? "KO" : Name(unit.Id), unit.Feet.X - (unit.Hp == 0 ? 12 : 6), unit.Feet.Y - 48, 2, Color.White);
        }
        for (int i = 0; i < 2; i++)
        {
            var id = i == 0 ? state.Definition.FirstUnitId : state.Definition.SecondUnitId;
            var body = state.Battle.Combatants.Single(u => u.UnitId == id); var turn = turns.Units.Single(u => u.Id == id);
            var card = BattleUiLayout.CardBounds(i); Color color = i == 0 ? Cyan : Gold;
            Fill(card, interaction.SelectedUnit == id ? new(38, 55, 72) : new(24, 34, 49));
            Text($"{i + 1}  UNIT {Name(id)}", card.X + 12, card.Y + 10, 2, color);
            Text($"HP {body.Hp}/{body.MaxHp}", card.X + 12, card.Y + 36, 2, Color.White);
            Text($"CT {turn.Ct}", card.X + 12, card.Y + 60, 2, Muted);
            if (interaction.HasSelfAbility) Text($"PA {body.PhysicalAttack}", card.X + 112, card.Y + 64, 1, color);
            Text($"MOVE {(turn.MovementTaken ? "USED" : "OPEN")}", card.X + 12, card.Y + 82, 1, Muted);
            Text($"ACT {(turn.ActionTaken ? "USED" : "OPEN")}", card.X + 12, card.Y + 100, 1, Muted);
        }
        Text("ACTIONS", 728, 198, 2, Color.White);
        foreach (var button in BattleUiLayout.ButtonsFor(interaction.HasSelfAbility))
        {
            bool selected = button.Action == BattleUiAction.Move && interaction.Mode == BattleInteractionMode.Move ||
                button.Action == BattleUiAction.Attack && interaction.Mode == BattleInteractionMode.Attack;
            bool unavailable = button.Action == BattleUiAction.Accumulate && !interaction.CanUseSelfAbility;
            Fill(button.Bounds, selected ? new(46, 90, 121) : unavailable ? new(25, 32, 43) : new(35, 49, 66));
            Text(button.Label, button.Bounds.X + 12, button.Bounds.Y + 13, 2,
                unavailable || state.WinnerId is not null && button.Action != BattleUiAction.Restart ? Muted : Color.White);
        }
        Text(interaction.Mode == BattleInteractionMode.Move ? "BLUE LEGAL MOVES / WHITE CURSOR" :
            interaction.Mode == BattleInteractionMode.Attack ? "ORANGE LEGAL TARGETS / WHITE CURSOR" :
            "CLICK TO SELECT / FIRST KO ENDS BATTLE", 300, 478, 1, Muted);
        Text("CLICK UNIT OR CARD / TAB OR 1 2 SELECT", 24, 540, 2, Muted);
        if (interaction.HasSelfAbility) Text(interaction.CanUseSelfAbility ? "C ACCUMULATE / SELF / ACT READY" : "C ACCUMULATE / ACT UNAVAILABLE", 300, 510, 1, Muted);
        Text("M MOVE / A ATTACK / ARROWS AIM / ENTER CONFIRM", 24, 568, 2, Muted);
        Text("W WAIT / N NEXT TURN / BACKSPACE CANCEL / ESC CLOSE", 24, 596, 2, Muted);
    }

    private string Name(BattleUnitId id) => id == interaction.State.Definition.FirstUnitId ? "A" : "B";
    private void Fill(PixelRect r, Color color) => batch!.Draw(pixel!, new Rectangle(r.X, r.Y, r.Width, r.Height), color);
    private void OutlineDiamond(PixelPoint c, Color color)
    {
        Vector2[] corners = [new(c.X, c.Y - 40), new(c.X + 80, c.Y), new(c.X, c.Y + 40), new(c.X - 80, c.Y)];
        for (int i = 0; i < 4; i++)
        {
            Vector2 delta = corners[(i + 1) % 4] - corners[i];
            batch!.Draw(pixel!, corners[i], null, color, MathF.Atan2(delta.Y, delta.X), Vector2.Zero,
                new Vector2(delta.Length(), 2), SpriteEffects.None, 0);
        }
    }
    private void Text(string text, int x, int y, int scale, Color color)
    {
        foreach (char character in text)
        {
            var rows = TinyFont.Rows(character);
            for (int row = 0; row < rows.Length; row++)
            for (int col = 0; col < 5; col++)
                if ((rows[row] & (1 << (4 - col))) != 0)
                    batch!.Draw(pixel!, new Rectangle(x + col * scale, y + row * scale, scale, scale), color);
            x += 6 * scale;
        }
    }

    private void Capture(string name)
    {
        using var png = new FileStream(Path.Combine(checkDirectory!, name), FileMode.CreateNew);
        scene!.SaveAsPng(png, scene.Width, scene.Height);
    }

    // Diagnostic projection for replay comparison, not a save format or content serializer.
    private static string Snapshot(TwoUnitBattleState state) => JsonSerializer.Serialize(new
    {
        state.Outcome, state.WinnerId, state.Battle.Combatants, state.Battle.TurnBattle.Turns,
        state.Battle.TurnBattle.Battle.RngState,
        units = state.Battle.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }),
    });

    private void FinishCheck()
    {
        var replay = interaction.State.Definition.Start();
        int accepted = 0; var attempts = new List<object>();
        foreach (var command in interaction.Journal)
        {
            var result = TwoUnitBattleScenario.Apply(replay, command); replay = result.State;
            if (result.Accepted) accepted++;
            attempts.Add(new { kind = command.GetType().Name, command = JsonSerializer.SerializeToElement(command, command.GetType()),
                result.Accepted, result.ScenarioRejection, result.AttackRejection, result.TurnRejection, result.MovementRejection });
        }
        string final = Snapshot(interaction.State);
        if (final != Snapshot(replay) || final != Snapshot(BattleUiCheckPlan.Expected()) ||
            accepted != 12 || interaction.Journal.Length != 14 || !resizeChecked)
            throw new InvalidOperationException("Playable UI/replay/headless result mismatch.");
        var pp = GraphicsDevice.PresentationParameters;
        var report = new
        {
            evidence = "synthetic Windows GPU plus programmed UI routes; no physical input injection or original media",
            accepted, rejected = interaction.Journal.Length - accepted, attempts,
            inputs = BattleUiCheckPlan.Inputs.Select(i => new { key = i.Key?.ToString(), i.Click, i.Capture }),
            final = JsonSerializer.Deserialize<JsonElement>(final), headlessMatched = true, replayMatched = true,
            finalSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(final))),
            renderingPreservedState = true, resizeChecked, frames, adapter = GraphicsAdapter.DefaultAdapter.Description,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            backbuffer = new[] { pp.BackBufferWidth, pp.BackBufferHeight }, viewport = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight),
        };
        using var json = new FileStream(Path.Combine(checkDirectory!, "check.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(json, introduction is null ? (object)report : IntroductionReceipt(report), new JsonSerializerOptions { WriteIndented = true });
        CheckCompleted = true;
        Console.WriteLine($"Playable UI check passed: {accepted} accepted, 2 rejected; exact headless/replay match. Captures: {checkDirectory}");
    }

    private void BaselineInput()
    {
        input.Baseline(Keyboard.GetState().GetPressedKeys(), Mouse.GetState(Window).LeftButton == ButtonState.Pressed);
    }
    private void HandleActivated(object? sender, EventArgs e) => BaselineInput();
    private void HandleDeactivated(object? sender, EventArgs e) => input.ClearQueued();
    private void OnKeyDown(object? sender, InputKeyEventArgs e)
    {
        if (e.Key == Keys.Escape) { Exit(); return; }
        if (checkDirectory is null && IsActive) input.KeyDown(e.Key);
    }
    private void OnKeyUp(object? sender, InputKeyEventArgs e) => input.KeyUp(e.Key);
    private void OnDeviceReset(object? sender, EventArgs e)
    {
        if (scene is null) return;
        if (introduction is null) CreateResources(); else introduction.Recreate(CreateResources);
    }
    private void DisposeResources()
    {
        scene?.Dispose(); scene = null; pawn?.Dispose(); pawn = null; diamond?.Dispose(); diamond = null;
        introTexture?.Dispose(); introTexture = null; introTarget?.Dispose(); introTarget = null;
        pixel?.Dispose(); pixel = null; batch?.Dispose(); batch = null;
    }
    protected override void UnloadContent() => DisposeResources();
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Window.KeyDown -= OnKeyDown; Window.KeyUp -= OnKeyUp; Activated -= HandleActivated; Deactivated -= HandleDeactivated;
            graphics.DeviceReset -= OnDeviceReset; DisposeResources();
        }
        base.Dispose(disposing);
    }
}
