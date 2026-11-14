using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

internal static class MagicCurrentTestData
{
    internal static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "magic-current-input-contract-v1.json")));
    internal static MagicZodiacIdentity Identity(JsonElement d) => new((MagicZodiacSign)d.GetProperty("sign").GetInt32(),
        d.GetProperty("male").GetBoolean(), d.GetProperty("female").GetBoolean(), d.GetProperty("monster").GetBoolean());
    internal static bool Flag(JsonElement d, string name) => d.TryGetProperty(name, out var v) && v.GetBoolean();
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    internal static MagicCurrentUnit Unit(JsonElement d)
    {
        var a = d.GetProperty("affinities"); var eq = d.GetProperty("equipment");
        return new(new(d.GetProperty("id").GetString()!), d.GetProperty("ma").GetByte(), d.GetProperty("faith").GetByte(),
            Identity(d.GetProperty("identity")), d.GetProperty("statuses").Deserialize<MagicCurrentStatuses>(Options)!,
            d.GetProperty("supports").Deserialize<MagicCurrentSupports>(Options)!, OriginalElementEncoding.FromByte(d.GetProperty("strengthen").GetByte()),
            new(OriginalElementEncoding.FromByte(a.GetProperty("absorb").GetByte()), OriginalElementEncoding.FromByte(a.GetProperty("nullify").GetByte()),
                OriginalElementEncoding.FromByte(a.GetProperty("half").GetByte()), OriginalElementEncoding.FromByte(a.GetProperty("weak").GetByte())),
            eq.ValueKind == JsonValueKind.Null ? null : MagicEvasionTestData.Equipment(eq),
            d.GetProperty("unrestricted").GetBoolean(), d.GetProperty("rider").GetBoolean(), d.GetProperty("mp").GetUInt16());
    }
    internal static FaithMagicSpell Spell(JsonElement d) => new(Enum.Parse<FaithMagicAmountKind>(d.GetProperty("kind").GetString()!),
        d.GetProperty("power").GetByte(), OriginalElementEncoding.FromByte(d.GetProperty("elements").GetByte()), d.GetProperty("evadeable").GetBoolean());
    internal static FaithMagicCurrentRequest Request(JsonElement r)
    {
        var p = r.GetProperty("profile"); var pe = p.GetProperty("entry"); var entry = r.GetProperty("entry");
        return new(Unit(r.GetProperty("caster")), Unit(r.GetProperty("target")), Spell(r.GetProperty("spell")),
            (MagicDamageWeather)r.GetProperty("weather").GetInt32(), Enum.Parse<MagicFormulaEvaluationMode>(r.GetProperty("mode").GetString()!),
            new(p.GetProperty("living").GetBoolean(), p.GetProperty("noBlocked").GetBoolean(), p.GetProperty("noGolem").GetBoolean(),
                p.GetProperty("emptyStatus").GetBoolean(), p.GetProperty("noKnockback").GetBoolean(), p.GetProperty("hpOnly").GetBoolean(),
                new(pe.GetProperty("primary").GetBoolean(), pe.GetProperty("noPending").GetBoolean(), pe.GetProperty("clearFlag").GetBoolean())),
            new(entry.GetProperty("fresh").GetBoolean(), entry.GetProperty("noEarlier").GetBoolean(), entry.GetProperty("nonzero").GetBoolean()));
    }
    internal static MagicResultFrame Frame(JsonElement d)
    {
        var t = d.GetProperty("types"); var e = d.GetProperty("effects");
        return new(d.GetProperty("hit").GetBoolean(), d.GetProperty("accuracy").GetUInt16(), (MagicResultMissKind)d.GetProperty("miss").GetByte(),
            d.GetProperty("damage").GetInt16(), d.GetProperty("healing").GetInt16(),
            new(t.GetProperty("damage").GetBoolean(), t.GetProperty("healing").GetBoolean(), t.GetProperty("pseudo").GetBoolean()),
            new(e.GetProperty("absorb").GetBoolean(), e.GetProperty("weak").GetBoolean(), e.GetProperty("added").GetBoolean()), d.GetProperty("clear").GetBoolean());
    }
    internal static void AssertResult(FaithMagicResolutionResult r, JsonElement e)
    {
        Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
        Assert.Equal(Enum.Parse<FaithMagicResolutionRejection>(e.GetProperty("rejection").GetString()!), r.Rejection);
        Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), r.RngState);
        if (!r.Accepted) { Assert.Null(r.Outcome); Assert.Null(r.Evasion); Assert.Null(r.Proc); return; }
        Assert.Equal(Frame(e.GetProperty("outcome")), r.Outcome);
        var ev = e.GetProperty("evasion");
        if (ev.ValueKind == JsonValueKind.Null) Assert.Null(r.Evasion);
        else Assert.Equal(new MagicEvasionOutcome(ev.GetProperty("hit").GetBoolean(), ev.GetProperty("accuracy").GetUInt16(),
            (MagicResultMissKind)ev.GetProperty("miss").GetByte(), ev.GetProperty("itemLost").GetByte(), ev.GetProperty("abandon").GetBoolean()), r.Evasion);
        var proc = e.GetProperty("procDisposition");
        if (proc.ValueKind == JsonValueKind.Null) Assert.Null(r.Proc);
        else { Assert.Equal(Enum.Parse<MagicProcDisposition>(proc.GetString()!), r.Proc!.Disposition); Assert.Equal(e.GetProperty("setFlag").GetBoolean(), r.Proc.SetAddedEffectFlag); }
    }
}

[Trait("Ruleset", "OriginalFft")]
public sealed class FaithMagicCurrentInputTests
{
    [Fact]
    public void Eleven_literal_current_pair_queries_preserve_full_results_modes_and_rng_checkpoints()
    {
        using var doc = MagicCurrentTestData.Fixture();
        foreach (var row in doc.RootElement.GetProperty("queries").EnumerateArray())
            MagicCurrentTestData.AssertResult(FaithMagicInputs.Resolve(MagicCurrentTestData.Request(row),
                new(row.GetProperty("word").GetUInt32(), row.GetProperty("count").GetUInt64())), row.GetProperty("expected"));
    }

    [Fact]
    public void Unsupported_current_state_rejects_without_private_results_or_rng()
    {
        using var doc = MagicCurrentTestData.Fixture(); var request = MagicCurrentTestData.Request(doc.RootElement.GetProperty("queries")[0]);
        var before = new RandomState(57, 9);
        var r = FaithMagicInputs.Resolve(request with { Target = request.Target with { Statuses = new(Dead: true) } }, before);
        Assert.False(r.Accepted); Assert.Null(r.Outcome); Assert.Null(r.Evasion); Assert.Null(r.Proc); Assert.Equal(before, r.RngState);
        Assert.Throws<ArgumentNullException>(() => FaithMagicInputs.Resolve(null!, before));
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Current_pair_replay_preserves_both_snapshots()
    {
        using var doc = MagicCurrentTestData.Fixture(); var request = MagicCurrentTestData.Request(doc.RootElement.GetProperty("queries")[0]);
        var copy = request with { }; var before = new RandomState(57, 9); var r = FaithMagicInputs.Resolve(request, before);
        Assert.Equal(r, FaithMagicInputs.Resolve(request, before)); Assert.Equal(copy, request); Assert.Equal(new RandomState(57, 9), before);
    }

    [Fact]
    public void Twelve_current_input_projections_preserve_stored_bytes_and_derive_named_facts()
    {
        using var doc = MagicCurrentTestData.Fixture();
        foreach (var row in doc.RootElement.GetProperty("projections").EnumerateArray())
        {
            var c = MagicCurrentTestData.Unit(row.GetProperty("caster")); var t = MagicCurrentTestData.Unit(row.GetProperty("target"));
            var cBefore = c with { }; var tBefore = t with { };
            var r = FaithMagicInputs.Project(c, t, MagicCurrentTestData.Spell(row.GetProperty("spell")), (MagicDamageWeather)row.GetProperty("weather").GetInt32());
            var e = row.GetProperty("expected"); Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
            if (!r.Accepted) { Assert.Null(r.Outcome); continue; }
            Assert.Equal(MagicElementTestData.Input(e.GetProperty("amount")), r.Outcome!.Amount);
            Assert.Equal(t.Statuses.Undead, r.Outcome.TargetIsUndead);
            if (r.Outcome.Amount.Kind == FaithMagicAmountKind.Healing)
            { Assert.Null(r.Outcome.Elements); Assert.Null(r.Outcome.Equipment); Assert.Null(r.Outcome.EvadeModifiers); }
            else { Assert.Equal(t.Equipment, r.Outcome.Equipment); Assert.NotNull(r.Outcome.Elements); Assert.NotNull(r.Outcome.EvadeModifiers); }
            Assert.Equal(cBefore, c); Assert.Equal(tBefore, t);
        }
    }

    [Fact]
    public void Consistent_self_target_is_supported_but_conflicting_same_ID_is_not()
    {
        var unit = new MagicCurrentUnit(new("self"), 5, 70, new(MagicZodiacSign.Aries), new(), new());
        var spell = new FaithMagicSpell(FaithMagicAmountKind.Healing, 14, ElementToggles.None, false);
        Assert.True(FaithMagicInputs.Project(unit, unit with { }, spell, MagicDamageWeather.None).Accepted);
        Assert.False(FaithMagicInputs.Project(unit, unit with { Faith = 71 }, spell, MagicDamageWeather.None).Accepted);
        Assert.Throws<ArgumentException>(() => FaithMagicInputs.Project(unit with { UnitId = default }, unit, spell, MagicDamageWeather.None));
        Assert.Throws<ArgumentNullException>(() => FaithMagicInputs.Project(null!, unit, spell, MagicDamageWeather.None));
    }

    [Fact]
    public void Thirty_three_literal_compatibilities_preserve_sign_table_and_exact_identity_flags()
    {
        using var doc = MagicCurrentTestData.Fixture();
        foreach (var r in doc.RootElement.GetProperty("compatibility").EnumerateArray())
            Assert.Equal(Enum.Parse<MagicCompatibility>(r.GetProperty("expected").GetString()!),
                FaithMagicInputs.ClassifyCompatibility(MagicCurrentTestData.Identity(r.GetProperty("caster")), MagicCurrentTestData.Identity(r.GetProperty("target"))));
    }

    [Theory] [InlineData(13)] [InlineData(255)]
    public void Unknown_signs_reject_without_indexing_the_source_table(int sign)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FaithMagicInputs.ClassifyCompatibility(new((MagicZodiacSign)sign), new(MagicZodiacSign.Aries)));
        Assert.Throws<ArgumentOutOfRangeException>(() => FaithMagicInputs.ClassifyCompatibility(new(MagicZodiacSign.Serpentarius), new((MagicZodiacSign)sign)));
    }
}
