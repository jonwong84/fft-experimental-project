using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Live source facts, separate from retained decision flags. RiderMountTransparent is an already resolved partner lookup.</summary>
public sealed record AiRosterLiveFacts(bool Present = true, bool Crystal = false, bool Treasure = false,
    bool Mount = false, bool Transparent = false, bool Rider = false, bool RiderMountTransparent = false,
    bool Petrified = false, bool BloodSuck = false, bool Confused = false, bool Dead = false,
    bool Critical = false, bool DeathSentence = false);
public sealed record AiRosterDecisionFacts(bool Enemy = false, bool Untargetable = false,
    bool DeadWithoutReraise = false, bool Considered = false);

/// <summary>Read-only target projection; delay and disabled status are resolved source facts, not a managed status simulation.</summary>
public sealed class AiRosterUnit
{
    public BattleUnitId UnitId { get; }
    public int SourceOrder { get; }
    public TilePosition Position { get; }
    public AiRosterLiveFacts Live { get; }
    public AiRosterDecisionFacts Decision { get; }
    public int TurnDelay { get; }
    public bool DisabledThroughActorDelay { get; }

    public AiRosterUnit(BattleUnitId unitId, int sourceOrder, TilePosition position, AiRosterLiveFacts live,
        AiRosterDecisionFacts decision, int turnDelay, bool disabledThroughActorDelay)
    {
        if (string.IsNullOrWhiteSpace(unitId.Value)) throw new ArgumentException("An explicit unit ID is required.", nameof(unitId));
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOrder);
        ValidatePosition(position);
        ArgumentNullException.ThrowIfNull(live); ArgumentNullException.ThrowIfNull(decision);
        ArgumentOutOfRangeException.ThrowIfNegative(turnDelay);
        UnitId = unitId; SourceOrder = sourceOrder; Position = position; Live = live; Decision = decision;
        TurnDelay = turnDelay; DisabledThroughActorDelay = disabledThroughActorDelay;
    }

    internal static void ValidatePosition(TilePosition position)
    {
        if (position.X is < 0 or > 255 || position.Y is < 0 or > 255 || position.Layer is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(position), "Explicit byte-coordinate facts are required.");
    }
}

/// <summary>Owned source-order roster for one actor/cache basis. It grants no battle-state, RNG or command authority.</summary>
/// <remarks>ZOD-271/272; decomp 11464943bbdbd2a0af022d90cb7f10455cf911df. No original slot/compact capacity is imposed.</remarks>
public sealed class AiTargetRoster
{
    public BattleUnitId ActorId { get; }
    public byte CachedActorDelay { get; }
    public ImmutableArray<AiRosterUnit> Units { get; }
    public ImmutableDictionary<BattleUnitId, AiRosterUnit> ById { get; }

    public AiTargetRoster(BattleUnitId actorId, byte cachedActorDelay, IEnumerable<AiRosterUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        var copy = units.ToImmutableArray();
        if (copy.IsEmpty || copy.Any(u => u is null) || copy.Select(u => u.UnitId).Distinct().Count() != copy.Length ||
            copy.Select(u => u.SourceOrder).Distinct().Count() != copy.Length || !copy.Any(u => u.UnitId == actorId))
            throw new ArgumentException("Distinct units/order keys and a represented actor are required.", nameof(units));
        ActorId = actorId; CachedActorDelay = cachedActorDelay;
        Units = copy.OrderBy(u => u.SourceOrder).ToImmutableArray(); ById = Units.ToImmutableDictionary(u => u.UnitId);
    }

    internal ImmutableHashSet<BattleUnitId> CopyMembership(IEnumerable<BattleUnitId> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        var copy = units.ToImmutableHashSet();
        if (copy.Any(id => !ById.ContainsKey(id))) throw new ArgumentException("Target membership must resolve within this roster.", nameof(units));
        return copy;
    }
}

internal static class AiRosterEligibility
{
    /// <summary>Semantic team indices 0..3; target initial team is deliberately distinct from actor current team.</summary>
    public static bool IsEnemy(BattleUnitId actorId, int actorCurrentTeam, BattleUnitId targetId, int targetInitialTeam)
    {
        if (string.IsNullOrWhiteSpace(actorId.Value) || string.IsNullOrWhiteSpace(targetId.Value))
            throw new ArgumentException("Explicit actor and target IDs are required.");
        if (actorCurrentTeam is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(actorCurrentTeam));
        if (targetInitialTeam is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(targetInitialTeam));
        return actorId != targetId && actorCurrentTeam != targetInitialTeam;
    }

    public static bool IsUntargetable(AiRosterUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit); var f = unit.Live;
        return !f.Present || f.Crystal || f.Treasure || f.Mount || unit.Decision.Enemy && f.Transparent && (!f.Rider || !f.RiderMountTransparent);
    }

    public static bool IsOutOfPlay(AiRosterUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return unit.Decision.Untargetable || unit.Decision.DeadWithoutReraise || unit.Live.Petrified;
    }

    /// <summary>Only ordinary delay arithmetic. Timed/inert/dead-cache rules must be resolved separately.</summary>
    public static int OrdinaryTurnDelay(byte ct, byte speed) => speed == 0 ? int.MaxValue : (Math.Max(0, 100 - ct) + speed - 1) / speed;
}
