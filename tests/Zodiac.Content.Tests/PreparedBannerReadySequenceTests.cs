using System.Text;
using System.Text.Json.Nodes;
using Fft.Tests.Support;
using Zodiac.Content.Events;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedBannerReadySequenceTests
{
    [Theory]
    [InlineData(0, 0)] [InlineData(1, 255)] [InlineData(255, 1)] [InlineData(60, 60)]
    public void Round_trip_preserves_typed_holds_distinct_assets_and_canonical_program(int g, int r)
    {
        var original = AuthoredBannerReadySequence.Definition(g, r); var parsed = PreparedBannerReadySequence.Parse(original.ToUtf8());
        Assert.Equal(original, parsed); Assert.Equal(original.ToUtf8(), parsed.ToUtf8());
        var program = parsed.ToProgram(); Assert.Equal(g, program.General.Definition.HoldFrames); Assert.Equal(r, program.Ready.Definition.HoldFrames);
        Assert.Equal(8, program.General.Definition.Condition); Assert.Equal(0, program.Ready.Definition.Condition);
        Assert.NotEqual(program.General.Definition.Id, program.Ready.Definition.Id);
    }
    [Theory]
    [InlineData("format")] [InlineData("schemaVersion")] [InlineData("profile")] [InlineData("id")]
    [InlineData("generalArtworkId")] [InlineData("readyArtworkId")] [InlineData("ownerId")]
    [InlineData("generalHold")] [InlineData("readyHold")] [InlineData("provenance")] [InlineData("referenceRevision")]
    public void Missing_and_null_required_fields_reject(string field)
    {
        var node = JsonNode.Parse(AuthoredBannerReadySequence.Definition().ToUtf8())!.AsObject(); node.Remove(field);
        Assert.Throws<InvalidDataException>(() => PreparedBannerReadySequence.Parse(Encoding.UTF8.GetBytes(node.ToJsonString())));
        node[field] = null; Assert.Throws<InvalidDataException>(() => PreparedBannerReadySequence.Parse(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }
    [Theory]
    [InlineData("format", "fft.prepared-ready-event")] [InlineData("profile", "legacy")]
    [InlineData("provenance", "retail")] [InlineData("referenceRevision", "611b504d25d4a9b03fab8023d7dbea75fe381d5d")]
    [InlineData("generalArtworkId", "sample:ready/artwork")] [InlineData("id", "bad id")]
    [InlineData("readyHold", "60")]
    public void Wrong_domains_identifiers_and_field_types_reject(string field, string value)
    {
        var node = JsonNode.Parse(AuthoredBannerReadySequence.Definition().ToUtf8())!; node[field] = value;
        Assert.Throws<InvalidDataException>(() => PreparedBannerReadySequence.Parse(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }
    [Fact]
    public void Unknown_duplicate_oversized_or_original_bytes_do_not_become_executable_content()
    {
        string json = Encoding.UTF8.GetString(AuthoredBannerReadySequence.Definition().ToUtf8());
        foreach (string extra in new[] { ",\"readyHold\":60", ",\"instructions\":[]" })
            Assert.Throws<InvalidDataException>(() => PreparedBannerReadySequence.Parse(Encoding.UTF8.GetBytes(json[..json.LastIndexOf('}')] + extra + "}")));
        Assert.Throws<InvalidDataException>(() => PreparedBannerReadySequence.Parse(new byte[16385]));
        Assert.Throws<InvalidDataException>(() => PreparedBannerReadySequence.Parse(AuthoredReady.Block()));
        Assert.Throws<InvalidDataException>(() => PreparedReadyEvent.Parse(Encoding.UTF8.GetBytes(json)));
        Assert.Throws<InvalidDataException>(() => PreparedOrderedBannerEvent.Parse(Encoding.UTF8.GetBytes(json)));
    }
    [Theory]
    [InlineData(-1, 0)] [InlineData(256, 0)] [InlineData(0, -1)] [InlineData(0, 256)]
    public void Holds_remain_unsigned_bytes(int g, int r)
        => Assert.Throws<InvalidDataException>(() => AuthoredBannerReadySequence.Definition(g, r).ToUtf8());
    [Fact]
    public void Manifest_hash_and_selected_identity_are_verified()
    {
        string root = Path.Combine(Path.GetTempPath(), "fft-sequence-content-" + Guid.NewGuid().ToString("N"));
        try
        {
            AuthoredBannerReadySequence.Write(root); var store = LooseAssetStore.Load(root);
            Assert.Equal(AuthoredBannerReadySequence.Id.Value, PreparedBannerReadySequence.Load(store, AuthoredBannerReadySequence.Id).Id);
            File.WriteAllText(Path.Combine(root, "sequence.json"), "{}");
            Assert.Throws<InvalidDataException>(() => PreparedBannerReadySequence.Load(store, AuthoredBannerReadySequence.Id));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
