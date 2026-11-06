using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record FacingChoiceResult(bool Accepted, FacingTurnRejection Rejection, BattleFacing? Facing);
