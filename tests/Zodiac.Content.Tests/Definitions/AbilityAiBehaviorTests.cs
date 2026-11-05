using System.Text.Json;
using Xunit;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;

namespace Zodiac.Content.Tests.Definitions;

public sealed class AbilityAiBehaviorTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-behavior-contract-v1.json")));
    public static IEnumerable<object[]> Ids()
    { using var doc = Fixture(); return doc.RootElement.GetProperty("vectors").EnumerateArray().Select(r => new object[] { r.GetProperty("id").GetString()! }).ToArray(); }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Original_static_bits_decode_to_semantics_without_mutating_or_reinterpreting_packing(string id)
    {
        using var doc = Fixture(); var row = doc.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var flags = row.GetProperty("flags").EnumerateArray().Select(v => v.GetByte()).ToArray(); var expected = row.GetProperty("expected");
        var metadata = new AbilityMetadataDefinition(new("test:ability"), 0, 0, 1, flags, unknownTypeBit4: true);
        var behavior = JsonSerializer.SerializeToElement(metadata.AiBehavior);
        foreach (var field in expected.GetProperty("behavior").EnumerateObject()) Assert.Equal(field.Value.GetBoolean(), behavior.GetProperty(field.Name).GetBoolean());
        Assert.Equal(21, behavior.EnumerateObject().Count()); Assert.Equal(expected.GetProperty("usable").GetBoolean(), metadata.UsableByAi);
        Assert.Equal(expected.GetProperty("motion").GetBoolean(), metadata.AiEvadeWithMotion); Assert.Equal(expected.GetProperty("unresolved").GetBoolean(), metadata.HasUnresolvedAiFlags);
        Assert.Equal(flags, metadata.AiFlagsRaw); Array.Fill(flags, (byte)0); Assert.Equal(row.GetProperty("flags").EnumerateArray().Select(v => v.GetByte()), metadata.AiFlagsRaw);
        Assert.True(metadata.UnknownTypeBit4);
    }

    [Fact]
    public void Semantic_view_is_derived_after_unchanged_schema_roundtrip_and_is_not_a_second_serialized_source()
    {
        var ability = new AbilityMetadataDefinition(new("test:stone"), 90, 80, 1, [130,2,65,129]);
        var package = new ContentPackage("test:metadata", "test:profile", ContentPackageKind.Baseline, null, [], [], [ability]);
        string json = package.ToJson(); var loaded = ContentPackage.ReadJson(json); var copy = Assert.Single(loaded.Abilities);
        Assert.Equal(json, loaded.ToJson()); Assert.Equal(ability.AiBehavior, copy.AiBehavior); Assert.Equal(ability.AiFlagsRaw.ToArray(), copy.AiFlagsRaw.ToArray());
        using var doc = JsonDocument.Parse(json); var row = doc.RootElement.GetProperty("abilities")[0];
        Assert.True(row.TryGetProperty("aiFlagsRaw", out _)); Assert.False(row.TryGetProperty("aiBehavior", out _));
        Assert.True(copy.AiBehavior.AffectsHp); Assert.True(copy.AiBehavior.TargetsEnemies); Assert.True(copy.AiBehavior.PhysicalAttack); Assert.True(copy.AiBehavior.DirectTrajectory);
        Assert.False(copy.AiBehavior.AffectedBySilence); Assert.False(copy.AiBehavior.TargetsAllies);
    }
}
