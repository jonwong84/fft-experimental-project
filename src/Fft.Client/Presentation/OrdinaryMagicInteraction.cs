using System.Collections.Immutable;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

/// <summary>Player intent only. Framework commands alone publish all gameplay state.</summary>
public sealed class OrdinaryMagicInteraction
{
    public PreparedOrdinaryMagicBattle Content { get; }
    public OrdinaryMagicBattleState State { get; private set; }
    public OrdinaryMagicInteractionMode Mode { get; private set; }
    public AssetId? SelectedSpell { get; private set; }
    public TilePosition? Aim { get; private set; }
    public bool FollowUnit { get; private set; } = true;
    public string Message { get; private set; } = "N advances to the next turn or spell.";
    public bool IsError { get; private set; }
    public ImmutableArray<OrdinaryMagicCommand> Journal { get; private set; } = [];
    public ImmutableArray<OrdinaryMagicEvent> LastEvents { get; private set; } = [];
    public ImmutableArray<string> Log { get; private set; } = [];

    public OrdinaryMagicInteraction(PreparedOrdinaryMagicBattle content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content; State = content.Definition.Start();
    }

    public string Name(BattleUnitId id) => Content.UnitNames[id];
    public ImmutableArray<AssetId> AvailableSpells => State.Turns.ActiveUnitId is { } actor
        ? Content.SpellOrder.Where(OrdinaryMagicBattle.KnownSpells(State, actor).Contains).ToImmutableArray() : [];
    public ImmutableArray<TilePosition> Range => State.Turns.ActiveUnitId is not { } actor ? [] :
        Mode == OrdinaryMagicInteractionMode.Move ? OrdinaryMagicMovement.Query(State, actor).Outcome?.PhysicalDestinations ?? [] :
        SelectedSpell is { } spell ? OrdinaryMagicBattle.Choice(State, actor, spell).Outcome?.Range?.Panels ?? [] : [];
    public FlatMovementPath? PreviewPath => Mode == OrdinaryMagicInteractionMode.Move && Aim is { } aim &&
        State.Turns.ActiveUnitId is { } actor ? OrdinaryMagicMovement.Query(State, actor, aim).Outcome?.Path : null;
    public ImmutableArray<TilePosition> Area
    {
        get
        {
            if (Mode != OrdinaryMagicInteractionMode.Cast || Aim is not { } aim || SelectedSpell is not { } spell ||
                State.Turns.ActiveUnitId is not { } actor || !Range.Contains(aim)) return [];
            var position = State.Units.Single(u => u.Id == actor).Position;
            return DefaultAbilityTargeting.Area(State.Definition.Map, position, aim, State.Definition.Spells[spell].Targeting!,
                new(true, true, true, true)).Outcome?.Panels ?? [];
        }
    }

    public void Advance() => Apply(new OrdinaryMagicCommand.Advance());
    public void Wait()
    {
        if (State.Turns.ActiveUnitId is { } id) Apply(new OrdinaryMagicCommand.Wait(id));
        else Error("Advance to a unit turn first.");
    }
    public void BeginCast()
    {
        if (ReportPending()) return;
        if (State.Finished || State.Turns.ActiveUnitId is not { } actor) { Error("Advance to a unit turn first."); return; }
        var unit = State.Units.Single(u => u.Id == actor);
        var learned = AvailableSpells;
        if (learned.Length == 0) { Error("This unit has no learned spells. Move or wait."); return; }
        if (State.Pending.ContainsKey(actor)) { Error("This unit is already charging a spell."); return; }
        if (State.Turns.Units.Single(u => u.Id == actor).ActionTaken) { Error("The action has already been used."); return; }
        SelectedSpell = learned[0]; Mode = OrdinaryMagicInteractionMode.Cast; Aim = null;
        Message = "Choose a blue tile then confirm. Orange tiles show the area."; IsError = false;
    }
    public void SelectSpell(AssetId spell)
    {
        if (Mode != OrdinaryMagicInteractionMode.Cast || !AvailableSpells.Contains(spell))
        { Error("Choose a learned spell from the open magic list."); return; }
        SelectedSpell = spell; Aim = null;
        Message = $"{Content.SpellNames[spell]} selected. Choose a blue tile then confirm."; IsError = false;
    }
    public void NextSpell(int direction = 1)
    {
        if (Mode != OrdinaryMagicInteractionMode.Cast || SelectedSpell is not { } selected) return;
        var available = AvailableSpells;
        int index = available.IndexOf(selected);
        SelectSpell(available[(index + Math.Sign(direction) + available.Length) % available.Length]);
    }
    public void BeginStep()
    {
        if (ReportPending()) return;
        if (State.Finished || State.Turns.ActiveUnitId is null) { Error("Advance to a unit turn first."); return; }
        Mode = OrdinaryMagicInteractionMode.Step; SelectedSpell = null; Aim = null;
        Message = "Choose a free adjacent tile then confirm."; IsError = false;
    }
    public void BeginMove()
    {
        if (ReportPending()) return;
        if (State.Finished || State.Turns.ActiveUnitId is not { } actor) { Error("Advance to a unit turn first."); return; }
        if (State.Turns.Units.Single(u => u.Id == actor).MovementTaken) { Error("Movement has already been used."); return; }
        var query = OrdinaryMagicMovement.Query(State, actor);
        if (!query.Accepted || query.Outcome!.PhysicalDestinations.IsEmpty) { Error("No supported movement is available."); return; }
        Mode = OrdinaryMagicInteractionMode.Move; SelectedSpell = null; Aim = null;
        Message = "Choose a blue tile to preview a path then confirm."; IsError = false;
    }
    public void Select(TilePosition tile)
    {
        if (Mode == OrdinaryMagicInteractionMode.Browse) return;
        if (!State.Definition.Map.TryGetTile(tile, out _)) { Error("Choose a map tile."); return; }
        Aim = tile;
        if (Mode == OrdinaryMagicInteractionMode.Move)
        {
            if (!Range.Contains(tile)) { Error("Choose a free blue tile in movement range."); return; }
            Message = $"Walk {PreviewPath!.PackedSteps.Length} tiles. Confirm to move."; IsError = false; return;
        }
        if (Mode == OrdinaryMagicInteractionMode.Cast && !Range.Contains(tile)) { Error("Choose a blue tile in spell range."); return; }
        Message = Mode == OrdinaryMagicInteractionMode.Step ? "Confirm this step." :
            FollowUnit && State.Units.FirstOrDefault(u => u.Position == tile) is { } target
                ? $"Follow {Name(target.Id)} when the spell resolves. Confirm to cast."
                : "Keep the spell on this tile. Confirm to cast.";
        IsError = false;
    }
    public void ToggleFollow()
    {
        FollowUnit = !FollowUnit;
        if (Aim is { } aim) Select(aim);
    }
    public void Confirm()
    {
        if (State.LearningReport is not null) { AnswerLearning(true); return; }
        if (State.Turns.ActiveUnitId is not { } actor || Aim is not { } aim) { Error("Choose a tile first."); return; }
        if (Mode == OrdinaryMagicInteractionMode.Cast && SelectedSpell is { } spell)
            Apply(new OrdinaryMagicCommand.Cast(actor, spell, aim, FollowUnit && State.Units.Any(u => u.Position == aim)));
        else if (Mode == OrdinaryMagicInteractionMode.Step) Apply(new OrdinaryMagicCommand.Step(actor, aim));
        else if (Mode == OrdinaryMagicInteractionMode.Move) Apply(new OrdinaryMagicCommand.Move(actor, aim));
    }
    public void Cancel()
    {
        if (State.LearningReport is not null) { AnswerLearning(false); return; }
        ClearSelection();
    }
    private void ClearSelection()
    {
        Mode = OrdinaryMagicInteractionMode.Browse; Aim = null; SelectedSpell = null;
        Message = "N next event / F magic / M move / W wait."; IsError = false;
    }
    public void AnswerLearning(bool accept)
    {
        if (State.LearningReport is not { } report) { Error("No learning report is open."); return; }
        Apply(new OrdinaryMagicCommand.AnswerLearning(report.Id, report.LearnerId, accept));
    }
    public void Restart()
    {
        State = Content.Definition.Start(); Journal = []; LastEvents = []; Log = []; FollowUnit = true;
        Cancel(); Message = "Encounter restarted. N advances to the first turn.";
    }

    private void Apply(OrdinaryMagicCommand command)
    {
        Journal = Journal.Add(command);
        var result = OrdinaryMagicBattle.Apply(State, command);
        LastEvents = result.Events;
        if (!result.Accepted) { Error(result.Rejection!); return; }
        State = result.State; ClearSelection();
        foreach (var ev in result.Events)
        {
            string? line = ev switch
            {
                OrdinaryMagicEvent.TurnReady e => $"{Name(e.UnitId)} turn.",
                OrdinaryMagicEvent.TurnEnded e => $"{Name(e.UnitId)} turn ends. N advances.",
                OrdinaryMagicEvent.Stepped e => $"{Name(e.UnitId)} moves.",
                OrdinaryMagicEvent.Moved e => $"{Name(e.UnitId)} walks {e.Path.PackedSteps.Length} tiles / faces {e.Path.FinalHeading}.",
                OrdinaryMagicEvent.Queued e => $"{Name(e.UnitId)} charges {Content.SpellNames[e.SpellId]} / {e.ChargeTicks} CT. " +
                    (State.Turns.ActiveUnitId is null ? "N advances." : "W waits."),
                OrdinaryMagicEvent.Resolved e => $"{Content.SpellNames[e.SpellId]} resolves / {e.MpCost} MP spent.",
                OrdinaryMagicEvent.Fizzled e => e.Reason,
                OrdinaryMagicEvent.Applied e => $"{Name(e.UnitId)} / HP {e.Hp}" + (e.Dead ? " / KO" : e.Critical ? " / Critical" : ""),
                OrdinaryMagicEvent.Rewarded e => $"{Name(e.UnitId)} gains {e.Exp} EXP / {e.Jp} JP" + (e.Level > 0 ? $" / Level {e.Level}" : ""),
                OrdinaryMagicEvent.EncounterEnded => "First KO ends the encounter. R restarts.",
                OrdinaryMagicEvent.LearningOffered e => $"{Name(e.UnitId)} can learn {Content.SpellNames[e.AbilityId]}. Enter accepts / Back declines.",
                OrdinaryMagicEvent.LearningAnswered e => $"{Name(e.UnitId)} " + (e.Accepted ? "learned " : "declined ") +
                    Content.SpellNames[e.AbilityId] + (e.Automatic ? " automatically." : "."),
                _ => null
            };
            if (line is null) continue;
            Log = Log.Add(line); if (Log.Length > 8) Log = Log.RemoveAt(0); Message = line;
        }
    }
    private bool ReportPending()
    {
        if (State.LearningReport is null) return false;
        Error("Answer the learning report first. Enter accepts / Back declines."); return true;
    }
    private void Error(string message) { Message = message; IsError = true; }
}
