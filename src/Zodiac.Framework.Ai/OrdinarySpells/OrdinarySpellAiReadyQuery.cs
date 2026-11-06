using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Initial aim and current-fact readiness hypothesis. Movement opportunities are supplied at this explicit boundary.</summary>
public sealed record OrdinarySpellAiReadyRequest(BattleUnitId ActorId, int PrimarySlot, TilePosition Aim,
    ImmutableHashSet<BattleUnitId> HadMoveOpportunity, bool FollowUnit = true);
public enum OrdinarySpellAiReadyDisposition { Rejected, NotValuable, Scored }
public sealed record OrdinarySpellAiReadyOutcome(OrdinarySpellAiConsidered Ability,
    ImmutableArray<OrdinarySpellAiUsefulTarget> Usefulness, ImmutableArray<MagicCastTargetResult> Frames,
    OrdinarySpellAiAccountingOutcome Accounting, ImmutableArray<OrdinarySpellAiUnit> Units, OrdinarySpellAiScore? Score);
public sealed record OrdinarySpellAiReadyResult(OrdinarySpellAiReadyDisposition Disposition, string? Rejection,
    OrdinarySpellAiReadyOutcome? Outcome, RandomState RngState)
{
    public bool Accepted => Disposition != OrdinarySpellAiReadyDisposition.Rejected;
}

/// <summary>Owns fresh ready-hit calculations at one initial aim; never a full delayed candidate or command authority.</summary>
public static class OrdinarySpellAiReadyQuery
{
    public static OrdinarySpellAiReadyResult Evaluate(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        OrdinarySpellAiReadyRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(request);
        OrdinarySpellAiReadyResult Reject(string reason) => new(OrdinarySpellAiReadyDisposition.Rejected, reason, null, rng);
        if (!ReferenceEquals(catalog.Definition, state.Definition) || state.Finished || state.LearningReport is not null ||
            !state.Pending.IsEmpty || request.HadMoveOpportunity is null || state.Turns.ActiveUnitId != request.ActorId ||
            request.HadMoveOpportunity.Any(id => !state.Units.Any(u => u.Id == id)))
            return Reject("The current-fact ready-hit boundary is unavailable.");
        var actor = state.Units.Single(u => u.Id == request.ActorId);
        if (state.Turns.Units.Single(u => u.Id == actor.Id).ActionTaken)
            return Reject("The acting unit has already acted.");
        var units = state.Units.Select(u => new OrdinarySpellAiUnit(u.Occupant, u.Current, u.Vitals)).ToImmutableArray();
        if (units.Any(u => !OrdinarySpellAiProjection.Supported(u)) || actor.Current.Statuses.Charging)
            return Reject("Current units exceed the ordinary ready-hit profile.");
        try
        {
            var lists = state.Units.ToImmutableDictionary(u => u.Id, u => catalog.List(state, u));
            var entries = lists[actor.Id].Entries.Where(e => e.Slot.Index == request.PrimarySlot).ToArray();
            if (entries.Length != 1) return Reject("That primary spell slot is not in the initialized AI list.");
            var ability = OrdinarySpellAiParameters.Load(entries[0], new(), new(Usable: true));
            ability = ability with { Usage = OrdinarySpellAiParameters.InitializeUsage(ability) };
            var b = ability.Behavior;
            if (b.HasExhaustiveAreaShape || b.DirectTrajectory || b.ArcTrajectory || b.TargetsMapTiles)
                return Reject("Special targeting/search shapes are outside the ordinary ready-hit profile.");
            if (request.FollowUnit && !b.FollowTarget)
                return Reject("Primary AI metadata does not offer a followed-unit hypothesis.");
            if (ability.MpCost > actor.Current.Mp) return Reject("Not enough current MP.");
            var useful = OrdinarySpellAiUsefulness.Filter(state, actor.Id, ability);
            OrdinarySpellAiReadyResult NotValuable(ImmutableArray<MagicCastTargetResult> frames, RandomState exit,
                OrdinarySpellAiAccountingOutcome? accounting = null) => new(OrdinarySpellAiReadyDisposition.NotValuable, null,
                    new(ability, useful, frames, accounting ?? new([], 0, 0, false), units, null), exit);
            if (!useful.Any(t => t.Effect == OrdinarySpellAiEffect.Useful)) return NotValuable([], rng);
            var choice = OrdinaryMagicBattle.Choice(state, actor.Id, entries[0].Slot.Spell.Id);
            if (!choice.Accepted || choice.Outcome is null || request.FollowUnit && choice.Outcome.Mode != SpellTargetChoiceMode.UnitOrTile)
                return Reject("The initial targeting choice is unavailable.");
            var occupant = state.Units.FirstOrDefault(u => u.Position == request.Aim)?.Occupant;
            var capture = SpellAimCapture.Capture(choice.Outcome, request.Aim, request.FollowUnit ? (byte)6 : (byte)5,
                request.FollowUnit ? occupant : null, null, new(true, true, true));
            if (!capture.Accepted) return Reject("Choose a legal initial aim.");
            var geometry = OrdinarySpellTargeting.Area(state.Definition.Map, actor.Position, request.Aim,
                ability.Geometry, new(true, true, true, true));
            if (!geometry.Accepted) return Reject("The green area is unavailable.");
            var selected = OrdinarySpellTargeting.SelectTargets(geometry.Outcome!, actor.Id,
                state.Units.Select(u => u.Occupant).ToImmutableArray(), new(true));
            if (!selected.Accepted) return Reject("The area roster is unavailable.");
            var targets = selected.Outcome!.UnitIds.Select(id => units.Single(u => u.Id == id)).ToImmutableArray();
            if (targets.IsEmpty) return NotValuable([], rng);
            var projected = OrdinarySpellAiProjection.Calculate(units.Single(u => u.Id == actor.Id), entries[0].Slot.Spell, targets, rng);
            if (!projected.Accepted) return Reject("Ready formula/resource projection rejected: " + projected.Rejection);
            var frames = projected.Outcome!.Cast.Targets;
            var accounting = OrdinarySpellAiAccounting.Evaluate(b, frames.Select(f => new OrdinarySpellAiStrikeTarget(f.TargetId,
                f.Result.Accuracy, state.Units.Single(u => u.Id == f.TargetId).Occupant.InitialTeam != actor.Occupant.InitialTeam,
                useful.Single(u => u.UnitId == f.TargetId).Effect == OrdinarySpellAiEffect.Useful)).ToImmutableArray(),
                request.HadMoveOpportunity, request.FollowUnit ? occupant?.UnitId : null);
            if (!accounting.Valuable) return NotValuable(frames, projected.RngState, accounting);
            int actorIndex = units.IndexOf(units.Single(u => u.Id == actor.Id));
            units = units.SetItem(actorIndex, projected.Outcome.Caster);
            foreach (var frame in frames.Where(f => accounting.IncludedTargets.Contains(f.TargetId)))
            {
                int index = units.IndexOf(units.Single(u => u.Id == frame.TargetId));
                var applied = OrdinaryMagicHitApplication.Project(units[index].Current, units[index].Vitals, frame.Result);
                units = units.SetItem(index, units[index] with { Current = applied.Current, Vitals = applied.Vitals });
            }
            var score = OrdinarySpellAiScoring.Calculate(units.Select(u => new OrdinarySpellAiScoringUnit(u.Id,
                u.Occupant.SourceOrder, u.Vitals, u.Current.Mp, u.Current.Statuses.Dead,
                u.Id != actor.Id && u.Occupant.InitialTeam != actor.Occupant.InitialTeam,
                OrdinarySpellAiStatistics.ForOrdinaryHumanList(lists[u.Id], new()))), accounting.HitPercent, accounting.PriorityDelta);
            return new(OrdinarySpellAiReadyDisposition.Scored, null, new(ability, useful, frames, accounting, units, score), projected.RngState);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ready-hit context: " + e.Message);
        }
    }
}
