using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Bounded evaluation.c masks/passes and turn.c usage policy; reference 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
internal static class DefaultAbilityAiAimEnumeration
{
    internal static void ValidateRoster(DefaultAbilityBattleState state, BattleUnitId actorId)
    {
        var units = state.Units;
        if (state.ClockPhase != DefaultAbilityClockPhase.UnitTurn || units.IsDefaultOrEmpty || units.Length > 21 ||
            !units.Any(u => u.Id == actorId) || units.Any(u => u.Occupant.SourceOrder is < 0 or > 20 ||
                u.Current.Statuses.Dead || u.Current.Statuses.Charging || !state.Definition.Map.TryGetTile(u.Position, out _)) ||
            units.Select(u => u.Id).Distinct().Count() != units.Length ||
            units.Select(u => u.Occupant.SourceOrder).Distinct().Count() != units.Length ||
            units.Select(u => u.Position).Distinct().Count() != units.Length ||
            state.Turns.Units.Length != units.Length ||
            !state.Turns.Units.Select(u => u.Id).ToHashSet().SetEquals(units.Select(u => u.Id)) ||
            state.Turns.Units.Any(u => u.Speed is < 1 or > 50))
            throw new NotSupportedException("The current source-sized ordinary turn roster is required.");
    }

    internal static DefaultAbilityAiAimOutcome Build(DefaultAbilityAiWork work) => BuildCore(work, currentOriginOnly: true);

    /// <summary>Caller passes and walkable aims before any cast origin's selectable-range intersection.</summary>
    internal static DefaultAbilityAiAimOutcome BuildPotentialPasses(DefaultAbilityAiWork work) => BuildCore(work, currentOriginOnly: false);

    private static DefaultAbilityAiAimOutcome BuildCore(DefaultAbilityAiWork work, bool currentOriginOnly)
    {
        var map = work.State.Definition.Map;
        var ability = work.Ability;
        var geometry = ability.Geometry;
        var behavior = ability.Behavior;
        // The encounter's explicit dry flat ground certificate excludes blocked/cross-section panels.
        // Source walkable aims include occupied panels and panels outside the actor's Move range.
        if (map.Width > 16 || map.Depth > 16 || map.LayerCount != 1 || work.State.Definition.MovementGround is null ||
            geometry.Range == 0 || geometry.Range + geometry.Area > 7 || behavior.FollowTarget && geometry.NoFollowTarget)
            throw new NotSupportedException("Certified ground, nonzero range, safe row-template radii and consistent unit choices are required.");
        var selfEffect = work.Usefulness.Single(u => u.UnitId == work.Actor.Id).Effect;
        if (selfEffect == DefaultAbilityAiEffect.Useful)
            throw new NotSupportedException("Useful self-target alternatives require a separate source pathway.");

        var deadlines = work.State.Units.OrderBy(u => u.Occupant.SourceOrder).Select(u =>
        {
            var turn = work.State.Turns.Units.Single(t => t.Id == u.Id);
            int delay = AiRosterEligibility.OrdinaryTurnDelay((byte)turn.Ct, (byte)turn.Speed);
            return new DefaultAbilityAiAimDeadline(u.Id, delay, ability.ChargeTime != 0 && ability.ChargeTime >= delay);
        }).ToImmutableArray();
        var moving = deadlines.Where(d => d.CanMoveByChargeDeadline).Select(d => d.UnitId).ToHashSet();
        // ability_considerations_set completes the working entry flags after InitializeUsage.
        // This is not the static metadata's fourth byte and not a caller-supplied policy override.
        bool consume = ability.Usage.ForceConsumeAim || !ability.Usage.ExhaustiveOrigins &&
            (ability.ChargeTime >= deadlines.Single(d => d.UnitId == work.Actor.Id).TurnDelay || selfEffect == DefaultAbilityAiEffect.None);
        ability = ability with { Usage = ability.Usage with { ConsumeAim = consume } };
        var useful = work.Usefulness.Where(u => u.Effect == DefaultAbilityAiEffect.Useful).Select(u => u.UnitId).ToImmutableArray();
        var passes = ImmutableArray.CreateBuilder<DefaultAbilityAiAimPass>();
        if (useful.IsEmpty) return Outcome();

        var choice = DefaultAbilityBattle.Choice(work.State, work.Actor.Id, work.Spell.Id);
        if (!choice.Accepted || choice.Outcome?.Range is null ||
            behavior.FollowTarget && choice.Outcome.Mode != SpellTargetChoiceMode.UnitOrTile)
            throw new NotSupportedException("The fresh selectable range is unavailable.");
        var range = choice.Outcome.Range.Panels.ToHashSet();
        if (behavior.FollowTarget)
        {
            if (consume) AddPass(DefaultAbilityAiAimPassKind.Units, useful, true, true);
            else
            {
                AddPass(DefaultAbilityAiAimPassKind.UnitsMovingByDeadline, useful.Where(moving.Contains).ToImmutableArray(), true, true);
                AddPass(DefaultAbilityAiAimPassKind.UnitsStillAtDeadline, useful.Where(id => !moving.Contains(id)).ToImmutableArray(), true, false);
            }
            if (geometry.Area == 0) return Outcome();
        }
        var tileTargets = useful.Where(id => !behavior.TargetsEnemies || !moving.Contains(id) ||
            work.State.Units.Single(u => u.Id == id).Occupant.InitialTeam == work.Actor.Occupant.CurrentTeam).ToImmutableArray();
        if (!tileTargets.IsEmpty) AddPass(DefaultAbilityAiAimPassKind.Tiles, tileTargets, false, consume);
        return Outcome();

        DefaultAbilityAiAimOutcome Outcome() => new(ability, work.Actor.Position, work.Usefulness, deadlines, passes.ToImmutable());

        void AddPass(DefaultAbilityAiAimPassKind kind, ImmutableArray<BattleUnitId> targets, bool unitMode, bool consumeAims)
        {
            var rows = new ushort[map.Depth];
            int radius = unitMode ? 0 : geometry.Area;
            foreach (var id in targets)
            {
                var center = work.State.Units.Single(u => u.Id == id).Position;
                // Safe radius0..7 row templates are diamonds; bit15 is X0. No radius8 table overread is generalized.
                for (int y = 0; y < map.Depth; y++)
                for (int x = 0; x < map.Width; x++)
                    if (Math.Abs(x - center.X) + Math.Abs(y - center.Y) <= radius)
                        rows[y] |= (ushort)(0x8000 >> x);
            }
            if (behavior.FollowTarget && !unitMode)
                foreach (var id in targets)
                {
                    var position = work.State.Units.Single(u => u.Id == id).Position;
                    rows[position.Y] ^= (ushort)(0x8000 >> position.X);
                }
            var aims = ImmutableArray.CreateBuilder<DefaultAbilityAiAim>();
            // Only the certified lower sheet exists here. The source scans layer, Y, X, not roster order.
            for (int layer = 0; layer < map.LayerCount; layer++)
            for (int y = 0; y < map.Depth; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var position = new TilePosition(x, y, layer);
                if ((rows[y] & (0x8000 >> x)) == 0 || currentOriginOnly && !range.Contains(position)) continue;
                var unit = unitMode ? work.State.Units.SingleOrDefault(u => u.Position == position) : null;
                if (unitMode && unit is null) continue;
                aims.Add(new(position, unit?.Id));
            }
            // origin_reachable_evaluate clears the setting for an exhaustive scan, after caller grouping.
            passes.Add(new(kind, consumeAims && !ability.Usage.ExhaustiveOrigins, targets, aims.ToImmutable()));
        }
    }
}
