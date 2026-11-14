namespace Zodiac.Framework;

/// <summary>Current formula/vital facts beside source-order metadata; inactive anchors may retain only metadata.</summary>
public sealed record MagicProjectionUnit(SpellTargetOccupant Occupant, MagicCurrentUnit? Current, MagicUnitVitals? Vitals);
