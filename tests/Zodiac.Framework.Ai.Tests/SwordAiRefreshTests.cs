using System.Collections.Immutable;
using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiRefreshTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly FftRandomState Decision = new(1, 7), LiveRng = new(57, 100);
    private static readonly SwordAiCandidateProfile Candidate = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    private static readonly SwordAiScoreProfile Score = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);
    private static readonly SwordAiRefreshProfile Profile = new(true, true, true);
    private static readonly ImmutableArray<SwordAiRefreshPolicy> Policies = [new(A, true, false), new(B, false, true)];

    public static IEnumerable<object[]> ClockVectors()
    {
        using var document = Fixture();
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray())
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(ClockVectors))]
    public void Refresh_executes_source_clock_vectors_and_rolls_back_exhausted_counters(string id)
    {
        using var document = Fixture();
        var row = document.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var input = row.GetProperty("input"); var expected = row.GetProperty("expected");
        var actor = input.GetProperty("units").EnumerateArray().Single(u => u.GetProperty("id").GetString() == "a");
        var peer = input.GetProperty("units").EnumerateArray().Single(u => u.GetProperty("id").GetString() == "b");
        var state = State(ct: actor.GetProperty("ct").GetInt32(), peerCt: peer.GetProperty("ct").GetInt32(),
            speed: actor.GetProperty("speed").GetInt32(), peerSpeed: peer.GetProperty("speed").GetInt32(),
            ticks: input.GetProperty("ticks").GetUInt64(), reverse: true);
        var rng = new FftRandomState(input.GetProperty("word").GetUInt32(), input.GetProperty("count").GetUInt64());
        var before = Snapshot(state); var result = Evaluate(state, rng: rng);
        Assert.Equal(before, Snapshot(state));
        Assert.Equal(new FftRandomState(expected.GetProperty("word").GetUInt32(), expected.GetProperty("count").GetUInt64()), result.RngState);
        if (!expected.GetProperty("accepted").GetBoolean())
        {
            Atomic(result, id.StartsWith("draw-", StringComparison.Ordinal)
                ? SwordAiRefreshRejection.RandomDrawCountExhausted : SwordAiRefreshRejection.TickCountExhausted, rng);
            return;
        }
        var outcome = Success(result);
        Assert.Equal(expected.GetProperty("endCt").GetInt32(), outcome.TurnEndCt);
        Assert.Equal(expected.GetProperty("elapsed").GetUInt64(), outcome.ElapsedTicks);
        Assert.Equal(expected.GetProperty("ticks").GetUInt64(), outcome.Ticks);
        Assert.Equal(expected.GetProperty("picked").GetString() == "a" ? A : B, outcome.MoveOpportunityUnitId);
        var pick = Assert.Single(outcome.UpkeepPicks);
        Assert.Equal(expected.GetProperty("raw").GetUInt32(), pick.RawRoll);
        Assert.Equal(outcome.MoveOpportunityUnitId, pick.UnitId); Assert.False(pick.Dead);
        foreach (var unit in outcome.Units)
        {
            Assert.Equal(expected.GetProperty("ct").GetProperty(unit.UnitId == A ? "a" : "b").GetInt32(), unit.Ct);
            Assert.Equal(unit.UnitId != pick.UnitId, unit.MovementTaken);
            Assert.Equal(unit.UnitId == B && unit.UnitId != pick.UnitId, unit.ActionTaken);
        }
    }

    [Theory]
    [InlineData(24, 49, false, true)] [InlineData(23, 49, true, true)]
    [InlineData(36, 49, false, true)] [InlineData(37, 49, false, false)]
    [InlineData(36, 48, false, false)]
    public void HP_flags_preserve_source_floor_and_strict_three_quarter_comparison(int hp, int maxHp, bool half, bool threeQuarters)
    {
        var outcome = Success(Evaluate(State(hp: hp, maxHp: maxHp)));
        var actor = outcome.RefreshedUnits.Single(u => u.UnitId == A);
        Assert.Equal(half, actor.HpBelowHalf); Assert.Equal(threeQuarters, actor.HpBelowThreeQuarters);
    }

    [Fact]
    public void Refresh_retains_explicit_policies_and_derives_roles_and_transient_facts_for_both_units()
    {
        var outcome = Success(Evaluate(State(hp: 10, maxHp: 50)));
        Assert.Collection(outcome.RefreshedUnits,
            actor => Assert.Equal(new(A, false, true, false, false, false, true, true, false, false, false, false, true, false), actor),
            peer => Assert.Equal(new SwordAiRefreshedUnit(B, true, true, false, false, false, false, false, false, false, false, false, false, true), peer));
        var peerUnit = outcome.Units.Single(u => u.UnitId == B);
        Assert.Equal((19, BattleFacing.East, 50, AttackUnitStatus.None),
            ((int)peerUnit.DeathCounter, peerUnit.Facing, peerUnit.Hp, peerUnit.Status));
        Assert.Equal(AttackUnitStatus.Critical, outcome.Units.Single(u => u.UnitId == A).Status);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_replay_and_collection_order_preserve_live_battle_and_carry_only_hypothetical_RNG()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = State(second: second); var before = Snapshot(state);
            var result = Evaluate(state, second: second);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, second: second)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(State(second: second, reverse: true), second: second,
                policies: Policies.Reverse().ToImmutableArray())));
            var outcome = Success(result);
            Assert.Equal(second ? A : B, outcome.RefreshedUnits.Single(u => u.Enemy).UnitId);
            Assert.Equal(new FftRandomState(1103527590, 8), result.RngState);
            Assert.Equal(new FftBiosRandom().Next(result.RngState).State, Evaluate(state, second: second, rng: result.RngState).RngState);
            Assert.Equal(before, Snapshot(state));
            Assert.Equal(LiveRng, state.TurnBattle.Battle.RngState); Assert.Equal(LiveRng, state.TurnBattle.Turns.RngState);
            foreach (var unit in outcome.Units)
            {
                var combat = state.Combatants.Single(u => u.UnitId == unit.UnitId);
                state.TurnBattle.Battle.TryGetUnit(unit.UnitId, out var body);
                Assert.Equal((combat.Hp, combat.MaxHp, combat.Status, combat.Facing, body!.Position),
                    (unit.Hp, unit.MaxHp, unit.Status, unit.Facing, unit.Position));
            }
        }
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [Trait("Coverage", "CurrentAdmission")]
    public void Unrepresented_refresh_facts_require_explicit_certification(int missing)
    {
        var profile = missing switch
        {
            0 => Profile with { FreshRefreshEntry = false },
            1 => Profile with { BothUnitsTargetable = false },
            _ => Profile with { ZeroCachedMpAbilityModifiers = false }
        };
        Atomic(Evaluate(State(), profile: profile), SwordAiRefreshRejection.UnsupportedRefreshProfile);
    }

    [Fact]
    public void Missing_duplicate_unknown_and_null_policy_rows_reject_without_partial_flags()
    {
        foreach (var policies in new ImmutableArray<SwordAiRefreshPolicy>[]
            { default, [], [Policies[0]], [Policies[0], Policies[0]],
                [Policies[0], new(new("sample:unit/unknown"), false, false)], [Policies[0], null!] })
            Atomic(Evaluate(State(), policies: policies), SwordAiRefreshRejection.InvalidPolicies);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Wrong_phase_unavailable_choices_terminal_units_and_unsupported_context_reject_before_simulation()
    {
        Atomic(Evaluate(State(moved: false)), SwordAiRefreshRejection.UnavailableAction);
        Atomic(Evaluate(State(moved: false, acted: true)), SwordAiRefreshRejection.UnavailableAction);
        Atomic(Evaluate(State(active: false, acted: true)), SwordAiRefreshRejection.UnavailableAction);
        Atomic(Evaluate(State(active: false)), SwordAiRefreshRejection.UnavailableAction);
        Atomic(Evaluate(State(second: true)), SwordAiRefreshRejection.UnavailableAction);
        Atomic(Evaluate(State(peerHp: 0)), SwordAiRefreshRejection.TerminalInput);
        Atomic(Evaluate(State(badGround: true)), SwordAiRefreshRejection.UnsupportedProfile);
        var unknown = new BattleUnitId("sample:unit/unknown");
        Atomic(SwordAiRefresh.Evaluate(State(), new(unknown, B, Policies), Candidate, Score, Profile, Decision), SwordAiRefreshRejection.UnknownActor);
        Atomic(SwordAiRefresh.Evaluate(State(), new(A, unknown, Policies), Candidate, Score, Profile, Decision), SwordAiRefreshRejection.UnknownTarget);
        Atomic(SwordAiRefresh.Evaluate(State(), new(A, A, Policies), Candidate, Score, Profile, Decision), SwordAiRefreshRejection.UnsupportedProfile);
        var score = new SwordAiScoreProfile([new(A, SwordAiScoreFacts.None), new(B, SwordAiScoreFacts.ClosedProfile)]);
        Atomic(SwordAiRefresh.Evaluate(State(), new(A, B, Policies), Candidate, score, Profile, Decision), SwordAiRefreshRejection.UnsupportedScoreProfile);
    }

    private static SwordAiRefreshResult Evaluate(AttackBattleSnapshot state, bool second = false,
        FftRandomState? rng = null, SwordAiRefreshProfile? profile = null, ImmutableArray<SwordAiRefreshPolicy>? policies = null) =>
        SwordAiRefresh.Evaluate(state, new(second ? B : A, second ? A : B, policies ?? Policies), Candidate, Score, profile ?? Profile, rng ?? Decision);
    private static SwordAiRefreshOutcome Success(SwordAiRefreshResult result)
    {
        Assert.True(result.Accepted); Assert.Equal(SwordAiRefreshRejection.None, result.Rejection);
        return Assert.IsType<SwordAiRefreshOutcome>(result.Outcome);
    }
    private static void Atomic(SwordAiRefreshResult result, SwordAiRefreshRejection reason, FftRandomState? rng = null)
    {
        Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection);
        Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
    }
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-refresh-contract-v1.json")));
    private static string Serialize(object value) => JsonSerializer.Serialize(value);
    private static string Snapshot(AttackBattleSnapshot state) => Serialize(new { state.Combatants, state.TurnBattle.Turns,
        state.TurnBattle.Battle.Map, state.TurnBattle.Battle.RngState,
        Bodies = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved,
            u.MovementProfile.Move, u.MovementProfile.Jump, u.MovementProfile.BodyHeight,
            u.MovementProfile.Mode, u.MovementProfile.Traits,
            Costs = u.MovementProfile.TerrainCosts.Select(p => new { p.Key, p.Value }) }) });

    private static AttackBattleSnapshot State(int ct = 15, int peerCt = 95, int speed = 5, int peerSpeed = 5,
        ulong ticks = 20, int hp = 50, int maxHp = 50, int peerHp = 50, bool second = false, bool reverse = false,
        bool moved = true, bool acted = false, bool active = true, bool badGround = false)
    {
        var actor = second ? B : A; var peer = second ? A : B;
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), 25).ToArray();
        if (badGround) tiles[^1] = new(Ground, 11);
        var movement = new UnitMovementProfile(3, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, new(1, 1, 0), movement, moved), new(peer, new(3, 1, 0), movement, true) };
        var turns = new TurnUnitState[] { new(actor, speed, ct, 2, active, moved, acted), new(peer, peerSpeed, peerCt, 17, false, true, true) };
        AttackUnitState Combat(BattleUnitId unit, int current, int maximum, BattleFacing facing) =>
            new(unit, current, maximum, 0, 0, 5, 0, 0, 0, 0, 70, 0, facing,
                current == 0 ? AttackUnitStatus.Dead : current <= maximum / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, hp, maxHp, BattleFacing.West), Combat(peer, peerHp, 50, BattleFacing.East) };
        return new(new(new(new(5, 5, 1, tiles), reverse ? bodies.Reverse() : bodies, LiveRng),
            new(reverse ? turns.Reverse() : turns, LiveRng, ticks, active ? TurnCyclePhase.UnitTurn : TurnCyclePhase.PickUnit)),
            reverse ? combat.Reverse() : combat);
    }
}
