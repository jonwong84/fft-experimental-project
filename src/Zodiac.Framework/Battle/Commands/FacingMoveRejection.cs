using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public enum FacingMoveRejection
{
    None, AuthorityRejected, UnavailableChoices, DecisionRejected, InvalidDecision, InvalidIntent,
    InvalidRandomProgress, MoveRejected, MissingContinuation, StaleContinuation, WrongActor, CommandRejected
}
