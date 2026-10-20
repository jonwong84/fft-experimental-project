using System.Security.Cryptography;
using System.Text.Json;
using Fft.AssetTool;
using Fft.Content;
using Fft.Core;

namespace Fft.AssetTool.Cli;

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
        const string usage = "Usage: verify-sprite --content-root <prepared-content-directory>";
        if (args.Length != 3 || args[0] != "verify-sprite" || args[1] != "--content-root" || string.IsNullOrWhiteSpace(args[2]))
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
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or UnauthorizedAccessException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

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
