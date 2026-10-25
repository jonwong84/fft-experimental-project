using System.Globalization;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Zodiac.Framework;
using static Zodiac.Framework.Tests.Fixtures.MovementFixtureRunner;

namespace Zodiac.Framework.Tests.Fixtures;

// V2 preserves contract-only attacks. V3 supplies explicit weapon projections and executes attacks.
internal static class AttackFixtureRunner
{
    internal static Report Run(string json, string implementationRevision)
    {
        string id = "<invalid>", revision = "<invalid>", provenance = "{}";
        const string policy = "fft:combat/bounded-sword-contract";
        try
        {
            Sha(JsonValue.Create(implementationRevision));
            Require(json.Length <= 8 * 1024 * 1024 && Encoding.UTF8.GetByteCount(json) <= 8 * 1024 * 1024, "Fixture exceeds size budget");
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            RejectDuplicateKeys(doc.RootElement, "$");
            JsonNode root = JsonNode.Parse(json)!;
            Shape(root, "format schemaVersion fixtureId execution purpose target reference evidence preconditions exclusions unknowns scenario sourceCases expectations captures");
            EqualLiteral(root, "format", "fft.behavior-fixture");
            int version = Int(root["schemaVersion"]); Require(version is 2 or 3, "Unsupported schemaVersion");
            id = StableId(root["fixtureId"]); Text(root["purpose"]);
            Shape(root["target"], "game edition region platform policyId");
            EqualLiteral(root["target"]!, "game", "FFT"); EqualLiteral(root["target"]!, "edition", "SCUS-94221");
            EqualLiteral(root["target"]!, "region", "North America"); EqualLiteral(root["target"]!, "platform", "PS1");
            EqualLiteral(root["target"]!, "policyId", policy);
            Shape(root["reference"], "decompCommit observedOn usedForExpectations");
            revision = Sha(root["reference"]!["decompCommit"]);
            Require(DateOnly.TryParseExact(Text(root["reference"]!["observedOn"]), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "Invalid observedOn");
            Bool(root["reference"]!["usedForExpectations"]);
            foreach (string field in new[] { "preconditions", "exclusions", "unknowns" }) Strings(root[field]);
            var evidence = Evidence(root["evidence"]!); ValidateCaptures(root["captures"]!);
            foreach (var entry in Array(root["evidence"]))
            foreach (var source in Array(entry!["sources"]))
                if (Text(source!["path"]).EndsWith(".c", StringComparison.Ordinal) || Text(source["path"]).EndsWith(".h", StringComparison.Ordinal))
                    Require(Sha(source["revision"]) == revision, "Decomp source revision differs from reference");
            Require(Array(root["sourceCases"]).Count == 0, "V2 uses assertion evidence; sourceCases must be empty");
            provenance = new JsonObject(new[] { "target", "reference", "evidence", "preconditions", "exclusions", "unknowns", "captures" }
                .Select(name => new KeyValuePair<string, JsonNode?>(name, root[name]!.DeepClone()))).ToJsonString();
            string execution = Text(root["execution"]); Require(execution is "scenario" or "description-only", "Unsupported execution");
            var scenario = root["scenario"]!;
            Shape(scenario, version == 2 ? "weaponIds initialState commands" : "weapons initialState commands");
            BoundedSwordWeapon[] definitions = version == 2 ? [] : Array(scenario["weapons"]).Select(node =>
            {
                Shape(node, "id power");
                return new BoundedSwordWeapon(new AssetId(StableId(node!["id"])), Int(node["power"]));
            }).ToArray();
            string[] weapons = version == 2 ? Strings(scenario["weaponIds"]) : definitions.Select(weapon => weapon.Id.Value).ToArray();
            foreach (string weapon in weapons) _ = new AssetId(weapon);
            Require(weapons.Distinct(StringComparer.Ordinal).Count() == weapons.Length, "Duplicate weapon declaration");
            if (version == 3)
            {
                var audit = JsonNode.Parse(provenance)!.AsObject(); audit["weapons"] = scenario["weapons"]!.DeepClone();
                provenance = audit.ToJsonString();
            }
            var initial = scenario["initialState"]!;
            Shape(initial, "map units rngState turns combatants");
            var mapNode = initial["map"]!; Shape(mapNode, "id width depth layerCount tiles");
            string mapId = StableId(mapNode["id"]);
            var map = new BattleMapDefinition(Int(mapNode["width"]), Int(mapNode["depth"]), Int(mapNode["layerCount"]), Array(mapNode["tiles"]).Select(Tile).ToArray());
            var projected = initial.DeepClone().AsObject(); projected.Remove("map"); projected["mapId"] = mapId;
            AttackBattleSnapshot state = ReadState(projected, map, mapId, weapons);
            state.ValidateInitialSetup();
            var commands = Array(scenario["commands"]); var expectations = Array(root["expectations"]);
            Require(commands.Count is > 0 and <= 1000 && commands.Count == expectations.Count, "One expectation per command required (1..1000 commands)");
            bool hasAttack = false;
            for (int i = 0; i < commands.Count; i++)
            {
                var command = commands[i]!; string kind = Text(command["kind"]);
                switch (kind)
                {
                    case "advance": Shape(command, "index kind"); break;
                    case "move-unit": Shape(command, "index kind unitId destination"); StableId(command["unitId"]); Position(command["destination"]); break;
                    case "wait": case "confirm-act": Shape(command, "index kind unitId"); StableId(command["unitId"]); break;
                    case "attack-unit": Shape(command, "index kind actorId targetId"); StableId(command["actorId"]); StableId(command["targetId"]); hasAttack = true; break;
                    default: throw new InvalidDataException("Unsupported command kind");
                }
                Require(Int(command["index"]) == i, "Noncontiguous command index");
                var expected = expectations[i]!; Shape(expected, "commandIndex assertions"); Require(Int(expected["commandIndex"]) == i, "Noncontiguous expectation index");
                var groups = Array(expected["assertions"]); Require(groups.Count == 2, "Exactly two assertion groups required");
                var result = Group(groups, "result"); Shape(result, "kind accepted turnRejection movementRejection attackRejection attackOutcome evidenceIds");
                bool accepted = Bool(result["accepted"]);
                int rejections = (EnumValue<TurnCycleRejection>(result["turnRejection"]) != TurnCycleRejection.None ? 1 : 0)
                    + (EnumValue<BattleTransitionRejection>(result["movementRejection"]) != BattleTransitionRejection.None ? 1 : 0)
                    + (EnumValue<AttackCommandRejection>(result["attackRejection"]) != AttackCommandRejection.None ? 1 : 0);
                Require(rejections == (accepted ? 0 : 1), "Inconsistent rejection fields");
                string outcome = Text(result["attackOutcome"]);
                Require(kind == "attack-unit" ? (accepted ? outcome is "hit" or "miss" or "ko" : outcome == "rejected") : outcome == "none", "Inconsistent attack outcome");
                EvidenceLabel(result["evidenceIds"], evidence);
                var group = Group(groups, "state-and-events"); Shape(group, "kind stateRelation state events evidenceIds");
                Require(Text(group["stateRelation"]) == (accepted ? "new-instance" : "same-instance"), "Inconsistent stateRelation");
                EvidenceLabel(group["evidenceIds"], evidence);
                _ = ReadState(group["state"]!, map, mapId, weapons);
                foreach (var ev in Array(group["events"])) ValidateEvent(ev!);
            }
            if (execution == "description-only" || (version == 2 && hasAttack))
                return Result("not-executed", "Validated contract; description-only/v2 attack fixtures do not execute combat assertions.");
            for (int i = 0; i < commands.Count; i++)
            {
                if (version == 2) Require(state.Combatants.All(unit => unit.Status == AttackUnitStatus.None), "V2 turn replay after combat statuses is outside its contract");
                var before = Snapshot(state, mapId); var beforeMap = MapSnapshot(state.TurnBattle.Battle.Map);
                var command = commands[i]!;
                AttackBattleResult actual;
                if (version == 2)
                {
                    var turn = Text(command["kind"]) switch
                    {
                        "advance" => TurnBattleSimulator.Advance(state.TurnBattle),
                        "move-unit" => TurnBattleSimulator.Move(state.TurnBattle, new(new(StableId(command["unitId"])), Position(command["destination"]))),
                        "wait" => TurnBattleSimulator.Wait(state.TurnBattle, new(StableId(command["unitId"]))),
                        "confirm-act" => TurnBattleSimulator.ConfirmAct(state.TurnBattle, new(StableId(command["unitId"]))),
                        _ => throw new InvalidDataException("V2 attack execution unavailable"),
                    };
                    actual = new(turn.Accepted, AttackCommandRejection.None, turn.TurnRejection, turn.MovementRejection,
                        turn.Accepted ? state.WithTurnBattle(turn.State) : state,
                        turn.Events.Select(ev => (AttackResultEvent)new AttackTurnBattleEvent(ev)).ToImmutableArray());
                }
                else
                {
                    string? actorId = command["actorId"]?.GetValue<string>();
                    var actor = state.Combatants.FirstOrDefault(unit => unit.UnitId.Value == actorId);
                    var weapon = definitions.SingleOrDefault(item => item.Id == actor?.RightHandWeaponId);
                    actual = Text(command["kind"]) switch
                    {
                        "attack-unit" => AttackBattleSimulator.Attack(state, new(new(StableId(command["actorId"])), new(StableId(command["targetId"]))), weapon),
                        "advance" => AttackBattleSimulator.Advance(state),
                        "move-unit" => AttackBattleSimulator.Move(state, new(new(StableId(command["unitId"])), Position(command["destination"]))),
                        "wait" => AttackBattleSimulator.Wait(state, new(StableId(command["unitId"]))),
                        "confirm-act" => AttackBattleSimulator.ConfirmAct(state, new(StableId(command["unitId"]))),
                        _ => throw new InvalidDataException("Unsupported v3 command"),
                    };
                }
                AttackBattleSnapshot next = actual.State;
                string attackOutcome = Text(command["kind"]) != "attack-unit" ? "none" : !actual.Accepted ? "rejected" :
                    actual.Events.OfType<AttackKnockedOutEvent>().Any() ? "ko" : actual.Events.OfType<AttackResolvedEvent>().Single().Hit ? "hit" : "miss";
                var groups = Array(expectations[i]!["assertions"]); var result = Group(groups, "result"); var group = Group(groups, "state-and-events");
                string label = EvidenceLabel(group["evidenceIds"], evidence);
                string? mismatch = Difference(result["accepted"], JsonValue.Create(actual.Accepted), $"expectations[{i}].accepted")
                    ?? Difference(result["turnRejection"], JsonValue.Create(actual.TurnRejection.ToString()), $"expectations[{i}].turnRejection")
                    ?? Difference(result["movementRejection"], JsonValue.Create(actual.MovementRejection.ToString()), $"expectations[{i}].movementRejection")
                    ?? Difference(result["attackRejection"], JsonValue.Create(actual.AttackRejection.ToString()), $"expectations[{i}].attackRejection")
                    ?? Difference(result["attackOutcome"], JsonValue.Create(attackOutcome), $"expectations[{i}].attackOutcome")
                    ?? Difference(group["stateRelation"], JsonValue.Create(ReferenceEquals(state, next) ? "same-instance" : "new-instance"), $"expectations[{i}].stateRelation")
                    ?? Difference(Snapshot(ReadState(group["state"]!, map, mapId, weapons), mapId), Snapshot(next, mapId), $"expectations[{i}].state")
                    ?? Difference(group["events"], new JsonArray(actual.Events.Select(Event).ToArray()), $"expectations[{i}].events")
                    ?? Difference(before, Snapshot(state, mapId), $"commands[{i}].inputState")
                    ?? Difference(beforeMap, MapSnapshot(next.TurnBattle.Battle.Map), $"commands[{i}].map");
                if (mismatch is not null) return Result("failed-assertion", $"command {i}; {label}; {mismatch}");
                state = next;
            }
            return Result("passed-assertion", version == 2 ? "V2 movement/turn regression passed; combat fields preserved, no attack formula executed." : "V3 bounded attack/turn replay passed; weapon projections and complete state/events compared.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException or FormatException or NotSupportedException)
        { return Result("invalid-fixture", ex.Message); }
        Report Result(string status, string message) => new(status, id, policy, implementationRevision, revision, provenance, message);
    }

    internal static AttackBattleSnapshot ReadState(JsonNode node, BattleMapDefinition map, string mapId, string[] weapons)
    {
        Shape(node, "mapId units rngState turns combatants"); Require(StableId(node["mapId"]) == mapId, "Unknown map ID");
        var rng = node["rngState"]!; Shape(rng, "algorithmId value drawCount"); EqualLiteral(rng, "algorithmId", FftBiosRandom.AlgorithmId);
        ulong word = Unsigned(rng["value"]); Require(word <= uint.MaxValue, "RNG word exceeds UInt32");
        var random = new FftRandomState((uint)word, Unsigned(rng["drawCount"]));
        var battle = new BattleState(map, Array(node["units"]).Select(Unit), random);
        var turn = node["turns"]!; Shape(turn, "ticks phase units");
        var turns = new TurnCycleState(Array(turn["units"]).Select(u =>
        {
            Shape(u, "id speed ct tieKey hasTurn movementTaken actionTaken");
            return new TurnUnitState(new(StableId(u!["id"])), Int(u["speed"]), Int(u["ct"]), Int(u["tieKey"]), Bool(u["hasTurn"]), Bool(u["movementTaken"]), Bool(u["actionTaken"]));
        }), random, Unsigned(turn["ticks"]), EnumValue<TurnCyclePhase>(turn["phase"]));
        return new(new(battle, turns), Array(node["combatants"]).Select(u =>
        {
            Shape(u, "unitId hp maxHp mp maxMp physicalAttack classPhysicalEvade rightHandEvade leftHandEvade accessoryEvade brave zodiacSign facing status koCount rightHandWeaponId");
            string? weapon = u!["rightHandWeaponId"] is null ? null : StableId(u["rightHandWeaponId"]);
            Require(weapon is null || weapons.Contains(weapon, StringComparer.Ordinal), "Unresolved weapon reference");
            return new AttackUnitState(new(StableId(u["unitId"])), Int(u["hp"]), Int(u["maxHp"]), Int(u["mp"]), Int(u["maxMp"]),
                Int(u["physicalAttack"]), Int(u["classPhysicalEvade"]), Int(u["rightHandEvade"]), Int(u["leftHandEvade"]), Int(u["accessoryEvade"]),
                Int(u["brave"]), Int(u["zodiacSign"]), EnumValue<BattleFacing>(u["facing"]), EnumValue<AttackUnitStatus>(u["status"]), Int(u["koCount"]), weapon is null ? null : new AssetId(weapon));
        }));
    }

    internal static JsonNode Snapshot(AttackBattleSnapshot state, string mapId)
    {
        var result = MovementFixtureRunner.Snapshot(state.TurnBattle.Battle, mapId);
        result["rngState"]!["algorithmId"] = FftBiosRandom.AlgorithmId;
        result["turns"] = JsonSerializer.SerializeToNode(new { ticks = state.TurnBattle.Turns.Ticks, phase = state.TurnBattle.Turns.Phase.ToString(),
            units = state.TurnBattle.Turns.Units.Select(u => new { id = u.Id.Value, speed = u.Speed, ct = u.Ct, tieKey = u.TieKey, hasTurn = u.HasTurn, movementTaken = u.MovementTaken, actionTaken = u.ActionTaken }) });
        result["combatants"] = JsonSerializer.SerializeToNode(state.Combatants.Select(u => new { unitId = u.UnitId.Value, hp = u.Hp, maxHp = u.MaxHp, mp = u.Mp, maxMp = u.MaxMp,
            physicalAttack = u.PhysicalAttack, classPhysicalEvade = u.ClassPhysicalEvade, rightHandEvade = u.RightHandEvade, leftHandEvade = u.LeftHandEvade, accessoryEvade = u.AccessoryEvade,
            brave = u.Brave, zodiacSign = u.ZodiacSign, facing = u.Facing.ToString(), status = u.Status.ToString(), koCount = u.KoCount, rightHandWeaponId = u.RightHandWeaponId?.Value }));
        return result;
    }

    private static ulong Unsigned(JsonNode? node) => node is JsonValue v && v.TryGetValue<ulong>(out ulong value) ? value : throw new InvalidDataException("Expected UInt64 integer");
    private static JsonNode Event(AttackResultEvent ev) => ev switch
    {
        AttackTurnBattleEvent turn => Event(turn.Event),
        AttackResolvedEvent a => JsonSerializer.SerializeToNode(new { kind = "attack-resolved", actorId = a.ActorId.Value, targetId = a.TargetId.Value, hit = a.Hit, critical = a.Critical, damage = a.Damage })!,
        AttackHpChangedEvent h => JsonSerializer.SerializeToNode(new { kind = "hp-changed", unitId = h.UnitId.Value, before = h.Before, after = h.After })!,
        AttackStatusChangedEvent s => JsonSerializer.SerializeToNode(new { kind = "status-changed", unitId = s.UnitId.Value, before = s.Before.ToString(), after = s.After.ToString() })!,
        AttackKnockedOutEvent k => JsonSerializer.SerializeToNode(new { kind = "unit-knocked-out", unitId = k.UnitId.Value, koCount = k.KoCount })!,
        _ => throw new InvalidDataException("Unsupported attack event"),
    };
    private static JsonNode Event(TurnBattleEvent ev) => ev switch
    {
        TurnBattleMovementEvent m => JsonSerializer.SerializeToNode(new { kind = "unit-moved", unitId = m.Movement.UnitId.Value, from = Pos(m.Movement.From), to = Pos(m.Movement.To) })!,
        TurnBattleClockEvent { Event: TurnClockAdvancedEvent t } => JsonSerializer.SerializeToNode(new { kind = "clock-advanced", tick = t.Tick })!,
        TurnBattleClockEvent { Event: TurnReadyEvent r } => JsonSerializer.SerializeToNode(new { kind = "unit-ready", unitId = r.UnitId.Value, ctBefore = r.CtBeforeSelection, ctAfter = r.CtAfterSelection, upkeepRoll = r.UpkeepRoll })!,
        TurnBattleClockEvent { Event: TurnChoiceConsumedEvent c } => JsonSerializer.SerializeToNode(new { kind = "choice-consumed", unitId = c.UnitId.Value, choice = c.Choice.ToString() })!,
        TurnBattleClockEvent { Event: TurnEndedEvent e } => JsonSerializer.SerializeToNode(new { kind = "turn-ended", unitId = e.UnitId.Value, ctBefore = e.CtBeforeCompletion, ctAfter = e.CtAfterCompletion })!,
        _ => throw new InvalidDataException("Unsupported event"),
    };
    private static void ValidateEvent(JsonNode ev)
    {
        switch (Text(ev["kind"]))
        {
            case "unit-moved": Shape(ev, "kind unitId from to"); StableId(ev["unitId"]); Position(ev["from"]); Position(ev["to"]); break;
            case "clock-advanced": Shape(ev, "kind tick"); Unsigned(ev["tick"]); break;
            case "unit-ready": Shape(ev, "kind unitId ctBefore ctAfter upkeepRoll"); StableId(ev["unitId"]); Int(ev["ctBefore"]); Int(ev["ctAfter"]); Require(Unsigned(ev["upkeepRoll"]) <= 32767, "Invalid upkeep roll"); break;
            case "choice-consumed": Shape(ev, "kind unitId choice"); StableId(ev["unitId"]); EnumValue<TurnCommandKind>(ev["choice"]); break;
            case "turn-ended": Shape(ev, "kind unitId ctBefore ctAfter"); StableId(ev["unitId"]); Int(ev["ctBefore"]); Int(ev["ctAfter"]); break;
            case "attack-resolved":
                Shape(ev, "kind actorId targetId hit critical damage"); StableId(ev["actorId"]); StableId(ev["targetId"]);
                bool hit = Bool(ev["hit"]), critical = Bool(ev["critical"]); int damage = Int(ev["damage"]);
                Require(damage is >= 0 and <= 999 && (hit || (!critical && damage == 0)), "Inconsistent hit/critical/damage event"); break;
            case "hp-changed": Shape(ev, "kind unitId before after"); StableId(ev["unitId"]); Require(Int(ev["before"]) is >= 0 and <= 65535 && Int(ev["after"]) is >= 0 and <= 65535, "Invalid HP event"); break;
            case "status-changed": Shape(ev, "kind unitId before after"); StableId(ev["unitId"]); EnumValue<AttackUnitStatus>(ev["before"]); EnumValue<AttackUnitStatus>(ev["after"]); break;
            case "unit-knocked-out": Shape(ev, "kind unitId koCount"); StableId(ev["unitId"]); Require(Int(ev["koCount"]) is >= 0 and <= 255, "Invalid KO count"); break;
            default: throw new InvalidDataException("Unsupported event kind");
        }
    }
}
