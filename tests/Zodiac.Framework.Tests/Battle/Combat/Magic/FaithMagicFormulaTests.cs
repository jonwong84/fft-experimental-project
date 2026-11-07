using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class FaithMagicFormulaTests
{
    private static readonly MagicResultFinalizationProfile Known = new(true, true, true, true, true, true, new(true, true, true));
    private static FaithMagicFormulaRequest Request(bool weak = false) => new(
        new(FaithMagicAmountKind.Damage, 5, 14, new(70), new(70)), false, 100,
        MagicFormulaEvaluationMode.Execute, Known,
        new(MagicElement.Fire, MagicDamageWeather.None,
            new(Weak: weak ? MagicElement.Fire : MagicElement.None), new(true, true, true)));

    [Fact]
    public void Shared_entry_matches_twenty_existing_literal_composed_results_from_raw_inputs()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "magic-finalization-contract-v1.json")));
        foreach (var row in doc.RootElement.GetProperty("composed").EnumerateArray())
        {
            var request = new FaithMagicFormulaRequest(MagicElementTestData.Input(row.GetProperty("input")),
                row.GetProperty("targetIsUndead").GetBoolean(), row.GetProperty("accuracy").GetUInt16(),
                Enum.Parse<MagicFormulaEvaluationMode>(row.GetProperty("mode").GetString()!),
                Profile(row.GetProperty("profile")), MagicElementTestData.Context(row.GetProperty("context")));
            var before = new RandomState(row.GetProperty("word").GetUInt32(), row.GetProperty("count").GetUInt64());
            var r = FaithMagicFormula.ResolveAfterHit(request, before);
            var e = row.GetProperty("expected");
            Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
            Assert.Equal(Enum.Parse<MagicResultFinalizationRejection>(e.GetProperty("rejection").GetString()!), r.Finalization.Rejection);
            Assert.Equal(Enum.Parse<MagicProcRejection>(e.GetProperty("procRejection").GetString()!), r.ProcRejection);
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), r.RngState);
            if (!r.Accepted)
            {
                Assert.Null(r.Finalization.Outcome); Assert.Null(r.Proc); Assert.Equal(before, r.RngState);
                continue;
            }
            Assert.Equal(Frame(e.GetProperty("outcome")), r.Finalization.Outcome);
            Assert.Equal(Enum.Parse<MagicProcDisposition>(e.GetProperty("procDisposition").GetString()!), r.Proc!.Disposition);
            Assert.Equal(e.GetProperty("setFlag").GetBoolean(), r.Proc.SetAddedEffectFlag);
        }
    }

    [Fact]
    public void Caller_ordered_targets_share_calculation_and_continue_RNG_without_target_state_leaking()
    {
        // Entry seed engineered for raw0; the second full-word checkpoint is literal LCG arithmetic.
        var before = new RandomState(unchecked((0x1234u - 12345u) * 4005161829u), 9);
        var first = FaithMagicFormula.ResolveAfterHit(Request(), before);
        Assert.Equal((short)34, first.Finalization.Outcome!.HpDamage);
        Assert.True(first.Finalization.Outcome.Effects.AddedEffect);
        Assert.Equal(new RandomState(0x1234, 10), first.RngState);

        var rejected = FaithMagicFormula.ResolveAfterHit(Request() with { Profile = null }, first.RngState);
        Assert.False(rejected.Accepted); Assert.Null(rejected.Finalization.Outcome); Assert.Null(rejected.Proc);
        Assert.Equal(first.RngState, rejected.RngState);

        var second = FaithMagicFormula.ResolveAfterHit(Request(weak: true), rejected.RngState);
        Assert.Equal((short)68, second.Finalization.Outcome!.HpDamage);
        Assert.True(second.Finalization.Outcome.Effects.Weakness);
        Assert.False(second.Finalization.Outcome.Effects.AddedEffect);
        Assert.True(second.Finalization.Outcome.Types.PseudoStatus);
        Assert.Equal(new RandomState(0x4dcbc85d, 11), second.RngState);
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Repeating_the_same_request_is_deterministic_and_preserves_its_inputs()
    {
        var request = Request(); var copy = request with { }; var rng = new RandomState(1, 7);
        var first = FaithMagicFormula.ResolveAfterHit(request, rng);
        Assert.Equal(first, FaithMagicFormula.ResolveAfterHit(request, rng));
        Assert.Equal(copy, request); Assert.Equal(new RandomState(1, 7), rng);
    }

    [Fact]
    public void Healing_requires_no_damage_element_context_and_keeps_undead_routing()
    {
        var request = Request() with
        {
            Amount = new(FaithMagicAmountKind.Healing, 5, 14, new(70), new(70)),
            TargetIsUndead = true, Elements = null
        };
        var before = new RandomState(1, ulong.MaxValue);
        var r = FaithMagicFormula.ResolveAfterHit(request, before);
        Assert.True(r.Accepted); Assert.Equal(before, r.RngState);
        Assert.Equal(new MagicResultFrame(true, 100, MagicResultMissKind.Hit, 34, 0,
            new(true), new(), true), r.Finalization.Outcome);
        Assert.Equal(MagicProcDisposition.NoConditionalBranch, r.Proc!.Disposition);
    }

    [Fact]
    public void Required_inputs_and_known_formula_modes_retain_their_argument_guards()
    {
        var rng = new RandomState(1, 0);
        Assert.Throws<ArgumentNullException>(() => FaithMagicFormula.ResolveAfterHit(null!, rng));
        Assert.Throws<ArgumentNullException>(() => FaithMagicFormula.ResolveAfterHit(Request() with { Amount = null! }, rng));
        Assert.Throws<ArgumentNullException>(() => FaithMagicFormula.ResolveAfterHit(Request() with { Elements = null }, rng));
        Assert.Throws<ArgumentOutOfRangeException>(() => FaithMagicFormula.ResolveAfterHit(Request() with { Mode = (MagicFormulaEvaluationMode)255 }, rng));
        Assert.Throws<ArgumentOutOfRangeException>(() => FaithMagicFormula.ResolveAfterHit(Request() with { Amount = Request().Amount with { Kind = (FaithMagicAmountKind)255 } }, rng));
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
