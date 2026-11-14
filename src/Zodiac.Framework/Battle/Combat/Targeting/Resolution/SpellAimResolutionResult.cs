namespace Zodiac.Framework;

public sealed record SpellAimResolutionResult(bool Accepted, SpellAimRejection Rejection, TilePosition? Position);
