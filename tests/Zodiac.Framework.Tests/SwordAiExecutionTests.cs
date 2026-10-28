using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiExecutionTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly SwordAiCandidateProfile Candidate = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    private static readonly SwordAiScoreProfile Score = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);
    private static readonly SwordAiMovementScenarioProfile Scenario = new(true, true, true);
    private static readonly SwordAiDerivedSelectionProfile Derived = new(true, true, true, true, true, true, true, true);
    private static readonly SwordAiSelectionProfile Selection = new(true, true, true);
    private static readonly SwordAiExecutionProfile Execution = new(true);
    private static readonly SwordAiWaitFacingProfile Wait = new(true, true);
    private static readonly SwordAiWaitSettlementProfile End = new(true);

    [Theory]
    [InlineData(89, 3655513600u, 14ul, 180171308u, 18ul, 1)]
    [InlineData(90, 2937909499u, 21ul, 3535774167u, 25ul, 8)]
    public void Fresh_selection_draws_continue_into_a_real_miss_instead_of_applying_projected_damage(int evade,
        uint selectedWord, ulong selectedCount, uint finalWord, ulong finalCount, int lotteryDraws)
    {
        var state = Ready(evade: evade); var before = Snapshot(state);
        Assert.Equal(new FftRandomState(1, 7), state.Battle.TurnBattle.Turns.RngState);
        var result = Execute(state); var o = Success(result);
        Assert.Equal(new FftRandomState(selectedWord, selectedCount), o.DecisionRng);
        Assert.Equal(lotteryDraws, o.Decision.Selection.Lottery.Length);
        Assert.Equal(new SwordAiLotteryRoll(0, 23010, 10, 100 - evade, evade == 89), o.Decision.Selection.Lottery[0]);
        var projection = o.Decision.Selection.Plan.Attack!.Evaluation.Projection!.Outcome!;
        Assert.Equal(25, projection.NormalDamage); Assert.Equal(25, projection.Units.Single(u => u.UnitId == B).Hp);
        Assert.Equal(SwordAiExecutedCommand.Attack, o.Command); Assert.Equal(new AttackUnitCommand(A, B), o.Attack);
        Assert.Null(o.WaitFacing); Assert.Equal(new AttackResolvedEvent(A, B, false, false, 0), Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(50, Unit(result.State, B).Hp); Assert.Equal(15, Turn(result.State, A).Ct);
        Assert.Equal(new FftRandomState(finalWord, finalCount), result.State.Battle.TurnBattle.Turns.RngState);
        ReceiptAndClock(state, result.State); Assert.Equal(before, Snapshot(state));
        var receipt = result.State.PendingFacing;
        var finish = SwordAiWaitSettlement.Finish(result.State, A, B, receipt, Candidate.Movement, Wait, End);
        Assert.True(finish.Accepted); Assert.Same(result.State.Battle.TurnBattle, finish.State.Battle.TurnBattle);
        Assert.Equal(BattleFacing.West, Unit(finish.State, A).Facing); Assert.Null(finish.State.PendingFacing);
        Assert.Single(finish.Events); Assert.Equal(result.State.Revision + 1, finish.State.Revision);
        Assert.Equal(FacingTurnRejection.MissingCompletion,
            FacingTurnBattle.FinishFacing(finish.State, A, receipt, new FacingChoice.KeepCurrent()).FacingRejection);
    }

    [Theory] [InlineData(false, 16ul, 3210001534u)] [InlineData(true, 21ul, 2937909499u)]
    public void No_ranked_Attack_waits_after_adopting_all_draws_without_spending_ACT(bool zeroPower, ulong count, uint word)
    {
        var state = Ready();
        var result = Execute(state, power: zeroPower ? 0 : 5, derived: Derived with { TargetActive = zeroPower }); var o = Success(result);
        Assert.Equal(SwordAiExecutedCommand.Wait, o.Command); Assert.Null(o.Attack); Assert.NotNull(o.WaitFacing);
        Assert.Equal(SwordAiSelectionReason.MovementFallback, o.Decision.Selection.Reason);
        Assert.Equal(8, o.Decision.Selection.Lottery.Length);
        Assert.Equal(new FftRandomState(word, count), o.DecisionRng);
        Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(35, Turn(result.State, A).Ct); Assert.True(Turn(result.State, A).MovementTaken);
        Assert.False(Turn(result.State, A).ActionTaken); Assert.Null(result.State.PendingFacing);
        Assert.Equal(50, Unit(result.State, B).Hp); Assert.Equal(BattleFacing.West, Unit(result.State, A).Facing);
        Assert.Empty(result.Events.OfType<AttackResolvedEvent>()); Assert.Single(Ended(result.Events));
        Assert.IsType<FacingSettledEvent>(result.Events[^1]); Assert.Equal(state.Revision + 1, result.State.Revision);
        AssertClockAndWorld(state, result.State); AssertRngCopies(result.State);
    }

    [Fact]
    public void Successful_noncritical_attack_runs_real_combat_after_selection()
    {
        var state = Ready(evade: 0); var result = Execute(state); var o = Success(result);
        Assert.Equal(new FftRandomState(3655513600, 14), o.DecisionRng);
        Assert.Equal(new AttackResolvedEvent(A, B, true, false, 25), Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(25, Unit(result.State, B).Hp); Assert.Equal(20ul, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        ReceiptAndClock(state, result.State);
    }

    [Theory] [InlineData(5, 45, 5)] [InlineData(6, 54, 0)]
    public void Actual_critical_push_or_KO_is_published_before_the_completion_receipt(int power, int damage, int hp)
    {
        var state = Ready(evade: 0, entryWord: 73); var result = Execute(state, power: power); var o = Success(result);
        Assert.Equal(new FftRandomState(2162298728, 14), o.DecisionRng);
        Assert.Equal(new FftRandomState(916350979, 21), result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(new AttackResolvedEvent(A, B, true, true, damage), Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(hp, Unit(result.State, B).Hp); ReceiptAndClock(state, result.State);
        if (hp > 0)
        {
            Assert.Equal(new TilePosition(0, 1, 0), Position(result.State, B)); Assert.Single(result.Events.OfType<SwordKnockbackEvent>());
            var finish = SwordAiWaitSettlement.Finish(result.State, A, B, result.State.PendingFacing, Candidate.Movement, Wait, End);
            Assert.True(finish.Accepted); Assert.Equal(new TilePosition(0, 1, 0), finish.Facing!.TargetPosition);
        }
        else
        {
            Assert.Equal(new TilePosition(1, 1, 0), Position(result.State, B)); Assert.Single(result.Events.OfType<AttackKnockedOutEvent>());
            var finish = SwordAiWaitSettlement.Finish(result.State, A, B, result.State.PendingFacing, Candidate.Movement, Wait, End);
            Assert.False(finish.Accepted); Assert.Equal(SwordAiWaitFacingRejection.UnsupportedProfile, finish.FacingRejection);
            Assert.Same(result.State, finish.State); Assert.Equal(0, Unit(result.State, B).Hp); // A later facing failure does not undo the committed Attack.
        }
    }

    [Theory] [InlineData(11, true)] [InlineData(10, false)]
    public void Actual_draw_budget_reaches_MAX_or_rolls_back_the_entire_command(int available, bool accepted)
    {
        var state = Ready(count: ulong.MaxValue - (ulong)available); var before = Snapshot(state);
        var result = Execute(state);
        if (accepted)
        {
            Success(result); Assert.Equal(ulong.MaxValue, result.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            ReceiptAndClock(state, result.State);
        }
        else
        {
            var failure = Atomic(state, result, SwordAiExecutionRejection.CommandRejected, SwordAiExecutionStage.Command);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failure.CommandFailure!.AttackRejection);
        }
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Fallback_Wait_finishes_at_MAX_with_no_extra_draw_or_refund()
    {
        var state = Ready(count: ulong.MaxValue - 9);
        var result = Execute(state, derived: Derived with { TargetActive = false }); var o = Success(result);
        Assert.Equal(ulong.MaxValue, o.DecisionRng.DrawCount); Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(35, Turn(result.State, A).Ct); Assert.Null(result.State.PendingFacing);
        AssertClockAndWorld(state, result.State); AssertRngCopies(result.State);
    }

    [Fact]
    public void Failed_movement_prefix_or_lottery_exposes_only_reasons_and_original_session()
    {
        foreach (int available in new[] { 0, 6 })
        {
            var state = Ready(count: ulong.MaxValue - (ulong)available);
            var failure = Atomic(state, Execute(state), SwordAiExecutionRejection.SelectionRejected, SwordAiExecutionStage.Selection);
            var selection = failure.SelectionFailure!;
            if (available == 0)
            {
                Assert.Equal(SwordAiDerivedSelectionStage.MovementScenario, selection.Stage);
                Assert.Equal(SwordAiMovementBaselineRejection.RandomDrawCountExhausted, selection.ScenarioFailure!.MovementReason);
            }
            else
            {
                Assert.Equal(SwordAiDerivedSelectionStage.Lottery, selection.Stage);
                Assert.Equal(SwordAiSelectionRejection.RandomDrawCountExhausted, selection.SelectionFailure!.Reason);
                Assert.Equal(0, selection.SelectionFailure.LotteryIndex);
            }
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Unknown_entry_Wait_end_or_selector_context_and_invalid_weapon_reject_atomically()
    {
        var state = Ready();
        Atomic(state, Execute(state, execution: new()), SwordAiExecutionRejection.UnsupportedContext, SwordAiExecutionStage.Admission);
        Atomic(state, Execute(state, wait: new()), SwordAiExecutionRejection.UnsupportedContext, SwordAiExecutionStage.Admission);
        Atomic(state, Execute(state, end: new()), SwordAiExecutionRejection.UnsupportedContext, SwordAiExecutionStage.Admission);
        Atomic(state, Execute(state, derived: Derived with { FirstAttackEntry = false }), SwordAiExecutionRejection.SelectionRejected, SwordAiExecutionStage.Selection);
        var invalid = SwordAiExecution.ExecuteAlreadyMoved(state, null, Candidate, Score, new(A, B, true), Scenario,
            Derived with { TargetActive = false }, Selection, Execution, Wait, End);
        var failure = Atomic(state, invalid, SwordAiExecutionRejection.SelectionRejected, SwordAiExecutionStage.Selection);
        Assert.Equal(SwordAiCandidateRejection.UnsupportedProfile, failure.SelectionFailure!.AdmissionFailure!.ProjectionReason);
    }

    [Fact]
    public void Real_choice_authority_and_revision_guards_precede_decision_work()
    {
        var unmoved = Ready(move: false);
        Atomic(unmoved, Execute(unmoved), SwordAiExecutionRejection.UnavailableChoices, SwordAiExecutionStage.Admission);
        var acted = Accepted(FacingTurnBattle.Attack(unmoved, new(A, B), new(Weapon, 0)));
        Atomic(acted, Execute(acted), SwordAiExecutionRejection.UnavailableChoices, SwordAiExecutionStage.Admission);
        var state = Ready(); var pending = Execute(state); Success(pending);
        var failure = Atomic(pending.State, Execute(pending.State), SwordAiExecutionRejection.AuthorityRejected, SwordAiExecutionStage.Admission);
        Assert.Equal(FacingTurnRejection.FacingPending, failure.AuthorityRejection);
        failure = Atomic(state, Execute(state, actor: B, target: A), SwordAiExecutionRejection.AuthorityRejected, SwordAiExecutionStage.Admission);
        Assert.Equal(FacingTurnRejection.InactiveActor, failure.AuthorityRejection);
        // Friend construction only for the unreachable-in-practice counter boundary, as in existing facing tests.
        var exhausted = new FacingTurnState(state.Battle, state.Profile, ulong.MaxValue, null, state.Lineage);
        failure = Atomic(exhausted, Execute(exhausted), SwordAiExecutionRejection.AuthorityRejected, SwordAiExecutionStage.Admission);
        Assert.Equal(FacingTurnRejection.RevisionExhausted, failure.AuthorityRejection);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Identity_collection_order_replay_and_receipt_lineage_remain_deterministic(bool second)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var state = Ready(second: second); var before = Snapshot(state);
        var first = Execute(state, actor: actor, target: target); var replay = Execute(state, actor: actor, target: target);
        var other = Execute(Ready(second: second, reverse: true), actor: actor, target: target);
        Success(first); Success(replay); Success(other);
        Assert.Equal(Snapshot(first.State), Snapshot(replay.State)); Assert.Equal(Snapshot(first.State), Snapshot(other.State));
        Assert.Equal(first.Events.ToArray(), replay.Events.ToArray()); Assert.Equal(first.Events.ToArray(), other.Events.ToArray());
        Assert.Equal(JsonSerializer.Serialize(first.Outcome), JsonSerializer.Serialize(other.Outcome));
        Assert.Equal(before, Snapshot(state));
        Assert.Equal(FacingTurnRejection.StaleCompletion,
            FacingTurnBattle.FinishFacing(replay.State, actor, first.State.PendingFacing, new FacingChoice.KeepCurrent()).FacingRejection);
        Assert.True(FacingTurnBattle.FinishFacing(first.State, actor, first.State.PendingFacing, new FacingChoice.KeepCurrent()).Accepted);
        Assert.True(FacingTurnBattle.FinishFacing(first.State, actor, first.State.PendingFacing, new FacingChoice.KeepCurrent()).Accepted);
    }

    private static SwordAiExecutionResult Execute(FacingTurnState state, int power = 5, SwordAiDerivedSelectionProfile? derived = null,
        SwordAiExecutionProfile? execution = null, SwordAiWaitFacingProfile? wait = null, SwordAiWaitSettlementProfile? end = null,
        BattleUnitId? actor = null, BattleUnitId? target = null) => SwordAiExecution.ExecuteAlreadyMoved(state, new(Weapon, power),
            Candidate, Score, new(actor ?? A, target ?? B, true), Scenario, derived ?? Derived, Selection, execution ?? Execution, wait ?? Wait, end ?? End);
    private static SwordAiExecutionOutcome Success(SwordAiExecutionResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Null(result.Failure); return Assert.IsType<SwordAiExecutionOutcome>(result.Outcome); }
    private static SwordAiExecutionFailure Atomic(FacingTurnState state, SwordAiExecutionResult result, SwordAiExecutionRejection reason, SwordAiExecutionStage stage)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Same(state, result.State); Assert.Empty(result.Events);
        var failure = Assert.IsType<SwordAiExecutionFailure>(result.Failure); Assert.Equal(reason, failure.Reason); Assert.Equal(stage, failure.Stage); return failure; }
    private static FacingTurnState Accepted(FacingTurnResult result)
    { Assert.True(result.Accepted, $"{result.FacingRejection}/{result.AttackRejection}/{result.TurnRejection}/{result.MovementRejection}"); return result.State; }
    private static AttackUnitState Unit(FacingTurnState state, BattleUnitId id) => state.Battle.Combatants.Single(u => u.UnitId == id);
    private static TurnUnitState Turn(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == id);
    private static TilePosition Position(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == id).Position;
    private static IEnumerable<TurnEndedEvent> Ended(IEnumerable<AttackResultEvent> events) => events.OfType<AttackTurnBattleEvent>()
        .Select(e => e.Event).OfType<TurnBattleClockEvent>().Select(e => e.Event).OfType<TurnEndedEvent>();
    private static void AssertRngCopies(FacingTurnState state) => Assert.Equal(state.Battle.TurnBattle.Turns.RngState, state.Battle.TurnBattle.Battle.RngState);
    private static void AssertClockAndWorld(FacingTurnState before, FacingTurnState after)
    { Assert.Equal(before.Battle.TurnBattle.Turns.Ticks, after.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(Turn(before, B), Turn(after, B)); Assert.Equal(Position(before, A), Position(after, A)); }
    private static void ReceiptAndClock(FacingTurnState before, FacingTurnState after)
    {
        Assert.Equal(before.Revision + 1, after.Revision); Assert.Null(after.Battle.TurnBattle.Turns.ActiveUnitId);
        var receipt = Assert.IsType<FacingCompletionReceipt>(after.PendingFacing);
        Assert.Equal(after.Revision, receipt.Revision); Assert.Same(after.Battle, receipt.ExpectedBattle); Assert.Same(before.Lineage, receipt.Lineage);
        AssertClockAndWorld(before, after); AssertRngCopies(after);
    }
    private static string Snapshot(FacingTurnState state) => JsonSerializer.Serialize(new { state.Revision,
        pending = state.PendingFacing is { } p ? new { p.ActorId, p.Revision } : null,
        combat = state.Battle.Combatants.OrderBy(u => u.UnitId.Value), turns = state.Battle.TurnBattle.Turns.Units.OrderBy(u => u.Id.Value),
        bodies = state.Battle.TurnBattle.Battle.Units.OrderBy(u => u.Id.Value).Select(u => new { u.Id, u.Position, u.HasMoved }),
        state.Battle.TurnBattle.Turns.Phase, state.Battle.TurnBattle.Turns.Ticks, state.Battle.TurnBattle.Turns.RngState,
        bodyRng = state.Battle.TurnBattle.Battle.RngState });

    private static FacingTurnState Ready(int evade = 89, uint entryWord = 1, ulong count = 7,
        bool second = false, bool reverse = false, bool move = true)
    {
        var actor = second ? B : A; var target = second ? A : B;
        // Inverse BIOS step: Advance's actual upkeep draw reaches the authored entry word/count.
        uint setupWord = unchecked((entryWord - 12345u) * 4005161829u);
        var rng = new FftRandomState(setupWord, count - 1);
        var map = new BattleMapDefinition(5, 3, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 15).ToArray());
        var movement = new UnitMovementProfile(1, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        BattleUnitState[] bodies = [new(actor, new(move ? 3 : 2, 1, 0), movement), new(target, new(1, 1, 0), movement)];
        TurnUnitState[] turns = [new(actor, 5, 110, 2), new(target, 5, 90, 17)];
        AttackUnitState Combat(BattleUnitId id, int evasion) => new(id, 50, 50, 0, 0, 5, evasion, 0, 0, 0, 70, 0,
            BattleFacing.East, AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, 0), Combat(target, evade) };
        var battle = new AttackBattleSnapshot(new(new(map, reverse ? bodies.Reverse() : bodies, rng),
            new(reverse ? turns.Reverse() : turns, rng)), reverse ? combat.Reverse() : combat);
        var state = Accepted(FacingTurnBattle.Advance(FacingTurnBattle.Begin(battle, new(Ground))));
        return move ? Accepted(FacingTurnBattle.Move(state, new(actor, new(2, 1, 0)))) : state;
    }
}
