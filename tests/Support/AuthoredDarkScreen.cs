using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Fft.Tests.Support;

internal static class AuthoredDarkScreen
{
    internal static readonly AssetId Id = new("sample:dark-screen/test");
    internal static PreparedDarkScreen Definition(int hold = 44) => new()
    {
        Format = "fft.dark-screen", SchemaVersion = 1, ExecutionProfile = PreparedDarkScreen.Profile,
        Id = Id.Value, OwnerId = "sample:owner/dark-screen", HoldFrames = hold, Parameters = new(),
        Tiles = [.. Enumerable.Range(0, 288).Select(i => new DarkScreenTileDefinition(i % 24 * 12 - 12, i / 24 * 24 - (i % 2 == 0 ? 12 : 24), i == 287 ? 41 : 0))],
        Rotations = [.. Enumerable.Range(0, 9).Select(i => new DarkScreenRotation(i * 64, 0, 4096, -4096, 0))],
        Source = new("authored-test", PreparedDarkScreen.ReferenceRevision, null, null, null),
    };
    internal static string Write(string root, PreparedDarkScreen? definition = null, byte[]? bytes = null)
    {
        definition ??= Definition(); bytes ??= definition.ToUtf8(); Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "dark-screen.json"), bytes);
        new PreparedAssetManifest([new(new(definition.Id), "dark-screen.json", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))])
            .SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
        return root;
    }
}
