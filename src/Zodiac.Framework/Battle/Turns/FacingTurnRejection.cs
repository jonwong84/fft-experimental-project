using System.Collections.Immutable;

namespace Zodiac.Framework;

public enum FacingTurnRejection
{
    None, FacingPending, MissingCompletion, StaleCompletion, WrongActor, InactiveActor,
    UnknownActor, InconsistentCompletion, RevisionExhausted, UnsupportedFacing, NoLivingEnemy,
}
