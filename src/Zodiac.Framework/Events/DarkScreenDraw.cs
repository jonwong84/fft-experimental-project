using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>One accepted submission, in native submission order; coverage/material are later presentation work.</summary>
public sealed record DarkScreenDraw(DarkScreenDrawKind Kind, ImmutableArray<DarkScreenTileDraw> Tiles)
{
    public const int Red = 48, Green = 40, Blue = 16;
    internal static DarkScreenDraw Empty { get; } = new(DarkScreenDrawKind.None, []);
    internal static DarkScreenDraw Flash { get; } = new(DarkScreenDrawKind.Flash, []);
}
