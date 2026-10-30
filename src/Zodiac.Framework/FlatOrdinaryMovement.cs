using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Explicit certification of stable opponents and ordinary ground for the closed movement query.</summary>
/// <remarks>
/// The caller certifies no mounts, stepping-stone flags, traps, pickups or unrepresented movement/status/team
/// effects. IDs do not imply teams or original terrain types. Both effective terrain costs are also checked.
/// </remarks>
public sealed class FlatOrdinaryMovementProfile
{
    public TerrainSurfaceId Ground { get; }
    public BattleUnitId FirstOpponent { get; }
    public BattleUnitId SecondOpponent { get; }

    public FlatOrdinaryMovementProfile(TerrainSurfaceId ground, BattleUnitId firstOpponent, BattleUnitId secondOpponent)
    {
        if (string.IsNullOrWhiteSpace(ground.Value)) throw new ArgumentException("Explicit ground certification required.", nameof(ground));
        if (string.IsNullOrWhiteSpace(firstOpponent.Value) || string.IsNullOrWhiteSpace(secondOpponent.Value)
            || firstOpponent == secondOpponent) throw new ArgumentException("Certify two distinct stable opponents.");
        Ground = ground; FirstOpponent = firstOpponent; SecondOpponent = secondOpponent;
    }

    /// <summary>Pure geometry/profile admission, without granting a movement transition.</summary>
    public bool Supports(AttackBattleSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var battle = state.TurnBattle.Battle;
        var map = battle.Map;
        if (map.Width > 16 || map.Depth > 16 || map.LayerCount != 1 || battle.Units.Count != 2
            || !battle.TryGetUnit(FirstOpponent, out var first) || !battle.TryGetUnit(SecondOpponent, out var second)
            || first!.Position == second!.Position || state.Combatants.Any(unit => unit.Hp == 0)) return false;
        foreach (var unit in battle.Units)
        {
            var movement = unit.MovementProfile;
            if (movement.Move > byte.MaxValue || movement.Jump > byte.MaxValue || movement.BodyHeight != 6
                || movement.Mode != UnitMovementMode.Normal || movement.Traits != UnitMovementTraits.None
                || !movement.TryGetTerrainCost(Ground, out var cost) || cost.IsBlocked || cost.MovePoints != 1) return false;
        }
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, 0), out var tile);
            if (tile.Surface != Ground || tile.Height != 10 || tile.HalfHeight != 0 || tile.WaterDepth != 0
                || tile.CeilingDepth != 0 || tile.Slope.ToPackedByte() != 0 || tile.MovementFlags != BattleTileMovementFlags.None) return false;
        }
        return true;
    }
}

public enum FlatMovementRejection { None, UnknownActor, UnsupportedProfile, InvalidDestination }
public sealed record FlatMovementResidual(TilePosition Tile, byte Remaining);
/// <summary>Every x in this lower-layer row; omitted rows are unqueried, not zero filled.</summary>
public sealed record FlatMovementDistanceRow(int Y, ImmutableArray<byte> Distances);
/// <summary>Physical path including origin and endpoint. Heading is data, not a facing mutation.</summary>
public sealed record FlatMovementPath(ImmutableArray<TilePosition> Tiles, ImmutableArray<byte> PackedSteps, BattleFacing FinalHeading);
public sealed record FlatMovementOutcome(int EffectiveMove, int JumpHalf,
    ImmutableArray<TilePosition> PhysicalDestinations, ImmutableArray<FlatMovementResidual> RemainingRanges,
    ImmutableArray<TilePosition> AiOrigins, ImmutableArray<FlatMovementDistanceRow> TargetDistanceRows, FlatMovementPath? Path);
/// <summary>Rejections return no partial outcome. Every result carries the unchanged input RNG.</summary>
public sealed record FlatMovementQueryResult(bool Accepted, FlatMovementRejection Rejection, FlatMovementOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure opt-in geometry query for the certified open flat two-living-opponent profile.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-120/121.
/// No command authority, preview placement, facing publication, simulation clock, RNG draws or legacy activation.
/// Both actors may query any phase. MovementTaken restricts AI origins only; ActionTaken does not restrict geometry.
/// </remarks>
public static class FlatOrdinaryMovement
{
    // Source spread order differs from packed move-step and managed facing ordinals. +y is North.
    private static readonly (int X, int Y, byte Packed, BattleFacing Facing)[] Steps =
    [ (1, 0, 0, BattleFacing.East), (0, 1, 192, BattleFacing.North),
      (-1, 0, 64, BattleFacing.West), (0, -1, 128, BattleFacing.South) ];

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
        var map = battle.Map;
        var origin = actor!.Position;
        int move = Math.Min(124, actor.Move);
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
                if (!Inside(next) || next == enemy.Position || distance[Index(next)] >= 0) continue;
                distance[Index(next)] = distance[Index(current)] + 1;
                pending.Enqueue(next);
            }
        }

        FlatMovementPath? path = null;
        if (destination is { } target)
        {
            if (!Inside(target) || target == origin || distance[Index(target)] < 1 || distance[Index(target)] > move)
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
                if (tile != origin) physical.Add(tile);
                if (!actor.HasMoved || tile == origin) { origins.Add(tile); selectedRow = true; }
            }
            if (selectedRow)
            {
                var values = ImmutableArray.CreateBuilder<byte>(map.Width);
                // Reverse source propagation clears every unit record. Manhattan is valid only here.
                for (int x = 0; x < map.Width; x++) values.Add((byte)(Math.Abs(enemy.Position.X - x) + Math.Abs(enemy.Position.Y - y)));
                rows.Add(new(y, values.ToImmutable()));
            }
        }
        return new(true, FlatMovementRejection.None,
            new(move, Math.Min(7, actor.MovementProfile.Jump) >> 1, physical.ToImmutable(), residuals.ToImmutable(),
                origins.ToImmutable(), rows.ToImmutable(), path), rng);
    }
}
