using Fft.Content.ContentPackages;

namespace Fft.AssetTool.Cli;

/// <summary>Validates explicitly selected metadata packages; never loads authoring code or game media.</summary>
public sealed class ValidateContentCommand
{
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            string? packagePath = null;
            var addonPaths = new List<string>();
            if (args.Length == 0 || args[0] != "validate-content")
                throw new ArgumentException("Expected validate-content command.");
            for (int i = 1; i < args.Length; i++)
            {
                string option = args[i];
                if (option is not ("--package" or "--addon") || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Unknown option or missing value: {option}");
                string path = args[++i];
                ArgumentException.ThrowIfNullOrWhiteSpace(path);
                if (option == "--package")
                {
                    if (packagePath is not null) throw new ArgumentException("Specify --package exactly once.");
                    packagePath = path;
                }
                else addonPaths.Add(path);
            }
            if (packagePath is null) throw new ArgumentException("--package is required.");
            if (addonPaths.Count >= ContentPackage.MaxPackagesPerComposition)
                throw new ArgumentException("Too many selected packages.");
            ContentPackage baseline = ContentPackage.Load(packagePath);
            ContentPackage[] addons = addonPaths.Select(ContentPackage.Load).ToArray();
            var catalog = ContentPackage.Compose(baseline, addons);
            output.WriteLine($"Validated metadata package '{baseline.PackageId}' with {addons.Length} add-ons: " +
                $"{catalog.Jobs.Length} jobs, {catalog.Skillsets.Length} skillsets, {catalog.Abilities.Length} abilities.");
            return 0;
        }
        catch (ContentPackageValidationException exception)
        {
            error.WriteLine($"Invalid content: package={exception.PackageId ?? "<unreadable>"}; " +
                $"record={exception.RecordId ?? "<package>"}; field={exception.FieldPath}; {exception.Message}");
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Content file error: {exception.Message}");
            return 1;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine("Usage: validate-content --package <baseline.json> [--addon <addon.json> ...]");
            return 2;
        }
    }
}
