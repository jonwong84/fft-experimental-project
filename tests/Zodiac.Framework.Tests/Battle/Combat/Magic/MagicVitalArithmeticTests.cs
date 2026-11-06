using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicVitalArithmeticTests
{
    [Fact]
    public void Eighteen_literal_vectors_preserve_signed_fields_unsigned_storage_and_clamp_order()
    {
        using var doc = MagicPublicationTestData.Fixture();
        foreach (var row in doc.RootElement.GetProperty("vitals").EnumerateArray())
        {
            var d = MagicPublicationTestData.Vital(row.GetProperty("input")); var e = row.GetProperty("expected");
            Assert.Equal(new MagicVitalChange(e.GetProperty("hp").GetUInt16(), e.GetProperty("mp").GetUInt16(),
                e.GetProperty("changed").GetBoolean()), MagicVitalArithmetic.Apply(d));
        }
    }

    [Fact]
    public void Miss_skips_even_over_maximum_clamps_and_inputs_remain_immutable()
    {
        var d = new MagicVitalInput(200, 100, 60, 50, false, 300, -10, 50);
        var copy = d with { }; var a = MagicVitalArithmetic.Apply(d); var b = MagicVitalArithmetic.Apply(d);
        Assert.Equal(new(200, 60, false), a); Assert.Equal(a, b); Assert.Equal(copy, d);
        Assert.Throws<ArgumentNullException>(() => MagicVitalArithmetic.Apply(null!));
    }
}
