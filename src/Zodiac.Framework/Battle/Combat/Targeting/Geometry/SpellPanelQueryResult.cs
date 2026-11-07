namespace Zodiac.Framework;

public sealed record SpellPanelQueryResult(bool Accepted, SpellTargetingRejection Rejection, SpellTargetPanels? Outcome);
