using System.Text;
using System.Text.Json.Nodes;
using Fft.Tests.Support;
using Zodiac.Content.Events;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedDarkScreenIntroductionTests
{
    [Theory]
    [InlineData(0, 0)] [InlineData(60, 60)] [InlineData(255, 255)]
    public void Closed_authored_profile_round_trips_and_selects_three_workers(int general, int ready)
    {
        var d = AuthoredDarkScreenIntroduction.Definition(general, ready);
        Assert.Equal(d, PreparedDarkScreenIntroduction.Parse(d.ToUtf8()));
        var p = d.ToProgram(AuthoredDarkScreenIntroduction.Dark());
        Assert.Equal(general, p.General.Definition.HoldFrames); Assert.Equal(ready, p.Ready.Definition.HoldFrames);
        Assert.Equal(d.Id + "/general", p.General.Definition.Id.Value); Assert.Equal(d.DarkScreenId, p.Dark.Definition.Id.Value);
    }
    public static IEnumerable<object[]> Fields() => JsonNode.Parse(AuthoredDarkScreenIntroduction.Definition().ToUtf8())!.AsObject()
        .Select(p => new object[] { p.Key });
    [Theory] [MemberData(nameof(Fields))]
    public void Every_field_is_explicitly_required_and_non_null(string field)
    {
        var json = JsonNode.Parse(AuthoredDarkScreenIntroduction.Definition().ToUtf8())!.AsObject(); json.Remove(field);
        Assert.Throws<InvalidDataException>(() => PreparedDarkScreenIntroduction.Parse(Encoding.UTF8.GetBytes(json.ToJsonString())));
        json[field] = null;
        Assert.Throws<InvalidDataException>(() => PreparedDarkScreenIntroduction.Parse(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }
    [Theory]
    [InlineData("format", "fft.prepared-banner-ready-sequence")] [InlineData("profile", "other")]
    [InlineData("provenance", "original-event")] [InlineData("referenceRevision", "wrong")]
    [InlineData("ownerId", "BAD:owner")] [InlineData("darkScreenId", "sample:introduction/test")]
    [InlineData("generalArtworkId", "sample:introduction/test/general")]
    [InlineData("readyArtworkId", "sample:owner/dark-screen")]
    public void Invalid_profile_provenance_ids_and_derived_collisions_reject(string field, string value)
    {
        var json = JsonNode.Parse(AuthoredDarkScreenIntroduction.Definition().ToUtf8())!.AsObject(); json[field] = value;
        Assert.Throws<InvalidDataException>(() => PreparedDarkScreenIntroduction.Parse(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }
    [Fact]
    public void Unknown_duplicate_wrong_version_depth_size_and_old_formats_are_rejected()
    {
        string json = Encoding.UTF8.GetString(AuthoredDarkScreenIntroduction.Definition().ToUtf8());
        foreach (string text in new[] { json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"),
            json.Replace("{", "{\"extra\":1,", StringComparison.Ordinal), json.Replace("{", "{\"id\":\"sample:duplicate/id\",", StringComparison.Ordinal),
            "null", "[]", "{}", "{\"extra\":{\"a\":{\"b\":{\"c\":{}}}}}" })
            Assert.Throws<InvalidDataException>(() => PreparedDarkScreenIntroduction.Parse(Encoding.UTF8.GetBytes(text)));
        Assert.Throws<InvalidDataException>(() => PreparedDarkScreenIntroduction.Parse(new byte[16385]));
        Assert.Throws<InvalidDataException>(() => PreparedBannerReadySequence.Parse(Encoding.UTF8.GetBytes(json)));
        Assert.Throws<InvalidDataException>(() => PreparedDarkScreenIntroduction.Parse(AuthoredBannerReadySequence.Definition().ToUtf8()));
    }
    [Theory]
    [InlineData(-1, 0)] [InlineData(256, 0)] [InlineData(0, -1)] [InlineData(0, 256)]
    public void Holds_are_unsigned_bytes(int g, int r) => Assert.Throws<InvalidDataException>(() => AuthoredDarkScreenIntroduction.Definition(g, r).ToUtf8());
    [Fact]
    public void Id_length_limits_include_resource_and_derived_ids()
    {
        var d = AuthoredDarkScreenIntroduction.Definition();
        var boundary = d with { Id = "sample:" + new string('x', 241), DarkScreenId = "sample:" + new string('y', 249) };
        boundary.Validate();
        Assert.Throws<InvalidDataException>(() => (boundary with { Id = boundary.Id + "x" }).Validate());
        Assert.Throws<InvalidDataException>(() => (boundary with { DarkScreenId = boundary.DarkScreenId + "x" }).Validate());
    }
    [Fact]
    public void Dark_owner_identity_and_provenance_are_validated_without_its_standalone_timer()
    {
        var d = AuthoredDarkScreenIntroduction.Definition(); var dark = AuthoredDarkScreenIntroduction.Dark();
        Assert.Throws<InvalidDataException>(() => d.ToProgram(dark with { OwnerId = "sample:owner/other" }));
        Assert.Throws<InvalidDataException>(() => d.ToProgram(dark with { Id = "sample:dark/other" }));
        Assert.Throws<InvalidDataException>(() => d.ToProgram(AuthoredDarkScreen.Definition()));
        var a = d.ToProgram(dark with { HoldFrames = 0 }); var b = d.ToProgram(dark with { HoldFrames = 255 });
        Assert.Equal(a.Dark.Definition.Tiles.ToArray(), b.Dark.Definition.Tiles.ToArray()); Assert.Equal(a.General, b.General); Assert.Equal(a.Ready, b.Ready);
    }
}
