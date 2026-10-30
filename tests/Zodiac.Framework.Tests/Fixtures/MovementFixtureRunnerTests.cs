using System.Reflection;
using System.Text.Json.Nodes;
using Xunit;

namespace Zodiac.Framework.Tests.Fixtures;

public sealed class MovementFixtureRunnerTests
{
    private static string Revision => typeof(MovementFixtureRunnerTests).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[1].Split('.')[0];
    private static string Load(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));
    private static MovementFixtureRunner.Report Run(JsonNode node) => MovementFixtureRunner.Run(node.ToJsonString(), Revision);
    private static JsonNode Accepted() => JsonNode.Parse(Load("flat-move.synthetic.json"))!;

    [Theory]
    [InlineData("flat-move.synthetic.json")]
    [InlineData("flat-rejections.synthetic.json")]
    [InlineData("terrain-cost-accepted.synthetic.json")]
    [InlineData("terrain-cost-rejected.synthetic.json")]
    [InlineData("adjacent-step-accepted.synthetic.json")]
    [InlineData("adjacent-step-rejected.synthetic.json")]
    public void Committed_scenarios_replay_with_equal_reports_and_retained_provenance(string file)
    {
        string json = Load(file);
        var first = MovementFixtureRunner.Run(json, Revision);
        Assert.Equal("passed-assertion", first.Status);
        Assert.Equal(first, MovementFixtureRunner.Run(json, Revision));
        Assert.Equal("611b504d25d4a9b03fab8023d7dbea75fe381d5d", first.ReferenceRevision);
        Assert.Equal(Revision, first.ImplementationRevision);
        Assert.Contains("synthetic-expectation/specified", first.Diagnostic);
        Assert.Contains("src/Zodiac.Framework/", first.ProvenanceJson);
        Assert.Contains("exclusions", first.ProvenanceJson);
    }

    [Fact]
    public void Source_description_is_validated_but_never_reported_as_executed()
    {
        var report = MovementFixtureRunner.Run(Load("map-boundary.source-description.json"), Revision);
        Assert.Equal("not-executed", report.Status);
        Assert.Contains("not executed", report.Diagnostic);
    }

    [Theory]
    [InlineData(false, "state.units[0].position.x")]
    [InlineData(true, "events[0].to.x")]
    public void Deliberate_state_and_event_mismatches_identify_first_field(bool changeEvent, string path)
    {
        var fixture = Accepted();
        var assertion = fixture["expectations"]![0]!["assertions"]![1]!;
        if (changeEvent) assertion["events"]![0]!["to"]!["x"] = 0;
        else assertion["state"]!["units"]![0]!["position"]!["x"] = 0;
        var report = Run(fixture);
        Assert.Equal("failed-assertion", report.Status);
        Assert.Contains(path, report.Diagnostic);
        Assert.Contains("command 0", report.Diagnostic);
        Assert.Contains("expected 0, actual 1", report.Diagnostic);
    }

    [Fact]
    public void Extra_event_is_a_failure_not_an_unordered_set_match()
    {
        var fixture = Accepted();
        var events = fixture["expectations"]![0]!["assertions"]![1]!["events"]!.AsArray();
        events.Add(events[0]!.DeepClone());
        var report = Run(fixture);
        Assert.Equal("failed-assertion", report.Status);
        Assert.Contains("events.count", report.Diagnostic);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("unknown-field")]
    [InlineData("missing-field")]
    [InlineData("fraction")]
    [InlineData("enum")]
    [InlineData("id")]
    [InlineData("evidence")]
    [InlineData("unresolved")]
    [InlineData("capture-path")]
    [InlineData("duplicate-flags")]
    public void Malformed_fixtures_are_rejected_before_execution(string mutation)
    {
        var f = Accepted();
        var unit = f["scenario"]!["initialState"]!["units"]![0]!;
        switch (mutation)
        {
            case "version": f["schemaVersion"] = 2; break;
            case "unknown-field": unit["extra"] = 1; break;
            case "missing-field": unit.AsObject().Remove("hasMoved"); break;
            case "fraction": unit["movementProfile"]!["move"] = 1.5; break;
            case "enum": unit["movementProfile"]!["mode"] = "1"; break;
            case "id": unit["id"] = "Not an ID"; break;
            case "evidence": f["expectations"]![0]!["assertions"]![0]!["evidenceIds"]![0] = "missing"; break;
            case "unresolved": f["evidence"]![0]!["confidence"] = "unresolved"; break;
            case "capture-path": f["captures"] = JsonNode.Parse("""
                [{"id":"local","kind":"screenshot","localPath":"scratch/../outside.png","sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","byteLength":1,"tool":"test 1","procedure":"test","editionVerification":"none"}]
                """); break;
            case "duplicate-flags": unit["movementProfile"]!["traits"] = new JsonArray("AnyGround", "AnyGround"); break;
        }
        Assert.Equal("invalid-fixture", Run(f).Status);
    }

    [Fact]
    public void Duplicate_JSON_properties_are_invalid()
    {
        string json = Load("flat-move.synthetic.json").Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1");
        var report = MovementFixtureRunner.Run(json, Revision);
        Assert.Equal("invalid-fixture", report.Status);
        Assert.Contains("Duplicate property", report.Diagnostic);
    }

    [Fact]
    public void Unsupported_adjacent_policy_domain_is_reported_as_invalid_fixture()
    {
        var f = JsonNode.Parse(Load("adjacent-step-accepted.synthetic.json"))!;
        f["scenario"]!["initialState"]!["units"]![0]!["movementProfile"]!["mode"] = "Fly";
        var report = Run(f);
        Assert.Equal("invalid-fixture", report.Status);
        Assert.Contains("Normal mode", report.Diagnostic);
    }

    [Fact]
    public void Flag_order_is_semantic_but_duplicate_flags_are_invalid()
    {
        var f = Accepted();
        f["scenario"]!["initialState"]!["units"]![0]!["movementProfile"]!["traits"] = new JsonArray("AnyWeather", "AnyGround");
        f["expectations"]![0]!["assertions"]![1]!["state"]!["units"]![0]!["movementProfile"]!["traits"] = new JsonArray("AnyGround", "AnyWeather");
        Assert.Equal("passed-assertion", Run(f).Status);
    }
}
