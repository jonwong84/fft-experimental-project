using Zodiac.Content;
using Zodiac.Framework;
using Zodiac.Content.Samples;
using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Client;

if (args.Length > 0 && args[0] == "--general-banner") return GeneralBannerPreview.Run(args);
if (args.Length > 0 && args[0] == "--ordered-banner") return OrderedBannerPreview.Run(args);
if (args.Length > 0 && args[0] == "--ready") return ReadyPreview.Run(args);
if (args.Length > 0 && args[0] == "--dark-screen") return DarkScreenPreview.Run(args);
if (args.Length > 0 && args[0] == "--banner-ready-sequence") return BannerReadyPreview.Run(args);
if (args.Length > 0 && args[0] == "--intro-battle") return IntroductionBattleHost.Run(args);
if (args.Length == 1 && args[0] == "--window") return WindowsBattleHost.Run();
if (args.Length == 2 && args[0] == "--window-check") return WindowsBattleHost.Run(args[1]);
if (args.Length == 1 && args[0] == "--battle") return WindowsPlayableBattleHost.Run();
if (args.Length == 2 && args[0] == "--battle-check") return WindowsPlayableBattleHost.Run(args[1]);
if (args.Length > 0 && args[0] == "--prepared-scene") return WindowsPreparedSceneHost.Run(args);
if (args.Length == 2 && args[0] == "--save-check") return BattleSaveCheck.Run(args[1]);

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

if (args.Length != 4 || args[0] != "--content-root" || args[2] is not ("--asset-id" or "--sprite-pose" or "--map"))
{
    Console.Error.WriteLine("Usage: Fft.Client [--battle | --battle-check <new-directory> | --window | --window-check <new-directory> | --prepared-scene --map-root <directory> --pose-root <directory> [--check <new-directory>] | --scenario two-unit-battle | --save-check <new-directory> | --content-root <directory> (--asset-id|--sprite-pose|--map) <namespace:path>]");
    return 2;
}

try
{
    var assetId = new AssetId(args[3]);
    LooseAssetStore content = LooseAssetStore.Load(args[1]);
    if (args[2] == "--map")
    {
        var map = PreparedBattleMap.Load(content, assetId);
        var terrain = map.Definition.ToBattleMap();
        Console.WriteLine($"Loaded prepared map '{map.Definition.Id}': {terrain.Width}x{terrain.Depth}, " +
            $"{terrain.LayerCount} terrain layers, {map.Definition.Polygons.Length} polygons, " +
            $"{map.TextureIndices.Length} texture indices; manifest hashes verified. Base presentation frozen.");
        return 0;
    }
    if (args[2] == "--sprite-pose")
    {
        var pose = PreparedSpritePose.Load(content, assetId);
        Console.WriteLine($"Loaded prepared pose '{pose.Definition.Id}': frame {pose.Definition.FrameId}, " +
            $"wait {pose.Definition.Wait} (frozen), palette {pose.Definition.PaletteIndex}, " +
            $"{pose.Definition.PartsBackToFront.Length} pieces, {pose.Rgba.Length} RGBA bytes; manifest hashes verified.");
        return 0;
    }
    byte[] asset = content.ReadAllBytes(assetId);
    Console.WriteLine($"Loaded prepared asset '{assetId}' ({asset.Length} bytes; manifest size and SHA-256 verified).");
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or JsonException or UnauthorizedAccessException or KeyNotFoundException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
