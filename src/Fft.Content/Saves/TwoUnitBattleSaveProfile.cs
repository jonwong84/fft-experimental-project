using Fft.Core;

namespace Fft.Content.Saves;

/// <summary>Explicitly selected, immutable content binding. Save files cannot supply definitions or select profiles.</summary>
public sealed class TwoUnitBattleSaveProfile
{
    public string Id { get; }
    public int Version { get; }
    public int ContentVersion { get; }
    public TwoUnitBattleDefinition Definition { get; }

    public TwoUnitBattleSaveProfile(string id, int version, int contentVersion, TwoUnitBattleDefinition definition)
    {
        if (id is null || id.Length > 256) throw new ArgumentException("Profile ID is required and limited to 256 characters.", nameof(id));
        Id = new AssetId(id).Value;
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(contentVersion, 1);
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Id.Value.Length > 256 || definition.InitialBattle.TurnBattle.Battle.Units.Any(unit => unit.Id.Value.Length > 256) ||
            definition.Weapons.Keys.Any(id => id.Value.Length > 256))
            throw new ArgumentException("Save definition IDs cannot exceed 256 characters.", nameof(definition));
        foreach (var unit in definition.InitialBattle.TurnBattle.Battle.Units) _ = new AssetId(unit.Id.Value);
        Version = version; ContentVersion = contentVersion; Definition = definition;
    }
}

public sealed class BattleSaveValidationException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record TwoUnitBattleSaveLoadResult(TwoUnitBattleState State, int SourceSchemaVersion)
{
    public bool WasMigrated => SourceSchemaVersion != TwoUnitBattleSave.SchemaVersion;
}
