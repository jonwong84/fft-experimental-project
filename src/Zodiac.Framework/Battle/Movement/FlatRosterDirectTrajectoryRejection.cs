using System.Collections.Immutable;
using System.Numerics;

namespace Zodiac.Framework;

public enum FlatRosterDirectTrajectoryRejection { None, UnknownActor, UnknownTarget, InvalidTarget, UnsupportedProfile,
    MissingSpriteHeights, InvalidOrigin, OutOfRange }
