using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

/// <summary>A supplied learned action selection, not an automatic grant from job membership.</summary>
public sealed record LearnedImmediateAbilityGrant(BattleUnitId UnitId, int ActionSlot, AbilityId AbilityId);

public static class ImmediateAbilityBattleSetup
{
    public static ImmediateAbilityBattleProfile CreateProfile(ContentPackage metadata, ImmediateAbilityContentPackage effects,
        BaseJobBattleSetup setup, IEnumerable<LearnedImmediateAbilityGrant> grants, int equipmentPhysicalAttackBonus)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(grants);
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
                throw new ArgumentException("Grant must resolve a unit, an explicit action slot and a supported effect.", nameof(grants));
            if (!selected[grant.UnitId].Add(grant.AbilityId)) throw new ArgumentException("Duplicate learned action grant.", nameof(grants));
        }
        return new(effects.Abilities.Select(ability => new BoundedImmediateAbility(new(ability.Id.Value))),
            units.Keys.Select(id => new ImmediateAbilityUnitGrant(id, selected[id].Select(ability => new AssetId(ability.Value)), equipmentPhysicalAttackBonus)));
    }
}
