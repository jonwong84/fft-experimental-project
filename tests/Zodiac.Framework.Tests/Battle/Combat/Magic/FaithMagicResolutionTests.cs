using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class FaithMagicResolutionTests
{
    private static readonly MagicResultFinalizationProfile Known = new(true, true, true, true, true, true, new(true, true, true));
    private static FaithMagicResolutionRequest Request() => new(new(new(FaithMagicAmountKind.Damage, 5, 14, new(70), new(70)),
        false, 100, MagicFormulaEvaluationMode.Execute, Known,
        new(MagicElement.Fire, MagicDamageWeather.None, new(), new(true, true, true))), new(true, true, true), true,
        new(false, new(255, 0, false), new(255, 0, false), new(255, 0, false)), new(true));

    [Fact]
    public void Twenty_four_literal_entries_cover_evasion_tail_bypass_and_whole_query_atomicity()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "magic-entry-contract-v1.json")));
        foreach (var row in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            var f = row.GetProperty("formula"); var entry = row.GetProperty("entry");
            var request = new FaithMagicResolutionRequest(new(MagicElementTestData.Input(f.GetProperty("input")),
                f.GetProperty("targetIsUndead").GetBoolean(), f.GetProperty("accuracy").GetUInt16(),
                Enum.Parse<MagicFormulaEvaluationMode>(f.GetProperty("mode").GetString()!), Profile(f.GetProperty("profile")),
                MagicElementTestData.Context(f.GetProperty("context"))),
                new(entry.GetProperty("fresh").GetBoolean(), entry.GetProperty("noEarlier").GetBoolean(), entry.GetProperty("nonzero").GetBoolean()),
                row.GetProperty("evadeable").GetBoolean(), MagicEvasionTestData.Equipment(row.GetProperty("equipment")),
                MagicEvasionTestData.Modifiers(row.GetProperty("modifiers")));
            var before = new RandomState(row.GetProperty("word").GetUInt32(), row.GetProperty("count").GetUInt64());
            var r = FaithMagicFormula.Resolve(request, before); var e = row.GetProperty("expected");
            Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
            Assert.Equal(Enum.Parse<FaithMagicResolutionRejection>(e.GetProperty("rejection").GetString()!), r.Rejection);
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), r.RngState);
            if (!r.Accepted)
            { Assert.Null(r.Outcome); Assert.Null(r.Evasion); Assert.Null(r.Proc); Assert.Equal(before, r.RngState); continue; }
            Assert.Equal(Frame(e.GetProperty("outcome")), r.Outcome);
            var ev = e.GetProperty("evasion");
            if (ev.ValueKind == JsonValueKind.Null) Assert.Null(r.Evasion);
            else Assert.Equal(new MagicEvasionOutcome(ev.GetProperty("hit").GetBoolean(), ev.GetProperty("accuracy").GetUInt16(),
                (MagicResultMissKind)ev.GetProperty("miss").GetByte(), ev.GetProperty("itemLost").GetByte(), ev.GetProperty("abandon").GetBoolean()), r.Evasion);
            var proc = e.GetProperty("procDisposition");
            if (proc.ValueKind == JsonValueKind.Null) Assert.Null(r.Proc);
            else
            {
                Assert.Equal(Enum.Parse<MagicProcDisposition>(proc.GetString()!), r.Proc!.Disposition);
                Assert.Equal(e.GetProperty("setFlag").GetBoolean(), r.Proc.SetAddedEffectFlag);
            }
        }
    }

    [Fact]
    public void Every_unsupported_finalizer_certificate_rejects_before_any_evasion_output_or_draws()
    {
        MagicResultFinalizationProfile?[] profiles = [null, Known with { LivingTarget = false },
            Known with { NoPetrifyOrWall = false }, Known with { NoGolem = false }, Known with { EmptyStatusRecord = false },
            Known with { NoPendingKnockback = false }, Known with { OnlySupportedHpResultWork = false },
            Known with { EntryProcContext = null }, Known with { EntryProcContext = new(false, true, true) },
            Known with { EntryProcContext = new(true, false, true) }];
        var request = Request(); var before = new RandomState(57, 9);
        foreach (var p in profiles)
        {
            var r = FaithMagicFormula.Resolve(request with { Formula = request.Formula with { Profile = p } }, before);
            Assert.False(r.Accepted); Assert.Equal(FaithMagicResolutionRejection.UnsupportedContext, r.Rejection);
            Assert.Null(r.Outcome); Assert.Null(r.Evasion); Assert.Null(r.Proc); Assert.Equal(before, r.RngState);
        }
        Assert.False(FaithMagicFormula.Resolve(request with { Entry = null }, before).Accepted);
    }

    [Fact]
    public void Healing_entry_needs_no_evasion_inputs_and_no_damage_element_context()
    {
        var request = Request();
        request = request with { Formula = request.Formula with { Amount = request.Formula.Amount with { Kind = FaithMagicAmountKind.Healing }, Elements = null },
            Equipment = null, EvadeModifiers = null };
        var before = new RandomState(1, ulong.MaxValue); var r = FaithMagicFormula.Resolve(request, before);
        Assert.True(r.Accepted); Assert.Null(r.Evasion); Assert.Equal(before, r.RngState);
        Assert.Equal(new MagicResultFrame(true, 100, MagicResultMissKind.Hit, 0, 34, new(false, true), new()), r.Outcome);
        Assert.Equal(MagicProcDisposition.NoConditionalBranch, r.Proc!.Disposition);
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Entry_replay_preserves_inputs_and_candidate_RNG_has_no_implicit_adoption()
    {
        var request = Request(); var copy = request with { }; var before = new RandomState(57, 9);
        var r = FaithMagicFormula.Resolve(request, before);
        Assert.Equal(r, FaithMagicFormula.Resolve(request, before)); Assert.Equal(copy, request);
        Assert.Equal(new RandomState(57, 9), before); Assert.Equal(14ul, r.RngState.DrawCount);
    }

    [Fact]
    public void Invalid_required_inputs_or_modes_throw_and_damage_preparation_is_required_even_if_not_evadeable()
    {
        var request = Request(); var before = new RandomState(1, 0);
        Assert.Throws<ArgumentNullException>(() => FaithMagicFormula.Resolve(null!, before));
        Assert.Throws<ArgumentNullException>(() => FaithMagicFormula.Resolve(request with { Formula = null! }, before));
        Assert.Throws<ArgumentNullException>(() => FaithMagicFormula.Resolve(request with { Formula = request.Formula with { Amount = null! } }, before));
        Assert.Throws<ArgumentNullException>(() => FaithMagicFormula.Resolve(request with { SecondaryEvadeable = false, Equipment = null }, before));
        Assert.Throws<ArgumentNullException>(() => FaithMagicFormula.Resolve(request with { EvadeModifiers = null }, before));
        Assert.Throws<ArgumentOutOfRangeException>(() => FaithMagicFormula.Resolve(request with { Formula = request.Formula with { Mode = (MagicFormulaEvaluationMode)255 } }, before));
    }

    private static MagicResultFinalizationProfile Profile(JsonElement p)
    {
        var e = p.GetProperty("entry");
        return new(p.GetProperty("living").GetBoolean(), p.GetProperty("noBlocked").GetBoolean(), p.GetProperty("noGolem").GetBoolean(),
            p.GetProperty("emptyStatus").GetBoolean(), p.GetProperty("noKnockback").GetBoolean(), p.GetProperty("hpOnly").GetBoolean(),
            new(e.GetProperty("primary").GetBoolean(), e.GetProperty("noPending").GetBoolean(), e.GetProperty("clearFlag").GetBoolean()));
    }
    private static MagicResultFrame Frame(JsonElement d)
    {
        var t = d.GetProperty("types"); var e = d.GetProperty("effects");
        return new(d.GetProperty("hit").GetBoolean(), d.GetProperty("accuracy").GetUInt16(), (MagicResultMissKind)d.GetProperty("miss").GetByte(),
            d.GetProperty("damage").GetInt16(), d.GetProperty("healing").GetInt16(),
            new(t.GetProperty("damage").GetBoolean(), t.GetProperty("healing").GetBoolean(), t.GetProperty("pseudo").GetBoolean()),
            new(e.GetProperty("absorb").GetBoolean(), e.GetProperty("weak").GetBoolean(), e.GetProperty("added").GetBoolean()), d.GetProperty("clear").GetBoolean());
    }
}
