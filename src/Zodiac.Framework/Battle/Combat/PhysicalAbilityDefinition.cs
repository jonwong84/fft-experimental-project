using Zodiac.Framework.Formulas;

namespace Zodiac.Framework;

/// <summary>Data for an immediate, single-target neutral physical action. Admission remains with the selected battle profile.</summary>
public sealed record PhysicalAbilityDefinition
{
    public AssetId Id { get; }
    public int Range { get; }
    public bool Evadeable { get; }
    public bool DirectTrajectory { get; }
    public PhysicalDamageFormula Formula { get; }

    public PhysicalAbilityDefinition(AssetId id, int range, bool evadeable, bool directTrajectory, PhysicalDamageFormula formula)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("An explicit ability ID is required.", nameof(id));
        if (range is < 1 or > 255) throw new ArgumentOutOfRangeException(nameof(range));
        ArgumentNullException.ThrowIfNull(formula);
        Id = id; Range = range; Evadeable = evadeable; DirectTrajectory = directTrajectory; Formula = formula;
    }
}
