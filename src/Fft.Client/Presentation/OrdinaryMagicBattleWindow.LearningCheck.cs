using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

internal sealed partial class OrdinaryMagicBattleWindow
{
    private int learningStage;
    private string? learningOffer;

    private void CheckLearningInput()
    {
        if (checkStep > 100) throw new InvalidOperationException("Learning check exceeded its event budget.");
        switch (checkStep++)
        {
            case 0: Dispatch(Keys.N); pendingCapture = "turn.png"; return;
            case 1: Dispatch(Keys.N); return;
            case 2:
                Dispatch(Keys.F); CheckSpellSelection(); Dispatch(Keys.Tab);
                InjectTile(6, 4); Dispatch(Keys.Enter); pendingCapture = "spell-list.png"; return;
            case 3: InjectTile(4, 2); pendingCapture = "aim.png"; return;
            case 4: Dispatch(Keys.Enter); pendingCapture = "charging.png"; return;
            case 5: Inject(new(400, 565)); return;
        }
        var fire4 = new AssetId("fft.ps1:ability/source-0019");
        var learnerId = new BattleUnitId("guard");
        switch (learningStage)
        {
            case 0:
            case 3:
                if (interaction.State.LearningReport is null) { PumpLearningClock(); return; }
                var state = interaction.State;
                if (state.Units[1].Vitals.Hp != 75 || state.Units[0].Current.Mp != 27 || state.Units[0].Level != 11 ||
                    state.Turns.RngState.DrawCount != 14 || state.LearningReport!.AbilityId != fire4 ||
                    state.LearningReport.LearnerId != learnerId || state.Units[1].OutcomeMarker != AbilityOutcomeMarker.LearnChecked)
                    throw new InvalidOperationException("Prepared Fire4 learning offer differed from independent values.");
                string offered = Checkpoint(learningStage == 0 ? "offered-before-decline" : "offered-after-restart");
                if (learningStage == 0) learningOffer = offered;
                else if (offered != learningOffer) throw new InvalidOperationException("Restart changed the learning offer.");
                pendingCapture = learningStage == 0 ? "learning-offer.png" : "learning-offer-restarted.png";
                learningStage++; return;
            case 1:
                string before = OrdinaryMagicSnapshot.Capture(interaction.State);
                Dispatch(Keys.N); Dispatch(Keys.F); Dispatch(Keys.M); Dispatch(Keys.W);
                if (OrdinaryMagicSnapshot.Capture(interaction.State) != before)
                    throw new InvalidOperationException("An open report allowed an unrelated command.");
                Inject(new(570, 608)); // Resized pointer route to Back/Decline.
                if (interaction.State.LearningReport is not null || OrdinaryMagicBattle.KnownSpells(interaction.State, learnerId).Contains(fire4) ||
                    interaction.State.Turns.RngState.DrawCount != 14)
                    throw new InvalidOperationException("Decline learned or rerolled the ability.");
                Checkpoint("declined"); pendingCapture = "declined.png"; learningStage++; return;
            case 2:
                Dispatch(Keys.R);
                if (OrdinaryMagicSnapshot.Capture(interaction.State) != initial)
                    throw new InvalidOperationException("Learning restart changed initial state.");
                restarted = true;
                Dispatch(Keys.N); Dispatch(Keys.F); Dispatch(Keys.Tab); InjectTile(4, 2); Dispatch(Keys.Enter); Dispatch(Keys.W);
                learningStage++; return;
            case 4:
                Inject(new(360, 608)); // Same native pointer route to Enter/Accept.
                if (interaction.State.LearningReport is not null || !OrdinaryMagicBattle.KnownSpells(interaction.State, learnerId).Contains(fire4) ||
                    interaction.State.Turns.RngState.DrawCount != 14)
                    throw new InvalidOperationException("Acceptance failed or consumed RNG.");
                Checkpoint("accepted"); pendingCapture = "accepted.png"; learningStage++; return;
            case 5:
                if (interaction.State.Turns.ActiveUnitId != learnerId) { PumpLearningClock(); return; }
                Dispatch(Keys.F);
                if (interaction.SelectedSpell != fire4 || interaction.AvailableSpells.Length != 1)
                    throw new InvalidOperationException("The newly learned spell was absent from the player's normal list.");
                InjectTile(0, 2); Dispatch(Keys.Enter); Dispatch(Keys.W);
                learningStage++; pendingCapture = "learned-spell-charging.png"; return;
            case 6:
                PumpLearningClock();
                if (!interaction.LastEvents.OfType<OrdinaryMagicEvent.Resolved>().Any(e => e.UnitId == learnerId)) return;
                if (interaction.State.Units[0].Vitals.Hp != 22 || interaction.State.Units[1].Current.Mp != 27 ||
                    interaction.State.LearningReport is not null)
                    throw new InvalidOperationException("The learned spell did not execute normally.");
                Checkpoint("learned-spell-resolved"); pendingCapture = "learned-spell-resolved.png"; learningStage++; return;
            case 7: FinishLearningCheck(); return;
        }
    }

    private void PumpLearningClock()
    {
        if (interaction.State.LearningReport is not null)
            throw new InvalidOperationException("Unexpected additional learning report.");
        Dispatch(interaction.State.Turns.ActiveUnitId is null ? Keys.N : Keys.W);
        if (interaction.IsError) throw new InvalidOperationException(interaction.Message);
    }

    private void FinishLearningCheck()
    {
        if (!resizeChecked || !restarted || !movementChecked || learningOffer is null)
            throw new InvalidOperationException("Incomplete native learning check.");
        string final = OrdinaryMagicSnapshot.Capture(interaction.State);
        var report = new
        {
            evidence = "Windows GPU plus programmed key/pointer routes; no physical input injection or original media",
            referenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df", interaction.Content.Ruleset,
            scenario = "fire4-learn-on-hit", checkpoints, spellSelectionsChecked, movementChecked,
            acceptedAndDeclined = true, newlyLearnedSpellExecuted = true, restartMatched = true,
            renderingPreservedState = true, resizeChecked, frames,
            finalSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(final))),
            adapter = GraphicsAdapter.DefaultAdapter.Description,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };
        using var file = new FileStream(Path.Combine(checkDirectory!, "check.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(file, report, new JsonSerializerOptions { WriteIndented = true });
        CheckCompleted = true;
        Console.WriteLine("Native ordinary learning check passed: decline, accept, pause, restart, learned cast and exact replay.");
        Exit();
    }
}
