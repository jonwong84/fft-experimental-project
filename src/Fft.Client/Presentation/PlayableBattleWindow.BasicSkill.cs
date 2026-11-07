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
    private readonly PreparedBasicSkillBattle? basicSkillBattle;
    private ImmutableArray<BasicSkillCheckStep> basicSkillSteps;
    private TwoUnitBattleState basicSkillExpected = null!;
    private readonly List<object> basicSkillReceipts = [];
    private readonly List<string> basicSkillEventTrace = [];
    private bool basicSkillReplayChecked;
    private int basicSkillReplays;
    private bool basicSkillFocusChecked;

    private void InitializeBasicSkillCheck()
    {
        basicSkillSteps = BasicSkillUiCheckPlan.Create(basicSkillBattle!.Definition, basicSkillBattle.SelfAbilityId, basicSkillBattle.TargetedAbilityId);
        basicSkillExpected = basicSkillBattle.Definition.Start();
    }
    private void UpdateBasicSkillCheck()
    {
        if (checkWatch.Elapsed > TimeSpan.FromSeconds(30)) throw new InvalidOperationException("Basic Skill window check exceeded 30 seconds.");
        if (checkStep >= basicSkillSteps.Length || frames < nextStepFrame) return;
        var step = basicSkillSteps[checkStep++];
        string before = Snapshot(interaction.State);
        int historyBefore = interaction.Journal.Length;
        if (step.Focus)
        {
            input.KeyUp(Microsoft.Xna.Framework.Input.Keys.T);
            input.KeyDown(Microsoft.Xna.Framework.Input.Keys.T);
            HandleDeactivated(this, EventArgs.Empty);
            if (input.TryTake(out _)) throw new InvalidOperationException("Focus loss retained queued input.");
            HandleActivated(this, EventArgs.Empty);
            input.Baseline([Microsoft.Xna.Framework.Input.Keys.T], true);
            input.KeyDown(Microsoft.Xna.Framework.Input.Keys.T);
            if (input.TryTake(out _) || input.LeftPress(true)) throw new InvalidOperationException("Held input repeated across focus baseline.");
            BaselineInput();
            if (Snapshot(interaction.State) != before || interaction.Journal.Length != historyBefore) throw new InvalidOperationException("Focus changed battle/history.");
            basicSkillFocusChecked = true;
        }
        if (step.Resize)
        {
            graphics.PreferredBackBufferWidth = 1280; graphics.PreferredBackBufferHeight = 720; graphics.ApplyChanges();
            if (Snapshot(interaction.State) != before || interaction.Journal.Length != historyBefore) throw new InvalidOperationException("Basic Skill resize changed battle state/history.");
            resizeChecked = true;
        }
        if (step.Restart)
        {
            var replay = basicSkillBattle!.Definition.Start(); var replayEvents = new List<string>();
            foreach (var command in interaction.Journal)
            {
                var result = TwoUnitBattleScenario.Apply(replay, command); replay = result.State;
                replayEvents.AddRange(result.Events.Select(value => value.ToString()!));
            }
            if (Snapshot(replay) != before || !replayEvents.SequenceEqual(basicSkillEventTrace)) throw new InvalidOperationException("Basic Skill journal replay diverged.");
            basicSkillReplayChecked = true;
            basicSkillReplays++;
            basicSkillEventTrace.Clear();
            basicSkillExpected = basicSkillBattle.Definition.Start();
        }
        TwoUnitBattleResult? expected = null;
        if (step.Command is not null)
        {
            expected = TwoUnitBattleScenario.Apply(basicSkillExpected, step.Command);
            basicSkillExpected = expected.State;
        }
        int journalBefore = interaction.Journal.Length;
        if (step.Input.Key is { } key) DispatchKey(key);
        if (step.Input.Click is { } click) InjectCanvasClick(click);
        if (Snapshot(interaction.State) != Snapshot(basicSkillExpected)) throw new InvalidOperationException($"Basic Skill UI diverged at step {checkStep}.");
        if (step.Restart)
        {
            if (!interaction.Journal.IsEmpty || interaction.SelectedUnit is not null) throw new InvalidOperationException("Restart retained history/selection.");
        }
        else if (expected is not null)
        {
            if (interaction.Journal.Length != journalBefore + 1 || interaction.Journal[^1] != step.Command ||
                interaction.LastResult!.Accepted != expected.Accepted ||
                !interaction.LastResult.Events.SequenceEqual(expected.Events))
                throw new InvalidOperationException($"Basic Skill routed command/events diverged at step {checkStep}.");
            basicSkillEventTrace.AddRange(expected.Events.Select(value => value.ToString()!));
        }
        else if (interaction.Journal.Length != journalBefore) throw new InvalidOperationException("Selection changed command history.");
        string priorProbe = Snapshot(interaction.State); int priorCount = interaction.Journal.Length;
        bool available = interaction.CanUseSelfAbility;
        bool targetedAvailable = interaction.CanUseTargetedAbility;
        var targets = interaction.TargetedTargets;
        var range = interaction.TargetedRange;
        if (priorProbe != Snapshot(interaction.State) || priorCount != interaction.Journal.Length) throw new InvalidOperationException("Basic Skill availability mutated state/history.");
        basicSkillReceipts.Add(new
        {
            step = checkStep, key = step.Input.Key?.ToString(), step.Input.Click, step.Input.Capture,
            step.Restart, step.Resize, step.Focus, accepted = expected?.Accepted,
            kind = step.Command?.GetType().Name,
            rejection = expected is null || expected.Accepted ? null : $"{expected.ScenarioRejection}/{expected.AttackRejection}/{expected.TurnRejection}/{expected.MovementRejection}",
            command = step.Command is null ? (JsonElement?)null : JsonSerializer.SerializeToElement(step.Command, step.Command.GetType()),
            events = expected?.Events.Select(value => value.ToString()).ToArray(),
            state = JsonSerializer.Deserialize<JsonElement>(Snapshot(interaction.State)),
            interaction.SelectedUnit, interaction.Mode, interaction.Status, interaction.StatusIsError, available, targetedAvailable, targets, range,
        });
        pendingCapture = step.Input.Capture; nextStepFrame = frames + 1;
    }
    private void FinishBasicSkillCheck()
    {
        if (!basicSkillReplayChecked || basicSkillReplays != 2 || !basicSkillFocusChecked || !resizeChecked || Snapshot(interaction.State) != initialBattle)
            throw new InvalidOperationException("Basic Skill check missed replay/resize/restart.");
        var pp = GraphicsDevice.PresentationParameters;
        string final = Snapshot(interaction.State);
        var report = new
        {
            format = "fft.basic-skill-window-check", schemaVersion = 1,
            evidence = "authored Windows GPU and programmed keyboard/canvas routes; not physical input or original equivalence",
            metadataSha256 = basicSkillBattle!.MetadataSha256, effectsSha256 = basicSkillBattle.EffectsSha256, targetedSha256 = basicSkillBattle.TargetedEffectsSha256,
            focusBaselineChecked = basicSkillFocusChecked, journalReplays = basicSkillReplays,
            headlessMatched = true, replayMatched = basicSkillReplayChecked, resizeChecked, renderingPreservedState = true,
            steps = basicSkillReceipts, frames, final = JsonSerializer.Deserialize<JsonElement>(final),
            finalSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(final))),
            adapter = GraphicsAdapter.DefaultAdapter.Description,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            backbuffer = new[] { pp.BackBufferWidth, pp.BackBufferHeight },
        };
        using var output = new FileStream(Path.Combine(checkDirectory!, "check.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(output, report, new JsonSerializerOptions { WriteIndented = true });
        CheckCompleted = true;
        Console.WriteLine($"Basic Skill UI check passed: {checkStep} steps, exact headless/replay and restart; {checkDirectory}");
    }
}
