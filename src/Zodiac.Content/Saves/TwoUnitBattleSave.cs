using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;

namespace Zodiac.Content.Saves;

/// <summary>Explicit versioned persistence DTOs for the bounded scenario, never the simulation object graph.</summary>
public static partial class TwoUnitBattleSave
{
    public const string Format = "fft.two-unit-battle-save";
    public const int SchemaVersion = 2;
    public const int MaxJsonBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
        MaxDepth = 16,
    };

    public static string ToJson(TwoUnitBattleSaveProfile profile, TwoUnitBattleState state)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        if (!ReferenceEquals(profile.Definition, state.Definition))
            throw new BattleSaveValidationException("Save state must use the explicitly selected profile's definition.");
        _ = profile.Definition.Restore(state.Battle);
        var turns = state.Battle.TurnBattle.Turns;
        var units = profile.Definition.InitialBattle.TurnBattle.Battle.Units.Select(template =>
        {
            var unit = state.Battle.TurnBattle.Battle.Units.Single(item => item.Id == template.Id);
            var turn = turns.Units.Single(item => item.Id == template.Id);
            var combat = state.Battle.Combatants.Single(item => item.UnitId == template.Id);
            return new UnitDto(unit.Id.Value, combat.RightHandWeaponId!.Value.Value,
                unit.Position.X, unit.Position.Y, unit.Position.Layer, combat.Hp, combat.Mp,
                FacingText(combat.Facing), StatusText(combat.Status), combat.KoCount,
                turn.Ct, turn.MovementTaken, turn.ActionTaken);
        }).ToArray();
        var snapshot = new SnapshotDto(profile.Id, profile.Version, profile.ContentVersion, state.Definition.Id.Value,
            FftBiosRandom.AlgorithmId, new(turns.Ticks, PhaseText(turns.Phase), turns.ActiveUnitId?.Value,
                turns.RngState.Value, turns.RngState.DrawCount, OutcomeText(state.Outcome), units));
        string json = JsonSerializer.Serialize(new Envelope<SnapshotDto>(Format, SchemaVersion, Hash(snapshot), snapshot), JsonOptions) + "\n";
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes) throw new BattleSaveValidationException("Save exceeds its JSON byte budget.");
        return json;
    }

    public static TwoUnitBattleSaveLoadResult ReadJson(TwoUnitBattleSaveProfile profile, string json)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (json is null || Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new BattleSaveValidationException($"Save is required and limited to {MaxJsonBytes} UTF-8 bytes.");
        try
        {
            using var document = JsonDocument.Parse(json, new() { MaxDepth = 16, AllowDuplicateProperties = false });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var versionNode) || !versionNode.TryGetInt32(out int version))
                throw new BattleSaveValidationException("Save requires an integer schemaVersion.");
            SnapshotDto snapshot;
            if (version == SchemaVersion)
            {
                var envelope = JsonSerializer.Deserialize<Envelope<SnapshotDto>>(json, JsonOptions)!;
                ValidateEnvelope(envelope);
                snapshot = envelope.Snapshot;
            }
            else if (version == 1)
            {
                // Synthetic predecessor: the RNG algorithm was implicitly the known FFT BIOS generator.
                var envelope = JsonSerializer.Deserialize<Envelope<LegacySnapshotDto>>(json, JsonOptions)!;
                ValidateEnvelope(envelope); // Verify legacy bytes/fields before migration.
                var old = envelope.Snapshot;
                snapshot = new(old.ProfileId, old.ProfileVersion, old.ContentVersion, old.ScenarioId, FftBiosRandom.AlgorithmId, old.Battle);
            }
            else throw new BattleSaveValidationException($"Unsupported save schema version {version}; supported versions are 1 and {SchemaVersion}.");
            return new(Restore(profile, snapshot), version);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            throw new BattleSaveValidationException($"Invalid battle save: {exception.Message}", exception);
        }
    }

    private static void ValidateEnvelope<T>(Envelope<T> envelope)
    {
        if (envelope.Format != Format) throw new BattleSaveValidationException("Unsupported save format.");
        if (envelope.Snapshot is null) throw new BattleSaveValidationException("Save snapshot is required.");
        if (!string.Equals(envelope.Sha256, Hash(envelope.Snapshot), StringComparison.Ordinal))
            throw new BattleSaveValidationException("Save SHA-256 does not match its snapshot; data is corrupt or edited.");
    }

    private static TwoUnitBattleState Restore(TwoUnitBattleSaveProfile profile, SnapshotDto saved)
    {
        CheckId(saved.ProfileId); CheckId(saved.ScenarioId);
        if (saved.ProfileId != profile.Id || saved.ProfileVersion != profile.Version || saved.ContentVersion != profile.ContentVersion)
            throw new BattleSaveValidationException("Save content profile ID/version or content version does not match the selected profile.");
        if (saved.ScenarioId != profile.Definition.Id.Value) throw new BattleSaveValidationException("Save scenario ID is not the selected definition.");
        if (saved.RngAlgorithmId != FftBiosRandom.AlgorithmId) throw new BattleSaveValidationException("Unsupported RNG algorithm ID.");
        var data = saved.Battle;
        if (data.Units.Length != 2 || data.Units.Any(unit => unit is null))
            throw new BattleSaveValidationException("Save requires exactly two nonnull units.");
        var byId = new Dictionary<BattleUnitId, UnitDto>();
        foreach (var unit in data.Units)
        {
            CheckId(unit.Id); CheckId(unit.WeaponId);
            if (!byId.TryAdd(new(unit.Id), unit)) throw new BattleSaveValidationException($"Duplicate unit ID '{unit.Id}'.");
        }
        BattleUnitId? active = data.ActiveUnitId is null ? null : new(CheckId(data.ActiveUnitId));
        var initial = profile.Definition.InitialBattle;
        if (active is { } id && !byId.ContainsKey(id)) throw new BattleSaveValidationException("Active unit ID is missing from the save.");
        var rng = new FftRandomState(data.RngWord, data.RngDrawCount);
        var movement = new List<BattleUnitState>();
        var turns = new List<TurnUnitState>();
        var combatants = new List<AttackUnitState>();
        // Preserve authored ordering and tie keys; save-array order is not simulation ordering.
        foreach (var template in initial.TurnBattle.Battle.Units)
        {
            if (!byId.TryGetValue(template.Id, out var unit)) throw new BattleSaveValidationException($"Missing definition unit '{template.Id}'.");
            var combat = initial.Combatants.Single(item => item.UnitId == template.Id);
            var turn = initial.TurnBattle.Turns.Units.Single(item => item.Id == template.Id);
            if (unit.WeaponId != combat.RightHandWeaponId!.Value.Value || !profile.Definition.Weapons.ContainsKey(new(unit.WeaponId)))
                throw new BattleSaveValidationException($"Unresolved or changed weapon ID '{unit.WeaponId}' for '{unit.Id}'.");
            movement.Add(new(template.Id, new(unit.X, unit.Y, unit.Layer), template.MovementProfile, unit.MovementTaken));
            turns.Add(new(template.Id, turn.Speed, unit.Ct, turn.TieKey, active == template.Id, unit.MovementTaken, unit.ActionTaken));
            combatants.Add(new(template.Id, unit.Hp, combat.MaxHp, unit.Mp, combat.MaxMp, combat.PhysicalAttack,
                combat.ClassPhysicalEvade, combat.RightHandEvade, combat.LeftHandEvade, combat.AccessoryEvade,
                combat.Brave, combat.ZodiacSign, ParseFacing(unit.Facing), ParseStatus(unit.Status), unit.KoCount, combat.RightHandWeaponId));
        }
        var battle = new AttackBattleSnapshot(new(new(initial.TurnBattle.Battle.Map, movement, rng),
            new(turns, rng, data.Ticks, ParsePhase(data.Phase))), combatants);
        var state = profile.Definition.Restore(battle);
        if (data.Outcome != OutcomeText(state.Outcome)) throw new BattleSaveValidationException("Saved outcome disagrees with unit HP.");
        return state;
    }

    private static string CheckId(string id)
    {
        if (id.Length > 256) throw new BattleSaveValidationException("Stable ID exceeds 256 characters.");
        return new AssetId(id).Value;
    }
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions)));
    private static string PhaseText(TurnCyclePhase phase) => phase switch { TurnCyclePhase.AddCt => "add-ct", TurnCyclePhase.PickUnit => "pick-unit", TurnCyclePhase.UnitTurn => "unit-turn", _ => throw new ArgumentOutOfRangeException(nameof(phase)) };
    private static TurnCyclePhase ParsePhase(string phase) => phase switch { "add-ct" => TurnCyclePhase.AddCt, "pick-unit" => TurnCyclePhase.PickUnit, "unit-turn" => TurnCyclePhase.UnitTurn, _ => throw new BattleSaveValidationException("Unknown turn phase.") };
    private static string FacingText(BattleFacing facing) => facing switch { BattleFacing.East => "east", BattleFacing.North => "north", BattleFacing.West => "west", BattleFacing.South => "south", _ => throw new ArgumentOutOfRangeException(nameof(facing)) };
    private static BattleFacing ParseFacing(string facing) => facing switch { "east" => BattleFacing.East, "north" => BattleFacing.North, "west" => BattleFacing.West, "south" => BattleFacing.South, _ => throw new BattleSaveValidationException("Unknown unit facing.") };
    private static string StatusText(AttackUnitStatus status) => status switch { AttackUnitStatus.None => "healthy", AttackUnitStatus.Critical => "critical", AttackUnitStatus.Dead => "dead", _ => throw new ArgumentOutOfRangeException(nameof(status)) };
    private static AttackUnitStatus ParseStatus(string status) => status switch { "healthy" => AttackUnitStatus.None, "critical" => AttackUnitStatus.Critical, "dead" => AttackUnitStatus.Dead, _ => throw new BattleSaveValidationException("Unknown unit status.") };
    private static string OutcomeText(TwoUnitBattleOutcome outcome) => outcome switch { TwoUnitBattleOutcome.InProgress => "in-progress", TwoUnitBattleOutcome.FirstUnitWon => "first-unit-won", TwoUnitBattleOutcome.SecondUnitWon => "second-unit-won", _ => throw new ArgumentOutOfRangeException(nameof(outcome)) };

    // Persistence contracts contain scalar state and stable IDs only, with no simulation types.
    private sealed record Envelope<T>(string Format, int SchemaVersion, string Sha256, T Snapshot);
    private sealed record SnapshotDto(string ProfileId, int ProfileVersion, int ContentVersion, string ScenarioId, string RngAlgorithmId, BattleDto Battle);
    private sealed record LegacySnapshotDto(string ProfileId, int ProfileVersion, int ContentVersion, string ScenarioId, BattleDto Battle);
    private sealed record BattleDto(ulong Ticks, string Phase, string? ActiveUnitId, uint RngWord, ulong RngDrawCount, string Outcome, UnitDto[] Units);
    private sealed record UnitDto(string Id, string WeaponId, int X, int Y, int Layer, int Hp, int Mp, string Facing, string Status, int KoCount, int Ct, bool MovementTaken, bool ActionTaken);
}
