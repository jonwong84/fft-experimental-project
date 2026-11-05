using System.Text.Json;
using Xunit;

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
            d.GetProperty("supports").Deserialize<MagicCurrentSupports>(Options)!, (MagicElement)d.GetProperty("strengthen").GetByte(),
            new((MagicElement)a.GetProperty("absorb").GetByte(), (MagicElement)a.GetProperty("nullify").GetByte(),
                (MagicElement)a.GetProperty("half").GetByte(), (MagicElement)a.GetProperty("weak").GetByte()),
            eq.ValueKind == JsonValueKind.Null ? null : MagicEvasionTestData.Equipment(eq),
            d.GetProperty("unrestricted").GetBoolean(), d.GetProperty("rider").GetBoolean(), d.GetProperty("mp").GetUInt16());
    }
    internal static FaithMagicSpell Spell(JsonElement d) => new(Enum.Parse<FaithMagicAmountKind>(d.GetProperty("kind").GetString()!),
        d.GetProperty("power").GetByte(), (MagicElement)d.GetProperty("elements").GetByte(), d.GetProperty("evadeable").GetBoolean());
}

[Trait("Ruleset", "OriginalFft")]
public sealed class FaithMagicCurrentInputTests
{
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
        var spell = new FaithMagicSpell(FaithMagicAmountKind.Healing, 14, MagicElement.None, false);
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
