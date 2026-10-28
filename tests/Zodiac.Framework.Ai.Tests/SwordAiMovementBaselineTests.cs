using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiMovementBaselineTests
{
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly TilePosition Origin = new(1, 1, 0), Destination = new(2, 1, 0);
    private static readonly FftRandomState Decision = new(1, 200), LiveRng = new(57, 100);
    private static readonly SwordAiCandidateProfile CandidateProfile = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    private static readonly SwordAiScoreProfile ScoreProfile = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);
    private static readonly SwordAiMovementBaselineProfile Profile = new(true, true, true, true);

    [Theory]
    [InlineData(false, false, 10, 50)] [InlineData(false, true, 10, 30)]
    [InlineData(true, false, 10, 30)] [InlineData(false, false, 50, 60)]
    public void Move_stay_and_already_moved_preserve_unused_ACT_refund_and_cap(bool alreadyMoved, bool change, int ct, int expected)
    {
        var state = State(ct: ct, targetCt: 100, moved: alreadyMoved);
        var result = Evaluate(state, change ? Destination : Origin); var outcome = Success(result);
        var actor = outcome.Units.Single(u => u.UnitId == A);
        Assert.Equal(expected, outcome.TurnEndCt); Assert.Equal(expected, actor.Ct);
        Assert.Equal(alreadyMoved || change, actor.MovementTaken); Assert.False(actor.ActionTaken);
        Assert.Equal(change ? Destination : Origin, actor.Position);
        Assert.Equal(0ul, outcome.ElapsedTicks); Assert.Equal(B, outcome.MoveOpportunityUnitId);
        Assert.Equal(new FftRandomState(1103527590, 201), result.RngState);
        Assert.Equal(16838u, Assert.Single(outcome.UpkeepPicks).RawRoll);
    }

    [Fact]
    public void Move_only_refund_changes_the_first_ready_unit_compared_with_the_same_move_and_Attack()
    {
        var state = State(); var outcome = Success(Evaluate(state));
        Assert.Equal((30, A, 7ul, 14ul), (outcome.TurnEndCt, outcome.MoveOpportunityUnitId, outcome.ElapsedTicks, outcome.Ticks));
        Assert.Equal(0, outcome.Units.Single(u => u.UnitId == A).Ct);
        Assert.Equal(90, outcome.Units.Single(u => u.UnitId == B).Ct);
        var attack = SwordAiCandidateProjection.Evaluate(state, new(A, B, Destination, SwordAiCandidateOrder.MoveThenAct),
            new(Weapon, 5), CandidateProfile, Decision);
        Assert.Equal(SwordAiCandidateDisposition.Projected, attack.Disposition);
        Assert.Equal((10, B, 8ul), (attack.Outcome!.TurnEndCt, attack.Outcome.MoveOpportunityUnitId, attack.Outcome.ElapsedTicks));
        Assert.Equal(205ul, attack.RngState.DrawCount);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Tied_readiness_uses_original_slot_order_and_modulo_not_input_order(bool actorFirst)
    {
        var state = State(targetCt: 50, actorFirst: actorFirst, second: true, reverse: true);
        var outcome = Success(Evaluate(state, Origin, second: true));
        Assert.Equal(actorFirst ? B : A, outcome.MoveOpportunityUnitId);
        Assert.Equal(5ul, outcome.ElapsedTicks);
        Assert.Equal((100, 0), (outcome.UpkeepPicks[0].CtBefore, outcome.UpkeepPicks[0].CtAfter));
        Assert.False(outcome.Units.Single(u => u.UnitId == outcome.MoveOpportunityUnitId).ActionTaken);
    }

    [Theory]
    [InlineData(100, 100, 100, 100, 0)] [InlineData(10, 100, 100, 100, -148)]
    [InlineData(100, 100, 10, 100, 148)] [InlineData(1, 65535, 100, 100, -160)]
    [InlineData(100, 100, 1, 65535, 160)]
    public void Absolute_score_keeps_zero_negative_and_boundary_values_without_HP_status_or_facing_changes(
        int hp, int maxHp, int targetHp, int targetMaxHp, int expected)
    {
        var state = State(hp: hp, maxHp: maxHp, targetHp: targetHp, targetMaxHp: targetMaxHp, targetCt: 100);
        var outcome = Success(Evaluate(state));
        Assert.Equal(expected, outcome.Score.Priority); Assert.Equal(100, outcome.Score.HitPercent);
        Assert.Equal(expected, outcome.Score.Contributions[^1].RunningPriority);
        Assert.Equal(2, outcome.Score.Contributions.Length);
        foreach (var contribution in outcome.Score.Contributions)
        {
            Assert.Equal(357, contribution.EquipmentTerm);
            Assert.Equal(contribution.UnitId == B, contribution.Enemy);
        }
        foreach (var combat in state.Combatants)
        {
            var unit = outcome.Units.Single(u => u.UnitId == combat.UnitId);
            Assert.Equal((combat.Hp, combat.MaxHp, combat.Status, combat.Facing), (unit.Hp, unit.MaxHp, unit.Status, unit.Facing));
            Assert.Equal(CandidateProfile.Units.Single(u => u.UnitId == combat.UnitId).DeathCounter, unit.DeathCounter);
        }
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Actor_identity_replay_collection_order_and_carried_RNG_leave_original_inputs_unchanged()
    {
        var state = State(second: true, hp: 10); var before = Snapshot(state);
        var result = Evaluate(state, second: true);
        Assert.Equal(-148, Success(result).Score.Priority);
        Assert.Equal(Serialize(result), Serialize(Evaluate(State(second: true, hp: 10, reverse: true), second: true)));
        Assert.Equal(Serialize(result), Serialize(Evaluate(state, second: true)));
        var next = Evaluate(state, second: true, rng: result.RngState);
        Assert.Equal(new FftBiosRandom().Next(result.RngState).State, next.RngState);
        Assert.Equal(before, Snapshot(state)); Assert.Equal(LiveRng, state.TurnBattle.Battle.RngState);
        Assert.Equal(LiveRng, state.TurnBattle.Turns.RngState); Assert.Equal(7ul, state.TurnBattle.Turns.Ticks);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [Trait("Coverage", "CurrentAdmission")]
    public void Every_unrepresented_baseline_fact_requires_explicit_certification(int missing)
    {
        var profile = missing switch
        {
            0 => Profile with { OriginalScenarioZeroDestination = false }, 1 => Profile with { FreshSourceScoreCell = false },
            2 => Profile with { NormalScoring = false }, _ => Profile with { CanonicalCoordinates = false }
        };
        Atomic(Evaluate(State(), profile: profile), SwordAiMovementBaselineRejection.UnsupportedBaselineProfile);
    }

    [Theory] [InlineData(-1, 1, 0)] [InlineData(5, 1, 0)] [InlineData(2, 1, 1)]
    [InlineData(3, 1, 0)] [InlineData(4, 4, 0)]
    public void Invalid_layer_occupied_and_over_budget_destinations_reject_before_RNG(int x, int y, int layer)
    {
        Atomic(Evaluate(State(move: 1), new(x, y, layer)), SwordAiMovementBaselineRejection.InvalidDestination);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Original_state_and_profile_admission_precede_simulation_even_for_stay()
    {
        Atomic(Evaluate(State(moved: true)), SwordAiMovementBaselineRejection.InvalidDestination);
        Atomic(Evaluate(State(targetHp: 0), Origin), SwordAiMovementBaselineRejection.TerminalInput);
        Atomic(Evaluate(State(acted: true), Origin), SwordAiMovementBaselineRejection.UnavailableAction);
        Atomic(Evaluate(State(active: false), Origin), SwordAiMovementBaselineRejection.UnavailableAction);
        Atomic(Evaluate(State(second: true), Origin), SwordAiMovementBaselineRejection.UnavailableAction);
        Atomic(Evaluate(State(badGround: true), Origin), SwordAiMovementBaselineRejection.UnsupportedProfile);
        Atomic(Evaluate(State(), Origin, profile: new()), SwordAiMovementBaselineRejection.UnsupportedBaselineProfile);
        var unknown = new BattleUnitId("sample:unit/unknown");
        Atomic(SwordAiMovementBaseline.Evaluate(State(), new(unknown, B, Origin), CandidateProfile, ScoreProfile, Profile, Decision),
            SwordAiMovementBaselineRejection.UnknownActor);
        Atomic(SwordAiMovementBaseline.Evaluate(State(), new(A, unknown, Origin), CandidateProfile, ScoreProfile, Profile, Decision),
            SwordAiMovementBaselineRejection.UnknownTarget);
        Atomic(SwordAiMovementBaseline.Evaluate(State(), new(A, A, Origin), CandidateProfile, ScoreProfile, Profile, Decision),
            SwordAiMovementBaselineRejection.UnsupportedProfile);
        var score = new SwordAiScoreProfile([new(A, SwordAiScoreFacts.None), new(B, SwordAiScoreFacts.ClosedProfile)]);
        Atomic(SwordAiMovementBaseline.Evaluate(State(), new(A, B, Origin), CandidateProfile, score, Profile, Decision),
            SwordAiMovementBaselineRejection.UnsupportedScoreProfile);
    }

    [Fact]
    public void Exactly_one_draw_fits_at_MAX_and_exhaustion_returns_no_partial_outcome()
    {
        var fits = new FftRandomState(1, ulong.MaxValue - 1);
        Assert.Equal(ulong.MaxValue, Evaluate(State(), rng: fits).RngState.DrawCount);
        var exhausted = new FftRandomState(1, ulong.MaxValue);
        Atomic(Evaluate(State(), rng: exhausted), SwordAiMovementBaselineRejection.RandomDrawCountExhausted, exhausted);
    }

    [Fact]
    public void Tick_exhaustion_rolls_back_but_ready_at_MAX_needs_no_tick_reservation()
    {
        var state = State(ticks: ulong.MaxValue); var before = Snapshot(state);
        Atomic(Evaluate(state), SwordAiMovementBaselineRejection.TickCountExhausted);
        Assert.Equal(before, Snapshot(state));
        Assert.Equal(ulong.MaxValue, Success(Evaluate(State(ticks: ulong.MaxValue - 7))).Ticks);
        var ready = Success(Evaluate(State(ticks: ulong.MaxValue, targetCt: 254)));
        Assert.Equal(0ul, ready.ElapsedTicks); Assert.Equal(ulong.MaxValue, ready.Ticks);
        Assert.Equal((254, 54), (ready.UpkeepPicks[0].CtBefore, ready.UpkeepPicks[0].CtAfter));
    }

    private static SwordAiMovementBaselineResult Evaluate(AttackBattleSnapshot state, TilePosition? destination = null,
        bool second = false, SwordAiMovementBaselineProfile? profile = null, FftRandomState? rng = null) =>
        SwordAiMovementBaseline.Evaluate(state, new(second ? B : A, second ? A : B, destination ?? Destination),
            CandidateProfile, ScoreProfile, profile ?? Profile, rng ?? Decision);
    private static SwordAiMovementBaselineOutcome Success(SwordAiMovementBaselineResult result)
    {
        Assert.True(result.Accepted); Assert.Equal(SwordAiMovementBaselineRejection.None, result.Rejection);
        return Assert.IsType<SwordAiMovementBaselineOutcome>(result.Outcome);
    }
    private static void Atomic(SwordAiMovementBaselineResult result, SwordAiMovementBaselineRejection reason, FftRandomState? rng = null)
    {
        Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection);
        Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
    }
    private static string Serialize(object value) => JsonSerializer.Serialize(value);
    private static string Snapshot(AttackBattleSnapshot state) => Serialize(new { state.Combatants, state.TurnBattle.Turns,
        state.TurnBattle.Battle.Map, state.TurnBattle.Battle.RngState,
        Units = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved, u.MovementProfile.Move }) });

    private static AttackBattleSnapshot State(int ct = 10, int targetCt = 20, bool moved = false, bool acted = false,
        bool active = true, int hp = 100, int maxHp = 100, int targetHp = 100, int targetMaxHp = 100,
        bool actorFirst = true, bool second = false, bool reverse = false, int move = 3, bool badGround = false, ulong ticks = 7)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), 25).ToArray();
        if (badGround) tiles[^1] = new(Ground, 11);
        var movement = new UnitMovementProfile(move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, Origin, movement, moved), new(target, new(3, 1, 0), movement, true) };
        var turns = new TurnUnitState[] { new(actor, 10, ct, actorFirst ? 2 : 17, active, moved, acted),
            new(target, 10, targetCt, actorFirst ? 17 : 2, false, true, true) };
        AttackUnitState Combat(BattleUnitId id, int current, int maximum, BattleFacing facing) =>
            new(id, current, maximum, 0, 0, 5, 0, 0, 0, 0, 70, 0, facing,
                current == 0 ? AttackUnitStatus.Dead : current <= maximum / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, hp, maxHp, BattleFacing.West), Combat(target, targetHp, targetMaxHp, BattleFacing.East) };
        return new(new(new(new(5, 5, 1, tiles), reverse ? bodies.Reverse() : bodies, LiveRng),
            new(reverse ? turns.Reverse() : turns, LiveRng, ticks, active ? TurnCyclePhase.UnitTurn : TurnCyclePhase.PickUnit)),
            reverse ? combat.Reverse() : combat);
    }
}
