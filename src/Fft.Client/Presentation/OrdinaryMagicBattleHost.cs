using Zodiac.Content;
using Zodiac.Content.BattleSetup;

namespace Fft.Client.Presentation;

public static class OrdinaryMagicBattleHost
{
    public const string Usage = "--fire-battle --content-root <prepared-directory> [--check <fresh-directory>]";
    public static int Run(string[] args)
    {
        if (args.Length is not (3 or 5) || args[0] != "--fire-battle" || args[1] != "--content-root" ||
            args.Length == 5 && args[3] != "--check" || args.Where((_, i) => i is 2 or 4).Any(string.IsNullOrWhiteSpace))
        { Console.Error.WriteLine("Usage: " + Usage); return 2; }
        try
        {
            var prepared = PreparedOrdinaryMagicBattle.Load(LooseAssetStore.Load(args[2]));
            if (prepared.Definition.Units.Length != 2 || prepared.SpellNames.Count != 1 ||
                prepared.Definition.Map.Width != 7 || prepared.Definition.Map.Depth != 5)
                throw new NotSupportedException("This player window selects the two-unit 7x5 Fire encounter.");
            string? output = args.Length == 5 ? Path.GetFullPath(args[4]) : null;
            if (output is not null && (Directory.Exists(output) || File.Exists(output))) throw new IOException("Check output already exists.");
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The playable spell window supports Windows.");
            if (output is not null) Directory.CreateDirectory(output);
            Environment.SetEnvironmentVariable("SDL_WINDOWS_DPI_AWARENESS", "permonitorv2");
            using var window = new OrdinaryMagicBattleWindow(prepared, output);
            window.Run();
            if (output is not null && !window.CheckCompleted) throw new InvalidOperationException("Fire check closed before completion.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("Fire battle failed: " + error.Message); return 1; }
    }
}
