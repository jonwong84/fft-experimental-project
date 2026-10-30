namespace Tessera.Engine;

/// <summary>Immutable integer grid coordinates; layer is an index, not elevation.</summary>
public readonly record struct GridPosition(int X, int Y, int Layer);
