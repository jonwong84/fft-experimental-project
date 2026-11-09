using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

internal sealed record DefaultAbilityAiHitOutcome(ImmutableArray<DefaultAbilityAiUnit> Units,
    ImmutableArray<MagicCastTargetResult> Frames, DefaultAbilityAiAccountingOutcome Accounting, RandomState RngState);

/// <summary>Resolve a stored aim, project strikes, account for useful hits and apply included hypothetical results.</summary>
/// <remarks>
/// Bounded action_hits_simulate (0x8019DCBC) / strikes_simulate (0x8019DD8C),
/// fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.
/// Projection owns formula/resource work; accounting owns exclusions, priority and average hit.
/// Initial blue validation belongs only to Capture, never readiness. General reactions/multiple strikes remain gated.
/// </remarks>
internal static class DefaultAbilityAiHitSimulation
{
    /// <summary>action_hits_simulate: run strikes and report whether the candidate struck a valuable target.</summary>
    internal static bool ActionHitsSimulate(DefaultAbilityAiWork work, ImmutableArray<DefaultAbilityAiUnit> units,
        StoredSpellAim storedAim, ImmutableHashSet<BattleUnitId> moved, RandomState rng, out DefaultAbilityAiHitOutcome hit)
    {
        hit = StrikesSimulate(work, units, storedAim, moved, rng);
        // The fresh acting-unit subset always has accounting enabled. The shared accounting owner computes its average.
        return hit.Accounting.Valuable;
    }

    /// <summary>strikes_simulate: resolve recipients/formulas, apply exclusions and project the included results.</summary>
    private static DefaultAbilityAiHitOutcome StrikesSimulate(DefaultAbilityAiWork work, ImmutableArray<DefaultAbilityAiUnit> units,
        StoredSpellAim storedAim, ImmutableHashSet<BattleUnitId> moved, RandomState rng)
    {
        DefaultAbilityAiHitOutcome Empty() => new(units, [], new([], 0, 0, false), rng);
        var caster = units.Single(u => u.Id == work.Actor.Id);
        SpellAimAnchor? anchor = storedAim.TargetUnitId is { } id
            ? units.Where(u => u.Id == id).Select(u => new SpellAimAnchor(u.Id, u.Occupant.Position)).SingleOrDefault() : null;
        var aim = SpellAimResolver.Resolve(work.State.Definition.Map, storedAim, anchor, new(true, true));
        if (!aim.Accepted) throw new NotSupportedException("The stored area centre is unavailable.");
        var area = DefaultAbilityTargeting.Area(work.State.Definition.Map, caster.Occupant.Position, aim.Position!.Value,
            work.Ability.Geometry, new(true, true, true, true));
        if (!area.Accepted) throw new NotSupportedException("The green area is unavailable.");
        var selected = DefaultAbilityTargeting.SelectTargets(area.Outcome!, caster.Id,
            units.Select(u => u.Occupant).ToImmutableArray(), new(true));
        if (!selected.Accepted) throw new NotSupportedException("The area roster is unavailable.");
        var targets = selected.Outcome!.UnitIds.Select(t => units.Single(u => u.Id == t)).ToImmutableArray();
        if (targets.IsEmpty) return Empty();
        var projected = DefaultAbilityAiProjection.Calculate(caster, work.Spell, targets, rng);
        if (!projected.Accepted) throw new NotSupportedException("Ready formula/resource projection rejected: " + projected.Rejection);
        var frames = projected.Outcome!.Cast.Targets;
        var accounting = DefaultAbilityAiAccounting.Evaluate(work.Ability.Behavior, frames.Select(f => new DefaultAbilityAiStrikeTarget(
            f.TargetId, f.Result.Accuracy, units.Single(u => u.Id == f.TargetId).Occupant.InitialTeam != caster.Occupant.InitialTeam,
            work.Usefulness.Single(u => u.UnitId == f.TargetId).Effect == DefaultAbilityAiEffect.Useful)).ToImmutableArray(),
            moved, storedAim.TargetingType == 6 ? storedAim.TargetUnitId : null);
        if (!accounting.Valuable) return new(units, frames, accounting, projected.RngState);
        int actorIndex = units.IndexOf(caster);
        units = units.SetItem(actorIndex, projected.Outcome.Caster);
        foreach (var frame in frames.Where(f => accounting.IncludedTargets.Contains(f.TargetId)))
        {
            int index = units.IndexOf(units.Single(u => u.Id == frame.TargetId));
            var applied = DefaultAbilityHitApplication.Project(units[index].Current, units[index].Vitals, frame.Result);
            units = units.SetItem(index, units[index] with { Current = applied.Current, Vitals = applied.Vitals });
        }
        return new(units, frames, accounting, projected.RngState);
    }
}
