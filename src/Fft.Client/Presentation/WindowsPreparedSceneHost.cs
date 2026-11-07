using Zodiac.Content;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

public static class WindowsPreparedSceneHost
{
    public static int Run(string[] args)
    {
        if (args.Length is not (5 or 7) || args[0] != "--prepared-scene" || args[1] != "--map-root" ||
            args[3] != "--pose-root" || (args.Length == 7 && args[5] != "--check") ||
            (args.Length is 5 or 7 && new[] { args[2], args[4] }.Concat(args.Length == 7 ? [args[6]] : [])
                .Any(value => string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))))
        {
            Console.Error.WriteLine("Usage: --prepared-scene --map-root <prepared-map-directory> --pose-root <prepared-pose-directory> [--check <new-directory>]");
            return 2;
        }
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("The prepared graphical scene currently supports Windows only.");
            return 1;
        }
        try
        {
            var map = PreparedBattleMap.Load(LooseAssetStore.Load(args[2]), new AssetId("base:map/003/day-dry-frozen"));
            var pose = PreparedSpritePose.Load(LooseAssetStore.Load(args[4]), new AssetId("base:battle/cyoko/idle-front-pose"));
            var geometry = PreparedSceneGeometry.Create(map, pose);
            string? checkDirectory = args.Length == 7 ? Path.GetFullPath(args[6]) : null;
            if (checkDirectory is not null)
            {
                if (Directory.Exists(checkDirectory) || File.Exists(checkDirectory))
                    throw new IOException("Prepared scene check output must be a new directory.");
                Directory.CreateDirectory(checkDirectory);
            }
            Environment.SetEnvironmentVariable("SDL_WINDOWS_DPI_AWARENESS", "permonitorv2");
            using var window = new PreparedSceneWindow(map, pose, geometry, checkDirectory);
            window.Run();
            window.VerifyInputs();
            if (checkDirectory is not null && !window.CheckCompleted)
                throw new InvalidOperationException("Prepared scene closed before its capture completed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Prepared scene failed: " + exception.Message);
            return 1;
        }
    }
}
