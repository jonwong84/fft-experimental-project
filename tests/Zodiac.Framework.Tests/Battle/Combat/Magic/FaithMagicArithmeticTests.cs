using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset","OriginalFft")]
public sealed class FaithMagicArithmeticTests
{
    internal static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","faith-magic-contract-v1.json")));
    internal static FaithValue Faith(JsonElement d) => new(d.GetProperty("stored").GetByte(),d.GetProperty("faithStatus").GetBoolean(),d.GetProperty("innocentStatus").GetBoolean());

    [Fact]
    public void Seven_product_vectors_preserve_the_signed_halfword_store()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("products").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("expected").GetInt16(),FaithMagicArithmetic.MultiplyStored(row.GetProperty("xa").GetInt16(),row.GetProperty("ya").GetInt16()));count++;
        }
        Assert.Equal(7,count);
    }

    [Fact]
    public void Fifteen_Faith_vectors_preserve_single_division_signed_truncation_status_priority_and_widths()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("faith").EnumerateArray())
        {
            var d=row.GetProperty("input");
            Assert.Equal(row.GetProperty("expected").GetInt16(),FaithMagicArithmetic.ScaleStored(d.GetProperty("amount").GetInt16(),Faith(d.GetProperty("caster")),Faith(d.GetProperty("target"))));count++;
        }
        Assert.Equal(15,count);
    }
}
