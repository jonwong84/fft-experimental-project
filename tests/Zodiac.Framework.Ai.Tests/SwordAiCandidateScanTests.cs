using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiCandidateScanTests
{
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly FftRandomState LiveRng = new(57, 100);
    private static BattleUnitId Id(string value) => new("sample:unit/" + value);
    private static TilePosition Point(JsonElement value) => new(value[0].GetInt32(), value[1].GetInt32(), 0);
    private static JsonElement Case(string id)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-scan-contract-v1.json")));
        return doc.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id).Clone();
    }

    public static IEnumerable<object[]> Proposals()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-scan-contract-v1.json")));
        foreach (var row in doc.RootElement.GetProperty("vectors").EnumerateArray().Where(r => r.GetProperty("family").GetString() == "proposed-scan"))
            yield return [row.GetProperty("id").GetString()!];
    }

    [Theory] [MemberData(nameof(Proposals))]
    public void Source_design_scenarios_execute_through_real_movement_formula_clock_and_scoring(string id)
    {
        var row = Case(id); var expected = row.GetProperty("expected"); var frame = Materialize(row.GetProperty("inputs"));
        string before = Snapshot(frame.State); var result = Scan(frame);
        Assert.Equal(expected.GetProperty("disposition").GetString(), result.Disposition.ToString());
        Assert.Equal(new FftRandomState(expected.GetProperty("rngWord").GetUInt32(), expected.GetProperty("rngCount").GetUInt64()), result.RngState);
        Assert.Equal(before, Snapshot(frame.State));
        if (result.Disposition == SwordAiScanDisposition.Rejected)
        {
            Assert.Equal(expected.GetProperty("reason").GetString(), result.Rejection.ToString()); Assert.Null(result.Outcome);
            if (result.Rejection == SwordAiScanRejection.EvaluationRejected)
            {
                Assert.Equal(expected.GetProperty("failedIndex").GetInt32(), result.FailedCandidateIndex);
                var nested = expected.GetProperty("evaluationRejection");
                Assert.Equal(nested.GetProperty("reason").GetString(), result.ScoringRejection.ToString());
                Assert.Equal(nested.GetProperty("projectionReason").GetString(), result.ProjectionRejection.ToString());
            }
            return;
        }
        var actual = Success(result); var outcome = expected.GetProperty("outcome");
        var retained = outcome.GetProperty("retainedIndex");
        Assert.Equal(retained.ValueKind == JsonValueKind.Null ? null : (int?)retained.GetInt32(), actual.RetainedCandidateIndex);
        Assert.Equal(outcome.GetProperty("actorDelay").GetInt32(), actual.Cache.ActorDelay);
        Assert.Equal(outcome.GetProperty("targetDelay").GetInt32(), actual.Cache.TargetDelay);
        var enemy = outcome.GetProperty("eligibleEnemyId");
        Assert.Equal(enemy.ValueKind == JsonValueKind.Null ? null : (BattleUnitId?)Id(enemy.GetString()!), actual.Cache.EligibleEnemyId);
        var evaluations = outcome.GetProperty("evaluations");
        Assert.Equal(evaluations.GetArrayLength(), actual.Candidates.Length);
        for (int i = 0; i < actual.Candidates.Length; i++)
        {
            var e = evaluations[i]; var a = actual.Candidates[i]; var candidate = e.GetProperty("candidate");
            Assert.Equal(Point(candidate.GetProperty("origin")), a.Origin);
            Assert.Equal(Point(candidate.GetProperty("targetPanel")), a.TargetPanel);
            Assert.Equal(Point(candidate.GetProperty("destination")), a.Candidate.Destination);
            Assert.Equal(candidate.GetProperty("order").GetString(), a.Candidate.Order.ToString());
            Assert.Equal(frame.Comparison.ActorId, a.Candidate.ActorId); Assert.Equal(frame.Comparison.TargetId, a.Candidate.TargetId);
            Assert.Equal(e.GetProperty("disposition").GetString(), a.Evaluation.Disposition.ToString());
            Assert.Equal(new FftRandomState(e.GetProperty("rngWord").GetUInt32(), e.GetProperty("rngCount").GetUInt64()), a.Evaluation.RngState);
            Assert.Equal(e.GetProperty("proximity").GetInt32(), a.Proximity);
            Assert.Equal(new(a.Candidate.Destination, a.Proximity), actual.Cache.Destinations[i]);
            if (e.GetProperty("score").ValueKind == JsonValueKind.Null) Assert.Null(a.Evaluation.Score);
            else
            {
                var score = Assert.IsType<SwordAiCandidateScore>(a.Evaluation.Score); var s = e.GetProperty("score");
                Assert.Equal(s.GetProperty("priority").GetInt32(), score.Priority);
                Assert.Equal(s.GetProperty("hitPercent").GetInt32(), score.HitPercent);
                foreach (var contribution in s.GetProperty("contributions").EnumerateArray())
                {
                    var unit = score.Contributions.Single(u => u.TieKey == contribution.GetProperty("slot").GetInt32());
                    Assert.Equal(contribution.GetProperty("signedContribution").GetInt32(), unit.SignedContribution);
                    Assert.Equal(contribution.GetProperty("storedPriority").GetInt32(), unit.RunningPriority);
                }
            }
        }
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [Trait("Coverage", "CurrentAdmission")]
    public void Each_source_context_certificate_is_required_even_when_the_target_is_inactive(int missing)
    {
        var f = FrameFor(); var profile = f.Profile with { TargetActive = false };
        profile = missing switch
        {
            0 => profile with { OrdinaryAttackTilePolicy = false }, 1 => profile with { OriginalScenarioMask = false },
            2 => profile with { TargetSelectionMatchesBaseline = false }, _ => profile with { FreshAbilityBestReset = false }
        };
        Rejected(f with { Profile = profile }, SwordAiScanRejection.UnsupportedScanProfile);
    }

    [Theory] [InlineData("empty-destination")] [InlineData("missing-destination")] [InlineData("upper-layer")]
    [InlineData("outside")] [InlineData("unreachable")] [InlineData("already-moved")] [InlineData("inactive-duplicate")]
    public void Invalid_supplied_scenarios_reject_before_any_formula_or_rng_work(string kind)
    {
        var data = Data();
        if (kind == "already-moved") data["state"]!["units"]![0]!["moved"] = true;
        var f = Materialize(JsonSerializer.SerializeToElement(data));
        var scenario = kind switch
        {
            "empty-destination" => new SwordAiScanScenario([], new(2, 1, 0)),
            "missing-destination" => new SwordAiScanScenario([new(2, 1, 0)], null),
            "upper-layer" => new SwordAiScanScenario([new(1, 0, 1)], new(1, 0, 1)),
            "outside" => new SwordAiScanScenario([new(-1, 0, 0)], new(-1, 0, 0)),
            "unreachable" => new SwordAiScanScenario([new(0, 0, 0)], new(0, 0, 0)),
            _ => f.Scenario
        };
        if (kind == "unreachable")
        {
            data["state"]!["move"] = 0;
            f = Materialize(JsonSerializer.SerializeToElement(data));
        }
        if (kind == "inactive-duplicate")
        {
            scenario = new(f.Scenario.Origins.Add(f.Scenario.Origins[0]), f.Scenario.Destination);
            f = f with { Profile = f.Profile with { TargetActive = false } };
        }
        Rejected(f with { Scenario = scenario }, SwordAiScanRejection.InvalidScenario);
    }

    [Theory] [InlineData("actor")] [InlineData("target")] [InlineData("inactive-actor")]
    public void Inactive_empty_scan_requires_the_bound_pair_and_current_actor(string kind)
    {
        var f = FrameFor(); var pair = f.Comparison;
        pair = kind switch
        {
            "actor" => pair with { ActorId = Id("unknown") }, "target" => pair with { TargetId = Id("unknown") },
            _ => pair with { ActorId = pair.TargetId, TargetId = pair.ActorId }
        };
        var result = Rejected(f with { Comparison = pair, Scenario = new([], null), Profile = f.Profile with { TargetActive = false } },
            SwordAiScanRejection.UnsupportedState);
        Assert.Equal(kind == "actor" ? SwordAiCandidateRejection.UnknownActor : kind == "target"
            ? SwordAiCandidateRejection.UnknownTarget : SwordAiCandidateRejection.UnavailableAction, result.ProjectionRejection);
    }

    [Theory] [InlineData("weapon")] [InlineData("envelope")] [InlineData("acted")]
    [InlineData("inactive-actor")] [InlineData("dead-target")] [InlineData("geometry")] [InlineData("map-size")]
    [Trait("Coverage", "CurrentAdmission")]
    public void Empty_scan_still_validates_original_action_weapon_clock_and_geometry(string kind)
    {
        var data = Data();
        if (kind == "envelope") { data["state"]!["units"]![0]!["pa"] = 255; data["weaponPower"] = 65; }
        if (kind == "acted") data["state"]!["units"]![0]!["acted"] = true;
        if (kind == "inactive-actor") data["state"]!["activeActor"] = false;
        if (kind == "dead-target") data["state"]!["units"]![1]!["hp"] = 0;
        var f = Materialize(JsonSerializer.SerializeToElement(data)) with { Scenario = new([], null) };
        if (kind == "weapon") f = f with { Weapon = null };
        if (kind is "geometry" or "map-size")
        {
            var map = new BattleMapDefinition(kind == "map-size" ? 17 : 4, 3, 1,
                Enumerable.Repeat(new BattleTile(Ground, kind == "geometry" ? 11 : 10), (kind == "map-size" ? 17 : 4) * 3).ToArray());
            f = f with { State = new(new(new(map, f.State.TurnBattle.Battle.Units, LiveRng), f.State.TurnBattle.Turns), f.State.Combatants) };
        }
        var result = Rejected(f, SwordAiScanRejection.UnsupportedState);
        Assert.Equal(kind == "dead-target" ? SwordAiCandidateRejection.TerminalInput : kind is "acted" or "inactive-actor"
            ? SwordAiCandidateRejection.UnavailableAction : SwordAiCandidateRejection.UnsupportedProfile, result.ProjectionRejection);
    }

    [Theory] [InlineData(5, 1)] [InlineData(10, 2)] [InlineData(15, 3)]
    public void Failure_after_completed_candidates_rolls_back_the_whole_query(int budget, int failedIndex)
    {
        var f = FrameFor() with { Decision = new(1, ulong.MaxValue - (ulong)budget) };
        var result = Rejected(f, SwordAiScanRejection.EvaluationRejected);
        Assert.Equal(failedIndex, result.FailedCandidateIndex);
        Assert.Equal(SwordAiScoringRejection.ProjectionRejected, result.ScoringRejection);
        Assert.Equal(SwordAiCandidateRejection.RandomDrawCountExhausted, result.ProjectionRejection);
    }

    [Fact]
    public void Scenario_snapshots_caller_data_and_canonicalizes_emission_without_deduplicating_destinations()
    {
        var f = FrameFor(); var cells = f.Scenario.Origins.Reverse().ToList();
        var scenario = new SwordAiScanScenario(cells, f.Scenario.Destination); cells.Clear();
        var result = Success(Scan(f with { Scenario = scenario }));
        Assert.Equal(new TilePosition[] { new(1, 0, 0), new(0, 1, 0), new(2, 1, 0), new(1, 2, 0) }, result.Candidates.Select(c => c.Origin));
        Assert.Equal(SwordAiCandidateOrder.ActThenMove, result.Candidates[2].Candidate.Order);
        Assert.Equal(result.Candidates[0].Candidate.Destination, result.Candidates[2].Candidate.Destination);
        Assert.Equal(4, result.Candidates.Length); Assert.Equal(0, result.RetainedCandidateIndex);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Replays_and_branches_keep_original_state_and_carry_every_completed_evaluation()
    {
        var f = FrameFor(); string before = Snapshot(f.State);
        var first = Scan(f); var replay = Scan(f);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(replay));
        Assert.Equal(new ulong[] { 12, 17, 22, 27 }, first.Outcome!.Candidates.Select(c => c.Evaluation.RngState.DrawCount));
        Assert.All(first.Outcome.Candidates, c => Assert.Equal(25, c.Evaluation.Projection!.Outcome!.Units.Single(u => u.UnitId == f.Comparison.TargetId).Hp));
        var branch = Scan(f with { Decision = first.RngState });
        Assert.Equal(47ul, branch.RngState.DrawCount); Assert.NotEqual(first.RngState, branch.RngState);
        Assert.Equal(before, Snapshot(f.State)); Assert.Equal(LiveRng, f.State.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Original_slot_order_controls_contributions_independently_of_actor_identity_and_scan_order()
    {
        var data = Data(); data["state"]!["units"]![0]!["slot"] = 1; data["state"]!["units"]![1]!["slot"] = 0;
        var result = Scan(Materialize(JsonSerializer.SerializeToElement(data))); var outcome = Success(result);
        Assert.Equal(0, outcome.RetainedCandidateIndex);
        Assert.All(outcome.Candidates, c =>
        {
            Assert.Equal(64, c.Evaluation.Score!.Priority);
            Assert.Equal(new[] { Id("B"), Id("A") }, c.Evaluation.Score.Contributions.Select(u => u.UnitId));
        });
    }

    [Fact]
    public void Original_map_edge_uses_y_x_order_and_retains_the_own_origin_at_its_source_position()
    {
        var data = Data(); data["state"]!["width"] = 16; data["state"]!["depth"] = 16;
        data["state"]!["units"]![0]!["position"] = new JsonArray(15, 14);
        data["state"]!["units"]![1]!["position"] = new JsonArray(15, 15);
        var f = Materialize(JsonSerializer.SerializeToElement(data)) with { Scenario = new([new(14, 15, 0), new(15, 14, 0)], new(14, 15, 0)) };
        var result = Scan(f); var outcome = Success(result);
        Assert.Equal(new TilePosition[] { new(15, 14, 0), new(14, 15, 0) }, outcome.Candidates.Select(c => c.Origin));
        Assert.Equal(SwordAiCandidateOrder.ActThenMove, outcome.Candidates[0].Candidate.Order);
        Assert.Equal(17ul, result.RngState.DrawCount);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Supported_empty_or_inactive_scan_needs_no_clock_or_rng_headroom(bool inactive)
    {
        var data = Data(); data["state"]!["ticks"] = ulong.MaxValue;
        var f = Materialize(JsonSerializer.SerializeToElement(data)) with { Decision = new(1, ulong.MaxValue) };
        f = inactive ? f with { Profile = f.Profile with { TargetActive = false } } : f with { Scenario = new([], null) };
        var result = Scan(f); Assert.Equal(SwordAiScanDisposition.NoCandidates, result.Disposition);
        Assert.Empty(Success(result).Candidates); Assert.Equal(f.Decision, result.RngState);
    }

    private sealed record Frame(AttackBattleSnapshot State, SwordAiScanScenario Scenario, BoundedSwordWeapon? Weapon,
        SwordAiCandidateProfile Candidate, SwordAiScoreProfile Score, SwordAiComparisonProfile Comparison,
        SwordAiScanProfile Profile, FftRandomState Decision);
    private static JsonNode Data() => JsonNode.Parse(Case("scan-four-origins").GetProperty("inputs").GetRawText())!;
    private static Frame FrameFor() => Materialize(Case("scan-four-origins").GetProperty("inputs"));
    private static Frame Materialize(JsonElement data)
    {
        var state = data.GetProperty("state"); var profile = data.GetProperty("profile");
        bool Flag(string name) => profile.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        var actor = Id(state.GetProperty("actorId").GetString()!); var target = Id(state.GetProperty("targetId").GetString()!);
        var units = state.GetProperty("units").EnumerateArray().ToArray();
        BattleUnitId UnitId(JsonElement u) => Id(u.GetProperty("id").GetString()!);
        var movement = new UnitMovementProfile(state.GetProperty("move").GetInt32(), 0, 6, UnitMovementMode.Normal,
            UnitMovementTraits.None, [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = units.Select(u => new BattleUnitState(UnitId(u), Point(u.GetProperty("position")), movement, u.GetProperty("moved").GetBoolean())).ToArray();
        bool active = state.GetProperty("activeActor").GetBoolean();
        var turns = units.Select(u => new TurnUnitState(UnitId(u), u.GetProperty("speed").GetInt32(), u.GetProperty("ct").GetInt32(),
            u.GetProperty("slot").GetInt32(), active && UnitId(u) == actor, u.GetProperty("moved").GetBoolean(), u.GetProperty("acted").GetBoolean())).ToArray();
        var combatants = units.Select(u =>
        {
            int hp = u.GetProperty("hp").GetInt32(), max = u.GetProperty("maxHp").GetInt32();
            return new AttackUnitState(UnitId(u), hp, max, 0, 0, u.GetProperty("pa").GetInt32(), u.GetProperty("evade").GetInt32(),
                0, 0, 0, 70, 0, Enum.Parse<BattleFacing>(u.GetProperty("facing").GetString()!), hp == 0 ? AttackUnitStatus.Dead : hp <= max / 5
                    ? AttackUnitStatus.Critical : AttackUnitStatus.None, 255, Weapon);
        }).ToArray();
        int width = state.GetProperty("width").GetInt32(), depth = state.GetProperty("depth").GetInt32();
        var snapshot = new AttackBattleSnapshot(new(new(new(width, depth, 1, Enumerable.Repeat(new BattleTile(Ground, 10), width * depth).ToArray()),
            bodies, LiveRng), new(turns, LiveRng, state.GetProperty("ticks").GetUInt64(), active ? TurnCyclePhase.UnitTurn : TurnCyclePhase.PickUnit)), combatants);
        var candidateProfile = new SwordAiCandidateProfile(new(Ground), new(Ground, actor, target),
            units.Select(u => new SwordAiUnitBaseline(UnitId(u), u.GetProperty("deathCounter").GetByte())));
        var score = new SwordAiScoreProfile(units.Select(u => new SwordAiScoreUnitCertificate(UnitId(u), Flag("scoreProfileClosed") ? SwordAiScoreFacts.ClosedProfile : SwordAiScoreFacts.None)));
        var scenario = data.GetProperty("scenario"); var dest = scenario.GetProperty("destination");
        return new(snapshot, new(scenario.GetProperty("origins").EnumerateArray().Select(Point), dest.ValueKind == JsonValueKind.Null ? null : Point(dest)),
            new(Weapon, data.GetProperty("weaponPower").GetInt32()), candidateProfile, score,
            new(actor, target, Flag("freshDecisionBaseline")),
            new(Flag("targetActive"), Flag("ordinaryAttackTilePolicy"), Flag("originalScenarioMask"), Flag("targetSelectionMatchesBaseline"), Flag("freshAbilityBestReset")),
            new(data.GetProperty("rngWord").GetUInt32(), data.GetProperty("rngCount").GetUInt64()));
    }
    private static SwordAiScanResult Scan(Frame f) => SwordAiCandidateScan.Evaluate(f.State, f.Scenario, f.Weapon, f.Candidate, f.Score, f.Comparison, f.Profile, f.Decision);
    private static SwordAiScanOutcome Success(SwordAiScanResult result)
    {
        Assert.NotEqual(SwordAiScanDisposition.Rejected, result.Disposition); Assert.Equal(SwordAiScanRejection.None, result.Rejection);
        Assert.Null(result.FailedCandidateIndex); Assert.Equal(SwordAiScoringRejection.None, result.ScoringRejection);
        Assert.Equal(SwordAiCandidateRejection.None, result.ProjectionRejection);
        return Assert.IsType<SwordAiScanOutcome>(result.Outcome);
    }
    private static SwordAiScanResult Rejected(Frame f, SwordAiScanRejection reason)
    {
        string before = Snapshot(f.State); var result = Scan(f);
        Assert.Equal(SwordAiScanDisposition.Rejected, result.Disposition); Assert.Equal(reason, result.Rejection);
        Assert.Null(result.Outcome); Assert.Equal(f.Decision, result.RngState); Assert.Equal(before, Snapshot(f.State));
        return result;
    }
    private static string Snapshot(AttackBattleSnapshot s) => JsonSerializer.Serialize(new
    {
        s.Combatants, s.TurnBattle.Turns, s.TurnBattle.Battle.Map, s.TurnBattle.Battle.RngState,
        Units = s.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved, u.MovementProfile.Move, u.MovementProfile.Jump,
            Costs = u.MovementProfile.TerrainCosts.Select(c => new { c.Key.Value, Cost = c.Value }) })
    });
}
