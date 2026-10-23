namespace Fft.Client.Presentation;

public static class WindowsPlayableBattleHost
{
    public static int Run(string? checkDirectory = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("The playable window currently supports Windows only. Use --scenario two-unit-battle for the headless script.");
            return 1;
        }
        try
        {
            if (checkDirectory is not null)
            {
                checkDirectory = Path.GetFullPath(checkDirectory);
                if (Directory.Exists(checkDirectory) || File.Exists(checkDirectory))
                    throw new IOException("Battle-check output must be a new directory.");
                Directory.CreateDirectory(checkDirectory);
            }
            Environment.SetEnvironmentVariable("SDL_WINDOWS_DPI_AWARENESS", "permonitorv2");
            using var window = new PlayableBattleWindow(checkDirectory);
            window.Run();
            if (checkDirectory is not null && !window.CheckCompleted)
                throw new InvalidOperationException("Battle check closed before completion.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Playable battle failed: " + exception.Message);
            return 1;
        }
    }
}
