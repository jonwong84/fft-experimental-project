using Fft.Content.Samples;

namespace Fft.Client.Presentation;

public static class WindowsBattleHost
{
    public static int Run(string? checkDirectory = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("The graphical host currently supports Windows only. Use --scenario two-unit-battle for headless execution.");
            return 1;
        }

        try
        {
            // SDL reads this process-local hint when the Game constructor initializes video.
            Environment.SetEnvironmentVariable("SDL_WINDOWS_DPI_AWARENESS", "permonitorv2");
            if (checkDirectory is not null)
            {
                checkDirectory = Path.GetFullPath(checkDirectory);
                if (Directory.Exists(checkDirectory) || File.Exists(checkDirectory))
                    throw new IOException("Window-check output must be a new directory.");
                Directory.CreateDirectory(checkDirectory);
            }

            using var window = new SyntheticBattleWindow(SyntheticTwoUnitBattle.Create().Start().Battle, checkDirectory);
            window.Run();
            window.VerifySnapshot();
            if (checkDirectory is not null && !window.CheckCompleted)
                throw new InvalidOperationException("Window check closed before its capture completed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Windows host failed: {exception.Message}");
            return 1;
        }
    }
}
