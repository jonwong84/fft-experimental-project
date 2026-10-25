using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record DarkScreenParameters(int Reserved = 0, int Mode = 1, int Divisor = 12,
    int AngleStep = 64, int RadiusStep = 4, int Image = 0);

public readonly record struct DarkScreenTileDefinition(int X, int Y, int InitialDelay);

/// <summary>Prevalidated post-readiness data. Original table conversion belongs to offline preparation.</summary>
public sealed class DarkScreenDefinition
{
    public const int Rows = 12, Columns = 24, TileCount = Rows * Columns;
    public AssetId Id { get; }
    public DarkScreenParameters Parameters { get; }
    public ImmutableArray<DarkScreenTileDefinition> Tiles { get; }

    public DarkScreenDefinition(AssetId id, DarkScreenParameters parameters, IEnumerable<DarkScreenTileDefinition> tiles)
    {
        if (!SceneTaskState.ValidId(id)) throw new ArgumentException("A bounded definition ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(tiles);
        if (parameters != new DarkScreenParameters())
            throw new ArgumentException("Only reserved-0/origin/divisor-12/angle-64/radius-4/image-0 is supported.", nameof(parameters));
        var data = tiles.Take(TileCount + 1).ToImmutableArray();
        if (data.Length != TileCount) throw new ArgumentException("The compatibility layout requires 288 cells.", nameof(tiles));
        for (int i = 0; i < data.Length; i++)
        {
            int row = i / Columns, column = i % Columns;
            if (data[i].X != column * 12 - 12 || data[i].Y != row * 24 - (column % 2 == 0 ? 12 : 24) ||
                data[i].InitialDelay is < 0 or > 41)
                throw new ArgumentException("Expected the staggered row-major layout and prevalidated delays in 0..41.", nameof(tiles));
        }
        (Id, Parameters, Tiles) = (id, parameters, data);
    }
}

public enum DarkScreenPhase { Entering, Holding, Leaving, Completed }
public enum DarkScreenDrawKind { None, Tiles, Flash }
public readonly record struct DarkScreenTileState(int RemainingDelay, int Angle, int Radius);
public readonly record struct DarkScreenTileDraw(int TileIndex, int Angle, int Radius);

/// <summary>One accepted submission, in native submission order; coverage/material are later presentation work.</summary>
public sealed record DarkScreenDraw(DarkScreenDrawKind Kind, ImmutableArray<DarkScreenTileDraw> Tiles)
{
    public const int Red = 48, Green = 40, Blue = 16;
    internal static DarkScreenDraw Empty { get; } = new(DarkScreenDrawKind.None, []);
    internal static DarkScreenDraw Flash { get; } = new(DarkScreenDrawKind.Flash, []);
}

/// <summary>Immutable source-ordered counters. No input, loading, renderer or clock authority.</summary>
public sealed class DarkScreenState
{
    public DarkScreenDefinition Definition { get; }
    public SceneTaskId TaskId { get; }
    public AssetId OwnerId { get; }
    public long StartTick { get; }
    public long LastProcessedTick { get; }
    public DarkScreenPhase Phase { get; }
    public ImmutableArray<DarkScreenTileState> Tiles { get; }
    public bool BodyFinished { get; }
    public bool ReleaseYieldPending { get; }
    public bool ExitYield { get; }
    public DarkScreenDraw Draw { get; }
    public bool IsTerminal => Phase == DarkScreenPhase.Completed;

    private DarkScreenState(DarkScreenDefinition definition, SceneTaskId taskId, AssetId ownerId,
        long startTick, long lastTick, DarkScreenPhase phase, ImmutableArray<DarkScreenTileState> tiles,
        bool bodyFinished, bool releaseYieldPending, bool exitYield, DarkScreenDraw draw)
        => (Definition, TaskId, OwnerId, StartTick, LastProcessedTick, Phase, Tiles, BodyFinished, ReleaseYieldPending, ExitYield, Draw) =
            (definition, taskId, ownerId, startTick, lastTick, phase, tiles, bodyFinished, releaseYieldPending, exitYield, draw);

    internal static DarkScreenState Start(DarkScreenDefinition definition, SceneTaskId task, AssetId owner, long tick)
        => new(definition, task, owner, tick, tick, DarkScreenPhase.Entering,
            definition.Tiles.Select(t => new DarkScreenTileState(t.InitialDelay, 0, 0)).ToImmutableArray(),
            false, false, false, DarkScreenDraw.Empty);

    internal DarkScreenState SignalRelease()
        => new(Definition, TaskId, OwnerId, StartTick, LastProcessedTick, DarkScreenPhase.Leaving,
            Tiles, false, true, false, Draw);

    internal DarkScreenState Advance(long tick)
    {
        if (IsTerminal) return this;
        if (Phase == DarkScreenPhase.Holding)
            return Next(tick, Phase, Tiles, false, false, false, DarkScreenDraw.Flash);
        if (ReleaseYieldPending)
            return Next(tick, Phase, Tiles.Select((t, i) => t with { RemainingDelay = Definition.Tiles[i].InitialDelay }).ToImmutableArray(),
                false, false, false, DarkScreenDraw.Flash);
        if (ExitYield)
            return Next(tick, DarkScreenPhase.Completed, Tiles, BodyFinished, false, false, DarkScreenDraw.Empty);

        var cells = Tiles.ToBuilder();
        var draws = ImmutableArray.CreateBuilder<DarkScreenTileDraw>();
        bool any = false;
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell.RemainingDelay > 0)
            {
                if (Phase == DarkScreenPhase.Leaving) draws.Add(new(i, cell.Angle, cell.Radius));
                cells[i] = cell with { RemainingDelay = cell.RemainingDelay - 1 };
                any = true;
                continue;
            }
            draws.Add(new(i, cell.Angle, cell.Radius)); // Submit before changing counters, including zero-area quads.
            int angle = cell.Angle + (Phase == DarkScreenPhase.Entering ? 64 : -64);
            int radius = cell.Radius + (Phase == DarkScreenPhase.Entering ? 4 : -4);
            any |= Phase == DarkScreenPhase.Entering ? angle < 513 || radius < 49 : angle >= 0 || radius >= 0;
            cells[i] = new(0, Math.Clamp(angle, 0, 512), Math.Clamp(radius, 0, 48));
        }
        // A latched all-finished body still performs this entire extra draw pass.
        var phase = BodyFinished && Phase == DarkScreenPhase.Entering ? DarkScreenPhase.Holding : Phase;
        return Next(tick, phase, cells.ToImmutable(), !any, false,
            BodyFinished && Phase == DarkScreenPhase.Leaving, new(DarkScreenDrawKind.Tiles, draws.ToImmutable()));
    }

    private DarkScreenState Next(long tick, DarkScreenPhase phase, ImmutableArray<DarkScreenTileState> cells,
        bool done, bool release, bool exit, DarkScreenDraw draw)
        => new(Definition, TaskId, OwnerId, StartTick, tick, phase, cells, done, release, exit, draw);
}
