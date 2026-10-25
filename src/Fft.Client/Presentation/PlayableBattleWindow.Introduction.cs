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
    private IntroductionBattleSession? introduction;
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
        if (introduction is null || introduction.Gate.BattleAvailable) return false;
        if (checkDirectory is null && !IsActive) { input.ClearQueued(); return true; }
        var state = introduction.Gate.Introduction;
        // Host cadence only: each accepted tick is presented before another is requested.
        if (introPainted != state.Event.Tick) return true;
        bool circle = false;
        while (input.TryTake(out var key)) circle |= key == Keys.C;
        if (checkDirectory is not null) circle = introductionOptions!.CircleTicks.Contains(checked((int)state.Event.Tick + 1));
        if (state.Event.Tick >= 4096) throw new InvalidOperationException("Introduction exceeded its finite tick budget.");
        var result = introduction.AdvanceIntroduction(circle);
        if (!result.Accepted) throw new InvalidOperationException("Introduction rejected: " + result.Rejection);
        AssertFrozenBattle();
        if (result.BattleBecameAvailable)
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
        gate = introduction?.Gate, introduction?.AvailabilityPublications, introduction?.AvailabilityTick,
        battle = Snapshot(interaction.State), interaction.SelectedUnit, interaction.Mode, interaction.Journal, router.Cursor,
        trace = introduction?.IntroductionTrace,
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
        bool active = !introduction.Gate.BattleAvailable;
        var state = introduction.Gate.Introduction;
        if (!active && introPainted == state.Event.Tick) return false;
        string before = OwnershipSnapshot();
        if (checkDirectory is not null && state.Event.Tick == 5 && !introResourcesRebuilt)
        { CreateResources(); introResourcesRebuilt = true; }
        if (checkDirectory is not null && state.Event.Tick == 6 && !introFocusChecked)
        { CheckFocusBoundary(); introFocusChecked = true; }
        AssertFrozenBattle();
        byte[] pixels = introduction.ComposeIntroduction(); introTexture!.SetData(pixels);
        GraphicsDevice.SetRenderTarget(introTarget); GraphicsDevice.Clear(Color.Black);
        batch!.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(introTexture, Vector2.Zero, Color.White); batch.End(); GraphicsDevice.SetRenderTarget(null);
        if (checkDirectory is not null && introPainted != state.Event.Tick)
        {
            byte[] readback = new byte[pixels.Length]; introTarget!.GetData(readback);
            if (!pixels.AsSpan().SequenceEqual(readback)) throw new InvalidOperationException("Introduction GPU readback differs from CPU.");
            string file = $"intro/tick-{state.Event.Tick:D3}.png";
            using (var stream = File.Create(Path.Combine(checkDirectory, file))) introTarget.SaveAsPng(stream, 256, 240);
            introCaptures.Add(new
            {
                tick = state.Event.Tick, file, rgbaSha256 = Convert.ToHexStringLower(SHA256.HashData(pixels)),
                stage = state.Ready is null ? "general" : "ready", general = state.General?.Phase.ToString(), ready = state.Ready?.ChildPhase.ToString(),
                generalReveal = state.General?.RevealCompleted, generalHold = state.General?.HoldCompleted, generalHide = state.General?.HideCompleted,
                readyScale = state.Ready?.DrawScale, totalTasks = state.Event.Tasks.TotalStarted, liveTasks = state.Event.Tasks.LiveTasks,
                state.Event.Exited, battleAvailable = introduction.Gate.BattleAvailable,
                battleSha256 = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(initialBattle))),
                journalEntries = interaction.Journal.Length,
            });
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
        introPainted = state.Event.Tick;
        if (before != OwnershipSnapshot()) throw new InvalidOperationException("Introduction drawing changed ownership or simulation.");
        return active;
    }

    private object IntroductionReceipt(object battle)
    {
        if (introduction is null || !introduction.Gate.BattleAvailable || introduction.AvailabilityPublications != 1 ||
            introCaptures.Count != introduction.AvailabilityTick + 1 || !introResourcesRebuilt || !introFocusChecked || !battleFocusChecked || !heldInputChecked)
            throw new InvalidOperationException("Introduction check did not satisfy ownership/input/presentation boundaries.");
        return new
        {
            format = "fft.introduction-battle-capture", schemaVersion = 1, definition = preparedIntroduction!.Definition,
            circleTicks = introductionOptions!.CircleTicks, captures = introCaptures,
            availabilityTick = introduction.AvailabilityTick, availabilityPublications = introduction.AvailabilityPublications,
            initialBattle = JsonSerializer.Deserialize<JsonElement>(initialBattle), finalIntroduction = introduction.Gate,
            introductionTraceSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(introduction.IntroductionTrace))),
            battleFrozenThroughHandoff = true, transitionSampleForwarded = false, rendererTaskAcknowledgments = 0,
            resourcesRebuiltDuringIntroduction = introResourcesRebuilt, focusDuringIntroduction = introFocusChecked,
            focusAfterHandoff = battleFocusChecked, heldInputBoundaryChecked = heldInputChecked,
            inputCheckScope = "Programmed calls through production latch/focus handlers; no physical input injection or latency claim",
            cadence = "Authored explicit ticks, one per presented host update; no original duration claim", battle,
            scope = "Authored prepared introduction plus synthetic battle; no retail EventEnd, original startup or original control-timing equivalence",
        };
    }
}
