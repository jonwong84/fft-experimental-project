using System.Text.Json;
using Fft.Content.Samples;
using Fft.Content.Saves;
using Fft.Core;

namespace Fft.Client;

/// <summary>Finite, headless developer proof. No interactive UI or save-slot policy is introduced.</summary>
public static class BattleSaveCheck
{
    public static int Run(string directory)
    {
        try
        {
            string output = Path.GetFullPath(directory);
            if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Save-check output must be a new directory.");
            Directory.CreateDirectory(output);
            var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
            var uninterrupted = profile.Definition.Start();
            for (int checkpoint = 0; checkpoint <= SyntheticTwoUnitBattle.Commands.Length; checkpoint++)
            {
                string path = Path.Combine(output, $"checkpoint-{checkpoint:D2}.json");
                TwoUnitBattleSave.Save(profile, uninterrupted, path);
                var selected = SyntheticTwoUnitBattle.CreateSaveProfile();
                var resumed = TwoUnitBattleSave.Load(selected, path).State;
                var expected = uninterrupted;
                if (TwoUnitBattleSave.ToJson(profile, expected) != TwoUnitBattleSave.ToJson(selected, resumed))
                    throw new InvalidDataException($"Checkpoint {checkpoint} did not restore identically.");
                for (int index = checkpoint; index < SyntheticTwoUnitBattle.Commands.Length; index++)
                {
                    var left = TwoUnitBattleScenario.Apply(expected, SyntheticTwoUnitBattle.Commands[index]);
                    var right = TwoUnitBattleScenario.Apply(resumed, SyntheticTwoUnitBattle.Commands[index]);
                    if (!left.Accepted || !right.Accepted || !left.Events.SequenceEqual(right.Events) ||
                        TwoUnitBattleSave.ToJson(profile, left.State) != TwoUnitBattleSave.ToJson(selected, right.State))
                        throw new InvalidDataException($"Checkpoint {checkpoint} continuation diverged at command {index}.");
                    expected = left.State; resumed = right.State;
                }
                if (checkpoint < SyntheticTwoUnitBattle.Commands.Length)
                    uninterrupted = TwoUnitBattleScenario.Apply(uninterrupted, SyntheticTwoUnitBattle.Commands[checkpoint]).State;
            }
            var turns = uninterrupted.Battle.TurnBattle.Turns;
            File.WriteAllText(Path.Combine(output, "check.json"), JsonSerializer.Serialize(new
            {
                evidence = "synthetic managed save/load; no original media or memory-card compatibility",
                schemaVersion = TwoUnitBattleSave.SchemaVersion, profileId = profile.Id,
                profileVersion = profile.Version, contentVersion = profile.ContentVersion,
                checkpoints = 13, continuationStateAndEventsMatched = true,
                outcome = uninterrupted.Outcome.ToString(), hp = uninterrupted.Battle.Combatants.Select(unit => unit.Hp),
                ticks = turns.Ticks, rngWord = turns.RngState.Value, rngDrawCount = turns.RngState.DrawCount,
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine($"Save/load check passed: 13 checkpoints; outcome {uninterrupted.Outcome}; ticks {turns.Ticks}; RNG {turns.RngState.Value}/{turns.RngState.DrawCount}.");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or BattleSaveValidationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
