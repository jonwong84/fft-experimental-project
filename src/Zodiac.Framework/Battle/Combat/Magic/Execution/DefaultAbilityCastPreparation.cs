using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>
/// Actual cast path: learned grant/resources, target choice/capture, shared primary preparation,
/// ACT commitment and immediate or delayed resolution. Called only by DefaultAbilityBattle.Apply.
/// </summary>
internal static class DefaultAbilityCastPreparation
{
    internal static DefaultAbilityBattleState PrepareAndQueue(DefaultAbilityBattleState state,
        DefaultAbilityCommand.Cast command, List<DefaultAbilityEvent> events)
    {
        var actor = DefaultAbilityBattle.Active(state, command.UnitId);
        if (!state.Definition.Spells.TryGetValue(command.SpellId, out var spell) ||
            !DefaultAbilityBattle.KnownSpells(state, actor.Id).Contains(command.SpellId))
            throw new DefaultAbilityExecutionException("That spell is not learned.");
        if (state.Pending.ContainsKey(actor.Id))
            throw new DefaultAbilityExecutionException("That unit is already charging a spell.");
        var resource = MagicResources.EvaluateMp(new(new(actor.Current.Mp, false, false, false, false, false, MagicActionRestriction.None),
            new(spell.OriginalAbilityId, spell.MpCost, MagicResourceMenu.Default, spell.AffectedBySilence),
            MagicFormulaEvaluationMode.Preview, new(true)));
        if (resource.Outcome is not { Usable: true })
            throw new DefaultAbilityExecutionException("Not enough MP.");
        var choice = DefaultAbilityBattle.Choice(state, actor.Id, spell.Id);
        if (!choice.Accepted || choice.Outcome is null || command.FollowUnit && choice.Outcome.Mode != SpellTargetChoiceMode.UnitOrTile)
            throw new DefaultAbilityExecutionException("That targeting choice is unavailable.");
        var occupant = state.Units.FirstOrDefault(u => u.Position == command.Aim)?.Occupant;
        var selection = SpellAimCapture.Capture(choice.Outcome, command.Aim, command.FollowUnit ? (byte)6 : (byte)5,
            occupant, null, new(true, true, true));
        if (!selection.Accepted)
            throw new DefaultAbilityExecutionException("Choose a tile in spell range.");
        var plan = CombatRules.DefaultAbilityPreparation.Prepare(new(selection.Outcome!, actor.Current,
            state.Units.Select(u => u.Occupant).ToImmutableArray(), new(true), new(true, true)));
        if (!plan.Accepted)
            throw new DefaultAbilityExecutionException("The spell could not be prepared.");
        var next = DefaultAbilityBattle.Consume(state, actor.Id, TurnCommandKind.ConfirmAct, events);
        var pending = plan.Outcome!.Plan;
        actor = actor with { Current = plan.Outcome.Caster };
        next = next.With(units: next.Units.SetItem(DefaultAbilityBattle.Index(next, actor.Id), actor), pending: next.Pending.Add(actor.Id, pending));
        events.Add(new DefaultAbilityEvent.Queued(actor.Id, spell.Id, pending.Counter));
        return pending.Counter == 0 ? DefaultAbilityCastExecution.Resolve(next, actor.Id, events) : next;
    }

}
