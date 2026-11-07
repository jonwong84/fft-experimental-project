using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class StoredSpellAimTests
{
    [Fact]
    public void Source_centre_literals_preserve_exact_unit_type_and_signed_byte_narrowing()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "spell-follow-contract-v1.json")));
        foreach (var r in doc.RootElement.GetProperty("centers").EnumerateArray())
        {
            var d = r.GetProperty("input");
            // Original slot bounds and active actor are certified at the stable-ID boundary, not managed roster caps.
            if (d.GetProperty("directional").GetBoolean() || !d.GetProperty("actorActive").GetBoolean() ||
                d.GetProperty("actorId").GetInt32() >= 21 || d.GetProperty("type").GetInt32() == 6 && d.GetProperty("targetId").GetInt32() >= 21) continue;
            int w = d.GetProperty("width").GetInt32(), h = d.GetProperty("depth").GetInt32();
            var tiles = Enumerable.Repeat(new BattleTile(0), w * h * 2).ToArray();
            foreach (var b in d.GetProperty("blocked").EnumerateArray())
            { var p = SpellTargetTestData.Position(b); tiles[(p.Layer * h + p.Y) * w + p.X] = new(TerrainSurfaceId.Unclassified, 0, movementFlags: BattleTileMovementFlags.Blocked); }
            var s = d.GetProperty("stored");
            var aim = new StoredSpellAim(d.GetProperty("type").GetByte(), s[0].GetInt16(), s[1].GetInt16(), s[2].GetInt16(), new("anchor"));
            var actual = SpellAimResolver.Resolve(new(w, h, 2, tiles), aim,
                new(new("anchor"), SpellTargetTestData.Position(d.GetProperty("targetPosition"))), new(true, true));
            var e = r.GetProperty("expected"); Assert.Equal(e.GetProperty("resolved").GetBoolean(), actual.Accepted);
            Assert.Equal(e.GetProperty("center").ValueKind == JsonValueKind.Null ? (TilePosition?)null :
                SpellTargetTestData.Position(e.GetProperty("center")), actual.Position);
        }
    }

    [Fact]
    public void Follow_requires_known_matching_identity_and_never_falls_back_to_the_stored_tile()
    {
        var map = new BattleMapDefinition(2, 1, 1, [new(0), new(0)]); var aim = new StoredSpellAim(6, 0, 0, 0, new("anchor"));
        Assert.Equal(SpellAimRejection.MissingAnchor, SpellAimResolver.Resolve(map, aim, null, new(true, true)).Rejection);
        Assert.Equal(SpellAimRejection.ConflictingAnchor, SpellAimResolver.Resolve(map, aim, new(new("other"), default), new(true, true)).Rejection);
        Assert.Equal(SpellAimRejection.UnsupportedContext, SpellAimResolver.Resolve(map, aim, new(new("anchor"), default), new(true)).Rejection);
        Assert.False(SpellAimResolver.Resolve(map, aim, new(new("anchor"), default), null).Accepted);
        Assert.Equal(new TilePosition(1, 0, 0), SpellAimResolver.Resolve(map, aim, new(new("anchor"), new(1, 0, 0)), new(true, true)).Position);
        Assert.True(SpellAimResolver.Resolve(map, aim with { TargetingType = 7 }, null, new(true)).Accepted);
    }

    [Fact]
    public void Centre_accepts_untargetable_tiles_but_rejects_blocked_or_unsupported_maps()
    {
        var aim = new StoredSpellAim(5, 0, 0, 0);
        BattleMapDefinition Map(BattleTileMovementFlags flags) => new(1, 1, 1, [new(TerrainSurfaceId.Unclassified, 0, movementFlags: flags)]);
        Assert.True(SpellAimResolver.Resolve(Map(BattleTileMovementFlags.Untargetable), aim, null, new(true)).Accepted);
        Assert.Equal(SpellAimRejection.BlockedCenter, SpellAimResolver.Resolve(Map(BattleTileMovementFlags.Blocked), aim, null, new(true)).Rejection);
        Assert.Equal(SpellAimRejection.UnsupportedMap, SpellAimResolver.Resolve(new(257, 1, 1, Enumerable.Repeat(new BattleTile(0), 257).ToArray()), aim, null, new(true)).Rejection);
    }
}
