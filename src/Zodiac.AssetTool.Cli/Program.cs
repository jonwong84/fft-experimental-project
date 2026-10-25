namespace Zodiac.AssetTool.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Metadata validation has no repository, scratch, BIN or reference-checkout dependency.
        if (args.Length > 0 && string.Equals(args[0], "validate-content", StringComparison.Ordinal))
            return new ValidateContentCommand().Run(args, Console.Out, Console.Error);

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

        if (args.Length > 0 && args[0] is "import-metadata" or "import-sword")
            return new ImportMetadataCommand(workspaceRoot).Run(args, Console.Out, Console.Error);

        if (args.Length > 0 && args[0] == "prepare-sprite-pose")
            return new PrepareSpritePoseCommand(workspaceRoot).Run(args, Console.Out, Console.Error);

        if (args.Length > 0 && args[0] == "prepare-general-banner")
            return new PrepareGeneralBannerCommand(workspaceRoot).Run(args, Console.Out, Console.Error);

        if (args.Length > 0 && args[0] == "prepare-ordered-banner-event")
            return new PrepareOrderedBannerEventCommand(workspaceRoot).Run(args, Console.Out, Console.Error);

        if (args.Length > 0 && args[0] is "prepare-ready-event" or "prepare-ready-artwork")
            return new PrepareReadyCommand(workspaceRoot).Run(args, Console.Out, Console.Error);

        if (args.Length > 0 && args[0] == "prepare-banner-ready-sequence")
            return new PrepareBannerReadySequenceCommand(workspaceRoot).Run(args, Console.Out, Console.Error);

        if (args.Length > 0 && args[0] == "prepare-map")
            return new PrepareMapCommand(workspaceRoot).Run(args, Console.Out, Console.Error);

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
