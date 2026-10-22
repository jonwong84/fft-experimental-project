namespace Fft.Content.ContentPackages;

/// <summary>A package validation failure with stable authoring context, separate from file I/O failures.</summary>
public sealed class ContentPackageValidationException : Exception
{
    public string? PackageId { get; }
    public string? RecordId { get; }
    public string FieldPath { get; }

    public ContentPackageValidationException(string? packageId, string? recordId, string fieldPath,
        string message, Exception? innerException = null)
        : base($"{message} (package: {packageId ?? "<unknown>"}, record: {recordId ?? "<none>"}, field: {fieldPath})", innerException)
    {
        PackageId = packageId;
        RecordId = recordId;
        FieldPath = fieldPath;
    }
}

public enum ContentPackageKind
{
    Baseline,
    Addon,
}
