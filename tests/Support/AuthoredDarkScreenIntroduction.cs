using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;

namespace Fft.Tests.Support;

internal static class AuthoredDarkScreenIntroduction
{
    internal static readonly AssetId Id = new("sample:introduction/test");
    internal static PreparedDarkScreenIntroduction Definition(int general = 60, int ready = 60) => new()
    {
        Format = PreparedDarkScreenIntroduction.FormatName, SchemaVersion = 1, Profile = PreparedDarkScreenIntroduction.ProfileName,
        Id = Id.Value, DarkScreenId = AuthoredDarkScreen.Id.Value, GeneralArtworkId = "sample:banner/test",
        ReadyArtworkId = AuthoredReady.ArtworkId.Value, OwnerId = "sample:owner/dark-screen", GeneralHold = general, ReadyHold = ready,
        Provenance = "authored", ReferenceRevision = PreparedDarkScreen.ReferenceRevision,
    };
    // Synthetic cells/artwork with required provenance labels exercise binding, not original math equivalence.
    internal static PreparedDarkScreen Dark(int hold = 44) => AuthoredDarkScreen.Definition(hold) with
    {
        Source = new("verified-retail", PreparedDarkScreen.ReferenceRevision, PreparedDarkScreen.ExecutableHash,
            PreparedDarkScreen.SqrtHash, PreparedDarkScreen.SinCosHash),
    };
    internal static void Write(string root, PreparedDarkScreenIntroduction? definition = null, byte[]? metadata = null)
    {
        definition ??= Definition(); byte[] bytes = metadata ?? definition.ToUtf8(); Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "introduction.json"), bytes);
        new PreparedAssetManifest([new(new(definition.Id), "introduction.json", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))])
            .SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
    }
}
