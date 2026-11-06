using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Private fresh admission/list/usefulness owner shared by current-aim and delayed queries.</summary>
internal sealed class OrdinarySpellAiWork
{
    internal OrdinaryMagicBattleState State { get; }
    internal OrdinaryMagicUnitState Actor { get; }
    internal ImmutableArray<OrdinarySpellAiUnit> Units { get; }
    internal ImmutableDictionary<BattleUnitId, OrdinarySpellAiList> Lists { get; }
    internal OrdinarySpellAiConsidered Ability { get; }
    internal ImmutableArray<OrdinarySpellAiUsefulTarget> Usefulness { get; }
    internal MagicCastDefinition Spell => Ability.Entry.Slot.Spell;

    internal OrdinarySpellAiWork(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        BattleUnitId actorId, int primarySlot, bool followUnit)
    {
        if (!ReferenceEquals(catalog.Definition, state.Definition) || state.Finished || state.LearningReport is not null ||
            !state.Pending.IsEmpty || state.Turns.ActiveUnitId != actorId)
            throw new NotSupportedException("The current ordinary spell hypothesis boundary is unavailable.");
        State = state;
        Actor = state.Units.Single(u => u.Id == actorId);
        if (state.Turns.Units.Single(u => u.Id == actorId).ActionTaken)
            throw new NotSupportedException("The acting unit has already acted.");
        Units = state.Units.Select(u => new OrdinarySpellAiUnit(u.Occupant, u.Current, u.Vitals)).ToImmutableArray();
        if (Units.Any(u => !OrdinarySpellAiProjection.Supported(u)) || Actor.Current.Statuses.Charging)
            throw new NotSupportedException("Current units exceed the ordinary spell hypothesis profile.");
        Lists = state.Units.ToImmutableDictionary(u => u.Id, u => catalog.List(state, u));
        var entries = Lists[actorId].Entries.Where(e => e.Slot.Index == primarySlot).ToArray();
        if (entries.Length != 1) throw new NotSupportedException("That primary spell slot is not in the initialized AI list.");
        var considered = OrdinarySpellAiParameters.Load(entries[0], new(), new(Usable: true));
        Ability = considered with { Usage = OrdinarySpellAiParameters.InitializeUsage(considered) };
        var b = Ability.Behavior;
        if (b.HasExhaustiveAreaShape || b.DirectTrajectory || b.ArcTrajectory || b.TargetsMapTiles)
            throw new NotSupportedException("Special targeting/search shapes are outside the ordinary spell hypothesis profile.");
        if (followUnit && !b.FollowTarget)
            throw new NotSupportedException("Primary AI metadata does not offer a followed-unit hypothesis.");
        if (Ability.MpCost > Actor.Current.Mp) throw new NotSupportedException("Not enough current MP.");
        Usefulness = OrdinarySpellAiUsefulness.Filter(state, actorId, Ability);
    }

    internal StoredSpellAim Capture(TilePosition aim, bool followUnit)
    {
        var choice = OrdinaryMagicBattle.Choice(State, Actor.Id, Spell.Id);
        if (!choice.Accepted || choice.Outcome is null || followUnit && choice.Outcome.Mode != SpellTargetChoiceMode.UnitOrTile)
            throw new NotSupportedException("The initial targeting choice is unavailable.");
        var occupant = State.Units.FirstOrDefault(u => u.Position == aim)?.Occupant;
        var capture = SpellAimCapture.Capture(choice.Outcome, aim, followUnit ? (byte)6 : (byte)5,
            followUnit ? occupant : null, null, new(true, true, true));
        if (!capture.Accepted) throw new NotSupportedException("Choose a legal initial aim.");
        return capture.Outcome!.Aim;
    }

    internal OrdinarySpellAiScore Score(ImmutableArray<OrdinarySpellAiUnit> units, byte hitPercent, short priority) =>
        OrdinarySpellAiScoring.Calculate(ScoreUnits(units), hitPercent, priority);

    internal IEnumerable<OrdinarySpellAiScoringUnit> ScoreUnits(ImmutableArray<OrdinarySpellAiUnit> units) =>
        units.Select(u => new OrdinarySpellAiScoringUnit(u.Id, u.Occupant.SourceOrder, u.Vitals, u.Current.Mp,
            u.Current.Statuses.Dead, u.Id != Actor.Id && u.Occupant.InitialTeam != Actor.Occupant.InitialTeam,
            OrdinarySpellAiStatistics.ForOrdinaryHumanList(Lists[u.Id], new())));
}

internal sealed record OrdinarySpellAiHitOutcome(ImmutableArray<OrdinarySpellAiUnit> Units,
    ImmutableArray<MagicCastTargetResult> Frames, OrdinarySpellAiAccountingOutcome Accounting, RandomState RngState);

/// <summary>Private fresh stored-aim work. Initial blue validation belongs only to Capture, never readiness.</summary>
internal static class OrdinarySpellAiHitWork
{
    internal static OrdinarySpellAiHitOutcome Resolve(OrdinarySpellAiWork work, ImmutableArray<OrdinarySpellAiUnit> units,
        StoredSpellAim storedAim, ImmutableHashSet<BattleUnitId> moved, RandomState rng)
    {
        OrdinarySpellAiHitOutcome Empty() => new(units, [], new([], 0, 0, false), rng);
        var caster = units.Single(u => u.Id == work.Actor.Id);
        SpellAimAnchor? anchor = storedAim.TargetUnitId is { } id
            ? units.Where(u => u.Id == id).Select(u => new SpellAimAnchor(u.Id, u.Occupant.Position)).SingleOrDefault() : null;
        var aim = SpellAimResolver.Resolve(work.State.Definition.Map, storedAim, anchor, new(true, true));
        if (!aim.Accepted) throw new NotSupportedException("The stored area centre is unavailable.");
        var area = OrdinarySpellTargeting.Area(work.State.Definition.Map, caster.Occupant.Position, aim.Position!.Value,
            work.Ability.Geometry, new(true, true, true, true));
        if (!area.Accepted) throw new NotSupportedException("The green area is unavailable.");
        var selected = OrdinarySpellTargeting.SelectTargets(area.Outcome!, caster.Id,
            units.Select(u => u.Occupant).ToImmutableArray(), new(true));
        if (!selected.Accepted) throw new NotSupportedException("The area roster is unavailable.");
        var targets = selected.Outcome!.UnitIds.Select(t => units.Single(u => u.Id == t)).ToImmutableArray();
        if (targets.IsEmpty) return Empty();
        var projected = OrdinarySpellAiProjection.Calculate(caster, work.Spell, targets, rng);
        if (!projected.Accepted) throw new NotSupportedException("Ready formula/resource projection rejected: " + projected.Rejection);
        var frames = projected.Outcome!.Cast.Targets;
        var accounting = OrdinarySpellAiAccounting.Evaluate(work.Ability.Behavior, frames.Select(f => new OrdinarySpellAiStrikeTarget(
            f.TargetId, f.Result.Accuracy, units.Single(u => u.Id == f.TargetId).Occupant.InitialTeam != caster.Occupant.InitialTeam,
            work.Usefulness.Single(u => u.UnitId == f.TargetId).Effect == OrdinarySpellAiEffect.Useful)).ToImmutableArray(),
            moved, storedAim.TargetingType == 6 ? storedAim.TargetUnitId : null);
        if (!accounting.Valuable) return new(units, frames, accounting, projected.RngState);
        int actorIndex = units.IndexOf(caster);
        units = units.SetItem(actorIndex, projected.Outcome.Caster);
        foreach (var frame in frames.Where(f => accounting.IncludedTargets.Contains(f.TargetId)))
        {
            int index = units.IndexOf(units.Single(u => u.Id == frame.TargetId));
            var applied = OrdinaryMagicHitApplication.Project(units[index].Current, units[index].Vitals, frame.Result);
            units = units.SetItem(index, units[index] with { Current = applied.Current, Vitals = applied.Vitals });
        }
        return new(units, frames, accounting, projected.RngState);
    }
}
