using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Known source labels, preserved as data; the corresponding miss behaviors are not implemented here.</summary>
public enum MagicResultMissKind : byte
{
    Hit, AccessoryEvade, RightHandEvade, LeftHandEvade, ClassEvade, Nullified, AccuracyMiss,
    ForcedFailure, Canceled, Reflected, GolemGuard, BladeGraspOrFingerGuard, BuggedReflection, Catch
}
