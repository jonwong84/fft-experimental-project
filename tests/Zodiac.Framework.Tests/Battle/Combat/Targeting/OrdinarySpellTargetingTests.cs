using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

internal static class SpellTargetTestData
{
    internal static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "spell-targeting-contract-v1.json")));
    internal static BattleTile Tile(JsonElement t) => new(TerrainSurfaceId.Unclassified, t.GetProperty("h").GetInt32(),
        t.GetProperty("half").GetInt32(), t.GetProperty("water").GetInt32(), movementFlags:
        (t.GetProperty("blocked").GetBoolean() ? BattleTileMovementFlags.Blocked : 0) |
        (t.GetProperty("untargetable").GetBoolean() ? BattleTileMovementFlags.Untargetable : 0));
}

[Trait("Ruleset", "OriginalFft")]
public sealed class OrdinarySpellTargetingTests
{
    [Fact]
    public void Literal_reference_and_column_vectors_preserve_source_width_sentinels_and_ties()
    {
        using var doc = SpellTargetTestData.Fixture();
        foreach (var r in doc.RootElement.GetProperty("references").EnumerateArray())
            Assert.Equal(r.GetProperty("expected").GetByte(), OrdinarySpellTargeting.ReferenceHeight(SpellTargetTestData.Tile(r.GetProperty("tile"))));
        foreach (var r in doc.RootElement.GetProperty("columns").EnumerateArray())
        {
            var u = r.GetProperty("upper"); var e = r.GetProperty("expected");
            Assert.Equal(e.ValueKind == JsonValueKind.Null ? (int?)null : e.GetInt32(),
                OrdinarySpellTargeting.SelectLayer(SpellTargetTestData.Tile(r.GetProperty("lower")),
                    u.ValueKind == JsonValueKind.Null ? null : SpellTargetTestData.Tile(u), r.GetProperty("reference").GetByte(),
                    r.GetProperty("tolerance").GetByte(), r.GetProperty("topDown").GetBoolean()));
        }
    }

    [Fact]
    public void Primitive_rejects_fields_outside_original_packing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrdinarySpellTargeting.ReferenceHeight(new BattleTile(256)));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrdinarySpellTargeting.SelectLayer(new BattleTile(1),
            new BattleTile(TerrainSurfaceId.Unclassified, 1, waterDepth: 8), 0, 1));
    }
}
