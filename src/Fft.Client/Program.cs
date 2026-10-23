using Fft.Content;
using Fft.Core;
using Fft.Content.Samples;
using System.Text.Json;

if (args.Length == 0)
{
    Console.WriteLine("FFT Native C# reimplementation bootstrap.");
    return 0;
}

if (args.Length == 2 && args[0] == "--scenario" && args[1] == "two-unit-battle")
{
    var definition = SyntheticTwoUnitBattle.Create();
    var state = definition.Start();
    Console.WriteLine("Synthetic two-unit battle: first KO completes the scenario (invented objective).");
    Console.WriteLine($"Movement: {definition.MovementPolicy}; seed: {state.Battle.TurnBattle.Turns.RngState.Value}.");
    int index = 0;
    foreach (var command in SyntheticTwoUnitBattle.Commands)
    {
        var result = TwoUnitBattleScenario.Apply(state, command);
        Console.WriteLine($"{index++}: {command} => {(result.Accepted ? "accepted" : "rejected")}");
        if (!result.Accepted)
        {
            Console.Error.WriteLine($"Scenario command failed: {result.ScenarioRejection}/{result.AttackRejection}/{result.TurnRejection}/{result.MovementRejection}.");
            return 1;
        }
        foreach (var ev in result.Events) Console.WriteLine($"  {ev}");
        state = result.State;
        Console.WriteLine($"  HP: {string.Join(", ", state.Battle.Combatants.Select(unit => $"{unit.UnitId}={unit.Hp}"))}; " +
            $"ticks={state.Battle.TurnBattle.Turns.Ticks}; RNG={state.Battle.TurnBattle.Turns.RngState.Value}/{state.Battle.TurnBattle.Turns.RngState.DrawCount}.");
    }
    Console.WriteLine($"Outcome: {state.Outcome}; winner: {state.WinnerId}.");
    return state.Outcome == TwoUnitBattleOutcome.FirstUnitWon ? 0 : 1;
}

if (args.Length != 4 || args[0] != "--content-root" || args[2] != "--asset-id")
{
    Console.Error.WriteLine("Usage: Fft.Client [--scenario two-unit-battle | --content-root <directory> --asset-id <namespace:path>]");
    return 2;
}

try
{
    var assetId = new AssetId(args[3]);
    LooseAssetStore content = LooseAssetStore.Load(args[1]);
    byte[] asset = content.ReadAllBytes(assetId);
    Console.WriteLine($"Loaded prepared asset '{assetId}' ({asset.Length} bytes; manifest size and SHA-256 verified).");
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or UnauthorizedAccessException or KeyNotFoundException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
