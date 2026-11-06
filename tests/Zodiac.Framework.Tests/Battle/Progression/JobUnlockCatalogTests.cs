using System.Collections.Immutable;
using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class JobUnlockCatalogTests
{
    internal static AssetId Id(string name) => new("fft.ps1:job/" + name);
    private static ImmutableArray<JobUnlockDefinition> Definitions(JsonElement fixture)
    {
        return ImmutableArray.Create(new JobUnlockDefinition(Id("squire"), [])).AddRange(
            fixture.GetProperty("unlockDefinitions").EnumerateArray().Select(row => new JobUnlockDefinition(Id(row.GetProperty("job").GetString()!),
                row.GetProperty("requires").EnumerateObject().Select(p => new JobLevelRequirement(Id(p.Name), p.Value.GetByte())).ToImmutableArray())));
    }
    private static JobUnlockCatalog Catalog(IEnumerable<JobUnlockDefinition> jobs) => new(jobs, Id("squire"), Id("bard"), Id("dancer"));
    private static ImmutableDictionary<AssetId, byte> Levels(JobUnlockCatalog catalog, byte level = 0) => catalog.Jobs.ToImmutableDictionary(j => j.JobId, _ => level);

    [Fact]
    public void Sixteen_original_and_explicit_synthetic_vectors_preserve_requirements_and_sex_semantics()
    {
        using var f = GrowthUnlockTestData.Fixture();
        foreach (var row in f.RootElement.GetProperty("unlocks").EnumerateArray())
        {
            var d = row.GetProperty("input"); var definitions = Definitions(f.RootElement);
            if (d.TryGetProperty("syntheticRequirements", out var custom))
                foreach (var replacement in custom.EnumerateObject())
                {
                    int i = Enumerable.Range(0, definitions.Length).Single(i => definitions[i].JobId == Id(replacement.Name));
                    definitions = definitions.SetItem(i, definitions[i] with { Requirements = replacement.Value.EnumerateObject()
                        .Select(p => new JobLevelRequirement(Id(p.Name), p.Value.GetByte())).ToImmutableArray() });
                }
            var catalog = Catalog(definitions); var levels = Levels(catalog, d.TryGetProperty("allLevels", out var all) ? all.GetByte() : (byte)0);
            if (d.TryGetProperty("levels", out var overrides)) foreach (var item in overrides.EnumerateObject()) levels = levels.SetItem(Id(item.Name), item.Value.GetByte());
            var actual = catalog.Calculate(levels, d.GetProperty("male").GetBoolean(), d.GetProperty("female").GetBoolean(), d.GetProperty("monster").GetBoolean());
            Assert.Equal(row.GetProperty("expected").EnumerateArray().Select(x => Id(x.GetString()!)), actual);
        }
    }

    [Fact]
    public void All_original_prerequisite_boundaries_match_individually()
    {
        using var f = GrowthUnlockTestData.Fixture(); var catalog = Catalog(Definitions(f.RootElement));
        foreach (var job in catalog.Jobs)
        {
            var exact = Levels(catalog);
            foreach (var p in job.Requirements) exact = exact.SetItem(p.JobId, p.MinimumLevel);
            Assert.Contains(job.JobId, catalog.Calculate(exact));
            foreach (var p in job.Requirements)
                if (p.MinimumLevel > 0) Assert.DoesNotContain(job.JobId, catalog.Calculate(exact.SetItem(p.JobId, (byte)(p.MinimumLevel - 1))));
        }
    }

    [Fact]
    public void Zero_jp_recalculation_feeds_threshold_level_then_recomputes_unlocks_from_scratch()
    {
        using var f = GrowthUnlockTestData.Fixture(); var catalog = Catalog(Definitions(f.RootElement));
        var levels = Levels(catalog); var jp = JobPointArithmetic.Add(1, 200, 0, JobPointArithmeticTests.Thresholds);
        Assert.Equal(2, jp.JobLevel);
        levels = levels.SetItem(Id("squire"), (byte)jp.JobLevel);
        Assert.Equal(new[] { Id("squire"), Id("chemist"), Id("knight"), Id("archer") }, catalog.Calculate(levels));
        Assert.Equal(new[] { Id("squire"), Id("chemist") }, catalog.Calculate(Levels(catalog)));
    }

    [Fact]
    public void Captured_catalog_and_levels_are_immutable_and_have_no_original_bitset_capacity_limit()
    {
        using var f = GrowthUnlockTestData.Fixture(); var jobs = Definitions(f.RootElement).ToList();
        jobs.AddRange(Enumerable.Range(0, 10).Select(i => new JobUnlockDefinition(new("custom:job/extra" + i), [new(Id("squire"), 2)])));
        var catalog = Catalog(jobs); var levels = Levels(catalog, 15).SetItem(Id("bard"), 0).SetItem(Id("dancer"), 0);
        var a = catalog.Calculate(levels, true, true); jobs.Clear();
        Assert.Equal(30, catalog.Jobs.Length); Assert.Equal(28, a.Length); Assert.Equal(a.ToArray(), catalog.Calculate(levels, true, true).ToArray());
        Assert.Equal((byte)0, levels[Id("bard")]); Assert.Equal((byte)0, levels[Id("dancer")]);
    }

    [Fact]
    public void Unknown_duplicate_and_out_of_nibble_content_is_rejected_before_calculation()
    {
        using var f = GrowthUnlockTestData.Fixture(); var jobs = Definitions(f.RootElement);
        Assert.Throws<ArgumentException>(() => Catalog(jobs.Add(jobs[0])));
        Assert.Throws<ArgumentException>(() => Catalog(jobs.SetItem(1, jobs[1] with { Requirements = default })));
        Assert.Throws<ArgumentException>(() => Catalog(jobs.SetItem(1, jobs[1] with { Requirements = [new(Id("unknown"), 1)] })));
        Assert.Throws<ArgumentException>(() => Catalog(jobs.SetItem(1, jobs[1] with { Requirements = [new(Id("squire"), 16)] })));
        Assert.Throws<ArgumentException>(() => Catalog(jobs.SetItem(1, jobs[1] with { Requirements = [new(Id("squire"), 1), new(Id("squire"), 2)] })));
        Assert.Throws<ArgumentException>(() => Catalog(jobs.SetItem(0, jobs[0] with { Requirements = [new(Id("squire"), 1)] })));
        Assert.Throws<ArgumentException>(() => new JobUnlockCatalog(jobs, Id("squire"), Id("squire"), Id("dancer")));
        Assert.Throws<ArgumentException>(() => new JobUnlockCatalog(jobs, Id("unknown"), Id("bard"), Id("dancer")));
        var catalog = Catalog(jobs); var levels = Levels(catalog);
        Assert.Throws<ArgumentException>(() => catalog.Calculate(levels.Remove(Id("chemist"))));
        Assert.Throws<ArgumentException>(() => catalog.Calculate(levels.SetItem(Id("squire"), 16)));
        Assert.Throws<ArgumentException>(() => catalog.Calculate(levels.Add(Id("unknown"), 0)));
        Assert.Throws<ArgumentNullException>(() => catalog.Calculate(null!));
    }
}
