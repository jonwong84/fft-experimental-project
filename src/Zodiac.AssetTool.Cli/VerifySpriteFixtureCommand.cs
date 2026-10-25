using System.Security.Cryptography;
using System.Text.Json;
using Zodiac.AssetTool;
using Zodiac.Content;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

internal sealed class VerifySpriteFixtureCommand
{
    private const string FixturePath = "docs/fixtures/chocobo-attack-image.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _workspaceRoot;

    public VerifySpriteFixtureCommand(string workspaceRoot) => _workspaceRoot = Path.GetFullPath(workspaceRoot);

    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        const string usage = "Usage: verify-sprite --content-root <prepared-content-directory> [--preview-output <scratch-file.png>]";
        if ((args.Length != 3 && args.Length != 5) || args[0] != "verify-sprite" ||
            args[1] != "--content-root" || string.IsNullOrWhiteSpace(args[2]) ||
            (args.Length == 5 && (args[3] != "--preview-output" || string.IsNullOrWhiteSpace(args[4]))))
        {
            error.WriteLine(usage);
            return 2;
        }

        try
        {
            string fixturePath = Path.Combine(_workspaceRoot, FixturePath.Replace('/', Path.DirectorySeparatorChar));
            using FileStream fixtureStream = File.OpenRead(fixturePath);
            SpriteFixture fixture = JsonSerializer.Deserialize<SpriteFixture>(fixtureStream, JsonOptions)
                ?? throw new InvalidDataException("The Chocobo sprite fixture is empty.");
            ValidateFixture(fixture);

            string contentRoot = Path.GetFullPath(args[2], _workspaceRoot);
            LooseAssetStore store = LooseAssetStore.Load(contentRoot);
            var assetId = new AssetId(fixture.AssetId!);
            byte[] source = store.ReadAllBytes(assetId);
            VerifyEqual("source byte length", fixture.SourceByteLength, source.Length);
            VerifyHash("source SHA-256", fixture.SourceSha256!, SHA256.HashData(source));

            UnitSpriteSheet sheet = UnitSpriteSheet.Parse(source);
            byte[] decoded = sheet.DecodeAttackImage();
            VerifyEqual("decoded byte length", fixture.DecodedByteLength, decoded.Length);
            VerifyHash("decoded SHA-256", fixture.DecodedSha256!, SHA256.HashData(decoded));

            output.WriteLine($"Verified {assetId}: {fixture.ImageWidth}x{fixture.ImageHeight} packed {fixture.BitsPerPixel}bpp pixels.");
            output.WriteLine($"Source SHA-256: {fixture.SourceSha256}");
            output.WriteLine($"Decoded SHA-256: {fixture.DecodedSha256}");
            output.WriteLine("All source and decoded-output fixture checks passed.");
            if (args.Length == 5)
            {
                string previewPath = ValidatePreviewPath(args[4]);
                using var previewStream = new FileStream(previewPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                WritePaletteContactSheet(previewStream, sheet, decoded);
                output.WriteLine($"Palette contact sheet written: {Path.GetRelativePath(_workspaceRoot, previewPath)}");
            }

            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or UnauthorizedAccessException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    private string ValidatePreviewPath(string value)
    {
        string scratchRoot = Path.GetFullPath(Path.Combine(_workspaceRoot, "scratch"));
        string path = Path.GetFullPath(value, _workspaceRoot);
        string relative = Path.GetRelativePath(scratchRoot, path);
        if (relative is "." or ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", PathComparison) || Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("Preview output must be a new file inside repository scratch/.");
        }

        if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Preview output must use the .png extension.");
        }

        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new IOException($"Preview output already exists: {path}");
        }

        return path;
    }

    private static void WritePaletteContactSheet(Stream output, UnitSpriteSheet sheet, byte[] decodedPixels)
    {
        const int columns = 4;
        const int rows = 4;
        const int labelHeight = 8;
        int cellHeight = labelHeight + SpriteAttackImageDecoder.ImageHeight;
        int width = columns * SpriteAttackImageDecoder.ImageWidth;
        int height = rows * cellHeight;
        var rgba = new byte[width * height * 4];
        for (int pixel = 0; pixel < width * height; pixel++)
        {
            int offset = pixel * 4;
            rgba[offset] = 0x30;
            rgba[offset + 1] = 0x30;
            rgba[offset + 2] = 0x30;
            rgba[offset + 3] = 0xff;
        }

        for (int palette = 0; palette < 16; palette++)
        {
            int originX = (palette % columns) * SpriteAttackImageDecoder.ImageWidth;
            int originY = (palette / columns) * cellHeight;
            DrawHexDigit(rgba, width, originX + 2, originY + 1, palette);
            for (int y = 0; y < SpriteAttackImageDecoder.ImageHeight; y++)
            {
                int outputY = originY + labelHeight + y;
                int sourcePixel = y * SpriteAttackImageDecoder.ImageWidth;
                for (int x = 0; x < SpriteAttackImageDecoder.ImageWidth; x++)
                {
                    int packed = decodedPixels[(sourcePixel + x) >> 1];
                    int colorIndex = ((sourcePixel + x) & 1) == 0 ? packed >> 4 : packed & 0x0f;
                    if (colorIndex == 0)
                    {
                        // The reference loader documents index zero as transparent.
                        continue;
                    }

                    ushort color = sheet.ReadPaletteColor(palette, colorIndex);
                    int outputOffset = (outputY * width + originX + x) * 4;
                    rgba[outputOffset] = Expand5Bit(color & 0x1f);
                    rgba[outputOffset + 1] = Expand5Bit((color >> 5) & 0x1f);
                    rgba[outputOffset + 2] = Expand5Bit((color >> 10) & 0x1f);
                    rgba[outputOffset + 3] = 0xff;
                }
            }
        }

        PngRgbaWriter.Write(output, width, height, rgba);
    }

    private static byte Expand5Bit(int value) => (byte)((value << 3) | (value >> 2));

    private static void DrawHexDigit(byte[] rgba, int width, int x, int y, int value)
    {
        ReadOnlySpan<byte> glyphRows = value switch
        {
            0x0 => [0b111, 0b101, 0b101, 0b101, 0b111],
            0x1 => [0b010, 0b110, 0b010, 0b010, 0b111],
            0x2 => [0b110, 0b001, 0b010, 0b100, 0b111],
            0x3 => [0b110, 0b001, 0b010, 0b001, 0b110],
            0x4 => [0b101, 0b101, 0b111, 0b001, 0b001],
            0x5 => [0b111, 0b100, 0b110, 0b001, 0b110],
            0x6 => [0b011, 0b100, 0b110, 0b101, 0b010],
            0x7 => [0b111, 0b001, 0b010, 0b010, 0b010],
            0x8 => [0b010, 0b101, 0b010, 0b101, 0b010],
            0x9 => [0b010, 0b101, 0b011, 0b001, 0b110],
            0xa => [0b010, 0b101, 0b111, 0b101, 0b101],
            0xb => [0b110, 0b101, 0b110, 0b101, 0b110],
            0xc => [0b011, 0b100, 0b100, 0b100, 0b011],
            0xd => [0b110, 0b101, 0b101, 0b101, 0b110],
            0xe => [0b111, 0b100, 0b110, 0b100, 0b111],
            _ => [0b111, 0b100, 0b110, 0b100, 0b100],
        };

        for (int row = 0; row < glyphRows.Length; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                if ((glyphRows[row] & (1 << (2 - column))) == 0)
                {
                    continue;
                }

                int offset = ((y + row) * width + x + column) * 4;
                rgba[offset] = 0xff;
                rgba[offset + 1] = 0xff;
                rgba[offset + 2] = 0xff;
                rgba[offset + 3] = 0xff;
            }
        }
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static void ValidateFixture(SpriteFixture fixture)
    {
        if (fixture.SchemaVersion != 1 || string.IsNullOrWhiteSpace(fixture.AssetId) ||
            fixture.SourceByteLength <= 0 || fixture.DecodedByteLength <= 0 ||
            fixture.ImageWidth <= 0 || fixture.ImageHeight <= 0 || fixture.BitsPerPixel != 4 ||
            !string.Equals(fixture.PixelPacking, "first-pixel-high-nibble", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(fixture.SourceSha256) || string.IsNullOrWhiteSpace(fixture.DecodedSha256))
        {
            throw new InvalidDataException("The Chocobo sprite fixture has invalid or unsupported metadata.");
        }

        _ = new AssetId(fixture.AssetId);
        ValidateSha256("source SHA-256", fixture.SourceSha256);
        ValidateSha256("decoded SHA-256", fixture.DecodedSha256);
        long expectedDecodedLength = (long)fixture.ImageWidth * fixture.ImageHeight * fixture.BitsPerPixel / 8;
        VerifyEqual("fixture dimensions and decoded byte length", expectedDecodedLength, fixture.DecodedByteLength);
    }

    private static void ValidateSha256(string name, string value)
    {
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"Fixture {name} must contain 64 hexadecimal characters.");
        }
    }

    private static void VerifyHash(string name, string expected, byte[] actualBytes)
    {
        string actual = Convert.ToHexStringLower(actualBytes);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Fixture {name} mismatch: expected {expected}, found {actual}.");
        }
    }

    private static void VerifyEqual(string name, long expected, long actual)
    {
        if (expected != actual)
        {
            throw new InvalidDataException($"Fixture {name} mismatch: expected {expected}, found {actual}.");
        }
    }

    private sealed class SpriteFixture
    {
        public int SchemaVersion { get; set; }
        public string? AssetId { get; set; }
        public long SourceByteLength { get; set; }
        public string? SourceSha256 { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int BitsPerPixel { get; set; }
        public string? PixelPacking { get; set; }
        public long DecodedByteLength { get; set; }
        public string? DecodedSha256 { get; set; }
    }
}
