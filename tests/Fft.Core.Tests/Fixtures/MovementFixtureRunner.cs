using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Fft.Core;

namespace Fft.Core.Tests.Fixtures;

// Test infrastructure, deliberately outside simulation and the Zodiac engine.
internal static class MovementFixtureRunner
{
    internal sealed record Report(string Status, string FixtureId, string PolicyId,
        string ImplementationRevision, string ReferenceRevision, string ProvenanceJson, string Diagnostic);

    public static Report Run(string json, string implementationRevision)
    {
        string id = "<invalid>", policy = "<invalid>", reference = "<invalid>";
        string provenance = "{}";
        try
        {
            if (!Regex.IsMatch(implementationRevision, "^[0-9a-f]{40}$"))
                throw new InvalidDataException("implementationRevision must be a full commit SHA");
            using var document = JsonDocument.Parse(json);
            RejectDuplicateKeys(document.RootElement, "$");
            var root = JsonNode.Parse(json)!;
            Shape(root, "format schemaVersion fixtureId execution purpose target reference evidence preconditions exclusions unknowns scenario sourceCases expectations captures");
            EqualLiteral(root, "format", "fft.behavior-fixture");
            Require(Int(root["schemaVersion"]) == 1, "Unsupported schemaVersion");
            id = StableId(root["fixtureId"]);
            Text(root["purpose"]);
            var target = root["target"]!;
            Shape(target, "game edition region platform policyId");
            EqualLiteral(target, "game", "FFT"); EqualLiteral(target, "edition", "SCUS-94221");
            EqualLiteral(target, "region", "North America"); EqualLiteral(target, "platform", "PS1");
            policy = StableId(target["policyId"]);
            var movementPolicy = policy switch
            {
                "fft:movement/flat-grid-prototype" => BattleMovementPolicy.FlatGridPrototype,
                "fft:movement/terrain-cost-prototype" => BattleMovementPolicy.TerrainCostPrototype,
                "fft:movement/ordinary-adjacent-step-prototype" => BattleMovementPolicy.OrdinaryAdjacentStepPrototype,
                _ => throw new InvalidDataException("Unsupported policyId"),
            };
            Shape(root["reference"], "decompCommit observedOn usedForExpectations");
            reference = Sha(root["reference"]!["decompCommit"]);
            Require(DateOnly.TryParseExact(Text(root["reference"]!["observedOn"]), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "Invalid observedOn date");
            Bool(root["reference"]!["usedForExpectations"]);
            foreach (string field in new[] { "preconditions", "exclusions", "unknowns" }) Strings(root[field]);
            var evidence = Evidence(root["evidence"]!);
            ValidateCaptures(root["captures"]!);
            provenance = new JsonObject(new[] { "target", "reference", "evidence", "preconditions", "exclusions", "unknowns", "captures" }
                .Select(name => new KeyValuePair<string, JsonNode?>(name, root[name]!.DeepClone()))).ToJsonString();
            foreach (var entry in Array(root["evidence"]))
            foreach (var source in Array(entry!["sources"]))
                if (Text(source!["path"]).EndsWith(".c", StringComparison.Ordinal) || Text(source["path"]).EndsWith(".h", StringComparison.Ordinal))
                    Require(Sha(source["revision"]) == reference, "Decomp source revision differs from reference");
            foreach (var sourceCase in Array(root["sourceCases"]))
            {
                Shape(sourceCase, "id function inputs expectedReturn evidenceIds");
                Text(sourceCase!["id"]); Text(sourceCase["function"]); Int(sourceCase["expectedReturn"]);
                Require(sourceCase["inputs"] is JsonObject, "sourceCase inputs must be an object");
                foreach (var input in sourceCase["inputs"]!.AsObject()) Int(input.Value);
                EvidenceLabel(sourceCase["evidenceIds"], evidence);
            }
            string execution = Text(root["execution"]);
            Require(execution is "scenario" or "description-only", "Unsupported execution");
            if (root["scenario"] is null)
            {
                Require(execution == "description-only" && Array(root["expectations"]).Count == 0,
                    "Only an empty description may omit scenario");
                return Result("not-executed", "Description only; no managed scenario executed.");
            }
            var scenario = root["scenario"]!;
            Shape(scenario, "initialState commands");
            Shape(scenario["initialState"], "map units rngState");
            Require(scenario["initialState"]!["rngState"] is null, "RNG unsupported in v1");
            var mapNode = scenario["initialState"]!["map"]!;
            Shape(mapNode, "id width depth layerCount tiles");
            string mapId = StableId(mapNode["id"]);
            var map = new BattleMapDefinition(Int(mapNode["width"]), Int(mapNode["depth"]),
                Int(mapNode["layerCount"]), Array(mapNode["tiles"]).Select(Tile).ToArray());
            var state = new BattleState(map, Array(scenario["initialState"]!["units"]).Select(Unit));
            var commands = Array(scenario["commands"]);
            Require(commands.Count > 0, "A scenario must have at least one command");
            var expectations = Array(root["expectations"]);
            Require(commands.Count == expectations.Count, "Expected exactly one expectation per command");
            // Validate the entire record before any execution, including later expectations.
            for (int i = 0; i < commands.Count; i++)
            {
                Shape(commands[i], "index kind unitId destination");
                Require(Int(commands[i]!["index"]) == i, "Noncontiguous command index");
                EqualLiteral(commands[i]!, "kind", "move-unit"); StableId(commands[i]!["unitId"]);
                Position(commands[i]!["destination"]);
                Shape(expectations[i], "commandIndex assertions");
                Require(Int(expectations[i]!["commandIndex"]) == i, "Noncontiguous expectation index");
                var groups = Array(expectations[i]!["assertions"]);
                Require(groups.Count == 2, "Exactly two assertion groups required");
                var resultGroup = Group(groups, "result");
                Shape(resultGroup, "kind accepted rejection evidenceIds");
                bool accepted = Bool(resultGroup["accepted"]);
                var rejection = EnumValue<BattleTransitionRejection>(resultGroup["rejection"]);
                Require(accepted == (rejection == BattleTransitionRejection.None), "Inconsistent accepted/rejection");
                EvidenceLabel(resultGroup["evidenceIds"], evidence);
                var stateGroup = Group(groups, "state-and-events");
                Shape(stateGroup, "kind stateRelation state events evidenceIds");
                Require(Text(stateGroup["stateRelation"]) == (accepted ? "new-instance" : "same-instance"),
                    "Inconsistent stateRelation");
                EvidenceLabel(stateGroup["evidenceIds"], evidence);
                Shape(stateGroup["state"], "mapId units rngState");
                Require(StableId(stateGroup["state"]!["mapId"]) == mapId, "Unknown expected mapId");
                Require(stateGroup["state"]!["rngState"] is null, "RNG unsupported in v1");
                _ = new BattleState(map, Array(stateGroup["state"]!["units"]).Select(Unit));
                foreach (var ev in Array(stateGroup["events"]))
                {
                    Shape(ev, "kind unitId from to"); EqualLiteral(ev!, "kind", "unit-moved");
                    StableId(ev!["unitId"]); Position(ev["from"]); Position(ev["to"]);
                }
            }
            if (execution == "description-only")
                return Result("not-executed", "Description validated; source cases and managed projection were not executed.");
            for (int i = 0; i < commands.Count; i++)
            {
                var before = Snapshot(state, mapId);
                var beforeMap = MapSnapshot(state.Map);
                var command = commands[i]!;
                var actual = BattleSimulator.Apply(state,
                    new MoveUnitCommand(new BattleUnitId(StableId(command["unitId"])), Position(command["destination"])), movementPolicy);
                var groups = Array(expectations[i]!["assertions"]);
                var resultGroup = Group(groups, "result");
                var stateGroup = Group(groups, "state-and-events");
                string? mismatch = Difference(resultGroup["accepted"], JsonValue.Create(actual.Accepted), $"expectations[{i}].accepted")
                    ?? Difference(resultGroup["rejection"], JsonValue.Create(actual.Rejection.ToString()), $"expectations[{i}].rejection");
                string label = EvidenceLabel(resultGroup["evidenceIds"], evidence);
                if (mismatch is null)
                {
                    label = EvidenceLabel(stateGroup["evidenceIds"], evidence);
                    string relation = ReferenceEquals(state, actual.State) ? "same-instance" : "new-instance";
                    mismatch = Difference(stateGroup["stateRelation"], JsonValue.Create(relation), $"expectations[{i}].stateRelation")
                        ?? Difference(beforeMap, MapSnapshot(actual.State.Map), $"expectations[{i}].state.map")
                        ?? Difference(CanonicalState(stateGroup["state"]!), Snapshot(actual.State, mapId), $"expectations[{i}].state")
                        ?? Difference(stateGroup["events"], Events(actual.Events), $"expectations[{i}].events")
                        ?? Difference(before, Snapshot(state, mapId), $"commands[{i}].inputState")
                        ?? Difference(beforeMap, MapSnapshot(state.Map), $"commands[{i}].inputMap");
                }
                if (mismatch is not null)
                    return Result("failed-assertion", $"command {i}; {label}; {mismatch}");
                state = actual.State;
            }
            return Result("passed-assertion", $"Prototype regression passed for {commands.Count} commands; " +
                string.Join("; ", evidence.Values) + ". Source cases were not executed; no original-game equivalence claim.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException or FormatException or NotSupportedException)
        {
            return Result("invalid-fixture", ex.Message);
        }
        Report Result(string status, string message) => new(status, id, policy, implementationRevision, reference, provenance,
            $"{id}; policy {policy}; {message}");
    }

    private static Dictionary<string, string> Evidence(JsonNode node)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in Array(node))
        {
            Shape(entry, "id kind confidence claim sources limitations");
            string id = Text(entry!["id"]), kind = Text(entry["kind"]), confidence = Text(entry["confidence"]);
            Require(kind is "synthetic-expectation" or "source-derived" or "executable-reference" or "original-observation", "Unknown evidence kind");
            Require(confidence is "specified" or "source-traced" or "independently-observed" or "unresolved", "Unknown confidence");
            if (confidence != "unresolved")
                Require((kind, confidence) is ("synthetic-expectation", "specified") or ("source-derived", "source-traced")
                    or ("executable-reference", "independently-observed") or ("original-observation", "independently-observed"), "Evidence kind/confidence mismatch");
            Text(entry["claim"]); Strings(entry["limitations"]);
            Require(Array(entry["sources"]).Count > 0, "Evidence sources required");
            foreach (var source in Array(entry["sources"]))
            {
                Shape(source, "path symbol revision"); RelativePath(Text(source!["path"]));
                Text(source["symbol"]); Sha(source["revision"]);
            }
            Require(result.TryAdd(id, $"{kind}/{confidence}"), "Duplicate evidence ID");
        }
        return result;
    }

    private static string EvidenceLabel(JsonNode? ids, Dictionary<string, string> evidence)
    {
        var labels = Strings(ids).Select(id => evidence.TryGetValue(id, out string? value)
            ? value : throw new InvalidDataException($"Unresolved evidence ID: {id}")).ToArray();
        Require(labels.Length > 0 && labels.Distinct().Any(label => !label.EndsWith("/unresolved", StringComparison.Ordinal)),
            "Assertion requires resolved evidence");
        return string.Join(", ", labels);
    }

    private static void ValidateCaptures(JsonNode node)
    {
        foreach (var capture in Array(node))
        {
            Shape(capture, "id kind localPath sha256 byteLength tool procedure editionVerification");
            Text(capture!["id"]); Text(capture["kind"]);
            string path = Text(capture["localPath"]); RelativePath(path);
            Require(path.StartsWith("scratch/", StringComparison.Ordinal), "Capture must be beneath scratch/");
            Require(Regex.IsMatch(Text(capture["sha256"]), "^[0-9a-f]{64}$"), "Invalid capture SHA-256");
            Require(capture["byteLength"] is JsonValue value && value.TryGetValue<long>(out long length) && length >= 0, "Invalid byteLength");
            Text(capture["tool"]); Text(capture["procedure"]); Text(capture["editionVerification"]);
        }
    }

    private static BattleTile Tile(JsonNode? node)
    {
        Shape(node, "surface height halfHeight waterDepth slope ceilingDepth movementFlags");
        Shape(node!["slope"], "east west south north"); var slope = node["slope"]!;
        return new(new TerrainSurfaceId(StableId(node["surface"])), Int(node["height"]), Int(node["halfHeight"]),
            Int(node["waterDepth"]), new TileSlope(Int(slope["east"]), Int(slope["west"]), Int(slope["south"]), Int(slope["north"])),
            Int(node["ceilingDepth"]), Flags<BattleTileMovementFlags>(node["movementFlags"]));
    }

    private static BattleUnitState Unit(JsonNode? node)
    {
        Shape(node, "id position hasMoved movementProfile"); var p = node!["movementProfile"]!;
        Shape(p, "move jump bodyHeight mode traits terrainCosts");
        var costs = Array(p["terrainCosts"]).Select(cost =>
        {
            Shape(cost, "surface movePoints isBlocked");
            return new KeyValuePair<TerrainSurfaceId, TerrainMovementCost>(new(StableId(cost!["surface"])),
                new(Int(cost["movePoints"]), Bool(cost["isBlocked"])));
        });
        return new(new BattleUnitId(StableId(node["id"])), Position(node["position"]),
            new UnitMovementProfile(Int(p["move"]), Int(p["jump"]), Int(p["bodyHeight"]),
                EnumValue<UnitMovementMode>(p["mode"]), Flags<UnitMovementTraits>(p["traits"]), costs), Bool(node["hasMoved"]));
    }

    private static TilePosition Position(JsonNode? node)
    {
        Shape(node, "x y layer"); return new(Int(node!["x"]), Int(node["y"]), Int(node["layer"]));
    }

    private static JsonNode Snapshot(BattleState state, string mapId) => JsonSerializer.SerializeToNode(new
    {
        mapId, units = state.Units.Select(u => new
        {
            id = u.Id.Value, position = Pos(u.Position), hasMoved = u.HasMoved,
            movementProfile = new
            {
                move = u.MovementProfile.Move, jump = u.MovementProfile.Jump, bodyHeight = u.MovementProfile.BodyHeight,
                mode = u.MovementProfile.Mode.ToString(), traits = FlagNames(u.MovementProfile.Traits),
                terrainCosts = u.MovementProfile.TerrainCosts.OrderBy(c => c.Key.Value, StringComparer.Ordinal)
                    .Select(c => new { surface = c.Key.Value, movePoints = c.Value.MovePoints, isBlocked = c.Value.IsBlocked })
            }
        }), rngState = state.RngState is { } rng ? new { value = rng.Value, drawCount = rng.DrawCount } : null
    })!;

    private static JsonNode MapSnapshot(BattleMapDefinition map)
    {
        var tiles = new List<object>();
        for (int layer = 0; layer < map.LayerCount; layer++)
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, layer), out var t);
            tiles.Add(new { surface = t.Surface.Value, height = t.Height, halfHeight = t.HalfHeight,
                waterDepth = t.WaterDepth, slope = new { east = t.Slope.East, west = t.Slope.West, south = t.Slope.South, north = t.Slope.North },
                ceilingDepth = t.CeilingDepth, movementFlags = FlagNames(t.MovementFlags) });
        }
        return JsonSerializer.SerializeToNode(new { width = map.Width, depth = map.Depth, layerCount = map.LayerCount, tiles })!;
    }

    private static JsonNode CanonicalState(JsonNode node)
    {
        var clone = node.DeepClone();
        foreach (var unit in Array(clone["units"]))
        {
            var p = unit!["movementProfile"]!;
            p["traits"] = JsonSerializer.SerializeToNode(Strings(p["traits"]).Order(StringComparer.Ordinal));
            p["terrainCosts"] = new JsonArray(Array(p["terrainCosts"]).OrderBy(c => Text(c!["surface"]), StringComparer.Ordinal).Select(c => c!.DeepClone()).ToArray());
        }
        return clone;
    }

    private static object Pos(TilePosition p) => new { x = p.X, y = p.Y, layer = p.Layer };
    private static JsonNode Events(IReadOnlyList<BattleMovementEvent> events) => JsonSerializer.SerializeToNode(
        events.Select(e => new { kind = "unit-moved", unitId = e.UnitId.Value, from = Pos(e.From), to = Pos(e.To) }))!;

    private static string[] FlagNames<T>(T value) where T : struct, Enum => Enum.GetValues<T>()
        .Where(flag => Convert.ToInt32(flag) != 0 && (Convert.ToInt32(value) & Convert.ToInt32(flag)) != 0)
        .Select(flag => flag.ToString()).Order(StringComparer.Ordinal).ToArray();
    private static T Flags<T>(JsonNode? node) where T : struct, Enum
    {
        var names = Strings(node); Require(names.Distinct(StringComparer.Ordinal).Count() == names.Length, "Duplicate flags");
        int result = 0;
        foreach (string name in names)
        {
            var flag = EnumValue<T>(JsonValue.Create(name));
            Require(Convert.ToInt32(flag) != 0, "Use an empty array for None"); result |= Convert.ToInt32(flag);
        }
        return (T)Enum.ToObject(typeof(T), result);
    }
    private static T EnumValue<T>(JsonNode? node) where T : struct, Enum
    {
        string value = Text(node);
        Require(Enum.GetNames<T>().Contains(value, StringComparer.Ordinal), $"Unknown {typeof(T).Name}: {value}");
        return Enum.Parse<T>(value);
    }
    private static JsonNode Group(JsonArray groups, string kind)
    {
        var matches = groups.Where(g => Text(g?["kind"]) == kind).ToArray();
        Require(matches.Length == 1, $"Exactly one {kind} assertion required"); return matches[0]!;
    }
    private static void Shape(JsonNode? node, string names)
    {
        Require(node is JsonObject, "Expected object");
        var expected = names.Split(' ').ToHashSet(StringComparer.Ordinal);
        Require(expected.SetEquals(node!.AsObject().Select(p => p.Key)), $"Missing or unknown fields; required: {names}");
    }
    private static JsonArray Array(JsonNode? node) => node as JsonArray ?? throw new InvalidDataException("Expected array");
    private static string[] Strings(JsonNode? node) => Array(node).Select(Text).ToArray();
    private static string Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
        ? text : throw new InvalidDataException("Expected nonblank string");
    private static int Int(JsonNode? node) => node is JsonValue v && v.TryGetValue<int>(out int number)
        ? number : throw new InvalidDataException("Expected signed 32-bit integer");
    private static bool Bool(JsonNode? node) => node is JsonValue v && v.TryGetValue<bool>(out bool value)
        ? value : throw new InvalidDataException("Expected boolean");
    private static string StableId(JsonNode? node) { string value = Text(node); _ = new AssetId(value); return value; }
    private static string Sha(JsonNode? node) { string value = Text(node); Require(Regex.IsMatch(value, "^[0-9a-f]{40}$"), "Invalid revision SHA"); return value; }
    private static void RelativePath(string value) => Require(!Path.IsPathRooted(value) && !value.Contains('\\') &&
        !value.Contains(':') && value.Split('/').All(part => part.Length > 0 && part is not "." and not ".."), "Invalid relative path");
    private static void EqualLiteral(JsonNode node, string field, string value) => Require(Text(node[field]) == value, $"Unsupported {field}");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static void RejectDuplicateKeys(JsonElement node, string path)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in node.EnumerateObject())
            {
                Require(names.Add(p.Name), $"Duplicate property {path}.{p.Name}"); RejectDuplicateKeys(p.Value, path + "." + p.Name);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray()) RejectDuplicateKeys(item, path + "[]");
    }
    private static string? Difference(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is JsonObject eo && actual is JsonObject ao)
        {
            foreach (var p in eo)
            {
                if (!ao.ContainsKey(p.Key)) return $"{path}.{p.Key}: missing actual field";
                string? diff = Difference(p.Value, ao[p.Key], path + "." + p.Key); if (diff is not null) return diff;
            }
            if (eo.Count != ao.Count) return $"{path}: unexpected actual fields";
            return null;
        }
        if (expected is JsonArray ea && actual is JsonArray aa)
        {
            if (ea.Count != aa.Count) return $"{path}.count: expected {ea.Count}, actual {aa.Count}";
            for (int i = 0; i < ea.Count; i++) { string? diff = Difference(ea[i], aa[i], $"{path}[{i}]"); if (diff is not null) return diff; }
            return null;
        }
        return JsonNode.DeepEquals(expected, actual) ? null : $"{path}: expected {expected?.ToJsonString() ?? "null"}, actual {actual?.ToJsonString() ?? "null"}";
    }
}
