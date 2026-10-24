using System.Text.Json;
using Fft.Core;

namespace Fft.Content.ContentPackages;

/// <summary>Strict JSON primitives shared by metadata and bounded executable-content formats.</summary>
internal sealed class PackageJsonValidation(string? packageId)
{
    internal void Fields(JsonElement node, string? record, string path, params string[] fields)
    {
        if (node.ValueKind != JsonValueKind.Object) throw Failure(record, path, "Expected an object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in node.EnumerateObject())
        {
            string propertyPath = path + "." + property.Name;
            if (!seen.Add(property.Name)) throw Failure(record, propertyPath, "Duplicate JSON field.");
            if (!fields.Contains(property.Name, StringComparer.Ordinal)) throw Failure(record, propertyPath, "Unknown field.");
        }
        foreach (string field in fields)
            if (!seen.Contains(field)) throw Failure(record, path + "." + field, "Required field is missing.");
    }

    internal JsonElement.ArrayEnumerator Array(JsonElement node, string? record, string path, int maximum)
    {
        if (node.ValueKind != JsonValueKind.Array) throw Failure(record, path, "Expected an array.");
        if (node.GetArrayLength() > maximum) throw Failure(record, path, $"Array exceeds {maximum} entries.");
        return node.EnumerateArray();
    }

    internal string Id(JsonElement node, string? record, string path) => Id(Text(node, record, path), record, path);
    internal string Id(string? value, string? record, string path)
    {
        if (value is null || value.Length > ContentPackage.MaxIdLength)
            throw Failure(record, path, $"Stable ID is required and cannot exceed {ContentPackage.MaxIdLength} characters.");
        try { return new AssetId(value).Value; }
        catch (ArgumentException exception)
        {
            throw new ContentPackageValidationException(packageId, record, path, "Invalid lowercase 'namespace:path' stable ID.", exception);
        }
    }
    internal string Text(JsonElement node, string? record, string path) => node.ValueKind == JsonValueKind.String
        ? node.GetString()! : throw Failure(record, path, "Expected a string.");
    internal int Integer(JsonElement node, string? record, string path, int minimum, int maximum)
    {
        if (node.ValueKind != JsonValueKind.Number || !node.TryGetInt32(out int value) || value < minimum || value > maximum)
            throw Failure(record, path, $"Expected an integer from {minimum} through {maximum}.");
        return value;
    }
    internal ContentPackageValidationException Failure(string? record, string path, string message) => new(packageId, record, path, message);
    internal static string? GuessString(JsonElement node, string property) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}
