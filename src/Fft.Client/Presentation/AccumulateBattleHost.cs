using Zodiac.Content;
using Zodiac.Content.BattleSetup;

namespace Fft.Client.Presentation;

public static class AccumulateBattleHost
{
    public const string Usage = "--accumulate-battle --content-root <prepared-directory> [--check <new-directory>]";
    public static int Run(string[] args)
    {
        if (args.Length is not (3 or 5) || args[0] != "--accumulate-battle" || args[1] != "--content-root" ||
            (args.Length == 5 && args[3] != "--check") || args.Where((_, index) => index is 2 or 4).Any(value => string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal)))
        { Console.Error.WriteLine("Usage: " + Usage); return 2; }
        try
        {
            // All definitions/readiness checks precede creating an output directory or native window.
            var prepared = PreparedAccumulateBattle.Load(LooseAssetStore.Load(args[2]));
            string? output = args.Length == 5 ? Path.GetFullPath(args[4]) : null;
            if (output is not null && (Directory.Exists(output) || File.Exists(output))) throw new IOException("Check output already exists.");
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The playable ability window currently supports Windows.");
            if (output is not null) Directory.CreateDirectory(output);
            Environment.SetEnvironmentVariable("SDL_WINDOWS_DPI_AWARENESS", "permonitorv2");
            using var window = new PlayableBattleWindow(output, abilityBattle: prepared);
            window.Run();
            if (output is not null && !window.CheckCompleted) throw new InvalidOperationException("Ability check closed before completion.");
            return 0;
        }
        catch (Exception exception)
        { Console.Error.WriteLine("Accumulate battle failed: " + exception.Message); return 1; }
    }
}
