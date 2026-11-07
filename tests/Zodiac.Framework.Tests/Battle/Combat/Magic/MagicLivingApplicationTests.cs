using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicLivingApplicationTests
{
    internal static MagicLivingApplicationProfile Known => new(true, true, true, true, true, true);
    [Fact]
    public void Eighteen_living_descriptions_preserve_admission_status_boundaries_and_miss_order()
    {
        using var doc = MagicPublicationTestData.Fixture(); var template = MagicCastTestData.Ready().Caster;
        foreach (var row in doc.RootElement.GetProperty("living").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected");
            var current = template with { Mp = d.GetProperty("mp").GetUInt16(), Statuses = new(Chicken: d.GetProperty("chicken").GetBoolean(),
                Sleep: d.GetProperty("sleep").GetBoolean(), Confusion: d.GetProperty("confusion").GetBoolean(), Transparent: d.GetProperty("transparent").GetBoolean(),
                Dead: d.GetProperty("dead").GetBoolean(), Petrify: d.GetProperty("petrify").GetBoolean(), Wall: d.GetProperty("wall").GetBoolean()) };
            var vitals = new MagicUnitVitals(d.GetProperty("hp").GetUInt16(), d.GetProperty("maxHp").GetUInt16(), d.GetProperty("maxMp").GetUInt16(), d.GetProperty("brave").GetByte(), d.GetProperty("critical").GetBoolean());
            var frame = new MagicResultFrame(d.GetProperty("hit").GetBoolean(), 100, MagicResultMissKind.Hit,
                d.GetProperty("damage").GetInt16(), d.GetProperty("healing").GetInt16(), new(d.GetProperty("hpDamageType").GetBoolean()), new());
            var profile = new MagicLivingApplicationProfile(true, d.GetProperty("reactionsAbsent").GetBoolean(), d.GetProperty("golemSharingAbsent").GetBoolean(),
                d.GetProperty("otherWorkAbsent").GetBoolean(), d.GetProperty("ordinaryStatusRules").GetBoolean(), d.GetProperty("charmAbsent").GetBoolean());
            var r = MagicLivingApplication.Project(current, vitals, frame, profile);
            Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
            Assert.Equal(Enum.Parse<MagicLivingApplicationRejection>(e.GetProperty("rejection").GetString()!), r.Rejection);
            if (!r.Accepted) { Assert.Null(r.Outcome); continue; }
            Assert.Equal(e.GetProperty("hp").GetUInt16(), r.Outcome!.Vitals.Hp); Assert.Equal(e.GetProperty("mp").GetUInt16(), r.Outcome.Current.Mp);
            Assert.Equal(e.GetProperty("changed").GetBoolean(), r.Outcome.VitalsChanged);
            Assert.Equal(current.Statuses, r.Outcome.Current.Statuses); Assert.Equal(vitals.Critical, r.Outcome.Vitals.Critical);
        }
    }

    [Fact]
    public void Missing_active_or_reaction_knowledge_is_not_inferred_even_for_misses()
    {
        var c = MagicCastTestData.Ready().Caster; var v = new MagicUnitVitals(100, 100, 50, 70);
        var miss = new MagicResultFrame(false, 0, MagicResultMissKind.AccuracyMiss, 999, -10, new(true), new());
        Assert.False(MagicLivingApplication.Project(c, v, miss, null).Accepted);
        Assert.False(MagicLivingApplication.Project(c, v, miss, new(true)).Accepted);
        Assert.True(MagicLivingApplication.Project(c, v, miss, new(true, true)).Accepted);
        Assert.Equal((ushort)100, v.Hp); Assert.Equal((ushort)30, c.Mp);
    }

    [Fact]
    public void Numeric_hp_changes_do_not_depend_on_the_type_bit_but_status_cancellations_do()
    {
        var c = MagicCastTestData.Ready().Caster with { Statuses = new(Sleep: true) }; var v = new MagicUnitVitals(100, 100, 50, 70);
        var frame = new MagicResultFrame(true, 100, MagicResultMissKind.Hit, 34, 0, new(), new());
        var r = MagicLivingApplication.Project(c, v, frame, Known with { CharmKnownAbsent = false });
        Assert.True(r.Accepted); Assert.Equal((ushort)66, r.Outcome!.Vitals.Hp); Assert.True(r.Outcome.Current.Statuses.Sleep);
        Assert.False(MagicLivingApplication.Project(c, v, frame with { Types = new(true) }, Known).Accepted);
    }
}
