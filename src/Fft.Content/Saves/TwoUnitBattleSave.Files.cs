using System.Text;
using Fft.Core;

namespace Fft.Content.Saves;

public static partial class TwoUnitBattleSave
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static TwoUnitBattleSaveLoadResult Load(TwoUnitBattleSaveProfile profile, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var bytes = new MemoryStream();
        byte[] chunk = new byte[4096];
        while (true)
        {
            int count = input.Read(chunk, 0, (int)Math.Min(chunk.Length, MaxJsonBytes - bytes.Length + 1));
            if (count == 0) break;
            if (bytes.Length + count > MaxJsonBytes) throw new BattleSaveValidationException($"Save exceeds {MaxJsonBytes} UTF-8 bytes.");
            bytes.Write(chunk, 0, count);
        }
        try { return ReadJson(profile, StrictUtf8.GetString(bytes.GetBuffer(), 0, (int)bytes.Length)); }
        catch (DecoderFallbackException exception) { throw new BattleSaveValidationException("Save is not valid UTF-8.", exception); }
    }

    /// <summary>Validate/serialize first, flush a unique sibling, then replace atomically. Never truncate the destination.</summary>
    public static void Save(TwoUnitBattleSaveProfile profile, TwoUnitBattleState state, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        byte[] bytes = StrictUtf8.GetBytes(ToJson(profile, state));
        string destination = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
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
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
