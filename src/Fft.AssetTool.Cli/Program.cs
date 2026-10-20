namespace Fft.AssetTool.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        string? workspaceRoot = FindWorkspaceRoot(AppContext.BaseDirectory)
            ?? FindWorkspaceRoot(Environment.CurrentDirectory);
        if (workspaceRoot is null)
        {
            Console.Error.WriteLine("Could not locate Fft.sln; run the tool from the repository.");
            return 2;
        }

        if (args.Length > 0 && string.Equals(args[0], "extract", StringComparison.Ordinal))
        {
            return new ExtractCommand(workspaceRoot).Run(args, Console.Out, Console.Error);
        }

        if (args.Length > 0 && string.Equals(args[0], "verify-sprite", StringComparison.Ordinal))
        {
            return new VerifySpriteFixtureCommand(workspaceRoot).Run(args, Console.Out, Console.Error);
        }

        return new InventoryCommand(workspaceRoot).Run(args, Console.Out, Console.Error);
    }

    private static string? FindWorkspaceRoot(string startPath)
    {
        DirectoryInfo? directory = new(Path.GetFullPath(startPath));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Fft.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
