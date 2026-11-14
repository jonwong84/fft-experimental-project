using System.Collections.Immutable;

namespace Zodiac.Framework.Tests;

internal static class StoredSpellTestData
{
    internal static StoredSpellCastRequest Request(MagicFormulaEvaluationMode mode = MagicFormulaEvaluationMode.Execute)
    {
        var r = MagicCastTestData.Ready(mode); r = r with { Definition = r.Definition with { Targeting = new(1, 0, 0) } };
        SpellTargetCombatant Unit(string id, int order, int x, bool weak = false) => new(
            new(new(id), order, new(x, 0, 0), SpellTargetTeam.Blue, SpellTargetTeam.Blue),
            r.Caster with { UnitId = new(id), Affinities = new(Weak: weak ? new ElementToggles { Fire = true } : ElementToggles.None) });
        return new(MagicCastQuery.PrepareReady(r).Cast!, new(5, 1, 1, Enumerable.Repeat(new BattleTile(0), 5).ToArray()),
            new(6, 1, 0, 0, new("first")), [Unit("second", 3, 3, true), new(new(r.Caster.UnitId, 0, default, SpellTargetTeam.Blue, SpellTargetTeam.Blue), r.Caster),
                Unit("old", 2, 1), Unit("first", 1, 3)], new(true, true, true, true), new(true), new(true, true), new(true, true));
    }
}
