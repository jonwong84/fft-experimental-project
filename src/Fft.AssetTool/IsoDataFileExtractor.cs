using System.Text.RegularExpressions;

namespace Fft.AssetTool;

/// <summary>
/// Extracts an ISO file by copying its 2048-byte Mode 2 Form 1 user-data payload.
/// </summary>
/// <remarks>
/// Use only for ordinary files stored as ISO user data. XA/STR entries whose
/// reference extraction uses a different sector representation need a separate
/// extraction policy. The output file is created without replacing an existing file.
/// </remarks>
public static partial class IsoDataFileExtractor
{
    /// <summary>
    /// Validates extraction paths for Windows case-insensitive collisions and file/directory conflicts.
    /// </summary>
    public static void ValidateOutputPaths(IEnumerable<IsoFileEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IsoFileEntry entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            string normalized = string.Join('/', ValidateRelativePath(entry.RelativePath));
            if (!paths.Add(normalized))
            {
                throw new InvalidDataException($"ISO paths collide on Windows: '{normalized}'.");
            }
        }

        string? previous = null;
        foreach (string path in paths)
        {
            if (previous is not null && path.StartsWith(previous + "/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"ISO file path '{previous}' conflicts with child path '{path}'.");
            }

            previous = path;
        }
    }

    public static string ExtractForm1DataFile(
        Mode2RawImageReader image,
        IsoFileEntry entry,
        string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        string[] components = ValidateRelativePath(entry.RelativePath);
        string root = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(root);
        string destination = Path.GetFullPath(Path.Combine(root, Path.Combine(components)));
        string rootPrefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The ISO output path resolves outside the extraction directory.");
        }

        string? parent = Path.GetDirectoryName(destination);
        if (parent is null)
        {
            throw new InvalidDataException("The ISO output path has no parent directory.");
        }

        Directory.CreateDirectory(parent);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            uint remaining = entry.ByteLength;
            uint lba = entry.EffectiveExtentLba;
            while (remaining > 0)
            {
                byte[] userData = image.ReadUserData(checked((int)lba));
                int bytesToWrite = (int)Math.Min((uint)Mode2RawImageReader.UserDataSize, remaining);
                output.Write(userData, 0, bytesToWrite);
                remaining -= (uint)bytesToWrite;
                lba = checked(lba + 1);
            }
        }
        catch
        {
            output.Dispose();
            File.Delete(destination);
            throw;
        }

        return destination;
    }

    private static string[] ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\'))
        {
            throw new InvalidDataException("The ISO relative path must use non-rooted '/' separated components.");
        }

        string[] components = path.Split('/');
        foreach (string component in components)
        {
            if (component.Length == 0 || component is "." or ".." ||
                component.EndsWith(' ') || component.EndsWith('.') ||
                component.Any(character => character < 0x20 || "<>:\"|?*".Contains(character)))
            {
                throw new InvalidDataException($"The ISO path contains an invalid Windows path component '{component}'.");
            }

            string deviceName = component.Split('.')[0];
            if (ReservedDeviceName().IsMatch(deviceName))
            {
                throw new InvalidDataException($"The ISO path contains a reserved Windows device name '{component}'.");
            }
        }

        return components;
    }

    [GeneratedRegex("^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedDeviceName();
}
