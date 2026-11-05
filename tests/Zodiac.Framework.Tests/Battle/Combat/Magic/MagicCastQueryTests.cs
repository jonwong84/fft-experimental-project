using Xunit;
using System.Collections.Immutable;
using System.Text.Json;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

internal static class MagicCastTestData
{
    internal static MagicReadyCastRequest Ready(MagicFormulaEvaluationMode mode = MagicFormulaEvaluationMode.Execute)
    {
        using var doc = MagicCurrentTestData.Fixture(); var r = MagicCurrentTestData.Request(doc.RootElement.GetProperty("queries")[0]);
        return new(r.Caster, new(new("fft:ability/test-fire"), 16, r.Spell, 9, 9, true), r.Weather, mode,
            r.Profile, new(true, true, true, true, true));
    }
}

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicCastQueryTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "magic-cast-contract-v1.json")));
    private static MagicReadyCastRequest Ready(JsonElement r)
    {
        var d = r.GetProperty("definition"); var p = r.GetProperty("resultProfile"); var pe = p.GetProperty("entry"); var ready = r.GetProperty("ready");
        return new(MagicCurrentTestData.Unit(r.GetProperty("caster")),
            new(new(d.GetProperty("id").GetString()!), d.GetProperty("originalId").GetInt16(), MagicCurrentTestData.Spell(r.GetProperty("spell")),
                d.GetProperty("cost").GetByte(), d.GetProperty("ct").GetByte(), d.GetProperty("affected").GetBoolean(), d.GetProperty("persevere").GetBoolean()),
            (MagicDamageWeather)r.GetProperty("weather").GetInt32(), Enum.Parse<MagicFormulaEvaluationMode>(r.GetProperty("mode").GetString()!),
            new(p.GetProperty("living").GetBoolean(), p.GetProperty("noBlocked").GetBoolean(), p.GetProperty("noGolem").GetBoolean(),
                p.GetProperty("emptyStatus").GetBoolean(), p.GetProperty("noKnockback").GetBoolean(), p.GetProperty("hpOnly").GetBoolean(),
                new(pe.GetProperty("primary").GetBoolean(), pe.GetProperty("noPending").GetBoolean(), pe.GetProperty("clearFlag").GetBoolean())),
            new(ready.GetProperty("chargeReady").GetBoolean(), ready.GetProperty("bookkeeping").GetBoolean(), ready.GetProperty("noHamedo").GetBoolean(), ready.GetProperty("single").GetBoolean(), ready.GetProperty("fresh").GetBoolean()));
    }

    [Fact]
    public void Thirteen_literal_casts_preserve_one_cost_order_modes_and_whole_query_rollback()
    {
        using var doc = Fixture();
        foreach (var row in doc.RootElement.GetProperty("casts").EnumerateArray())
        {
            var request = Ready(row); var prepared = MagicCastQuery.PrepareReady(request);
            Assert.True(prepared.Accepted); Assert.NotNull(prepared.Cast);
            var targets = row.GetProperty("targets").EnumerateArray().Select(MagicCurrentTestData.Unit).ToImmutableArray();
            var p = row.GetProperty("targetProfile"); var before = new RandomState(row.GetProperty("word").GetUInt32(), row.GetProperty("count").GetUInt64());
            var r = MagicCastQuery.ResolveTargets(prepared.Cast!, targets, new(p.GetProperty("order").GetBoolean(), p.GetProperty("noEarlier").GetBoolean()), before);
            var e = row.GetProperty("expected");
            Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
            Assert.Equal(Enum.Parse<FaithMagicResolutionRejection>(e.GetProperty("rejection").GetString()!), r.Rejection);
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), r.RngState);
            if (!r.Accepted) { Assert.Null(r.Outcome); Assert.Equal(before, r.RngState); continue; }
            var o = e.GetProperty("outcome"); Assert.Equal(o.GetProperty("mp").GetUInt16(), r.Outcome!.CandidateMp);
            Assert.Equal(o.GetProperty("cost").GetByte(), r.Outcome.RecordedCost);
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
            Assert.Equal((ushort)30, request.Caster.Mp);
        }
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Cast_replay_does_not_adopt_mp_rng_or_modify_target_snapshots()
    {
        using var doc = Fixture(); var row = doc.RootElement.GetProperty("casts")[0]; var request = Ready(row);
        var cast = MagicCastQuery.PrepareReady(request).Cast!;
        var targets = row.GetProperty("targets").EnumerateArray().Select(MagicCurrentTestData.Unit).ToImmutableArray();
        var copies = targets.ToArray(); var before = new RandomState(57, 9);
        var first = MagicCastQuery.ResolveTargets(cast, targets, new(true, true), before);
        var second = MagicCastQuery.ResolveTargets(cast, targets, new(true, true), before);
        Assert.Equal(first.RngState, second.RngState); Assert.Equal(first.Outcome!.CandidateMp, second.Outcome!.CandidateMp);
        Assert.Equal(first.Outcome.Targets.ToArray(), second.Outcome.Targets.ToArray());
        Assert.Equal(copies, targets.ToArray()); Assert.Equal((ushort)30, cast.Caster.Mp); Assert.Equal(new RandomState(57, 9), before);
    }

    [Fact]
    public void Missing_target_knowledge_and_conflicting_self_snapshots_reject_without_a_cast_outcome()
    {
        var request = MagicCastTestData.Ready(); var cast = MagicCastQuery.PrepareReady(request).Cast!; var before = new RandomState(57, 9);
        var targets = ImmutableArray.Create(request.Caster with { Faith = 71 });
        var r = MagicCastQuery.ResolveTargets(cast, targets, new(true, true), before);
        Assert.False(r.Accepted); Assert.Null(r.Outcome); Assert.Equal(before, r.RngState);
        Assert.False(MagicCastQuery.ResolveTargets(cast, default, new(true, true), before).Accepted);
        Assert.False(MagicCastQuery.ResolveTargets(cast, targets, null, before).Accepted);
        Assert.Throws<ArgumentNullException>(() => MagicCastQuery.ResolveTargets(null!, targets, new(true, true), before));
    }

    [Theory] [InlineData(MagicFormulaEvaluationMode.Execute, 21)] [InlineData(MagicFormulaEvaluationMode.AiSimulation, 21)]
    [InlineData(MagicFormulaEvaluationMode.Preview, 30)]
    public void Certified_ready_cast_prepares_one_mp_proposal_and_keeps_the_snapshot(MagicFormulaEvaluationMode mode, int candidate)
    {
        var request = MagicCastTestData.Ready(mode); var copy = request with { };
        Assert.Equal((byte)9, MagicCharge.CalculateSetup(request.Definition.RawChargeTime, false, false, false).Ticks);
        var result = MagicCastQuery.PrepareReady(request);
        Assert.True(result.Accepted); Assert.True(result.Resource!.Usable); Assert.NotNull(result.Cast);
        Assert.Equal((ushort)candidate, result.Resource.CandidateMp); Assert.Equal((byte)9, result.Resource.RecordedCost);
        Assert.Equal((ushort)30, result.Cast!.Caster.Mp); Assert.Equal(copy, request);
    }

    [Fact]
    public void Scheduling_and_uncertified_ready_context_never_propose_an_mp_subtraction()
    {
        var r = MagicCastTestData.Ready();
        MagicReadyCastProfile?[] profiles = [null, new(false, true, true, true, true), new(true, false, true, true, true),
            new(true, true, false, true, true), new(true, true, true, false, true), new(true, true, true, true, false)];
        foreach (var p in profiles)
        {
            var result = MagicCastQuery.PrepareReady(r with { ReadyProfile = p });
            Assert.False(result.Accepted); Assert.Null(result.Resource); Assert.Null(result.Cast);
        }
        Assert.False(MagicCastQuery.PrepareReady(r with { Caster = r.Caster with { IsMime = true } }).Accepted);
        Assert.False(MagicCastQuery.PrepareReady(r with { Caster = r.Caster with { Statuses = new(Charging: true) } }).Accepted);
    }

    [Fact]
    public void Source_mp_block_preserves_recorded_cost_without_a_prepared_cast()
    {
        var r = MagicCastTestData.Ready();
        var result = MagicCastQuery.PrepareReady(r with { Caster = r.Caster with { Mp = 8 } });
        Assert.True(result.Accepted); Assert.Null(result.Cast);
        Assert.Equal(new MagicMpOutcome(MagicMpBlock.NotEnoughMp, 9, 8, false), result.Resource);
        result = MagicCastQuery.PrepareReady(r with { Caster = r.Caster with { Statuses = new(Silence: true) } });
        Assert.Equal(new MagicMpOutcome(MagicMpBlock.Silenced, 0, 30, false), result.Resource); Assert.Null(result.Cast);
    }

    [Fact]
    public void Invalid_ready_cast_inputs_throw_without_a_proposal()
    {
        var r = MagicCastTestData.Ready();
        Assert.Throws<ArgumentNullException>(() => MagicCastQuery.PrepareReady(null!));
        Assert.Throws<ArgumentException>(() => MagicCastQuery.PrepareReady(r with { Definition = r.Definition with { Id = default } }));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicCastQuery.PrepareReady(r with { Mode = (MagicFormulaEvaluationMode)255 }));
    }
}
