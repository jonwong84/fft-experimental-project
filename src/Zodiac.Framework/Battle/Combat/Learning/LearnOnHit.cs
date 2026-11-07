using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Post-reward scan from outcomes.c at reference 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class LearnOnHit
{
    public static LearnOnHitResult Scan(BattleUnitId actorId, AssetId abilityId, bool learnOnHit, byte rate,
        ImmutableArray<LearnOnHitCandidate> units, RandomState rng, bool hamedo = false)
    {
        if (units.IsDefault || units.Any(u => u is null || u.PrimarySlots.IsDefault || u.LearnedSlots is null) ||
            units.Select(u => u.UnitId).Distinct().Count() != units.Length ||
            units.Select(u => u.SourceOrder).Distinct().Count() != units.Length || units.Any(u => u.SourceOrder < 0 ||
                u.PrimarySlots.Any(s => s is null || s.Index is < 0 or > 21 || string.IsNullOrWhiteSpace(s.AbilityId.Value)) ||
                u.PrimarySlots.Select(s => s.Index).Distinct().Count() != u.PrimarySlots.Length ||
                u.LearnedSlots.Any(s => s is < 0 or > 23)))
            throw new ArgumentException("Explicit unique unit slots and current-job learned rows are required.", nameof(units));
        if (hamedo || !learnOnHit)
            return new(units, null, rng);
        var changed = units.ToBuilder();
        foreach (int i in Enumerable.Range(0, units.Length).OrderBy(i => units[i].SourceOrder))
        {
            var unit = units[i];
            if (unit.UnitId == actorId || (unit.Marker & AbilityOutcomeMarker.Affected) == 0)
                continue;
            changed[i] = unit with { Marker = AbilityOutcomeMarker.LearnChecked };
            if (unit.IsMonster || unit.PreventReaction)
                continue;
            foreach (var slot in unit.PrimarySlots.OrderBy(s => s.Index))
            {
                if (unit.LearnedSlots.Contains(slot.Index) || slot.AbilityId != abilityId)
                    continue;
                var draw = new RandomStateGenerator().Next(rng);
                rng = draw.State;
                // Source uses rand()*100/0x8000, including a draw at rates 0 and >=100.
                if (draw.Value * 100 / 0x8000 < rate)
                    return new(changed.ToImmutable(), unit.UnitId, rng);
            }
        }
        return new(changed.ToImmutable(), null, rng);
    }

    public static ImmutableHashSet<int> Accept(AssetId abilityId, ImmutableArray<AbilityLearningSlot> primarySlots,
        ImmutableHashSet<int> learnedSlots) => learnedSlots.Union(primarySlots.Where(s => s.AbilityId == abilityId).Select(s => s.Index));
}
