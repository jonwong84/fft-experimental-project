using System.Collections.Immutable;
using Zodiac.Content.Definitions;
using Zodiac.Framework;

namespace Zodiac.Content.ContentPackages;

/// <summary>Immutable, versioned FFT metadata. Add-on references are resolved only by explicit composition.</summary>
public sealed partial class ContentPackage
{
    public const string Format = "fft.content-metadata";
    public const int SchemaVersion = 1;
    public const int MaxJsonBytes = 8 * 1024 * 1024;
    public const int MaxJsonDepth = 32;
    public const int MaxDefinitionsPerKind = 10000;
    public const int MaxSlotsPerSkillset = 4096;
    public const int MaxInnatesPerJob = 4096;
    public const int MaxTotalReferences = 100000;
    public const int MaxPackagesPerComposition = 128;
    public const int MaxIdLength = 256;

    public string PackageId { get; }
    public string ProfileId { get; }
    public ContentPackageKind Kind { get; }
    public string? RequiredBaselinePackageId { get; }
    public ImmutableArray<JobDefinition> Jobs { get; }
    public ImmutableArray<SkillsetDefinition> Skillsets { get; }
    public ImmutableArray<AbilityDefinition> Abilities { get; }

    public ContentPackage(string packageId, string profileId, ContentPackageKind kind,
        string? requiredBaselinePackageId, IEnumerable<JobDefinition> jobs,
        IEnumerable<SkillsetDefinition> skillsets, IEnumerable<AbilityDefinition> abilities)
    {
        PackageId = ValidateId(packageId, packageId, null, "$.packageId");
        ProfileId = ValidateId(profileId, PackageId, null, "$.profileId");
        if (!Enum.IsDefined(kind)) throw Error(null, "$.kind", "Unknown package kind.");
        Kind = kind;
        RequiredBaselinePackageId = requiredBaselinePackageId is null ? null :
            ValidateId(requiredBaselinePackageId, PackageId, null, "$.requiredBaselinePackageId");
        if ((kind == ContentPackageKind.Baseline) != (requiredBaselinePackageId is null))
            throw Error(null, "$.requiredBaselinePackageId", "A baseline requires null; an add-on requires an explicit baseline package ID.");
        Jobs = Snapshot(jobs, "$.jobs");
        Skillsets = Snapshot(skillsets, "$.skillsets");
        Abilities = Snapshot(abilities, "$.abilities");
        ValidateLocalRecords();
        if (kind == ContentPackageKind.Baseline)
            ValidateReferences(Skillsets.Select(skillset => skillset.Id).ToHashSet(),
                Abilities.Select(ability => ability.Id).ToHashSet());
    }

    private ImmutableArray<T> Snapshot<T>(IEnumerable<T>? source, string path) where T : class
    {
        if (source is null) throw Error(null, path, "Definition collection is required.");
        var result = ImmutableArray.CreateBuilder<T>();
        foreach (T value in source)
        {
            if (result.Count == MaxDefinitionsPerKind) throw Error(null, path, $"Collection exceeds {MaxDefinitionsPerKind} definitions.");
            if (value is null) throw Error(null, $"{path}[{result.Count}]", "Definition cannot be null.");
            result.Add(value);
        }
        return result.ToImmutable();
    }

    private void ValidateLocalRecords()
    {
        ValidateDeclarations(Jobs.Select(job => job.Id.Value), "jobs");
        ValidateDeclarations(Skillsets.Select(skillset => skillset.Id.Value), "skillsets");
        ValidateDeclarations(Abilities.Select(ability => ability.Id.Value), "abilities");
        int references = Jobs.Length;
        for (int i = 0; i < Jobs.Length; i++)
        {
            JobDefinition job = Jobs[i];
            ValidateId(job.PrimarySkillsetId.Value, PackageId, job.Id.Value, $"$.jobs[{i}].primarySkillsetId");
            if (job.InnateAbilityIds.Length > MaxInnatesPerJob)
                throw Error(job.Id.Value, $"$.jobs[{i}].innateAbilityIds", $"Too many innate references; maximum is {MaxInnatesPerJob}.");
            for (int j = 0; j < job.InnateAbilityIds.Length; j++)
                ValidateId(job.InnateAbilityIds[j].Value, PackageId, job.Id.Value, $"$.jobs[{i}].innateAbilityIds[{j}]");
            references += job.InnateAbilityIds.Length;
        }
        for (int i = 0; i < Skillsets.Length; i++)
        {
            SkillsetDefinition skillset = Skillsets[i];
            if (skillset.Slots.Length > MaxSlotsPerSkillset)
                throw Error(skillset.Id.Value, $"$.skillsets[{i}].slots", $"Too many slots; maximum is {MaxSlotsPerSkillset}.");
            for (int j = 0; j < skillset.Slots.Length; j++)
                if (skillset.Slots[j].AbilityId is { } id)
                    ValidateId(id.Value, PackageId, skillset.Id.Value, $"$.skillsets[{i}].slots[{j}].abilityId");
            references += skillset.Slots.Length;
        }
        if (references > MaxTotalReferences) throw Error(null, "$", $"Package exceeds {MaxTotalReferences} total reference/slot entries.");
    }

    private void ValidateDeclarations(IEnumerable<string> ids, string collection)
    {
        string owner = Namespace(PackageId);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (string id in ids)
        {
            string path = $"$.{collection}[{index++}].id";
            ValidateId(id, PackageId, id, path);
            if (Namespace(id) != owner) throw Error(id, path, $"Definition must belong to package namespace '{owner}'.");
            if (!seen.Add(id)) throw Error(id, path, $"Duplicate definition ID '{id}' in {collection}.");
        }
    }

    private void ValidateReferences(HashSet<SkillsetId> skillsets, HashSet<AbilityId> abilities)
    {
        for (int i = 0; i < Jobs.Length; i++)
        {
            JobDefinition job = Jobs[i];
            if (!skillsets.Contains(job.PrimarySkillsetId))
                throw Error(job.Id.Value, $"$.jobs[{i}].primarySkillsetId", $"Unresolved primary skillset '{job.PrimarySkillsetId}'.");
            for (int j = 0; j < job.InnateAbilityIds.Length; j++)
                if (!abilities.Contains(job.InnateAbilityIds[j]))
                    throw Error(job.Id.Value, $"$.jobs[{i}].innateAbilityIds[{j}]", $"Unresolved innate ability '{job.InnateAbilityIds[j]}'.");
        }
        for (int i = 0; i < Skillsets.Length; i++)
            for (int j = 0; j < Skillsets[i].Slots.Length; j++)
                if (Skillsets[i].Slots[j].AbilityId is { } id && !abilities.Contains(id))
                    throw Error(Skillsets[i].Id.Value, $"$.skillsets[{i}].slots[{j}].abilityId", $"Unresolved ability '{id}'.");
    }

    public static DefinitionCatalog Compose(ContentPackage baseline, params ContentPackage[] addons)
    {
        if (baseline is null) throw new ContentPackageValidationException(null, null, "$", "Baseline package is required.");
        if (baseline.Kind != ContentPackageKind.Baseline) throw baseline.Error(null, "$.kind", "Composition requires an explicit baseline.");
        if (addons is null) throw baseline.Error(null, "$", "Add-on selection cannot be null.");
        if (addons.Length >= MaxPackagesPerComposition)
            throw baseline.Error(null, "$", $"Composition exceeds {MaxPackagesPerComposition} packages including baseline.");
        var packageIds = new HashSet<string>(StringComparer.Ordinal) { baseline.PackageId };
        foreach (ContentPackage addon in addons)
        {
            if (addon is null) throw baseline.Error(null, "$", "Selected add-on cannot be null.");
            if (addon.Kind != ContentPackageKind.Addon) throw addon.Error(null, "$.kind", "Selected package must be an add-on.");
            if (addon.ProfileId != baseline.ProfileId) throw addon.Error(null, "$.profileId", "Add-on profile does not match baseline.");
            if (addon.RequiredBaselinePackageId != baseline.PackageId)
                throw addon.Error(null, "$.requiredBaselinePackageId", "Add-on dependency does not match selected baseline package ID.");
            if (!packageIds.Add(addon.PackageId)) throw addon.Error(null, "$.packageId", "Duplicate selected package ID.");
        }
        ContentPackage[] packages = [baseline, .. addons.OrderBy(addon => addon.PackageId, StringComparer.Ordinal)];
        var jobIds = new HashSet<JobId>();
        var skillsetIds = new HashSet<SkillsetId>();
        var abilityIds = new HashSet<AbilityId>();
        int totalReferences = 0;
        foreach (ContentPackage package in packages)
        {
            AddDeclarations(package, package.Jobs.Select(job => job.Id), jobIds, "jobs");
            AddDeclarations(package, package.Skillsets.Select(skillset => skillset.Id), skillsetIds, "skillsets");
            AddDeclarations(package, package.Abilities.Select(ability => ability.Id), abilityIds, "abilities");
            totalReferences += package.Jobs.Length + package.Jobs.Sum(job => job.InnateAbilityIds.Length) + package.Skillsets.Sum(skillset => skillset.Slots.Length);
            if (totalReferences > MaxTotalReferences) throw package.Error(null, "$", $"Composition exceeds {MaxTotalReferences} reference/slot entries.");
        }
        foreach (ContentPackage package in packages) package.ValidateReferences(skillsetIds, abilityIds);
        return new DefinitionCatalog(packages.SelectMany(package => package.Jobs).OrderBy(job => job.Id.Value, StringComparer.Ordinal),
            packages.SelectMany(package => package.Skillsets).OrderBy(skillset => skillset.Id.Value, StringComparer.Ordinal),
            packages.SelectMany(package => package.Abilities).OrderBy(ability => ability.Id.Value, StringComparer.Ordinal));
    }

    private static void AddDeclarations<T>(ContentPackage package, IEnumerable<T> values, HashSet<T> seen, string collection) where T : notnull
    {
        int index = 0;
        foreach (T id in values)
        {
            if (!seen.Add(id)) throw package.Error(id.ToString(), $"$.{collection}[{index}].id", $"Duplicate selected definition ID '{id}'.");
            if (seen.Count > MaxDefinitionsPerKind) throw package.Error(id.ToString(), $"$.{collection}", $"Composition exceeds {MaxDefinitionsPerKind} {collection}.");
            index++;
        }
    }

    private ContentPackageValidationException Error(string? recordId, string path, string message) => new(PackageId, recordId, path, message);
    private static string Namespace(string id) => id[..id.IndexOf(':')];
    private static string ValidateId(string? value, string? packageId, string? recordId, string path)
    {
        if (value is null || value.Length > MaxIdLength)
            throw new ContentPackageValidationException(packageId, recordId, path, $"Stable ID is required and cannot exceed {MaxIdLength} characters.");
        try { return new AssetId(value).Value; }
        catch (ArgumentException exception)
        {
            throw new ContentPackageValidationException(packageId, recordId, path, "Invalid lowercase 'namespace:path' stable ID.", exception);
        }
    }
}
