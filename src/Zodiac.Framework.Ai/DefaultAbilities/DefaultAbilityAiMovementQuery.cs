using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh ordinary-spell movement baselines. No spell candidates, dispatch, lottery or command authority.</summary>
/// <remarks>ZOD-437; turn.c/evaluation.c at 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class DefaultAbilityAiMovementQuery
{
    public static DefaultAbilityAiMovementResult Evaluate(DefaultAbilityBattleState state, DefaultAbilityAiCatalog catalog,
        DefaultAbilityAiMovementRequest request, DefaultAbilityAiMovementProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        DefaultAbilityAiMovementResult Reject(string reason) => new(false, reason, null, rng);
        try
        {
            DefaultAbilityAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new DefaultAbilityAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            return Build(work, profile, rng);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell movement: " + e.Message);
        }
    }

    // A fresh internal work owner shares admission/list binding with composed diagnostics; no cached public outcome is accepted.
    internal static DefaultAbilityAiMovementResult Build(DefaultAbilityAiWork work, DefaultAbilityAiMovementProfile profile, RandomState rng)
    {
        var state = work.State;
        var actorId = work.Actor.Id;
        DefaultAbilityAiMovementResult Reject(string reason) => new(false, reason, null, rng);
        // Restrict this first adapter to ordinary offensive general scenarios. Healer/retreat policy is separate.
        if (!profile.NormalFocusedScenario || state.Definition.MovementGround is null ||
            state.Definition.Map.Width > 16 || state.Definition.Map.Depth > 16 || work.Actor.Definition.Move > 124 ||
            state.Turns.Units.Any(t => t.TieKey != state.Units.Single(u => u.Id == t.Id).Occupant.SourceOrder) ||
            work.Actor.Vitals.Critical || work.Actor.Vitals.Hp * 128 / work.Actor.Vitals.MaxHp < 45 ||
            work.Lists[actorId].Entries.Any(e => e.Slot.Spell.Targeting is null ||
                e.Slot.Behavior.TargetsAllies || e.Slot.Behavior.RandomUse ||
                e.Slot.Behavior.AddsStatus || e.Slot.Behavior.CancelsStatus || e.Slot.Behavior.Unequips ||
                e.Slot.Behavior.AffectsStats || e.Slot.Behavior.AffectsMp || e.Slot.Behavior.TargetsMapTiles) ||
            !work.Ability.Behavior.TargetsEnemies)
            return Reject("A certified fresh offensive normal general scenario is required.");

        var refresh = DecisionFlagsRefresh(work, rng);
        var targets = BuildTargets(work);
        var focus = AiRosterNearest.FindEligibleEnemy(targets, targets.Units.Select(u => u.UnitId));
        var geometry = DefaultAbilityMovement.QueryFocused(state, actorId, focus.TargetId);
        if (!geometry.Accepted) return Reject("Movement geometry rejected: " + geometry.Rejection);
        // Bare Attack contributes range1; currently affordable initialized offensive spells can lower it.
        int minimumRange = work.Lists[actorId].Entries
            .Where(e => e.Slot.Behavior.TargetsEnemies && e.Slot.Spell.MpCost <= work.Actor.Current.Mp)
            .Select(e => e.Slot.Spell.Targeting!.Range + e.Slot.Spell.Targeting.Area).Append(1).Min();
        int reach = unchecked((byte)(geometry.Outcome!.EffectiveMove + minimumRange));
        return ScenariosBuild(work, targets, focus, refresh.Outcome!, geometry.Outcome, reach, refresh.RngState);
    }

    private static AiTargetRoster BuildTargets(DefaultAbilityAiWork work)
    {
        var actorTurn = work.State.Turns.Units.Single(u => u.Id == work.Actor.Id);
        byte cachedDelay = (byte)AiRosterEligibility.OrdinaryTurnDelay((byte)actorTurn.Ct, (byte)actorTurn.Speed);
        return new(work.Actor.Id, cachedDelay, work.State.Units.Select(unit =>
        {
            var turn = work.State.Turns.Units.Single(t => t.Id == unit.Id);
            bool enemy = unit.Id != work.Actor.Id && unit.Occupant.InitialTeam != work.Actor.Occupant.CurrentTeam;
            return new AiRosterUnit(unit.Id, unit.Occupant.SourceOrder, unit.Position,
                new(Critical: unit.Vitals.Critical), new(Enemy: enemy, Considered: true),
                AiRosterEligibility.OrdinaryTurnDelay((byte)turn.Ct, (byte)turn.Speed), false);
        }));
    }

    private static DefaultAbilityAiClockResult DecisionFlagsRefresh(DefaultAbilityAiWork work, RandomState rng) =>
        MoveOnlyClock(work, work.Actor.Position, rng);

    private static DefaultAbilityAiClockResult MoveOnlyClock(DefaultAbilityAiWork work, TilePosition destination, RandomState rng)
    {
        var units = work.State.Turns.Units.Select(turn => new DefaultAbilityAiClockUnit(turn.Id,
            work.State.Units.Single(u => u.Id == turn.Id).Occupant.SourceOrder, turn.Speed, turn.Ct,
            MovementTaken: turn.MovementTaken || turn.Id == work.Actor.Id && destination != work.Actor.Position,
            ActionTaken: turn.ActionTaken)).ToImmutableArray();
        var clock = DefaultAbilityAiClock.RunMoveOnly(units, work.Actor.Id, work.State.Turns.Ticks, rng);
        if (!clock.Accepted) throw new NotSupportedException("Movement clock rejected: " + clock.Rejection);
        return clock;
    }

    private static DefaultAbilityAiMovementResult ScenariosBuild(DefaultAbilityAiWork work, AiTargetRoster targets,
        AiRosterNearestOutcome focus, DefaultAbilityAiClockOutcome refresh, FlatMovementOutcome geometry, int reach, RandomState rng)
    {
        int Distance(TilePosition tile) => focus.UsedActorFallback ? 0 :
            geometry.TargetDistanceRows.Single(row => row.Y == tile.Y).Distances[tile.X];
        var partition = AiScenarioLayout.Partition(geometry.AiOrigins, work.Actor.Position, geometry.EffectiveMove, reach, Distance);
        var proximity = AiRosterProximity.Evaluate(targets, geometry.AiOrigins);
        var movement = ReachableTilesScore(work, partition.ScoreMask, rng);
        var exitRng = movement.IsEmpty ? rng : movement[^1].RngState;
        bool ranged = work.Lists[work.Actor.Id].Entries.Any(e => e.Slot.Spell.MpCost <= work.Actor.Current.Mp &&
            (e.Slot.Behavior.DirectTrajectory || e.Slot.Behavior.ArcTrajectory));
        var layout = AiScenarioLayout.Finish(partition, geometry.AiOrigins, work.Actor.Position, Distance,
            movement.Select(m => new AiScenarioCell(m.Destination, m.TilePriority, proximity.WrittenValues[m.Destination])).ToImmutableArray(),
            proximity.WrittenValues, work.Actor.Occupant.CurrentTeam != SpellTargetTeam.Blue, canAct: true,
            rangedEnemy: ranged && !focus.UsedActorFallback, exitRng);
        var baseline = RankBaselineSeed(layout.Cells.Single(c => c.Tile == layout.D0).WrittenPriority!.Value);
        return new(true, null, new(work.Ability, targets, focus, refresh, geometry, partition.ScoreMask,
            layout.Scenario1, layout.Scenario2, movement, layout.Cells, layout.D0, layout.D1, baseline,
            partition.Distance, partition.Reach, layout.EffectiveReach, layout.Adjustment, layout.BaselineLimit,
            partition.FilterFallback, layout.DestinationFallback, partition.OutOfReach), layout.RngState);
    }

    private static ImmutableArray<DefaultAbilityAiScoredMovement> ReachableTilesScore(DefaultAbilityAiWork work,
        ImmutableArray<TilePosition> mask, RandomState rng)
    {
        var result = ImmutableArray.CreateBuilder<DefaultAbilityAiScoredMovement>();
        foreach (var tile in mask)
        {
            var clock = MoveOnlyClock(work, tile, rng);
            int actor = work.Units.IndexOf(work.Units.Single(u => u.Id == work.Actor.Id));
            var units = work.Units.SetItem(actor, work.Units[actor] with { Occupant = work.Units[actor].Occupant with { Position = tile } });
            result.Add(new(tile, rng, clock.RngState, units, clock.Outcome!, work.Score(units, 100, 0)));
            rng = clock.RngState;
        }
        return result.ToImmutable();
    }

    // Original loads u16, negates, then stores s16. Keep narrowing explicit, including -32768.
    internal static DefaultAbilityAiMovementRank RankBaselineSeed(short tilePriority) =>
        new(false, unchecked((short)-(ushort)tilePriority), 100);
}
