using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum AiHumanAbilityKind { Attack, Accumulate, ThrowStone, FrogAttack, Unsupported, Dash }
public enum AiHumanEntrySource { Attack, Primary, Secondary, FrogAttack }
/// <summary>Semantic class-filtered action slot. Known is supplied resolved learning state, not inferred from job membership.</summary>
public sealed record AiHumanActionSlot(int Slot, AssetId? AbilityId, AiHumanAbilityKind Kind, bool Known, bool UsableByAi);

/// <summary>Explicit ordinary default-menu action slots, in source slot order; copies input storage.</summary>
public sealed class AiHumanSkillset
{
    public AssetId Id { get; }
    public ImmutableArray<AiHumanActionSlot> Slots { get; }
    public AiHumanSkillset(AssetId id, IEnumerable<AiHumanActionSlot> slots)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("A skillset ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(slots);
        Slots = slots.OrderBy(s => s?.Slot).ToImmutableArray();
        if (Slots.Any(s => s is null || s.Slot < 0 || s.AbilityId is { } a && string.IsNullOrWhiteSpace(a.Value) ||
            s.AbilityId is null && s.Known) || Slots.Select(s => s.Slot).Distinct().Count() != Slots.Length)
            throw new ArgumentException("Explicit distinct slots and valid nonempty known references are required.", nameof(slots));
        Id = id;
    }
}

public sealed record AiHumanUnitAbilitySource(BattleUnitId UnitId, AiHumanSkillset Primary, AiHumanSkillset? Secondary = null);
/// <summary>Unrepresented source facts: ordinary humans/default menus, no stale Innocent/Faith or retained usage restrictions.</summary>
public sealed record AiHumanAbilityListProfile(bool OrdinaryHumanDefaultMenus = false, bool CleanEligibilityAndUsageContext = false);
public sealed record AiHumanInitializedEntry(AiHumanAbilityKind Kind, AssetId? AbilityId,
    AiHumanEntrySource Source, AssetId? SkillsetId = null, int? Slot = null)
{
    public int Range => Kind == AiHumanAbilityKind.ThrowStone ? 4 : Kind == AiHumanAbilityKind.Accumulate ? 0 : 1;
    public bool OrdinaryUsable => Kind != AiHumanAbilityKind.FrogAttack;
    public bool Evadeable => Kind is AiHumanAbilityKind.Attack or AiHumanAbilityKind.ThrowStone;
    public bool Helpful => Kind == AiHumanAbilityKind.Accumulate;
    public bool Harmful => Kind is AiHumanAbilityKind.Attack or AiHumanAbilityKind.ThrowStone or AiHumanAbilityKind.Dash;
    public bool HasTrajectory => Kind == AiHumanAbilityKind.ThrowStone;
    public bool ExhaustiveOriginScan => Kind is AiHumanAbilityKind.Attack or AiHumanAbilityKind.FrogAttack;
    public bool ForceConsumeAim => Kind == AiHumanAbilityKind.Accumulate;
}

public sealed class AiHumanInitializedList
{
    public ImmutableArray<AiHumanInitializedEntry> Entries { get; }
    public int InitializedEntryCount => Entries.Length;
    public int EvadeQuarterShare => Entries.Count(e => e.Evadeable) * 4 / Entries.Length;
    public int SilenceQuarterShare => 0; // All admitted effects are zero-MP and not Silence-sensitive.
    public int MpQuarterShare => 0;
    public int HarmfulCount => Entries.Count(e => e.OrdinaryUsable && e.Harmful);
    public int HelpfulCount => Entries.Count(e => e.OrdinaryUsable && e.Helpful);
    public bool HasRangedAbility => Entries.Any(e => e.OrdinaryUsable && e.HasTrajectory);
    internal AiHumanInitializedList(ImmutableArray<AiHumanInitializedEntry> entries) => Entries = entries;
}

/// <summary>Owned initialized list data; only this builder can construct it. No RNG, plan or command authority.</summary>
/// <remarks>ZOD-261/262. This semantic representation imposes no original storage capacity on catalogs or units.
/// Canonical and variable-list preparation consumers independently enforce their supported shapes.</remarks>
public sealed class AiHumanAbilityLists
{
    public ImmutableDictionary<BattleUnitId, AiHumanInitializedList> Units { get; }
    private AiHumanAbilityLists(ImmutableDictionary<BattleUnitId, AiHumanInitializedList> units) => Units = units;

    public static AiHumanAbilityLists Build(IEnumerable<AiHumanUnitAbilitySource> units, AiHumanAbilityListProfile profile)
    {
        ArgumentNullException.ThrowIfNull(units); ArgumentNullException.ThrowIfNull(profile);
        if (!profile.OrdinaryHumanDefaultMenus || !profile.CleanEligibilityAndUsageContext)
            throw new NotSupportedException("Ordinary human default menus and clean source eligibility/usage context must be explicit.");
        var result = ImmutableDictionary.CreateBuilder<BattleUnitId, AiHumanInitializedList>();
        foreach (var unit in units)
        {
            ArgumentNullException.ThrowIfNull(unit); ArgumentNullException.ThrowIfNull(unit.Primary);
            if (string.IsNullOrWhiteSpace(unit.UnitId.Value) || result.ContainsKey(unit.UnitId))
                throw new ArgumentException("Distinct explicit units are required.", nameof(units));
            var entries = ImmutableArray.CreateBuilder<AiHumanInitializedEntry>();
            entries.Add(new(AiHumanAbilityKind.Attack, null, AiHumanEntrySource.Attack));
            void Append(AiHumanSkillset skillset, AiHumanEntrySource source)
            {
                foreach (var slot in skillset.Slots)
                {
                    if (slot.AbilityId is null || !slot.UsableByAi || !slot.Known) continue;
                    if (slot.Kind is not (AiHumanAbilityKind.Accumulate or AiHumanAbilityKind.ThrowStone or AiHumanAbilityKind.Dash))
                        throw new NotSupportedException("An eligible unsupported action cannot be omitted from the AI list.");
                    entries.Add(new(slot.Kind, slot.AbilityId, source, skillset.Id, slot.Slot));
                }
            }
            Append(unit.Primary, AiHumanEntrySource.Primary);
            if (unit.Secondary is { } secondary) Append(secondary, AiHumanEntrySource.Secondary);
            entries.Add(new(AiHumanAbilityKind.FrogAttack, null, AiHumanEntrySource.FrogAttack));
            result.Add(unit.UnitId, new(entries.ToImmutable()));
        }
        if (result.Count == 0) throw new ArgumentException("At least one unit is required.", nameof(units));
        return new(result.ToImmutable());
    }
}
