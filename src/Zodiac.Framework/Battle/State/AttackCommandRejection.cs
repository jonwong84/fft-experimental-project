using System.Collections.Immutable;

namespace Zodiac.Framework;

public enum AttackCommandRejection { None, UnknownActor, UnknownTarget, InactiveActor, ActionAlreadyTaken, SelfTarget, DeadTarget, OutOfRange, UnsupportedSetup, RandomDrawCountExhausted, UnknownAbility, AbilityNotGranted }
