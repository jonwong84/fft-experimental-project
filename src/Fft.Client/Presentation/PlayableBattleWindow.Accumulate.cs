using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

internal sealed partial class PlayableBattleWindow
{
    private readonly PreparedAccumulateBattle? abilityBattle;
    private ImmutableArray<AccumulateCheckStep> abilitySteps;
    private TwoUnitBattleState abilityExpected = null!;
    private readonly List<object> abilityReceipts = [];
    private readonly List<string> abilityEventTrace = [];
    private bool abilityReplayChecked;

    private void InitializeAbilityCheck()
    {
        abilitySteps = AccumulateUiCheckPlan.Create(abilityBattle!.Definition, abilityBattle.AbilityId);
        abilityExpected = abilityBattle.Definition.Start();
    }
    private void UpdateAbilityCheck()
    {
        if (checkWatch.Elapsed > TimeSpan.FromSeconds(30)) throw new InvalidOperationException("Ability window check exceeded 30 seconds.");
        if (checkStep >= abilitySteps.Length || frames < nextStepFrame) return;
        var step = abilitySteps[checkStep++];
        string before = Snapshot(interaction.State);
        if (step.Resize)
        {
            graphics.PreferredBackBufferWidth = 1280; graphics.PreferredBackBufferHeight = 720; graphics.ApplyChanges();
            if (Snapshot(interaction.State) != before) throw new InvalidOperationException("Ability resize changed battle state.");
            resizeChecked = true;
        }
        if (step.Restart)
        {
            var replay = abilityBattle!.Definition.Start(); var replayEvents = new List<string>();
            foreach (var command in interaction.Journal)
            {
                var result = TwoUnitBattleScenario.Apply(replay, command); replay = result.State;
                replayEvents.AddRange(result.Events.Select(value => value.ToString()!));
            }
            if (Snapshot(replay) != before || !replayEvents.SequenceEqual(abilityEventTrace)) throw new InvalidOperationException("Ability journal replay diverged.");
            abilityReplayChecked = true;
            abilityExpected = abilityBattle.Definition.Start();
        }
        TwoUnitBattleResult? expected = null;
        if (step.Command is not null)
        {
            expected = TwoUnitBattleScenario.Apply(abilityExpected, step.Command);
            abilityExpected = expected.State;
        }
        int journalBefore = interaction.Journal.Length;
        if (step.Input.Key is { } key) DispatchKey(key);
        if (step.Input.Click is { } click) InjectCanvasClick(click);
        if (Snapshot(interaction.State) != Snapshot(abilityExpected)) throw new InvalidOperationException($"Ability UI diverged at step {checkStep}.");
        if (step.Restart)
        {
            if (!interaction.Journal.IsEmpty || interaction.SelectedUnit is not null) throw new InvalidOperationException("Restart retained history/selection.");
        }
        else if (expected is not null)
        {
            if (interaction.Journal.Length != journalBefore + 1 || interaction.Journal[^1] != step.Command ||
                interaction.LastResult!.Accepted != expected.Accepted ||
                !interaction.LastResult.Events.SequenceEqual(expected.Events))
                throw new InvalidOperationException($"Ability routed command/events diverged at step {checkStep}.");
            abilityEventTrace.AddRange(expected.Events.Select(value => value.ToString()!));
        }
        else if (interaction.Journal.Length != journalBefore) throw new InvalidOperationException("Selection changed command history.");
        string priorProbe = Snapshot(interaction.State); int priorCount = interaction.Journal.Length;
        bool available = interaction.CanUseSelfAbility;
        if (priorProbe != Snapshot(interaction.State) || priorCount != interaction.Journal.Length) throw new InvalidOperationException("Ability availability mutated state/history.");
        abilityReceipts.Add(new
        {
            step = checkStep, key = step.Input.Key?.ToString(), step.Input.Click, step.Input.Capture,
            step.Restart, step.Resize, accepted = expected?.Accepted,
            command = step.Command is null ? (JsonElement?)null : JsonSerializer.SerializeToElement(step.Command, step.Command.GetType()),
            events = expected?.Events.Select(value => value.ToString()).ToArray(),
            state = JsonSerializer.Deserialize<JsonElement>(Snapshot(interaction.State)),
            interaction.SelectedUnit, interaction.Mode, interaction.Status, interaction.StatusIsError, available,
        });
        pendingCapture = step.Input.Capture; nextStepFrame = frames + 1;
    }
    private void FinishAbilityCheck()
    {
        if (!abilityReplayChecked || !resizeChecked || Snapshot(interaction.State) != initialBattle)
            throw new InvalidOperationException("Ability check missed replay/resize/restart.");
        var pp = GraphicsDevice.PresentationParameters;
        string final = Snapshot(interaction.State);
        var report = new
        {
            format = "fft.accumulate-window-check", schemaVersion = 1,
            evidence = "authored Windows GPU and programmed keyboard/canvas routes; not physical input or original equivalence",
            metadataSha256 = abilityBattle!.MetadataSha256, effectsSha256 = abilityBattle.EffectsSha256,
            headlessMatched = true, replayMatched = abilityReplayChecked, resizeChecked, renderingPreservedState = true,
            steps = abilityReceipts, frames, final = JsonSerializer.Deserialize<JsonElement>(final),
            finalSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(final))),
            adapter = GraphicsAdapter.DefaultAdapter.Description,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            backbuffer = new[] { pp.BackBufferWidth, pp.BackBufferHeight },
        };
        using var output = new FileStream(Path.Combine(checkDirectory!, "check.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(output, report, new JsonSerializerOptions { WriteIndented = true });
        CheckCompleted = true;
        Console.WriteLine($"Accumulate UI check passed: {checkStep} steps, exact headless/replay and restart; {checkDirectory}");
    }
}
