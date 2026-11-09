using System.Collections.Immutable;
using Zodiac.Content.Definitions;
using Zodiac.Framework;
using Zodiac.Framework.Ai;

namespace Zodiac.Content.BattleSetup;

/// <summary>Copy the existing immutable metadata into the definition-bound AI query; never infer missing secondary learning.</summary>
public static class PreparedDefaultAbilityAi
{
    public static DefaultAbilityAiCatalog CreateCatalog(PreparedDefaultAbilityBattle prepared,
        IReadOnlyDictionary<SkillsetId, byte>? originalSkillsetIds = null)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (prepared.MetadataPackage is not { } metadata || prepared.Definition.Learning is null)
            throw new NotSupportedException("Prepared AI queries require the explicit ability/skillset metadata companion.");
        if (metadata.Abilities.Any(a => a.HasUnresolvedAiFlags))
            throw new NotSupportedException("Unresolved static AI flags cannot authorize an ordinary ready-hit query.");
        originalSkillsetIds ??= new Dictionary<SkillsetId, byte>
        {
            [new("fft.ps1:skillset/basic-skill")] = 5,
            [new("fft.ps1:skillset/white-magic")] = 10,
            [new("fft.ps1:skillset/black-magic")] = 11
        };
        var primaries = ImmutableDictionary.CreateBuilder<AssetId, DefaultAbilityAiSkillset>();
        foreach (var job in prepared.JobDefinitions)
        {
            var id = job.Value.PrimarySkillsetId;
            if (!originalSkillsetIds.TryGetValue(id, out byte sourceId))
                throw new NotSupportedException($"An explicit original skillset ID is required for '{id.Value}'.");
            var skillset = metadata.Skillsets.Single(s => s.Id == id);
            var slots = skillset.Slots.Where(s => s.AbilityId is not null).Select(s =>
            {
                var abilityId = new AssetId(s.AbilityId!.Value.Value);
                var behavior = prepared.AbilityMetadata[abilityId];
                return new DefaultAbilityAiSlot(s.Slot, prepared.Definition.Spells[abilityId], behavior.AiBehavior, behavior.UsableByAi);
            }).ToImmutableArray();
            primaries.Add(new(job.Key), new(sourceId, new(id.Value), slots));
        }
        return new(prepared.Definition, primaries);
    }
}
