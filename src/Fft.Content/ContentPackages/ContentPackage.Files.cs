using System.Text;

namespace Fft.Content.ContentPackages;

public sealed partial class ContentPackage
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Reads a bounded UTF-8 package file. File access failures remain ordinary I/O exceptions.</summary>
    public static ContentPackage Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaxJsonBytes) throw new ContentPackageValidationException(null, null, "$", $"File exceeds {MaxJsonBytes} UTF-8 bytes.");
        using var bytes = new MemoryStream((int)input.Length);
        byte[] chunk = new byte[64 * 1024];
        while (true)
        {
            int count = input.Read(chunk, 0, (int)Math.Min(chunk.Length, MaxJsonBytes - bytes.Length + 1));
            if (count == 0) break;
            if (bytes.Length + count > MaxJsonBytes)
                throw new ContentPackageValidationException(null, null, "$", $"File exceeds {MaxJsonBytes} UTF-8 bytes.");
            bytes.Write(chunk, 0, count);
        }
        try { return ReadJson(StrictUtf8.GetString(bytes.GetBuffer(), 0, (int)bytes.Length)); }
        catch (DecoderFallbackException exception)
        {
            throw new ContentPackageValidationException(null, null, "$", "File is not valid UTF-8.", exception);
        }
    }

    /// <summary>Serializes before touching disk, then atomically renames a flushed sibling file over the destination.</summary>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        byte[] bytes = StrictUtf8.GetBytes(ToJson());
        string destination = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(destination)!;
        string temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            // Failed cleanup must not mask the write/rename failure; it never touches the existing destination.
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
