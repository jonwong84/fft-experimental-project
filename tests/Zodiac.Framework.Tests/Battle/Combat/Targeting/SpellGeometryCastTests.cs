using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SpellGeometryCastTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "spell-geometry-cast-contract-v1.json")));

    private static SpellGeometryCastRequest Request(JsonElement row)
    {
        var d = row.GetProperty("definition"); var p = row.GetProperty("resultProfile"); var pe = p.GetProperty("entry");
        var ready = row.GetProperty("ready"); var e = row.GetProperty("entry");
        var definition = new MagicCastDefinition(new(d.GetProperty("id").GetString()!), d.GetProperty("originalId").GetInt16(),
            MagicCurrentTestData.Spell(row.GetProperty("spell")), d.GetProperty("cost").GetInt32(), d.GetProperty("ct").GetByte(),
            d.GetProperty("affected").GetBoolean(), d.GetProperty("persevere").GetBoolean(),
            row.GetProperty("geometryBound").GetBoolean() ? SpellTargetTestData.Geometry(row.GetProperty("geometry")) : null);
        var prepared = MagicCastQuery.PrepareReady(new(MagicCurrentTestData.Unit(row.GetProperty("caster")), definition,
            (MagicDamageWeather)row.GetProperty("weather").GetInt32(), Enum.Parse<MagicFormulaEvaluationMode>(row.GetProperty("mode").GetString()!),
            new(p.GetProperty("living").GetBoolean(), p.GetProperty("noBlocked").GetBoolean(), p.GetProperty("noGolem").GetBoolean(),
                p.GetProperty("emptyStatus").GetBoolean(), p.GetProperty("noKnockback").GetBoolean(), p.GetProperty("hpOnly").GetBoolean(),
                new(pe.GetProperty("primary").GetBoolean(), pe.GetProperty("noPending").GetBoolean(), pe.GetProperty("clearFlag").GetBoolean())),
            new(ready.GetProperty("chargeReady").GetBoolean(), ready.GetProperty("bookkeeping").GetBoolean(), ready.GetProperty("noHamedo").GetBoolean(),
                ready.GetProperty("single").GetBoolean(), ready.GetProperty("fresh").GetBoolean())));
        Assert.True(prepared.Accepted); Assert.NotNull(prepared.Cast);
        var roster = row.GetProperty("units").EnumerateArray().Select(u => new SpellTargetCombatant(
            SpellTargetTestData.Occupant(u.GetProperty("occupant")), u.GetProperty("current").ValueKind == JsonValueKind.Null ? null :
                MagicCurrentTestData.Unit(u.GetProperty("current")))).ToImmutableArray();
        return new(prepared.Cast!, SpellTargetTestData.Map(row.GetProperty("map")), SpellTargetTestData.Position(row.GetProperty("aim")), roster,
            SpellTargetTestData.Profile(row.GetProperty("profile")), new(row.GetProperty("complete").GetBoolean()),
            new(e.GetProperty("validatedAim").GetBoolean(), e.GetProperty("noEarlier").GetBoolean()));
    }

    [Fact]
    public void Thirteen_literal_geometry_casts_preserve_target_order_one_cost_modes_and_atomic_failure()
    {
        using var doc = Fixture();
        foreach (var row in doc.RootElement.GetProperty("casts").EnumerateArray())
        {
            var q = Request(row); var before = new RandomState(row.GetProperty("word").GetUInt32(), row.GetProperty("count").GetUInt64());
            var r = DefaultAbilityTargeting.ResolveCast(q, before); var e = row.GetProperty("expected");
            Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
            Assert.Equal(Enum.Parse<FaithMagicResolutionRejection>(e.GetProperty("rejection").GetString()!), r.Rejection);
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), r.RngState);
            if (!r.Accepted) { Assert.Null(r.Outcome); Assert.Equal(before, r.RngState); continue; }
            var o = e.GetProperty("outcome"); Assert.Equal(o.GetProperty("mp").GetUInt16(), r.Outcome!.CandidateMp);
            Assert.Equal(o.GetProperty("cost").GetInt32(), r.Outcome.RecordedCost);
            Assert.Equal(o.GetProperty("targets").GetArrayLength(), r.Outcome.Targets.Length);
            for (int i = 0; i < r.Outcome.Targets.Length; i++)
            {
                var t = o.GetProperty("targets")[i]; var actual = r.Outcome.Targets[i];
                Assert.Equal(t.GetProperty("id").GetString(), actual.TargetId.Value);
                Assert.Equal(MagicCurrentTestData.Frame(t.GetProperty("result")), actual.Result);
                var ev = t.GetProperty("evasion");
                if (ev.ValueKind == JsonValueKind.Null) Assert.Null(actual.Evasion);
                else Assert.Equal(new MagicEvasionOutcome(ev.GetProperty("hit").GetBoolean(), ev.GetProperty("accuracy").GetUInt16(),
                    (MagicResultMissKind)ev.GetProperty("miss").GetByte(), ev.GetProperty("itemLost").GetByte(), ev.GetProperty("abandon").GetBoolean()), actual.Evasion);
                var proc = t.GetProperty("procDisposition");
                if (proc.ValueKind == JsonValueKind.Null) Assert.Null(actual.Proc);
                else { Assert.Equal(Enum.Parse<MagicProcDisposition>(proc.GetString()!), actual.Proc!.Disposition); Assert.Equal(t.GetProperty("setFlag").GetBoolean(), actual.Proc.SetAddedEffectFlag); }
            }
            Assert.Equal((ushort)30, q.Cast.Caster.Mp);
        }
    }

    [Fact]
    public void Missing_knowledge_and_conflicting_metadata_never_publish_a_partial_cast()
    {
        using var doc = Fixture(); var q = Request(doc.RootElement.GetProperty("casts")[0]); var before = new RandomState(57, 9);
        var actor = q.Roster.Single(u => u.Occupant.UnitId == q.Cast.Caster.UnitId);
        SpellGeometryCastRequest[] invalid = [q with { Entry = null }, q with { Entry = new(false, true) },
            q with { GeometryProfile = null }, q with { RosterProfile = null }, q with { Roster = default },
            q with { SelectedMapAim = new(3, 1, 0) }, q with { Roster = q.Roster.Add(actor) },
            q with { Roster = q.Roster.Replace(actor, actor with { Current = actor.Current! with { Faith = 71 } }) },
            q with { Roster = q.Roster.Replace(actor, actor with { Occupant = actor.Occupant with { Jump = true } }) },
            q with { Roster = q.Roster.SetItem(0, q.Roster[0] with { Current = q.Cast.Caster }) }];
        foreach (var r in invalid)
        { var result = DefaultAbilityTargeting.ResolveCast(r, before); Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(before, result.RngState); }
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Replay_preserves_roster_map_cost_and_rng_inputs()
    {
        using var doc = Fixture(); var q = Request(doc.RootElement.GetProperty("casts")[0]); var before = new RandomState(57, 9);
        var roster = q.Roster.ToArray(); q.Map.TryGetTile(new(1, 1, 0), out var tile);
        var a = DefaultAbilityTargeting.ResolveCast(q, before); var b = DefaultAbilityTargeting.ResolveCast(q, before);
        Assert.Equal(a.RngState, b.RngState); Assert.Equal(a.Outcome!.Targets.ToArray(), b.Outcome!.Targets.ToArray());
        Assert.Equal((ushort)21, a.Outcome.CandidateMp); Assert.Equal((ushort)30, q.Cast.Caster.Mp);
        Assert.Equal(roster, q.Roster.ToArray()); q.Map.TryGetTile(new(1, 1, 0), out var after); Assert.Equal(tile, after);
        Assert.Equal(new RandomState(57, 9), before);
    }
}
