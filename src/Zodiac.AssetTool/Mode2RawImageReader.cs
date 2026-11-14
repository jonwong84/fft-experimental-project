namespace Zodiac.AssetTool;

/// <summary>
/// Reads 2048-byte user-data payloads from a single-track raw Mode 2/2352 image.
/// </summary>
/// <remarks>
/// Track-relative LBA zero begins at byte zero of the supplied stream. This reader
/// does not parse CUE files or validate the image identity. The caller retains
/// ownership of the stream.
/// </remarks>
public sealed class Mode2RawImageReader
{
    public const int RawSectorSize = 2352;
    public const int UserDataOffset = 24;
    public const int UserDataSize = 2048;

    private static ReadOnlySpan<byte> SyncPattern =>
        [0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00];

    private readonly Stream _stream;
    private readonly long _sectorCount;
    private readonly object _streamLock = new();

    public long SectorCount => _sectorCount;

    public Mode2RawImageReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The image stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            throw new ArgumentException("The image stream must be seekable.", nameof(stream));
        }

        if (stream.Length == 0 || stream.Length % RawSectorSize != 0)
        {
            throw new InvalidDataException("The image must contain complete 2352-byte raw sectors.");
        }

        _stream = stream;
        _sectorCount = stream.Length / RawSectorSize;
    }

    /// <summary>
    /// Reads one track-relative LBA and returns its 2048-byte Mode 2 Form 1 payload.
    /// </summary>
    public byte[] ReadUserData(int trackRelativeLba)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(trackRelativeLba);
        if (trackRelativeLba >= _sectorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trackRelativeLba), trackRelativeLba, "LBA is outside the image.");
        }

        Span<byte> sector = stackalloc byte[RawSectorSize];
        long sectorOffset = checked((long)trackRelativeLba * RawSectorSize);
        lock (_streamLock)
        {
            _stream.Position = sectorOffset;
            _stream.ReadExactly(sector);
        }

        if (!sector[..SyncPattern.Length].SequenceEqual(SyncPattern))
        {
            throw InvalidSector(trackRelativeLba, "invalid sector sync pattern");
        }

        if (sector[15] != 0x02)
        {
            throw InvalidSector(trackRelativeLba, "sector mode is not Mode 2");
        }

        if (!sector.Slice(16, 4).SequenceEqual(sector.Slice(20, 4)))
        {
            throw InvalidSector(trackRelativeLba, "duplicated Mode 2 subheaders do not match");
        }

        if ((sector[18] & 0x20) != 0)
        {
            throw InvalidSector(trackRelativeLba, "Mode 2 Form 2 sectors are not supported");
        }

        return sector.Slice(UserDataOffset, UserDataSize).ToArray();
    }

    private static InvalidDataException InvalidSector(int lba, string reason) =>
        new($"Invalid raw Mode 2 sector at track-relative LBA {lba}: {reason}.");
}