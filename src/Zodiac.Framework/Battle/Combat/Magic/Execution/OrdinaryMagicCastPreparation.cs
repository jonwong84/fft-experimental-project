using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>
/// Actual cast path: learned grant/resources, target choice/capture, shared primary preparation,
/// ACT commitment and immediate or delayed resolution. Called only by OrdinaryMagicBattle.Apply.
/// </summary>
internal static class OrdinaryMagicCastPreparation
{
    internal static OrdinaryMagicBattleState PrepareAndQueue(OrdinaryMagicBattleState state,
        OrdinaryMagicCommand.Cast command, List<OrdinaryMagicEvent> events)
    {
        var actor = OrdinaryMagicBattle.Active(state, command.UnitId);
        if (!state.Definition.Spells.TryGetValue(command.SpellId, out var spell) ||
            !OrdinaryMagicBattle.KnownSpells(state, actor.Id).Contains(command.SpellId))
            throw new OrdinaryMagicExecutionException("That spell is not learned.");
        if (state.Pending.ContainsKey(actor.Id))
            throw new OrdinaryMagicExecutionException("That unit is already charging a spell.");
        var resource = MagicResources.EvaluateMp(new(new(actor.Current.Mp, false, false, false, false, false, MagicActionRestriction.None),
            new(spell.OriginalAbilityId, spell.MpCost, MagicResourceMenu.Default, spell.AffectedBySilence),
            MagicFormulaEvaluationMode.Preview, new(true)));
        if (resource.Outcome is not { Usable: true })
            throw new OrdinaryMagicExecutionException("Not enough MP.");
        var choice = OrdinaryMagicBattle.Choice(state, actor.Id, spell.Id);
        if (!choice.Accepted || choice.Outcome is null || command.FollowUnit && choice.Outcome.Mode != SpellTargetChoiceMode.UnitOrTile)
            throw new OrdinaryMagicExecutionException("That targeting choice is unavailable.");
        var occupant = state.Units.FirstOrDefault(u => u.Position == command.Aim)?.Occupant;
        var selection = SpellAimCapture.Capture(choice.Outcome, command.Aim, command.FollowUnit ? (byte)6 : (byte)5,
            occupant, null, new(true, true, true));
        if (!selection.Accepted)
            throw new OrdinaryMagicExecutionException("Choose a tile in spell range.");
        var plan = CombatRules.OrdinaryMagicPreparation.Prepare(new(selection.Outcome!, actor.Current,
            state.Units.Select(u => u.Occupant).ToImmutableArray(), new(true), new(true, true)));
        if (!plan.Accepted)
            throw new OrdinaryMagicExecutionException("The spell could not be prepared.");
        var next = OrdinaryMagicBattle.Consume(state, actor.Id, TurnCommandKind.ConfirmAct, events);
        var pending = plan.Outcome!.Plan;
        actor = actor with { Current = plan.Outcome.Caster };
        next = next.With(units: next.Units.SetItem(OrdinaryMagicBattle.Index(next, actor.Id), actor), pending: next.Pending.Add(actor.Id, pending));
        events.Add(new OrdinaryMagicEvent.Queued(actor.Id, spell.Id, pending.Counter));
        return pending.Counter == 0 ? OrdinaryMagicCastExecution.Resolve(next, actor.Id, events) : next;
    }

}
