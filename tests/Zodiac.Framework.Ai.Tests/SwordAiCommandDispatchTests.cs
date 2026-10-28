using System.Text.Json;
using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiCommandDispatchTests
{
    private static readonly SwordAiCommandDispatchProfile Profile = new(true, true, true, true, true);
    private static readonly TilePosition Origin = new(1, 1, 0), Destination = new(1, 2, 0);

    public static IEnumerable<object[]> SourceVectors()
    {
        using var fixture = Fixture();
        foreach (var row in fixture.RootElement.GetProperty("vectors").EnumerateArray())
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(SourceVectors))]
    public void Source_dispatch_vectors_execute_in_managed_query_and_reject_noncanonical_marker(string id)
    {
        using var fixture = Fixture();
        var row = fixture.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("input"); var expected = row.GetProperty("expected");
        bool Flag(string key) => input.GetProperty(key).GetBoolean();
        var current = input.GetProperty("current"); var saved = input.GetProperty("saved");
        var state = State(moved: current[0].GetBoolean(), acted: current[1].GetBoolean()); var before = Snapshot(state);
        var request = RequestFor(selection: Selected(Flag("sameTile") ? Origin : Destination, Flag("hasSkillset"), Flag("actFirst"))) with
        {
            Marker = (SwordAiDecisionMarker)input.GetProperty("decided").GetInt32(),
            SavedChoices = new(A, saved[0].GetBoolean(), saved[1].GetBoolean())
        };
        var result = Evaluate(state, request, fresh: input.GetProperty("stage").GetString() == "after-selection");
        if (row.TryGetProperty("managedChildAdmitted", out var admitted) && !admitted.GetBoolean())
            Atomic(state, result, SwordAiDispatchRejection.UnsupportedDecisionMarker);
        else
        {
            var o = Success(result);
            Assert.Equal(Enum.Parse<SwordAiDispatchKind>(expected.GetProperty("kind").GetString()!), o.Kind);
            Assert.Equal(expected.GetProperty("recordCommand").GetBoolean(), o.RecordCommand);
            Assert.Equal(o.RecordCommand ? new SwordAiSavedChoices(A, current[0].GetBoolean(), current[1].GetBoolean()) : null, o.RecordedChoices);
            Assert.Equal(o.Kind == SwordAiDispatchKind.EndTurn && !o.RecordCommand, o.EndedForNoProgress);
            Assert.Equal(o.Kind == SwordAiDispatchKind.Move ? new MoveUnitCommand(A, request.Selection!.Destination) : null, o.Move);
            Assert.Equal(o.Kind == SwordAiDispatchKind.Attack ? new AttackUnitCommand(A, B) : null, o.Attack);
        }
        Assert.Equal(before, Snapshot(state)); Assert.Equal(state.TurnBattle.Turns.RngState, result.RngState);
    }

    [Fact]
    public void Fresh_stage_skips_no_progress_and_entry_guard_precedes_ACT_reselection_and_payload_reads()
    {
        var state = State(moved: false); var request = RequestFor(selection: Selected(Destination));
        Assert.Equal(SwordAiDispatchKind.Move, Success(Evaluate(state, request, fresh: true)).Kind);
        var guarded = Success(Evaluate(state, request with { Selection = null }));
        Assert.Equal(SwordAiDispatchKind.EndTurn, guarded.Kind); Assert.True(guarded.EndedForNoProgress); Assert.False(guarded.RecordCommand);
        var acted = State(moved: false, acted: true);
        var repeat = request with { SavedChoices = new(A, false, true), Selection = null };
        Assert.True(Success(Evaluate(acted, repeat)).EndedForNoProgress);
        Assert.Equal(SwordAiDispatchKind.SelectionRequired, Success(Evaluate(acted, repeat with { SavedChoices = new(A, false, false) })).Kind);
        Assert.Equal(SwordAiDispatchKind.Move, Success(Evaluate(acted, request, fresh: true)).Kind);
        var end = Success(Evaluate(acted, request with { Selection = Selected(Origin) }, fresh: true));
        Assert.Equal(SwordAiDispatchKind.EndTurn, end.Kind); Assert.Null(end.Attack); Assert.True(end.RecordCommand);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Real_confirmed_Move_changes_choices_once_then_query_reuses_ACT_without_new_selection_or_RNG()
    {
        var initial = State(moved: false, distance: 3);
        var old = initial.TurnBattle;
        var bodies = old.Battle.Units.Select(u => new BattleUnitState(u.Id, u.Position, u.MovementProfile));
        var turns = old.Turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed, u.Id == A ? 110 : 90, u.TieKey));
        var battle = new AttackBattleSnapshot(new(new(old.Battle.Map, bodies, Rng), new(turns, Rng)), initial.Combatants);
        var advance = FacingTurnBattle.Advance(FacingTurnBattle.Begin(battle, new(Ground))); Assert.True(advance.Accepted);
        var state = advance.State; var before = Snapshot(state.Battle);
        var request = RequestFor(selection: Selected(new(3, 1, 0)));
        var first = Success(Evaluate(state.Battle, request, fresh: true)); Assert.Equal(before, Snapshot(state.Battle));
        Assert.Equal(SwordAiDispatchKind.Move, first.Kind);
        var moved = FacingTurnPathMovement.MoveAlongPath(state, first.Move!, Candidate.Movement); Assert.True(moved.Accepted);
        Assert.Equal(state.Revision + 1, moved.State.Revision); Assert.Null(moved.State.PendingFacing);
        Assert.Equal(state.Battle.TurnBattle.Turns.RngState, moved.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, moved.State.Battle.TurnBattle.Turns.Ticks);
        var movedBefore = Snapshot(moved.State.Battle);
        var next = Success(Evaluate(moved.State.Battle, request with { SavedChoices = first.RecordedChoices!.Value }));
        Assert.Equal(SwordAiDispatchKind.Attack, next.Kind); Assert.Equal(new AttackUnitCommand(A, B), next.Attack);
        var repeated = Success(Evaluate(moved.State.Battle, request with { SavedChoices = next.RecordedChoices!.Value }));
        Assert.True(repeated.EndedForNoProgress); Assert.False(repeated.RecordCommand);
        Assert.Equal(movedBefore, Snapshot(moved.State.Battle)); Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Either_actor_order_replay_and_MAX_counters_preserve_entire_query_inputs()
    {
        foreach (bool second in new[] { false, true })
        {
            var actor = second ? B : A; var target = second ? A : B;
            var state = WithMaxCounters(State(second: second)); var before = Snapshot(state);
            var selected = new SwordAiDispatchSelection(SwordAiSelectedPlanKind.Attack, actor, Origin,
                new(actor, target, Origin, SwordAiCandidateOrder.MoveThenAct));
            var request = new SwordAiDispatchRequest(actor, target, SwordAiDecisionMarker.Decided, new(actor, false, false), selected);
            var result = Evaluate(state, request); Assert.Equal(SwordAiDispatchKind.Attack, Success(result).Kind);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, request)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(WithMaxCounters(State(second: second, reverse: true)), request)));
            Assert.Equal(ulong.MaxValue, result.RngState.DrawCount); Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Unknown_context_stale_actor_or_inactive_state_rejects_atomically_before_descriptive_dispatch()
    {
        var state = State(); var request = RequestFor();
        foreach (var profile in new[] { new SwordAiCommandDispatchProfile(), Profile with { InitializedActorContext = false },
            Profile with { PresentCompactRecords = false }, Profile with { CanonicalCoordinates = false }, Profile with { OrdinaryContinuation = false } })
            Atomic(state, Evaluate(state, request, profile: profile), SwordAiDispatchRejection.UnsupportedContext);
        Atomic(state, Evaluate(state, request, fresh: true, profile: Profile with { FreshSelectionCompleted = false }), SwordAiDispatchRejection.UnsupportedContext);
        Atomic(state, Evaluate(state, request with { Marker = SwordAiDecisionMarker.Undecided }, fresh: true), SwordAiDispatchRejection.UnsupportedDecisionMarker);
        Atomic(state, Evaluate(state, request with { SavedChoices = new(B, false, false) }), SwordAiDispatchRejection.StaleActorContext);
        Atomic(state, Evaluate(state, request with { ActorId = B, TargetId = A, SavedChoices = new(B, false, false) }), SwordAiDispatchRejection.UnavailableTurn);
        var unknown = new BattleUnitId("sample:unit/unknown");
        Atomic(state, Evaluate(state, request with { ActorId = unknown }), SwordAiDispatchRejection.UnknownActor);
        Atomic(state, Evaluate(state, request with { TargetId = unknown }), SwordAiDispatchRejection.UnknownTarget);
        Atomic(state, Evaluate(state, request with { TargetId = A }), SwordAiDispatchRejection.InvalidTarget);
        var wrongMovement = new FlatOrdinaryMovementProfile(new("sample:terrain/other"), A, B);
        Atomic(state, SwordAiCommandDispatch.InspectEntry(state, request, wrongMovement, Profile), SwordAiDispatchRejection.UnsupportedProfile);
    }

    [Fact]
    public void Inconsistent_selected_payloads_publish_no_partial_command_or_recorded_choices()
    {
        var state = State(); var valid = Selected(Origin); var attack = valid.Attack!;
        SwordAiDispatchSelection?[] invalid = [null, valid with { ActorId = B }, valid with { Kind = (SwordAiSelectedPlanKind)99 },
            valid with { Destination = new(-1, 1, 0) }, valid with { Destination = new(1, 1, 1) }, valid with { Destination = new(2, 1, 0) },
            valid with { Attack = null }, valid with { Kind = SwordAiSelectedPlanKind.MoveOnly },
            valid with { Attack = attack with { ActorId = B } }, valid with { Attack = attack with { TargetId = A } },
            valid with { Attack = attack with { Destination = Destination } }, valid with { Attack = attack with { Order = (SwordAiCandidateOrder)99 } }];
        foreach (var selection in invalid)
            Atomic(state, Evaluate(state, RequestFor(selection: selection)), SwordAiDispatchRejection.InvalidSelection);
    }

    private static SwordAiDispatchRequest RequestFor(SwordAiDispatchSelection? selection = null) =>
        new(A, B, SwordAiDecisionMarker.Decided, new(A, false, false), selection);
    private static SwordAiDispatchSelection Selected(TilePosition destination, bool attack = true, bool actFirst = false) =>
        new(attack ? SwordAiSelectedPlanKind.Attack : SwordAiSelectedPlanKind.MoveOnly, A, destination,
            attack ? new(A, B, destination, actFirst ? SwordAiCandidateOrder.ActThenMove : SwordAiCandidateOrder.MoveThenAct) : null);
    private static SwordAiDispatchResult Evaluate(AttackBattleSnapshot state, SwordAiDispatchRequest request,
        bool fresh = false, SwordAiCommandDispatchProfile? profile = null) => fresh
            ? SwordAiCommandDispatch.DispatchFreshSelection(state, request, Candidate.Movement, profile ?? Profile)
            : SwordAiCommandDispatch.InspectEntry(state, request, Candidate.Movement, profile ?? Profile);
    private static SwordAiDispatchOutcome Success(SwordAiDispatchResult result)
    { Assert.True(result.Accepted, result.Rejection.ToString()); Assert.Equal(SwordAiDispatchRejection.None, result.Rejection); return Assert.IsType<SwordAiDispatchOutcome>(result.Outcome); }
    private static void Atomic(AttackBattleSnapshot state, SwordAiDispatchResult result, SwordAiDispatchRejection reason)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Null(result.Outcome); Assert.Equal(state.TurnBattle.Turns.RngState, result.RngState); }
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-command-continuation-contract-v1.json")));
    private static AttackBattleSnapshot WithMaxCounters(AttackBattleSnapshot state)
    {
        var old = state.TurnBattle; var rng = new FftRandomState(57, ulong.MaxValue);
        return new(new(new(old.Battle.Map, old.Battle.Units, rng), new(old.Turns.Units, rng, ulong.MaxValue, old.Turns.Phase)), state.Combatants);
    }
}
