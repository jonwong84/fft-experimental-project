using System.Collections.Immutable;

namespace Zodiac.Framework.Tests;

internal static class MagicProjectionTestData
{
    internal static MagicProjectionSnapshot Snapshot() { var q = StoredSpellTestData.Request();
        return new(q.Map, q.Roster.Select(u => new MagicProjectionUnit(u.Occupant, u.Current, new(100, 100, 50, 70))), new(57, 9)); }
    internal static MagicReadyPlanRequest Request(MagicProjectionSnapshot state, MagicFormulaEvaluationMode mode = MagicFormulaEvaluationMode.Execute)
    {
        var q = MagicReadyPlanTests.Request(mode);
        return q with { Map = state.Map, Roster = state.Units.Select(u => new SpellTargetCombatant(u.Occupant, u.Current)).ToImmutableArray() };
    }
}
