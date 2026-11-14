using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;

namespace Fft.Tests.Support;

internal static class AuthoredBannerReadySequence
{
    internal static readonly AssetId Id = new("sample:sequence/test");
    internal static PreparedBannerReadySequence Definition(int general = 60, int ready = 60) => new()
    {
        Format = PreparedBannerReadySequence.FormatName, SchemaVersion = 1, Profile = PreparedBannerReadySequence.ProfileName,
        Id = Id.Value, GeneralArtworkId = "sample:banner/test", ReadyArtworkId = "sample:ready/artwork", OwnerId = "sample:owner/sequence",
        GeneralHold = general, ReadyHold = ready, Provenance = "authored", ReferenceRevision = PreparedReadyArtwork.ReferenceRevision,
    };
    internal static void Write(string root, PreparedBannerReadySequence? definition = null, byte[]? metadata = null)
    {
        definition ??= Definition(); byte[] bytes = metadata ?? definition.ToUtf8(); Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "sequence.json"), bytes);
        new PreparedAssetManifest([new(new(definition.Id), "sequence.json", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))])
            .SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
    }
}
