namespace Zodiac.Framework;

/// <summary>A stable, namespaced identifier for content independent of its storage path.</summary>
public readonly record struct AssetId
{
    public string Value { get; }

    public AssetId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        int separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1 || value.IndexOf(':', separator + 1) >= 0)
        {
            throw new ArgumentException("An asset ID must have the form 'namespace:path'.", nameof(value));
        }

        ValidateSegments(value[..separator], allowSlash: false, nameof(value));
        ValidateSegments(value[(separator + 1)..], allowSlash: true, nameof(value));
        Value = value;
    }

    public override string ToString() => Value ?? string.Empty;

    private static void ValidateSegments(string value, bool allowSlash, string parameterName)
    {
        string[] segments = allowSlash ? value.Split('/') : [value];
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment is "." or ".." ||
                segment.Any(character => !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')))
            {
                throw new ArgumentException("Asset IDs use lowercase ASCII letters, digits, '.', '_' and '-'.", parameterName);
            }
        }
    }
}
