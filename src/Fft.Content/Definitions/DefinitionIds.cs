using Fft.Core;

namespace Fft.Content.Definitions;

/// <summary>A stable FFT job identity, independent of an original table index or display name.</summary>
public readonly record struct JobId
{
    public string Value { get; }
    public JobId(string value) => Value = DefinitionValidation.Id(value, "Job ID");
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>A stable FFT skillset identity, distinct from job and ability identities.</summary>
public readonly record struct SkillsetId
{
    public string Value { get; }
    public SkillsetId(string value) => Value = DefinitionValidation.Id(value, "Skillset ID");
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>A stable FFT ability identity; no numeric-ID category is implied.</summary>
public readonly record struct AbilityId
{
    public string Value { get; }
    public AbilityId(string value) => Value = DefinitionValidation.Id(value, "Ability ID");
    public override string ToString() => Value ?? string.Empty;
}

internal static class DefinitionValidation
{
    internal static string Id(string value, string label)
    {
        try
        {
            return new AssetId(value).Value;
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException($"{label} must be a valid lowercase 'namespace:path' identity.", nameof(value), exception);
        }
    }

    internal static void RequiredId(string? value, string parameterName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("A definition or reference must have a non-default stable ID.", parameterName);
        }
    }

    internal static void Range(int value, int maximum, string parameterName)
    {
        if (value < 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between 0 and {maximum} inclusive.");
        }
    }
}
