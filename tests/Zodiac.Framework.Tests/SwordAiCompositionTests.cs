using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiCompositionTests
{
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly TilePosition Actor = new(2, 1, 0), Top = new(1, 0, 0), Left = new(0, 1, 0), Bottom = new(1, 2, 0);
    private static readonly FftRandomState Decision = new(1, 7), LiveRng = new(57, 100);
    private static readonly SwordAiCandidateProfile CandidateProfile = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    private static readonly SwordAiScoreProfile ScoreProfile = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);

    private sealed record Frame(AttackBattleSnapshot State, SwordAiCompositionScenario Scenario, BoundedSwordWeapon? Weapon,
        SwordAiCandidateProfile Candidate, SwordAiScoreProfile Score, SwordAiComparisonProfile Comparison,
        SwordAiMovementBaselineProfile Baseline, SwordAiScanProfile Scan, SwordAiCompositionProfile Profile, FftRandomState Rng);
    private static Frame Setup(AttackBattleSnapshot? state = null, bool second = false) => new(state ?? State(),
        new([Bottom, Top, Left], Bottom, Top), new(Weapon, 5), CandidateProfile, ScoreProfile,
        new(second ? B : A, second ? A : B, true), new(true, true, true, true), new(true, true, true, true, true), new(true, true, true), Decision);
    private static SwordAiCompositionResult Evaluate(Frame f) => SwordAiComposition.Evaluate(f.State, f.Scenario,
        f.Weapon, f.Candidate, f.Score, f.Comparison, f.Baseline, f.Scan, f.Profile, f.Rng);

    [Fact]
    public void Supplied_scoring_mask_runs_once_before_four_fresh_Attacks_and_reads_baseline_without_an_extra_draw()
    {
        var origins = new List<TilePosition> { Bottom, Top, Left };
        var f = Setup() with { Scenario = new(origins, Bottom, Top) }; origins.Clear();
        var result = Evaluate(f); var outcome = Success(result);
        Assert.Equal(new[] { Top, Left, Bottom }, outcome.Movement.Select(m => m.Outcome.Destination));
        Assert.Equal(new[] { Top, Left, Actor, Bottom }, outcome.AttackScenario.Origins);
        Assert.Equal(2, outcome.BaselineIndex); Assert.Same(outcome.Movement[2], outcome.Baseline);
        Assert.Equal(new FftRandomState(662824084, 10), outcome.Movement[^1].RngState);
        Assert.Equal(new FftRandomState(1343201072, 30), result.RngState);
        Assert.Equal(new ulong[] { 8, 9, 10 }, outcome.Movement.Select(m => m.RngState.DrawCount));
        Assert.All(outcome.Movement, m =>
        {
            Assert.Equal(0, m.Outcome.Score.Priority); Assert.Equal(100, m.Outcome.Score.HitPercent);
            Assert.All(m.Outcome.Units, u => Assert.Equal(50, u.Hp));
            Assert.Equal(1ul, m.Outcome.ElapsedTicks); Assert.Equal(21ul, m.Outcome.Ticks);
        });
        var candidates = outcome.Scan.Outcome!.Candidates;
        Assert.Equal(new ulong[] { 15, 20, 25, 30 }, candidates.Select(c => c.Evaluation.RngState.DrawCount));
        Assert.All(candidates, c =>
        {
            Assert.Equal(64, c.Evaluation.Score!.Priority);
            Assert.Equal(25, c.Evaluation.Projection!.Outcome!.Units.Single(u => u.UnitId == B).Hp);
        });
        Assert.Equal(SwordAiCandidateOrder.ActThenMove, candidates[2].Candidate.Order);
        Assert.Equal(candidates[0].Candidate.Destination, candidates[2].Candidate.Destination);
        Assert.Equal(0, outcome.Scan.Outcome.RetainedCandidateIndex); // Strict tie keeps earliest; duplicates are evaluated.
        Assert.Equal(SwordAiAdmission.Admitted, outcome.Admission); Assert.Equal(8, outcome.RankedActions.Length);
        Assert.Same(candidates[0], outcome.RankedActions[0]); Assert.All(outcome.RankedActions.Skip(1), Assert.Null);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Actor_already_scored_is_added_once_and_stay_after_Move_remains_supported(bool moved)
    {
        var f = Setup(State(moved: moved)) with { Scenario = new([Actor], Actor, Actor) };
        var result = Evaluate(f); var outcome = Success(result);
        Assert.Single(outcome.Movement); Assert.Single(outcome.AttackScenario.Origins);
        Assert.Single(outcome.Scan.Outcome!.Candidates);
        Assert.Equal(moved ? 35 : 55, outcome.Baseline.Outcome.TurnEndCt);
        Assert.Equal(13ul, result.RngState.DrawCount); // One movement draw, four formula draws, one upkeep draw.
        Assert.Equal(SwordAiAdmission.Admitted, outcome.Admission);
    }

    [Theory] [InlineData("inactive")] [InlineData("no-origins")] [InlineData("zero-accuracy")]
    public void Empty_and_NotValuable_scans_keep_the_movement_prefix_without_inventing_a_zero_best(string kind)
    {
        Frame f;
        if (kind == "zero-accuracy") f = Setup(State(origin: new(3, 2, 0), evade: 100)) with { Scenario = new([Actor], Actor, Actor) };
        else if (kind == "no-origins") f = Setup(State(origin: new(3, 2, 0))) with { Scenario = new([new(3, 1, 0)], new(3, 1, 0), new(3, 1, 0)) };
        else { f = Setup(); f = f with { Scan = f.Scan with { TargetActive = false } }; }
        var result = Evaluate(f); var outcome = Success(result);
        Assert.Equal(SwordAiAdmission.NoScoredCandidate, outcome.Admission); Assert.All(outcome.RankedActions, Assert.Null);
        Assert.Null(outcome.Scan.Outcome!.RetainedCandidateIndex);
        Assert.Equal(kind == "zero-accuracy" ? SwordAiScanDisposition.NoScoredCandidate : SwordAiScanDisposition.NoCandidates, outcome.Scan.Disposition);
        Assert.Equal(Decision.DrawCount + (ulong)outcome.Movement.Length + (kind == "zero-accuracy" ? 4ul : 0), result.RngState.DrawCount);
    }

    [Fact]
    public void Movement_cells_outside_Attack_range_are_evaluated_before_the_added_actor_origin()
    {
        var far = new TilePosition(3, 2, 0); var f = Setup() with { Scenario = new([far], far, far) };
        var result = Evaluate(f); var outcome = Success(result);
        Assert.Equal(far, Assert.Single(outcome.Movement).Outcome.Destination);
        Assert.Equal(Actor, Assert.Single(outcome.Scan.Outcome!.Candidates).Origin);
        Assert.Equal(13ul, result.RngState.DrawCount);
    }

    [Theory] [InlineData("equal", 0, 0, SwordAiAdmission.NotAboveBaseline)]
    [InlineData("negative", -148, -116, SwordAiAdmission.Admitted)]
    [InlineData("zero-best", -13, 0, SwordAiAdmission.Admitted)]
    public void Strict_baseline_admission_distinguishes_equal_zero_and_improving_negative_scores(
        string kind, int baseline, int best, SwordAiAdmission admission)
    {
        var f = kind switch
        {
            "equal" => Setup() with { Weapon = new(Weapon, 0), Scenario = new([Bottom, new(3, 2, 0)], Bottom, new(3, 2, 0)) },
            "negative" => Setup(State(hp: 10, maxHp: 100, targetHp: 100, targetMaxHp: 100)),
            _ => Setup(State(hp: 10, maxHp: 100, targetHp: 20, targetMaxHp: 100, pa: 2))
        };
        var outcome = Success(Evaluate(f));
        Assert.Equal(baseline, outcome.Baseline.Outcome.Score.Priority);
        Assert.Equal(best, outcome.Scan.Outcome!.Candidates[outcome.Scan.Outcome.RetainedCandidateIndex!.Value].Evaluation.Score!.Priority);
        if (kind == "equal") Assert.Equal(7, outcome.Scan.Outcome.Candidates[outcome.Scan.Outcome.RetainedCandidateIndex!.Value].Proximity);
        Assert.Equal(admission, outcome.Admission);
        Assert.Equal(admission == SwordAiAdmission.Admitted ? 1 : 0, outcome.RankedActions.Count(c => c is not null));
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)]
    [Trait("Coverage", "CurrentAdmission")]
    public void Every_source_context_certificate_is_required_before_any_movement_even_when_Attack_is_inactive(int missing)
    {
        var f = Setup(State(ticks: ulong.MaxValue)); f = f with { Rng = new(1, ulong.MaxValue), Scan = f.Scan with { TargetActive = false } };
        f = missing switch
        {
            0 => f with { Profile = f.Profile with { OriginalMovementScoreMask = false } },
            1 => f with { Profile = f.Profile with { NoInterveningRandomWork = false } },
            2 => f with { Profile = f.Profile with { FreshEmptyRankedList = false } },
            3 => f with { Baseline = f.Baseline with { OriginalScenarioZeroDestination = false } },
            4 => f with { Baseline = f.Baseline with { FreshSourceScoreCell = false } },
            5 => f with { Baseline = f.Baseline with { NormalScoring = false } },
            6 => f with { Baseline = f.Baseline with { CanonicalCoordinates = false } },
            7 => f with { Scan = f.Scan with { OrdinaryAttackTilePolicy = false } },
            8 => f with { Scan = f.Scan with { OriginalScenarioMask = false } },
            9 => f with { Scan = f.Scan with { TargetSelectionMatchesBaseline = false } },
            10 => f with { Scan = f.Scan with { FreshAbilityBestReset = false } },
            11 => f with { Comparison = f.Comparison with { FreshDecisionBaseline = false } },
            _ => f with { Score = new([new(A, SwordAiScoreFacts.None), new(B, SwordAiScoreFacts.ClosedProfile)]) }
        };
        var failure = Rejected(f, SwordAiCompositionStage.Admission);
        Assert.Equal(missing < 7 ? SwordAiCompositionRejection.UnsupportedProfile : SwordAiCompositionRejection.AdmissionRejected, failure.Reason);
    }

    [Theory] [InlineData("empty")] [InlineData("duplicate")] [InlineData("baseline-added-actor")]
    [InlineData("attack-added-actor")] [InlineData("occupied")] [InlineData("layer")] [InlineData("outside")] [InlineData("moved")]
    public void Invalid_scoring_masks_and_unscored_destinations_reject_before_draws(string kind)
    {
        var f = Setup(); var p = kind switch { "occupied" => new TilePosition(1, 1, 0), "layer" => new(1, 0, 1), _ => new(-1, 0, 0) };
        f = f with { Scenario = kind switch
        {
            "empty" => new([], Bottom, Top), "duplicate" => new([Top, Top], Top, Top),
            "baseline-added-actor" => new([Top], Actor, Top), "attack-added-actor" => new([Top], Top, Actor),
            "moved" => f.Scenario, _ => new([p], p, p)
        } };
        if (kind == "moved") f = f with { State = State(moved: true) };
        Rejected(f, SwordAiCompositionStage.Admission);
    }

    [Theory] [InlineData("weapon")] [InlineData("geometry")] [InlineData("envelope")] [InlineData("terminal")] [InlineData("acted")]
    public void Unsupported_state_and_weapon_reject_before_prefix_even_if_Attack_is_filtered_out(string kind)
    {
        var f = Setup(kind switch { "geometry" => State(badGround: true), "envelope" => State(pa: 255),
            "terminal" => State(targetHp: 0), "acted" => State(acted: true), _ => State() });
        f = f with { Scan = f.Scan with { TargetActive = false }, Weapon = kind == "weapon" ? null : new(Weapon, 65) };
        Assert.Equal(SwordAiCompositionRejection.AdmissionRejected, Rejected(f, SwordAiCompositionStage.Admission).Reason);
    }

    [Theory] [InlineData(0, SwordAiCompositionStage.Movement, 0)] [InlineData(2, SwordAiCompositionStage.Movement, 2)]
    [InlineData(3, SwordAiCompositionStage.Attack, 0)] [InlineData(7, SwordAiCompositionStage.Attack, 0)]
    [InlineData(12, SwordAiCompositionStage.Attack, 1)] [InlineData(22, SwordAiCompositionStage.Attack, 3)]
    public void Movement_or_late_Attack_exhaustion_discards_the_entire_private_prefix(int available, SwordAiCompositionStage stage, int index)
    {
        var f = Setup() with { Rng = new(1, ulong.MaxValue - (ulong)available) };
        var failure = Rejected(f, stage); Assert.Equal(index, failure.Index);
        if (stage == SwordAiCompositionStage.Movement) Assert.Equal(SwordAiMovementBaselineRejection.RandomDrawCountExhausted, failure.MovementReason);
        else Assert.Equal(SwordAiCandidateRejection.RandomDrawCountExhausted, failure.ProjectionReason);
    }

    [Fact]
    public void Exact_MAX_draws_fit_and_every_movement_tile_restarts_the_original_clock()
    {
        var fits = Setup() with { Rng = new(1, ulong.MaxValue - 23) };
        Assert.Equal(ulong.MaxValue, Evaluate(fits).RngState.DrawCount);
        var origin = new TilePosition(0, 0, 0); var moved = new TilePosition(1, 0, 0);
        var f = Setup(State(origin: origin, target: new(3, 2, 0), ct: 10, targetCt: 20, speed: 10, ticks: ulong.MaxValue - 6))
            with { Scenario = new([moved, origin], origin, moved) };
        var failure = Rejected(f, SwordAiCompositionStage.Movement);
        Assert.Equal(1, failure.Index); Assert.Equal(SwordAiMovementBaselineRejection.TickCountExhausted, failure.MovementReason);
        f = f with { State = State(origin: origin, target: new(3, 2, 0), ct: 10, targetCt: 20, speed: 10, ticks: ulong.MaxValue - 7) };
        var outcome = Success(Evaluate(f));
        Assert.Equal(new ulong[] { 5, 7 }, outcome.Movement.Select(m => m.Outcome.ElapsedTicks));
        Assert.Equal(ulong.MaxValue, outcome.Movement[1].Outcome.Ticks);
        Assert.Equal(SwordAiScanDisposition.NoCandidates, outcome.Scan.Disposition);
        Assert.Equal(Decision.DrawCount + 2, outcome.Scan.RngState.DrawCount);
        Assert.Equal(0ul, Success(Evaluate(Setup(State(targetCt: 254, ticks: ulong.MaxValue)))).Movement[0].Outcome.ElapsedTicks);
    }

    [Fact]
    public void Every_lethal_candidate_uses_original_living_state_and_original_proximity_without_changing_live_state()
    {
        var f = Setup(State(targetHp: 20)); var before = Snapshot(f.State); var outcome = Success(Evaluate(f));
        Assert.Equal(B, outcome.Scan.Outcome!.Cache.EligibleEnemyId);
        Assert.All(outcome.Scan.Outcome.Candidates, c =>
        {
            Assert.Equal(9, c.Proximity); Assert.Equal(320, c.Evaluation.Score!.Priority);
            Assert.True(c.Evaluation.Projection!.Outcome!.TargetKnockedOut);
        });
        Assert.Equal(0, outcome.Scan.Outcome.RetainedCandidateIndex);
        Assert.Equal(before, Snapshot(f.State));
    }

    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Contract", "QueryIsolation")]
    public void Actor_identity_collection_order_replay_and_explicit_carry_preserve_original_inputs(bool second)
    {
        var f = Setup(State(second: second), second); var before = Snapshot(f.State); var result = Evaluate(f);
        var reversed = Setup(State(second: second, reverse: true), second);
        reversed = reversed with { Scenario = new(f.Scenario.MovementOrigins.Reverse(), Bottom, Top),
            Candidate = new(new(Ground), new(Ground, A, B), CandidateProfile.Units.Reverse()), Score = new(ScoreProfile.Units.Reverse()) };
        Assert.Equal(Serialize(result), Serialize(Evaluate(reversed)));
        Assert.Equal(Serialize(result), Serialize(Evaluate(f)));
        Assert.Equal(result.RngState.DrawCount + 23, Evaluate(f with { Rng = result.RngState }).RngState.DrawCount);
        Assert.Equal(before, Snapshot(f.State)); Assert.Equal(LiveRng, f.State.TurnBattle.Turns.RngState);
    }

    private static SwordAiCompositionOutcome Success(SwordAiCompositionResult result)
    { Assert.True(result.Accepted); Assert.Null(result.Failure); return Assert.IsType<SwordAiCompositionOutcome>(result.Outcome); }
    private static SwordAiCompositionFailure Rejected(Frame f, SwordAiCompositionStage stage)
    {
        var before = Snapshot(f.State); var result = Evaluate(f);
        Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(f.Rng, result.RngState);
        Assert.Equal(before, Snapshot(f.State)); var failure = Assert.IsType<SwordAiCompositionFailure>(result.Failure);
        Assert.Equal(stage, failure.Stage); return failure;
    }
    private static string Serialize(object value) => JsonSerializer.Serialize(value);
    private static string Snapshot(AttackBattleSnapshot state) => Serialize(new { state.Combatants, state.TurnBattle.Turns,
        state.TurnBattle.Battle.Map, state.TurnBattle.Battle.RngState,
        Units = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved, u.MovementProfile.Move }) });
    private static AttackBattleSnapshot State(TilePosition? origin = null, TilePosition? target = null, int ct = 15, int targetCt = 95,
        int speed = 5, int hp = 50, int maxHp = 50, int targetHp = 50, int targetMaxHp = 50, int pa = 5, int evade = 0,
        bool moved = false, bool acted = false, bool second = false, bool reverse = false, bool badGround = false, ulong ticks = 20)
    {
        var actor = second ? B : A; var enemy = second ? A : B;
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), 12).ToArray(); if (badGround) tiles[^1] = new(Ground, 11);
        var movement = new UnitMovementProfile(4, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, origin ?? Actor, movement, moved), new(enemy, target ?? new(1, 1, 0), movement) };
        var turns = new TurnUnitState[] { new(actor, speed, ct, 2, true, moved, acted), new(enemy, speed, targetCt, 17) };
        AttackUnitState Combat(BattleUnitId id, int current, int maximum, BattleFacing facing, int evasion) =>
            new(id, current, maximum, 0, 0, pa, evasion, 0, 0, 0, 70, 0, facing,
                current == 0 ? AttackUnitStatus.Dead : current <= maximum / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, hp, maxHp, BattleFacing.West, 0), Combat(enemy, targetHp, targetMaxHp, BattleFacing.East, evade) };
        return new(new(new(new(4, 3, 1, tiles), reverse ? bodies.Reverse() : bodies, LiveRng),
            new(reverse ? turns.Reverse() : turns, LiveRng, ticks, TurnCyclePhase.UnitTurn)), reverse ? combat.Reverse() : combat);
    }
}
