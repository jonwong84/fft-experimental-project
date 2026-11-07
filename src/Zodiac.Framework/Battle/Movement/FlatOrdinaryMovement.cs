using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Pure opt-in geometry query for the certified open flat two-living-opponent profile.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-120/121.
/// No command authority, preview placement, facing publication, simulation clock, RNG draws or legacy activation.
/// Both actors may query any phase. MovementTaken restricts AI origins only; ActionTaken does not restrict geometry.
/// </remarks>
public static class FlatOrdinaryMovement
{
    public static FlatMovementQueryResult Evaluate(AttackBattleSnapshot state, BattleUnitId actorId,
        FlatOrdinaryMovementProfile profile, TilePosition? destination = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(profile);
        var battle = state.TurnBattle.Battle;
        var rng = state.TurnBattle.Turns.RngState;
        FlatMovementQueryResult Reject(FlatMovementRejection reason) => new(false, reason, null, rng);
        if (!battle.TryGetUnit(actorId, out var actor)) return Reject(FlatMovementRejection.UnknownActor);
        if (!profile.Supports(state)) return Reject(FlatMovementRejection.UnsupportedProfile);
        var enemy = battle.Units.Single(unit => unit.Id != actorId);
        return FlatMovementKernel.Evaluate(state, actor!, new HashSet<TilePosition> { enemy.Position },
            new HashSet<TilePosition> { enemy.Position }, enemy.Position, destination);
    }
}

/// <summary>Shared admitted cost-one range and source backtrace; callers own team/occupancy and geometry admission.</summary>
internal static class FlatMovementKernel
{
    private static readonly (int X, int Y, byte Packed, BattleFacing Facing)[] Steps =
    [ (1, 0, 0, BattleFacing.East), (0, 1, 192, BattleFacing.North),
      (-1, 0, 64, BattleFacing.West), (0, -1, 128, BattleFacing.South) ];

    internal static FlatMovementQueryResult Evaluate(AttackBattleSnapshot state, BattleUnitState actor,
        IReadOnlySet<TilePosition> blocked, IReadOnlySet<TilePosition> occupied, TilePosition? focus, TilePosition? destination)
        => Evaluate(state.TurnBattle.Battle.Map, actor.Position, actor.Move, actor.MovementProfile.Jump, actor.HasMoved,
            blocked, occupied, focus, destination, state.TurnBattle.Turns.RngState);

    // Concrete ordinary-magic state shares the same admitted graph/backtrace without fabricating sword combat stats.
    internal static FlatMovementQueryResult Evaluate(BattleMapDefinition map, TilePosition origin, int movePoints, int jump,
        bool movementTaken, IReadOnlySet<TilePosition> blocked, IReadOnlySet<TilePosition> occupied,
        TilePosition? focus, TilePosition? destination, RandomState rng)
    {
        FlatMovementQueryResult Reject(FlatMovementRejection reason) => new(false, reason, null, rng);
        int move = Math.Min(124, movePoints);
        var distance = new int[map.Width * map.Depth];
        Array.Fill(distance, -1);
        int Index(TilePosition p) => p.Y * map.Width + p.X;
        bool Inside(TilePosition p) => p.Layer == 0 && p.X >= 0 && p.X < map.Width && p.Y >= 0 && p.Y < map.Depth;
        var pending = new Queue<TilePosition>();
        pending.Enqueue(origin); distance[Index(origin)] = 0;
        while (pending.TryDequeue(out var current))
        {
            foreach (var step in Steps)
            {
                var next = new TilePosition(current.X + step.X, current.Y + step.Y, 0);
                if (!Inside(next) || blocked.Contains(next) || distance[Index(next)] >= 0) continue;
                distance[Index(next)] = distance[Index(current)] + 1;
                pending.Enqueue(next);
            }
        }

        FlatMovementPath? path = null;
        if (destination is { } target)
        {
            if (!Inside(target) || target == origin || occupied.Contains(target) || distance[Index(target)] < 1 || distance[Index(target)] > move)
                return Reject(FlatMovementRejection.InvalidDestination);
            var reverse = new List<TilePosition> { target };
            var current = target;
            int preferred = 0; // Initial East predecessor; carry the chosen backward direction.
            while (current != origin)
            {
                int selected = -1;
                for (int i = 0; i < Steps.Length; i++)
                {
                    var step = Steps[i];
                    var next = new TilePosition(current.X + step.X, current.Y + step.Y, 0);
                    if (!Inside(next) || distance[Index(next)] != distance[Index(current)] - 1) continue;
                    if (selected < 0) selected = i;
                    if (i == preferred) { selected = i; break; }
                }
                // Every positive shortest distance has an adjacent predecessor in this certified graph.
                if (selected < 0) throw new InvalidOperationException("Missing closed flat path predecessor.");
                current = new(current.X + Steps[selected].X, current.Y + Steps[selected].Y, 0);
                reverse.Add(current); preferred = selected;
            }
            reverse.Reverse();
            var bytes = ImmutableArray.CreateBuilder<byte>(reverse.Count - 1);
            BattleFacing heading = default;
            for (int i = 1; i < reverse.Count; i++)
            {
                var step = Steps.Single(s => s.X == reverse[i].X - reverse[i - 1].X && s.Y == reverse[i].Y - reverse[i - 1].Y);
                bytes.Add(step.Packed); heading = step.Facing;
            }
            path = new(reverse.ToImmutableArray(), bytes.ToImmutable(), heading);
        }

        var physical = ImmutableArray.CreateBuilder<TilePosition>();
        var residuals = ImmutableArray.CreateBuilder<FlatMovementResidual>();
        var origins = ImmutableArray.CreateBuilder<TilePosition>();
        var rows = ImmutableArray.CreateBuilder<FlatMovementDistanceRow>();
        for (int y = 0; y < map.Depth; y++)
        {
            bool selectedRow = false;
            for (int x = 0; x < map.Width; x++)
            {
                var tile = new TilePosition(x, y, 0);
                int d = distance[Index(tile)];
                if (d < 0 || d > move) continue;
                residuals.Add(new(tile, (byte)(move + 1 - d)));
                bool open = tile == origin || !occupied.Contains(tile);
                if (tile != origin && open) physical.Add(tile);
                if (open && (!movementTaken || tile == origin)) { origins.Add(tile); selectedRow = true; }
            }
            if (selectedRow && focus is { } focusPosition)
            {
                var values = ImmutableArray.CreateBuilder<byte>(map.Width);
                // Reverse source propagation clears every unit record. Manhattan is valid only here.
                for (int x = 0; x < map.Width; x++) values.Add((byte)(Math.Abs(focusPosition.X - x) + Math.Abs(focusPosition.Y - y)));
                rows.Add(new(y, values.ToImmutable()));
            }
        }
        return new(true, FlatMovementRejection.None,
            new(move, Math.Min(7, jump) >> 1, physical.ToImmutable(), residuals.ToImmutable(),
                origins.ToImmutable(), rows.ToImmutable(), path), rng);
    }
}
