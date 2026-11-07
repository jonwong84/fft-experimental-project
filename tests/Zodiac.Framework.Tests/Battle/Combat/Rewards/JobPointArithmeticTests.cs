using System.Collections.Immutable;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class JobPointArithmeticTests
{
    internal static ImmutableArray<ushort> Thresholds => [100, 200, 350, 550, 800, 1150, 1550, 2100];

    [Fact]
    public void Eleven_literals_cover_independent_caps_zero_awards_and_threshold_counting()
    {
        using var fixture = ActionRewardTestData.Fixture();
        foreach (var row in fixture.RootElement.GetProperty("jobPoints").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected");
            Assert.Equal(new(e[0].GetUInt16(), e[1].GetUInt16(), e[2].GetInt32()),
                JobPointArithmetic.Add(d[0].GetUInt16(), d[1].GetUInt16(), d[2].GetInt32(), d[3].EnumerateArray().Select(v => v.GetUInt16()).ToImmutableArray()));
        }
    }

    [Fact]
    public void Every_prepared_threshold_uses_greater_than_or_equal_and_total_instead_of_current()
    {
        for (int i = 0; i < Thresholds.Length; i++)
        {
            ushort threshold = Thresholds[i];
            Assert.Equal(i, JobPointArithmetic.Add(9999, (ushort)(threshold - 1), 0, Thresholds).JobLevel);
            Assert.Equal(i + 1, JobPointArithmetic.Add(0, threshold, 0, Thresholds).JobLevel);
        }
    }

    [Fact]
    public void Original_job_adapter_retains_special_job_fallback_and_generic_endpoints()
    {
        foreach (var (job, slot) in new (byte, int)[] { (0, 0), (1, 0), (74, 0), (75, 1), (91, 17), (92, 18), (93, 19), (94, 0), (255, 0) })
            Assert.Equal(slot, JobPointArithmetic.OriginalRewardSlot(job));
    }

    [Fact]
    public void Negative_overflow_domain_and_unknown_threshold_data_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JobPointArithmetic.Add(0, 0, -1, Thresholds));
        Assert.Throws<ArgumentOutOfRangeException>(() => JobPointArithmetic.Add(0, 0, int.MaxValue, Thresholds));
        Assert.Throws<ArgumentException>(() => JobPointArithmetic.Add(0, 0, 1, default));
        Assert.Equal(new(0, 0, 0), JobPointArithmetic.Add(0, 0, 0, []));
    }
}
