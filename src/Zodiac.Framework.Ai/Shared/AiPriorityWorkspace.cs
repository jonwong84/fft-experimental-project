using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Explicit compatibility knowledge of retained tile priorities; not a plan, proximity cache or Framework authority.</summary>
/// <remarks>ZOD-255/256. Thread the correct branch with state/RNG. Forget after any unmodeled workspace writer.
/// Only owning AI queries can add numeric writes. Unknown never means zero; a map change starts unknown.</remarks>
public sealed class AiPriorityWorkspace
{
    private readonly BattleMapDefinition map;
    public ImmutableDictionary<TilePosition, short> KnownPriorities { get; }
    private AiPriorityWorkspace(BattleMapDefinition map, ImmutableDictionary<TilePosition, short> priorities)
    { this.map = map; KnownPriorities = priorities; }

    public static AiPriorityWorkspace StartUnknown(BattleMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return new(map, ImmutableDictionary<TilePosition, short>.Empty);
    }

    /// <summary>Discard knowledge after event/status staging, loader or other unmodeled use; does not clear retail memory.</summary>
    public AiPriorityWorkspace Forget() => StartUnknown(map);

    internal void ValidateMap(AttackBattleSnapshot state)
    {
        if (!ReferenceEquals(map, state.TurnBattle.Battle.Map))
            throw new ArgumentException("Priority workspace belongs to another map; start unknown for the new map.", nameof(state));
    }

    internal AiPriorityWorkspace Write(IEnumerable<KeyValuePair<TilePosition, short>> writes) =>
        new(map, KnownPriorities.SetItems(writes));

    internal AiPriorityWorkspace Write(ImmutableArray<AiScoredMovementTile> movement) =>
        Write(movement.Select(m => KeyValuePair.Create(m.Outcome.Destination, m.TilePriority)));

    internal AiPriorityWorkspace Write(ImmutableArray<SwordAiScoredMovementTile> movement) =>
        Write(movement.Select(m => KeyValuePair.Create(m.Outcome.Destination, unchecked((short)-m.Outcome.Score.Priority))));
}

public enum AiCastPriorityProvenance { Fresh, Retained }
/// <summary>Resolved cast-origin diagnostics, using current proximity even for a retained signed priority.</summary>
public sealed record AiCastOriginPriority(TilePosition Tile, short Priority, byte Proximity, AiCastPriorityProvenance Provenance)
{
    public int OriginKey => Priority * 65536 + Proximity;
}
