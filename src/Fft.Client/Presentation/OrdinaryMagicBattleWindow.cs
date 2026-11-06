using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Fft.Client.Presentation;

/// <summary>Authored local player view. Time and rendering never advance the deterministic battle.</summary>
internal sealed class OrdinaryMagicBattleWindow : Game
{
    private static readonly Color Background = new(14, 21, 34), Muted = new(145, 162, 184), Cyan = new(82, 202, 232), Gold = new(241, 172, 86);
    private readonly GraphicsDeviceManager graphics;
    private readonly OrdinaryMagicInteraction interaction;
    private readonly BattleInputBuffer input = new();
    private readonly string? checkDirectory;
    private readonly string initial;
    private SpriteBatch batch = null!;
    private Texture2D pixel = null!;
    private RenderTarget2D scene = null!;
    private int frames, checkStep, nextFrame = 15, casts;
    private string? pendingCapture = "start.png", firstResolution;
    private bool resizeChecked, restarted, finalCaptured;
    private int spellSelectionsChecked;
    private readonly List<object> checkpoints = [];
    public bool CheckCompleted { get; private set; }

    internal OrdinaryMagicBattleWindow(PreparedOrdinaryMagicBattle content, string? checkDirectory)
    {
        this.checkDirectory = checkDirectory; interaction = new(content); initial = OrdinaryMagicSnapshot.Capture(interaction.State);
        graphics = new(this) { PreferredBackBufferWidth = 960, PreferredBackBufferHeight = 640,
            GraphicsProfile = GraphicsProfile.Reach, SynchronizeWithVerticalRetrace = true };
        IsFixedTimeStep = true; TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60); IsMouseVisible = true;
        Window.AllowUserResizing = true; Window.Title = "FFT / Ordinary Magic / local two player / Esc closes";
        Window.KeyDown += KeyDown; Window.KeyUp += KeyUp;
        Activated += Focus; Deactivated += Blur;
    }

    protected override void LoadContent()
    {
        batch = new(GraphicsDevice); pixel = new(GraphicsDevice, 1, 1); pixel.SetData(new[] { Color.White });
        scene = new(GraphicsDevice, 960, 640, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        Baseline();
    }

    protected override void Update(GameTime gameTime)
    {
        if (checkDirectory is not null)
        {
            if (!resizeChecked && frames >= 5)
            {
                graphics.PreferredBackBufferWidth = 1120; graphics.PreferredBackBufferHeight = 760; graphics.ApplyChanges();
                resizeChecked = true;
            }
            if (frames >= nextFrame) { CheckInput(); nextFrame = frames + 8; }
        }
        else if (IsActive)
        {
            while (input.TryTake(out var key)) Dispatch(key);
            var mouse = Mouse.GetState(Window);
            if (input.LeftPress(mouse.LeftButton == ButtonState.Pressed)) Pointer(new(mouse.X, mouse.Y));
        }
        else input.ClearQueued();
        base.Update(gameTime);
    }

    private void Dispatch(Keys key)
    {
        switch (key)
        {
            case Keys.N: interaction.Advance(); break;
            case Keys.F: interaction.BeginCast(); break;
            case Keys.Tab: case Keys.Down: interaction.NextSpell(); break;
            case Keys.Up: interaction.NextSpell(-1); break;
            case Keys.W: interaction.Wait(); break;
            case Keys.M: interaction.BeginStep(); break;
            case Keys.U: interaction.ToggleFollow(); break;
            case Keys.Enter: interaction.Confirm(); break;
            case Keys.Back: interaction.Cancel(); break;
            case Keys.R: interaction.Restart(); break;
            case Keys.Escape:
                if (interaction.Mode == OrdinaryMagicInteractionMode.Browse) Exit(); else interaction.Cancel();
                break;
        }
    }

    private void Pointer(PixelPoint point)
    {
        var pp = GraphicsDevice.PresentationParameters;
        if (BattleUiLayout.CanvasPoint(point, Window.ClientBounds.Width, Window.ClientBounds.Height,
                pp.BackBufferWidth, pp.BackBufferHeight) is not { } canvas) return;
        foreach (var button in Buttons())
            if (Contains(button.Bounds, canvas)) { Dispatch(button.Key); return; }
        if (interaction.Mode == OrdinaryMagicInteractionMode.Cast && canvas.X is >= 520 and < 936 && canvas.Y >= 196)
        {
            int row = (canvas.Y - 196) / 22;
            if (row < interaction.AvailableSpells.Length) { interaction.SelectSpell(interaction.AvailableSpells[row]); return; }
        }
        int x = (canvas.X - 24) / 64, y = (canvas.Y - 196) / 56;
        if (canvas.X >= 24 && canvas.Y >= 196 && x < 7 && y < 5) interaction.Select(new(x, y, 0));
    }
    private static bool Contains(PixelRect r, PixelPoint p) => p.X >= r.X && p.Y >= r.Y && p.X < r.X + r.Width && p.Y < r.Y + r.Height;
    private void Inject(PixelPoint canvas)
    {
        var pp = GraphicsDevice.PresentationParameters; var fit = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight);
        Pointer(new((int)(((long)fit.X + (long)canvas.X * fit.Width / 960) * Window.ClientBounds.Width / pp.BackBufferWidth),
            (int)(((long)fit.Y + (long)canvas.Y * fit.Height / 640) * Window.ClientBounds.Height / pp.BackBufferHeight)));
    }
    private void InjectTile(int x, int y) => Inject(new(24 + x * 64 + 32, 196 + y * 56 + 28));
    private static IEnumerable<(PixelRect Bounds, Keys Key, string Label)> Buttons()
    {
        yield return (new(24, 548, 178, 34), Keys.N, "N NEXT EVENT");
        yield return (new(214, 548, 120, 34), Keys.F, "F MAGIC");
        yield return (new(346, 548, 120, 34), Keys.W, "W WAIT");
        yield return (new(478, 548, 120, 34), Keys.M, "M STEP");
        yield return (new(610, 548, 154, 34), Keys.R, "R RESTART");
        yield return (new(24, 594, 220, 30), Keys.U, "U UNIT OR TILE");
        yield return (new(256, 594, 208, 30), Keys.Enter, "ENTER CONFIRM");
        yield return (new(478, 594, 208, 30), Keys.Back, "BACK CANCEL");
    }

    protected override void Draw(GameTime gameTime)
    {
        if (Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0) return;
        string before = OrdinaryMagicSnapshot.Capture(interaction.State);
        GraphicsDevice.SetRenderTarget(scene); GraphicsDevice.Clear(Background);
        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
        Interface(); batch.End();
        GraphicsDevice.SetRenderTarget(null); GraphicsDevice.Clear(Color.Black);
        var pp = GraphicsDevice.PresentationParameters; var fit = SceneLayout.Fit(pp.BackBufferWidth, pp.BackBufferHeight);
        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
        batch.Draw(scene, new Rectangle(fit.X, fit.Y, fit.Width, fit.Height), Color.White); batch.End();
        if (before != OrdinaryMagicSnapshot.Capture(interaction.State)) throw new InvalidOperationException("Drawing changed gameplay state.");
        frames++;
        if (checkDirectory is not null && pendingCapture is { } capture)
        {
            using var output = new FileStream(Path.Combine(checkDirectory, capture), FileMode.CreateNew);
            scene.SaveAsPng(output, scene.Width, scene.Height); pendingCapture = null;
        }
        base.Draw(gameTime);
    }

    private void Interface()
    {
        var state = interaction.State;
        Text("ORDINARY MAGIC", 24, 24, 3, Color.White);
        Text("LOCAL TWO PLAYER / FIRST KO WINS", 24, 64, 2, Muted);
        Text(interaction.Message, 24, 104, 2, interaction.IsError ? Gold : Color.White, 75);
        string turn = state.Finished ? "ENCOUNTER COMPLETE" : state.Turns.ActiveUnitId is { } id
            ? $"TURN {interaction.Name(id)}" : "N ADVANCES TO NEXT EVENT";
        Text($"{turn} / TICK {state.Turns.Ticks}", 24, 146, 2, Cyan);
        if (interaction.SelectedSpell is { } selected)
        {
            var spell = state.Definition.Spells[selected];
            Text($"{interaction.Content.SpellNames[selected]} / MP {spell.MpCost} / CT {spell.RawChargeTime}", 24, 174, 2, Gold);
        }
        var range = interaction.Range; var area = interaction.Area;
        for (int y = 0; y < 5; y++) for (int x = 0; x < 7; x++)
        {
            var tile = new TilePosition(x, y, 0); int px = 24 + x * 64, py = 196 + y * 56;
            Color fill = area.Contains(tile) ? new(139, 80, 45) : range.Contains(tile) ? new(35, 87, 125) : new(34, 47, 65);
            Rect(px, py, 62, 54, fill);
            if (interaction.Aim == tile) { Rect(px, py, 62, 3, Gold); Rect(px, py + 51, 62, 3, Gold); }
            var unit = state.Units.FirstOrDefault(u => u.Position == tile);
            if (unit is null) continue;
            Color team = unit.Definition.Occupant.InitialTeam == SpellTargetTeam.Blue ? Cyan : Gold;
            Rect(px + 20, py + 5, 22, 26, unit.Current.Statuses.Dead ? Muted : team);
            Text(interaction.Name(unit.Id)[..1], px + 26, py + 11, 2, Background);
            Text(unit.Current.Statuses.Dead ? "KO" : unit.Vitals.Hp.ToString(), px + 5, py + 37, 1, Color.White);
            Rect(px + 25, py + 39, 32, 4, Background);
            Rect(px + 25, py + 39, (int)(32L * unit.Vitals.Hp / unit.Vitals.MaxHp), 4, team);
        }
        Text(interaction.Mode == OrdinaryMagicInteractionMode.Cast
            ? $"AIM / {(interaction.FollowUnit ? "FOLLOW UNIT" : "KEEP TILE")}" : "M STEP / ONE ADJACENT TILE", 24, 496, 2, Muted);
        if (interaction.Mode == OrdinaryMagicInteractionMode.Cast)
        {
            Text("MAGIC / TAB OR UP DOWN", 520, 174, 2, Muted);
            var available = interaction.AvailableSpells;
            for (int i = 0; i < available.Length; i++)
            {
                var spell = state.Definition.Spells[available[i]]; int y = 196 + i * 22;
                Rect(520, y, 416, 21, available[i] == interaction.SelectedSpell ? new(52, 83, 111) : new(25, 37, 54));
                Text($"{interaction.Content.SpellNames[spell.Id]} / MP {spell.MpCost} / CT {spell.RawChargeTime}", 532, y + 3, 2, Color.White);
            }
        }
        else for (int i = 0; i < state.Units.Length; i++)
        {
            var unit = state.Units[i]; int y = 196 + i * 142;
            Rect(520, y, 416, 132, new(25, 37, 54));
            Text($"{interaction.Name(unit.Id)} / LV {unit.Level} / EXP {unit.Experience}", 536, y + 12, 2, Color.White);
            Text($"HP {unit.Vitals.Hp} / {unit.Vitals.MaxHp}   MP {unit.Current.Mp} / {unit.Vitals.MaxMp}", 536, y + 40, 2, Cyan);
            var points = unit.JobPoints[unit.Definition.RewardJobId];
            Text($"JP {points.Current} / TOTAL {points.Total}", 536, y + 68, 2, Muted);
            Text(unit.Current.Statuses.Dead ? "KO" : state.Pending.TryGetValue(unit.Id, out var pending)
                ? $"CHARGING / {pending.Counter} CT" : unit.Vitals.Critical ? "CRITICAL" : "READY", 536, y + 96, 2, Gold);
        }
        var log = interaction.Log.TakeLast(2).ToArray();
        for (int i = 0; i < log.Length; i++) Text(log[i], 520, 490 + i * 20, 1, Muted, 68);
        foreach (var button in Buttons())
        {
            Rect(button.Bounds.X, button.Bounds.Y, button.Bounds.Width, button.Bounds.Height, new(36, 57, 79));
            Text(button.Label, button.Bounds.X + 8, button.Bounds.Y + 10, 2, Color.White);
        }
        Text("ESC CLOSES", 780, 600, 2, Muted);
    }
    private void Rect(int x, int y, int width, int height, Color color)
    { if (width > 0) batch.Draw(pixel, new Rectangle(x, y, width, height), color); }
    private void Text(string text, int x, int y, int scale, Color color, int max = 100)
    {
        foreach (char c in text.ToUpperInvariant().Take(max))
        {
            var rows = TinyFont.Rows(c);
            for (int row = 0; row < rows.Length; row++) for (int col = 0; col < 5; col++)
                if ((rows[row] & (1 << (4 - col))) != 0) Rect(x + col * scale, y + row * scale, scale, scale, color);
            x += 6 * scale;
        }
    }

    private void CheckInput()
    {
        // Scripted presses and pointers enter the same routes as real input. No simulation fields are patched.
        switch (checkStep++)
        {
            case 0: Dispatch(Keys.N); pendingCapture = "turn.png"; break;
            case 1: Dispatch(Keys.N); break; // Rejected while the unit owns its turn.
            case 2:
                Dispatch(Keys.F); CheckSpellSelection(); InjectTile(6, 4); Dispatch(Keys.Enter);
                pendingCapture = "spell-list.png"; break; // Rejected out-of-range aim.
            case 3: InjectTile(4, 2); pendingCapture = "aim.png"; break;
            case 4: Dispatch(Keys.Enter); pendingCapture = "charging.png"; break;
            case 5: Inject(new(400, 565)); break; // Native pointer route to Wait.
            case 6: Dispatch(Keys.N); pendingCapture = "other-turn.png"; break;
            case 7: Dispatch(Keys.F); Dispatch(Keys.W); break; // Guard has no learned spell.
            case 8:
                Dispatch(Keys.N); firstResolution = Checkpoint("first-resolution");
                ValidateFirstCast(); pendingCapture = "resolved.png"; break;
            case 9:
                Dispatch(Keys.R);
                if (OrdinaryMagicSnapshot.Capture(interaction.State) != initial) throw new InvalidOperationException("Restart changed the initial state.");
                restarted = true; break;
            default:
                if (interaction.State.Finished)
                {
                    if (!finalCaptured)
                    {
                        Checkpoint("first-ko"); pendingCapture = "finished.png"; finalCaptured = true;
                        Dispatch(Keys.N); // Closed encounter rejection preserves the final state.
                    }
                    else FinishCheck();
                    return;
                }
                if (interaction.State.Turns.ActiveUnitId is { } active)
                {
                    if (interaction.State.Units.Single(u => u.Id == active).Definition.LearnedSpells.Count > 0 &&
                        !interaction.State.Pending.ContainsKey(active))
                    {
                        Dispatch(Keys.F); InjectTile(4, 2); Dispatch(Keys.Enter);
                    }
                    Dispatch(Keys.W);
                }
                else Dispatch(Keys.N);
                if (interaction.LastEvents.Any(e => e is OrdinaryMagicEvent.Resolved))
                {
                    casts++;
                    if (casts == 1 && Checkpoint("restarted-resolution") != firstResolution)
                        throw new InvalidOperationException("Restarted cast differed from the first cast.");
                    if (interaction.State.Units[1].Vitals.Critical) pendingCapture = "critical.png";
                }
                break;
        }
        if (checkStep > 100) throw new InvalidOperationException("Native spell check exceeded its event budget.");
    }
    private void ValidateFirstCast()
    {
        var state = interaction.State;
        if (state.Units[1].Vitals.Hp != 146 || state.Units[0].Current.Mp != 69 || state.Units[0].Level != 11 ||
            state.Units[0].Experience != 0 || state.Turns.RngState.DrawCount != 12 || !state.Pending.IsEmpty)
            throw new InvalidOperationException("Prepared Fire first-cast result differed from the independent expected values.");
    }
    private void CheckSpellSelection()
    {
        string before = OrdinaryMagicSnapshot.Capture(interaction.State);
        var order = interaction.AvailableSpells;
        for (int i = 0; i < order.Length; i++)
        {
            if (interaction.SelectedSpell != order[i]) throw new InvalidOperationException("Player spell order differs from prepared learned slots.");
            spellSelectionsChecked++; Dispatch(Keys.Tab);
        }
        Inject(new(600, 196 + (order.Length - 1) * 22 + 10));
        if (interaction.SelectedSpell != order[^1]) throw new InvalidOperationException("Spell pointer selection failed.");
        Inject(new(600, 206));
        if (interaction.SelectedSpell != order[0] || OrdinaryMagicSnapshot.Capture(interaction.State) != before)
            throw new InvalidOperationException("Spell browsing changed gameplay state.");
    }
    private string Checkpoint(string name)
    {
        string snapshot = OrdinaryMagicSnapshot.Capture(interaction.State);
        string replay = OrdinaryMagicSnapshot.Capture(OrdinaryMagicSnapshot.Replay(interaction.State.Definition, interaction.Journal));
        if (snapshot != replay) throw new InvalidOperationException("Native Fire and headless command replay differed.");
        checkpoints.Add(new { name, snapshot = JsonSerializer.Deserialize<JsonElement>(snapshot), replayMatched = true,
            attempts = interaction.Journal.Select(c => new { kind = c.GetType().Name, command = JsonSerializer.SerializeToElement(c, c.GetType()) }) });
        return snapshot;
    }
    private void FinishCheck()
    {
        if (!resizeChecked || !restarted || !interaction.State.Finished || casts != 4 || interaction.State.Units[1].Vitals.Hp != 0)
            throw new InvalidOperationException("Incomplete native spell check.");
        string final = OrdinaryMagicSnapshot.Capture(interaction.State);
        var report = new
        {
            evidence = "Windows GPU plus programmed key/pointer routes; no physical input injection or original media",
            referenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df", interaction.Content.Ruleset,
            checkpoints, casts, spellSelectionsChecked, restartMatched = true, renderingPreservedState = true, resizeChecked, frames,
            finalSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(final))),
            adapter = GraphicsAdapter.DefaultAdapter.Description,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };
        using var file = new FileStream(Path.Combine(checkDirectory!, "check.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(file, report, new JsonSerializerOptions { WriteIndented = true });
        CheckCompleted = true; Console.WriteLine("Native Fire check passed: charge, HP/MP/rewards/growth, restart, first KO and exact headless replay."); Exit();
    }

    private void Baseline() => input.Baseline(Keyboard.GetState().GetPressedKeys(), Mouse.GetState(Window).LeftButton == ButtonState.Pressed);
    private void Focus(object? sender, EventArgs e) => Baseline();
    private void Blur(object? sender, EventArgs e) => input.ClearQueued();
    private void KeyDown(object? sender, InputKeyEventArgs e) { if (checkDirectory is null && IsActive) input.KeyDown(e.Key); }
    private void KeyUp(object? sender, InputKeyEventArgs e) => input.KeyUp(e.Key);
    protected override void UnloadContent() { scene?.Dispose(); pixel?.Dispose(); batch?.Dispose(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Window.KeyDown -= KeyDown; Window.KeyUp -= KeyUp; Activated -= Focus; Deactivated -= Blur; }
        base.Dispose(disposing);
    }
}
