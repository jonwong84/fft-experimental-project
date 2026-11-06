using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Stable-ID source unlock rules, independent of original nibble/bitset serialization or career adoption.</summary>
public sealed class JobUnlockCatalog
{
    public ImmutableArray<JobUnlockDefinition> Jobs { get; }
    public AssetId BaseJobId { get; }
    public AssetId BardJobId { get; }
    public AssetId DancerJobId { get; }

    public JobUnlockCatalog(IEnumerable<JobUnlockDefinition> jobs, AssetId baseJobId, AssetId bardJobId, AssetId dancerJobId)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        var captured = jobs.ToImmutableArray(); var ids = new HashSet<AssetId>();
        foreach (var job in captured)
        {
            if (job is null || string.IsNullOrWhiteSpace(job.JobId.Value) || !ids.Add(job.JobId) || job.Requirements.IsDefault)
                throw new ArgumentException("Explicit distinct job IDs and prerequisite arrays are required.", nameof(jobs));
        }
        if (baseJobId == bardJobId || baseJobId == dancerJobId || bardJobId == dancerJobId ||
            !ids.Contains(baseJobId) || !ids.Contains(bardJobId) || !ids.Contains(dancerJobId))
            throw new ArgumentException("Distinct known base, Bard and Dancer roles are required.", nameof(jobs));
        foreach (var job in captured)
        {
            if (job.JobId == baseJobId && !job.Requirements.IsEmpty)
                throw new ArgumentException("The always-unlocked base job has no prerequisite row.", nameof(jobs));
            var prerequisites = new HashSet<AssetId>();
            foreach (var requirement in job.Requirements)
            {
                if (requirement is null || !ids.Contains(requirement.JobId) || requirement.MinimumLevel > 15 || !prerequisites.Add(requirement.JobId))
                    throw new ArgumentException("Prerequisites require known distinct jobs and original nibble levels.", nameof(jobs));
            }
        }
        Jobs = captured; BaseJobId = baseJobId; BardJobId = bardJobId; DancerJobId = dancerJobId;
    }

    /// <summary>Recompute the entire unlocked set. Sex bits are independent; copied levels remain unchanged.</summary>
    public ImmutableArray<AssetId> Calculate(ImmutableDictionary<AssetId, byte> levels,
        bool male = false, bool female = false, bool monster = false)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count != Jobs.Length || Jobs.Any(job => !levels.ContainsKey(job.JobId)) || levels.Values.Any(level => level > 15))
            throw new ArgumentException("Capture exactly one original nibble level for each catalog job.", nameof(levels));
        if (monster) return [];
        byte EffectiveLevel(AssetId id) => male && id == DancerJobId || female && id == BardJobId ? (byte)15 : levels[id];
        var unlocked = ImmutableArray.CreateBuilder<AssetId>();
        foreach (var job in Jobs)
        {
            if (male && job.JobId == DancerJobId || female && job.JobId == BardJobId) continue;
            if (job.JobId == BaseJobId || job.Requirements.All(requirement => EffectiveLevel(requirement.JobId) >= requirement.MinimumLevel))
                unlocked.Add(job.JobId);
        }
        return unlocked.ToImmutable();
    }
}
