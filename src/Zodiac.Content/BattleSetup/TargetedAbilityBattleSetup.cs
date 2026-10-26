using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

public sealed record LearnedTargetedAbilityGrant(BattleUnitId UnitId, int ActionSlot, AbilityId AbilityId);

public static class TargetedAbilityBattleSetup
{
    /// <summary>The supplied surface is authored ordinary ground; no original terrain meanings are inferred.</summary>
    public static TargetedAbilityBattleProfile CreateProfile(ContentPackage metadata, TargetedAbilityContentPackage effects,
        BaseJobBattleSetup setup, IEnumerable<LearnedTargetedAbilityGrant> grants, TerrainSurfaceId ordinaryGroundSurface, int equipmentPhysicalAttackBonus)
    {
        ArgumentNullException.ThrowIfNull(effects); ArgumentNullException.ThrowIfNull(setup); ArgumentNullException.ThrowIfNull(grants);
        effects.ValidateAgainst(metadata);
        if (equipmentPhysicalAttackBonus != 0) throw new NotSupportedException("Only zero equipment PA bonus is supported.");
        var units = setup.Definitions.ToDictionary(binding => binding.UnitId);
        foreach (var binding in units.Values)
        {
            var job = metadata.Jobs.SingleOrDefault(job => job.Id == binding.Job.Id);
            var skillset = metadata.Skillsets.SingleOrDefault(skillset => skillset.Id == binding.PrimarySkillset.Id);
            if (!ReferenceEquals(job, binding.Job) || !ReferenceEquals(skillset, binding.PrimarySkillset))
                throw new ArgumentException("Job setup must resolve the selected metadata instances.", nameof(setup));
        }
        var executable = effects.Abilities.Select(ability => ability.Id).ToHashSet();
        var selected = units.Keys.ToDictionary(id => id, _ => new HashSet<AbilityId>());
        foreach (var grant in grants)
        {
            ArgumentNullException.ThrowIfNull(grant);
            if (!units.TryGetValue(grant.UnitId, out var binding) || !executable.Contains(grant.AbilityId) ||
                !binding.PrimarySkillset.Slots.Any(slot => slot.Slot == grant.ActionSlot && slot.AbilityId == grant.AbilityId))
                throw new ArgumentException("Grant must resolve a unit, explicit action slot and supported targeted effect.", nameof(grants));
            if (!selected[grant.UnitId].Add(grant.AbilityId)) throw new ArgumentException("Duplicate learned action grant.", nameof(grants));
        }
        return new(ordinaryGroundSurface, effects.Abilities.Select(ability => new BoundedThrowStoneAbility(new(ability.Id.Value))),
            units.Keys.Select(id => new TargetedAbilityUnitGrant(id, selected[id].Select(ability => new AssetId(ability.Value)), equipmentPhysicalAttackBonus)));
    }
}
