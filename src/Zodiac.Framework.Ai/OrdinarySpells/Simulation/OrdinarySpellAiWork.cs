using System.Collections.Immutable;

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
    internal OrdinarySpellAiOrigin? SelectedOrigin { get; }
    internal MagicCastDefinition Spell => Ability.Entry.Slot.Spell;

    internal bool MovementTaken => State.Turns.Units.Single(u => u.Id == Actor.Id).MovementTaken ||
        SelectedOrigin is { } origin && origin.MovementDestination != State.Units.Single(u => u.Id == Actor.Id).Position;

    internal OrdinarySpellAiWork(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        BattleUnitId actorId, int primarySlot, bool followUnit)
    {
        // ai_command_build requires a compact record for every active unit before considering actions.
        // Sixteen records can describe sparse slots anywhere in the 21-slot battle clock.
        if (state.Units.Length > 16)
            throw new NotSupportedException("The original AI admits at most sixteen active units into candidate processing.");
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

    // Only the fresh search owner supplies a pass. This copies the baseline and narrows valuable membership;
    // excluded units remain real formula recipients and remain in absolute roster scoring.
    private OrdinarySpellAiWork(OrdinarySpellAiWork source, OrdinarySpellAiAimPass pass, OrdinarySpellAiUsage usage,
        OrdinarySpellAiOrigin? origin = null)
    {
        State = source.State; Actor = source.Actor; Units = source.Units; Lists = source.Lists;
        SelectedOrigin = origin ?? source.SelectedOrigin;
        if (origin is not null)
        {
            // origin_aim_tiles_evaluate places the caster before the candidate's baseline snapshot.
            Actor = Actor with { Position = origin.CastOrigin };
            int actor = Units.IndexOf(Units.Single(u => u.Id == Actor.Id));
            Units = Units.SetItem(actor, Units[actor] with { Occupant = Actor.Occupant });
        }
        Ability = source.Ability with { Usage = usage };
        var targets = pass.UsefulTargets.ToHashSet();
        if (targets.Any(id => !source.Usefulness.Any(u => u.UnitId == id && u.Effect == OrdinarySpellAiEffect.Useful)))
            throw new NotSupportedException("A fresh pass must narrow the current useful-target mask.");
        Usefulness = source.Usefulness.Select(u => u.Effect == OrdinarySpellAiEffect.Useful && !targets.Contains(u.UnitId)
            ? u with { Effect = OrdinarySpellAiEffect.None } : u).ToImmutableArray();
    }

    internal OrdinarySpellAiWork ForAimPass(OrdinarySpellAiAimPass pass, OrdinarySpellAiUsage usage) => new(this, pass, usage);

    internal OrdinarySpellAiWork ForOrigin(OrdinarySpellAiAimPass pass, OrdinarySpellAiUsage usage,
        OrdinarySpellAiOrigin origin) => new(this, pass, usage, origin);

    internal StoredSpellAim Capture(TilePosition aim, bool followUnit) => Confirm(aim, followUnit).Aim;

    internal CombatRules.OrdinaryMagicPreparationOutcome Prepare(TilePosition aim, bool followUnit)
    {
        var result = CombatRules.OrdinaryMagicPreparation.Prepare(new(Confirm(aim, followUnit), Actor.Current,
            Units.Select(u => u.Occupant).ToImmutableArray(), new(true), new(true, true)));
        if (!result.Accepted) throw new NotSupportedException("The primary ordinary spell action could not be prepared.");
        return result.Outcome!;
    }

    private ConfirmedSpellAim Confirm(TilePosition aim, bool followUnit)
    {
        // Ownership was checked during fresh binding; a private origin view supplies the placed caster.
        var choice = SpellTargetChoiceQuery.Evaluate(State.Definition.Map, Actor.Occupant, Spell, new(true, true, true, true));
        if (!choice.Accepted || choice.Outcome is null || followUnit && choice.Outcome.Mode != SpellTargetChoiceMode.UnitOrTile)
            throw new NotSupportedException("The initial targeting choice is unavailable.");
        var occupant = Units.FirstOrDefault(u => u.Occupant.Position == aim)?.Occupant;
        var capture = SpellAimCapture.Capture(choice.Outcome, aim, followUnit ? (byte)6 : (byte)5,
            followUnit ? occupant : null, null, new(true, true, true));
        if (!capture.Accepted) throw new NotSupportedException("Choose a legal initial aim.");
        return capture.Outcome!;
    }

    internal OrdinarySpellAiScore Score(ImmutableArray<OrdinarySpellAiUnit> units, byte hitPercent, short priority) =>
        OrdinarySpellAiScoring.Calculate(ScoreUnits(units), hitPercent, priority);

    internal IEnumerable<OrdinarySpellAiScoringUnit> ScoreUnits(ImmutableArray<OrdinarySpellAiUnit> units) =>
        units.Select(u => new OrdinarySpellAiScoringUnit(u.Id, u.Occupant.SourceOrder, u.Vitals, u.Current.Mp,
            u.Current.Statuses.Dead, u.Id != Actor.Id && u.Occupant.InitialTeam != Actor.Occupant.InitialTeam,
            OrdinarySpellAiStatistics.ForOrdinaryHumanList(Lists[u.Id], new())));
}
