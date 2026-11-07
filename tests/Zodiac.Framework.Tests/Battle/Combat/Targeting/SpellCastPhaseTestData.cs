namespace Zodiac.Framework.Tests;

internal static class SpellCastPhaseTestData
{
    internal static SpellTargetChoice Choice(MagicCastDefinition? definition = null)
    {
        var q = StoredSpellTestData.Request(); var actor = q.Roster.Single(u => u.Occupant.UnitId == q.Cast.Caster.UnitId).Occupant;
        return SpellTargetChoiceQuery.Evaluate(q.Map, actor, definition ?? q.Cast.Definition, q.GeometryProfile).Outcome!;
    }
    internal static ConfirmedSpellAim Confirm(MagicCastDefinition? definition = null, byte type = 6)
    {
        var choice = Choice(definition); var anchor = new SpellTargetOccupant(new("first"), 1, new(1, 0, 0), SpellTargetTeam.Blue, SpellTargetTeam.Blue);
        return SpellAimCapture.Capture(choice, anchor.Position, type, anchor, null, new(true, true, true)).Outcome!;
    }
}
