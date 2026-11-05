using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicResourceTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "magic-resource-contract-v1.json")));
    [Fact]
    public void Thirty_two_literal_mp_checks_preserve_order_modes_recorded_cost_and_signed_reads()
    {
        using var doc = Fixture();
        foreach (var row in doc.RootElement.GetProperty("mp").EnumerateArray())
        {
            var d = row.GetProperty("input"); var a = d.GetProperty("actor"); var s = d.GetProperty("ability");
            var request = new MagicMpRequest(new(a.GetProperty("mp").GetUInt16(), a.GetProperty("half").GetBoolean(),
                a.GetProperty("mime").GetBoolean(), a.GetProperty("frog").GetBoolean(), a.GetProperty("silence").GetBoolean(),
                a.GetProperty("dontAct").GetBoolean(), Enum.Parse<MagicActionRestriction>(a.GetProperty("restriction").GetString()!)),
                new(s.GetProperty("id").GetInt16(), s.GetProperty("cost").GetByte(), Enum.Parse<MagicResourceMenu>(s.GetProperty("menu").GetString()!), s.GetProperty("affected").GetBoolean()),
                Enum.Parse<MagicFormulaEvaluationMode>(d.GetProperty("mode").GetString()!), new(d.GetProperty("bookkeeping").GetBoolean()));
            var before = request with { }; var r = MagicResources.EvaluateMp(request); var e = row.GetProperty("expected");
            Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
            if (!r.Accepted) { Assert.Null(r.Outcome); continue; }
            var o = e.GetProperty("outcome");
            Assert.Equal(new MagicMpOutcome(Enum.Parse<MagicMpBlock>(o.GetProperty("block").GetString()!),
                o.GetProperty("cost").GetByte(), o.GetProperty("remaining").GetUInt16(), o.GetProperty("subtracted").GetBoolean()), r.Outcome);
            Assert.Equal(before, request);
        }
    }

    [Fact]
    public void Missing_bookkeeping_is_unsupported_and_invalid_inputs_throw()
    {
        var request = new MagicMpRequest(new(30, false, false, false, false, false, MagicActionRestriction.None),
            new(16, 9, MagicResourceMenu.Default, true), MagicFormulaEvaluationMode.Execute, null);
        Assert.False(MagicResources.EvaluateMp(request).Accepted);
        Assert.Throws<ArgumentNullException>(() => MagicResources.EvaluateMp(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicResources.EvaluateMp(request with { Mode = (MagicFormulaEvaluationMode)255 }));
        Assert.Throws<ArgumentNullException>(() => MagicResources.EvaluateMp(request with { Actor = null! }));
    }
}
