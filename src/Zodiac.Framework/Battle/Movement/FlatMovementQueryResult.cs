using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Rejections return no partial outcome. Every result carries the unchanged input RNG.</summary>
public sealed record FlatMovementQueryResult(bool Accepted, FlatMovementRejection Rejection, FlatMovementOutcome? Outcome, RandomState RngState);
