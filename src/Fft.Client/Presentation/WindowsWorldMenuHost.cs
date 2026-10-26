namespace Fft.Client.Presentation;

public static class WindowsWorldMenuHost
{
    public static int Run(string? checkDirectory = null)
    {
        if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("World menu currently supports Windows only."); return 1; }
        try
        {
            if (checkDirectory is not null)
            {
                checkDirectory = Path.GetFullPath(checkDirectory);
                if (Directory.Exists(checkDirectory) || File.Exists(checkDirectory)) throw new IOException("Menu check output must be a new directory.");
                Directory.CreateDirectory(checkDirectory);
            }
            Environment.SetEnvironmentVariable("SDL_WINDOWS_DPI_AWARENESS", "permonitorv2");
            using var window = new WorldMenuWindow(checkDirectory);
            window.Run();
            if (checkDirectory is not null && !window.CheckCompleted) throw new InvalidOperationException("Menu check did not complete.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("World menu failed: " + e.Message); return 1; }
    }
}
