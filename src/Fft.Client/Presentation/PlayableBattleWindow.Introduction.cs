using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

internal sealed partial class PlayableBattleWindow
{
    private readonly BannerReadySession? preparedIntroduction;
    private readonly BannerReadyPreviewOptions? introductionOptions;
    private readonly DarkScreenIntroductionSession? preparedDarkIntroduction;
    private readonly DarkScreenIntroductionOptions? darkIntroductionOptions;
    private readonly System.Diagnostics.Stopwatch checkWatch = System.Diagnostics.Stopwatch.StartNew();
    private IWindowIntroduction? introduction;
    private Texture2D? introTexture;
    private RenderTarget2D? introTarget;
    private long introPainted = -1;
    private string initialBattle = "";
    private bool introResourcesRebuilt, introFocusChecked, battleFocusChecked, heldInputChecked;
    private readonly List<object> introCaptures = [];

    private void DispatchKey(Keys key)
    {
        if (introduction is null) router.Key(key); else introduction.Key(key);
    }
    private bool UpdateIntroduction()
    {
        if (introduction is null || introduction.BattleAvailable) return false;
        if (preparedDarkIntroduction is not null && checkDirectory is not null && checkWatch.Elapsed > TimeSpan.FromSeconds(120))
            throw new InvalidOperationException("Combined introduction exceeded 120 seconds.");
        if (checkDirectory is null && !IsActive) { input.ClearQueued(); return true; }
        // Host cadence only: each accepted tick is presented before another is requested.
        if (introPainted != introduction.Tick) return true;
        bool circle = false;
        while (input.TryTake(out var key)) circle |= key == Keys.C;
        if (checkDirectory is not null) circle = (darkIntroductionOptions?.CircleTicks ?? introductionOptions!.CircleTicks).Contains(checked((int)introduction.Tick + 1));
        if (introduction.Tick >= 4096) throw new InvalidOperationException("Introduction exceeded its finite tick budget.");
        bool becameAvailable = introduction.Advance(circle);
        AssertFrozenBattle();
        if (becameAvailable)
        {
            BaselineInput(); // Drop every queued intro press and baseline currently held physical buttons.
            if (checkDirectory is not null)
            {
                CheckHeldInputBoundary(); CheckFocusBoundary(); battleFocusChecked = true;
                pendingCapture = "start.png"; nextStepFrame = frames + 16;
            }
        }
        return true; // Even on the handoff tick, this update cannot dispatch a battle command.
    }

    private void AssertFrozenBattle()
    {
        if (Snapshot(interaction.State) != initialBattle || !interaction.Journal.IsEmpty || interaction.SelectedUnit is not null ||
            interaction.Mode != BattleInteractionMode.Inspect || router.Cursor != new TilePosition(0, 0, 0))
            throw new InvalidOperationException("Introduction changed battle state or command/selection history.");
    }
    private string OwnershipSnapshot() => JsonSerializer.Serialize(new
    {
        gate = introduction?.Ownership, introduction?.AvailabilityPublications, introduction?.AvailabilityTick,
        battle = Snapshot(interaction.State), interaction.SelectedUnit, interaction.Mode, interaction.Journal, router.Cursor,
        trace = introduction?.TraceMarker,
    });
    private void CheckFocusBoundary()
    {
        string before = OwnershipSnapshot();
        HandleDeactivated(this, EventArgs.Empty); HandleActivated(this, EventArgs.Empty);
        if (before != OwnershipSnapshot()) throw new InvalidOperationException("Focus handling changed ownership or simulation.");
    }
    private void CheckHeldInputBoundary()
    {
        string before = OwnershipSnapshot();
        input.KeyDown(Keys.N); input.KeyDown(Keys.R);
        input.Baseline([Keys.N], true);
        input.KeyDown(Keys.N);
        if (input.TryTake(out _) || input.LeftPress(true)) throw new InvalidOperationException("Held input crossed the ownership boundary.");
        input.KeyUp(Keys.N); input.LeftPress(false); input.KeyDown(Keys.N);
        if (!input.TryTake(out var key) || key != Keys.N || !input.LeftPress(true))
            throw new InvalidOperationException("Fresh input was not rearmed after release.");
        BaselineInput(); // This programmed probe verifies routing readiness without submitting its test press.
        if (before != OwnershipSnapshot()) throw new InvalidOperationException("Input baseline changed simulation.");
        heldInputChecked = true;
    }

    private bool DrawIntroduction()
    {
        if (introduction is null) return false;
        bool active = !introduction.BattleAvailable;
        long tick = introduction.Tick;
        if (!active && introPainted == tick) return false;
        string before = OwnershipSnapshot();
        if (checkDirectory is not null && tick == 5 && !introResourcesRebuilt)
        { introduction.Recreate(CreateResources); introResourcesRebuilt = true; }
        if (checkDirectory is not null && tick == 6 && !introFocusChecked)
        { CheckFocusBoundary(); introFocusChecked = true; }
        AssertFrozenBattle();
        byte[] pixels = introduction.Compose(); introTexture!.SetData(pixels);
        GraphicsDevice.SetRenderTarget(introTarget); GraphicsDevice.Clear(Color.Black);
        batch!.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(introTexture, Vector2.Zero, Color.White); batch.End(); GraphicsDevice.SetRenderTarget(null);
        if (checkDirectory is not null && introPainted != tick)
        {
            byte[] readback = new byte[pixels.Length]; introTarget!.GetData(readback);
            if (!pixels.AsSpan().SequenceEqual(readback)) throw new InvalidOperationException("Introduction GPU readback differs from CPU.");
            string file = $"intro/tick-{tick:D3}.png";
            using (var stream = File.Create(Path.Combine(checkDirectory, file))) introTarget.SaveAsPng(stream, 256, 240);
            introCaptures.Add(introduction.Capture(file, Convert.ToHexStringLower(SHA256.HashData(pixels)),
                Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(initialBattle)))));
        }
        if (active)
        {
            GraphicsDevice.Clear(Color.Black);
            var pp = GraphicsDevice.PresentationParameters;
            int scale = Math.Max(1, Math.Min(pp.BackBufferWidth / 256, pp.BackBufferHeight / 240));
            batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
            batch.Draw(introTarget!, new Rectangle((pp.BackBufferWidth - 256*scale)/2, (pp.BackBufferHeight - 240*scale)/2,
                256*scale, 240*scale), Color.White); batch.End();
        }
        introPainted = tick;
        if (before != OwnershipSnapshot()) throw new InvalidOperationException("Introduction drawing changed ownership or simulation.");
        return active;
    }

    private object IntroductionReceipt(object battle)
    {
        if (introduction is null || !introduction.BattleAvailable || introduction.AvailabilityPublications != 1 ||
            introCaptures.Count != introduction.AvailabilityTick + 1 || !introResourcesRebuilt || !introFocusChecked || !battleFocusChecked || !heldInputChecked)
            throw new InvalidOperationException("Introduction check did not satisfy ownership/input/presentation boundaries.");
        if (preparedDarkIntroduction is not null) return DarkIntroductionReceipt(battle);
        return new
        {
            format = "fft.introduction-battle-capture", schemaVersion = 1, definition = preparedIntroduction!.Definition,
            circleTicks = introductionOptions!.CircleTicks, captures = introCaptures,
            availabilityTick = introduction.AvailabilityTick, availabilityPublications = introduction.AvailabilityPublications,
            initialBattle = JsonSerializer.Deserialize<JsonElement>(initialBattle), finalIntroduction = introduction.Ownership,
            introductionTraceSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(introduction.Trace))),
            battleFrozenThroughHandoff = true, transitionSampleForwarded = false, rendererTaskAcknowledgments = 0,
            resourcesRebuiltDuringIntroduction = introResourcesRebuilt, focusDuringIntroduction = introFocusChecked,
            focusAfterHandoff = battleFocusChecked, heldInputBoundaryChecked = heldInputChecked,
            inputCheckScope = "Programmed calls through production latch/focus handlers; no physical input injection or latency claim",
            cadence = "Authored explicit ticks, one per presented host update; no original duration claim", battle,
            scope = "Authored prepared introduction plus synthetic battle; no retail EventEnd, original startup or original control-timing equivalence",
        };
    }

    private object DarkIntroductionReceipt(object battle) => new
    {
        format = "fft.dark-screen-introduction-battle-capture", schemaVersion = 1,
        definition = preparedDarkIntroduction!.Definition, darkDefinition = preparedDarkIntroduction.Dark,
        circleTicks = darkIntroductionOptions!.CircleTicks, captures = introCaptures,
        availabilityTick = introduction!.AvailabilityTick, availabilityPublications = introduction.AvailabilityPublications,
        initialBattle = JsonSerializer.Deserialize<JsonElement>(initialBattle), finalIntroduction = introduction.Ownership,
        introductionTraceSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(introduction.Trace))),
        battleFrozenThroughHandoff = true, transitionSampleForwarded = false, rendererTaskAcknowledgments = 0,
        resourcesRebuiltDuringIntroduction = introResourcesRebuilt, focusDuringIntroduction = introFocusChecked,
        focusAfterHandoff = battleFocusChecked, heldInputBoundaryChecked = heldInputChecked,
        inputCheckScope = "Programmed production latch/focus calls; no physical input injection or latency claim",
        cadence = "Authored explicit ticks, one per presented update; no original duration claim",
        composition = "fresh-rgb5-checker/dark/general-or-ready", gpuReadbackPerformed = true,
        elapsedSeconds = checkWatch.Elapsed.TotalSeconds, battle,
        scope = "Authored combined introduction and synthetic battle; no retail EventEnd or original startup equivalence",
    };
}
