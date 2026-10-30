using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;

namespace Zodiac.Content.Authoring;

/// <summary>A single-job authoring draft over an immutable, explicitly selected baseline.</summary>
public sealed class JobEditorDocument
{
    private JsonObject? draft;
    private string? cleanDraft;
    public ContentPackage Source { get; }
    public string? SourcePath { get; }
    public string SourceSha256 { get; }
    public JobDefinition? SourceJob { get; private set; }
    public bool HasDraft => draft is not null;
    public bool IsDirty => draft is not null && draft.ToJsonString() != cleanDraft;
    public string? ExportPath { get; private set; }
    public string PackageId => draft?["packageId"]!.GetValue<string>() ?? "";
    public string JobId => JobNode?["id"]!.GetValue<string>() ?? "";
    public string MoveText => Text(JobNode?["baseMove"]);
    public string JumpText => Text(JobNode?["baseJump"]);
    private JsonObject? JobNode => draft?["jobs"]?.AsArray()[0]?.AsObject();

    public JobEditorDocument(ContentPackage source) : this(source, null, null) { }

    private JobEditorDocument(ContentPackage source, string? path, string? hash)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Kind != ContentPackageKind.Baseline)
            throw new ArgumentException("Open a baseline package before creating an authored copy.", nameof(source));
        ContentPackage.Compose(source);
        Source = source;
        SourcePath = path;
        SourceSha256 = hash ?? Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source.ToJson())));
    }

    public static JobEditorDocument OpenSource(string path)
    {
        string full = Path.GetFullPath(path);
        RejectReparsePath(full);
        // Hold a read-only sharing lease across both parsing and hashing.
        using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        ContentPackage source = ContentPackage.Load(full);
        return new(source, full, Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    public ImmutableArray<JobDefinition> Search(string query) => Source.Jobs
        .Where(job => job.Id.Value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
        .OrderBy(job => job.Id.Value, StringComparer.Ordinal).ToImmutableArray();

    /// <summary>Call only after the host has resolved unsaved edits; failures retain the old draft.</summary>
    public void CreateCopy(JobId sourceJobId, string packageId)
    {
        JobDefinition job = ContentPackage.Compose(Source).GetJob(sourceJobId);
        ContentPackage copy = Copy(job, packageId);
        ContentPackage.Compose(Source, copy);
        SetDraft(job, copy, null, dirty: true);
    }

    private ContentPackage Copy(JobDefinition job, string packageId)
    {
        // Validate identity using the production package API before deriving any IDs.
        _ = new ContentPackage(packageId, Source.ProfileId, ContentPackageKind.Addon, Source.PackageId, [], [], []);
        string owner = packageId.Split(':')[0];
        if (owner == Source.PackageId.Split(':')[0])
            throw new ContentPackageValidationException(packageId, null, "$.packageId", "Use a distinct authored namespace.");
        string Remap(string id) => owner + id[id.IndexOf(':')..];
        SkillsetDefinition skillset = Source.Skillsets.Single(value => value.Id == job.PrimarySkillsetId);
        var referenced = skillset.Slots.Where(slot => slot.AbilityId.HasValue).Select(slot => slot.AbilityId!.Value)
            .Concat(job.InnateAbilityIds).ToHashSet();
        var copy = new ContentPackage(packageId, Source.ProfileId, ContentPackageKind.Addon, Source.PackageId,
            [new(new(Remap(job.Id.Value)), new(Remap(skillset.Id.Value)), job.BaseMove, job.BaseJump,
                job.SteppingStone, job.InnateAbilityIds.Select(id => new AbilityId(Remap(id.Value))))],
            [new(new(Remap(skillset.Id.Value)), skillset.Slots.Select(slot => new SkillsetSlot(slot.Slot,
                slot.AbilityId is { } id ? new AbilityId(Remap(id.Value)) : null)))],
            Source.Abilities.Where(ability => referenced.Contains(ability.Id)).Select(ability => new AbilityDefinition(
                new(Remap(ability.Id.Value)), ability.JpCost, ability.LearnOnHitRateRaw, ability.TypeFlagsRaw, ability.AiFlagsRaw)));
        // Round-trip enforces wire budgets and field-path diagnostics on generated identities too.
        return ContentPackage.ReadJson(copy.ToJson());
    }

    public ImmutableArray<EditorAbility> Abilities => draft is null ? [] : draft["abilities"]!.AsArray()
        .Select(node => new EditorAbility(node!["id"]!.GetValue<string>(), Text(node["jpCost"]))).ToImmutableArray();

    /// <summary>Browse learning records without validating or replacing an unfinished job/ability draft.</summary>
    public ImmutableArray<EditorAbility> BrowseAbilities(string query, bool authored)
    {
        ArgumentNullException.ThrowIfNull(query);
        IEnumerable<EditorAbility> records = authored ? Abilities : Source.Abilities.Select(ability =>
            new EditorAbility(ability.Id.Value, ability.JpCost.ToString(CultureInfo.InvariantCulture)));
        return records.Where(ability => ability.Id.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(ability => ability.Id, StringComparer.Ordinal).ToImmutableArray();
    }

    public void EditMovement(string move, string jump)
    {
        RequireDraft();
        JobNode!["baseMove"] = NumberOrText(move);
        JobNode["baseJump"] = NumberOrText(jump);
    }

    public void EditJp(string id, string jp)
    {
        RequireDraft();
        JsonNode ability = draft!["abilities"]!.AsArray().Single(node => node!["id"]!.GetValue<string>() == id)!;
        ability["jpCost"] = NumberOrText(jp);
    }

    public ContentPackage Validate()
    {
        RequireDraft();
        ContentPackage package = ContentPackage.ReadJson(draft!.ToJsonString());
        ContentPackage.Compose(Source, package);
        return package;
    }

    public void Export(string path)
    {
        ContentPackage package = Validate();
        string full = CheckExportPath(path);
        package.Save(full);
        // A failed validation/write never clears the dirty marker or export identity.
        cleanDraft = draft!.ToJsonString();
        ExportPath = full;
    }

    /// <summary>Reopen only this editor's supported copy shape; arbitrary packages remain readable via other tools.</summary>
    public void OpenCopy(JobId sourceJobId, string path)
    {
        ContentPackage package = ContentPackage.Load(path);
        ContentPackage.Compose(Source, package);
        JobDefinition job = Source.Jobs.Single(value => value.Id == sourceJobId);
        ContentPackage template = Copy(job, package.PackageId);
        JsonObject expected = JsonNode.Parse(template.ToJson())!.AsObject();
        if (package.Jobs.Length != 1) throw ShapeError();
        expected["jobs"]![0]!["baseMove"] = package.Jobs[0].BaseMove;
        expected["jobs"]![0]!["baseJump"] = package.Jobs[0].BaseJump;
        foreach (JsonNode? node in expected["abilities"]!.AsArray())
        {
            AbilityDefinition? ability = package.Abilities.FirstOrDefault(value => value.Id.Value == node!["id"]!.GetValue<string>());
            if (ability is null) throw ShapeError();
            node!["jpCost"] = ability.JpCost;
        }
        if (ContentPackage.ReadJson(expected.ToJsonString()).ToJson() != package.ToJson()) throw ShapeError();
        SetDraft(job, package, Path.GetFullPath(path), dirty: false);
    }

    private static ContentPackageValidationException ShapeError() => new(null, null, "$",
        "This editor reopens one copied job and its unchanged dependency graph. Select the matching source job; only Move, Jump and JP edits are supported.");

    private void SetDraft(JobDefinition job, ContentPackage package, string? path, bool dirty)
    {
        JsonObject next = JsonNode.Parse(package.ToJson())!.AsObject();
        SourceJob = job;
        draft = next;
        cleanDraft = dirty ? null : next.ToJsonString();
        ExportPath = path;
    }

    private string CheckExportPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (SourcePath is { } source)
        {
            string directory = Path.GetDirectoryName(source)!;
            string relative = Path.GetRelativePath(directory, full);
            if (relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relative))
                throw new IOException("Export to a separate authored directory; the source directory is read-only.");
        }
        RejectReparsePath(full);
        return full;
    }

    private static void RejectReparsePath(string full)
    {
        // Reject junctions/symlinks including ancestors, before opening a source or writing an export.
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Editor source/export paths cannot contain reparse points.");
        }
    }

    public EditorMovementPreview Preview()
    {
        ContentPackage package = Validate();
        JobDefinition original = SourceJob!;
        if (original.SteppingStone) throw new NotSupportedException("Stepping-stone movement is unsupported by the existing base-job preview.");
        var surface = new TerrainSurfaceId("editor:terrain/flat");
        var map = new BattleMapDefinition(9, 9, 1, Enumerable.Repeat(new BattleTile(surface, 0), 81).ToArray());
        var unit = new BattleUnitId("editor:unit/preview");
        ImmutableArray<TilePosition> Range(DefinitionCatalog catalog, JobId id)
        {
            var setup = BaseJobBattleSetup.Create(catalog, map,
                [new(unit, id, new(4, 4, 0), 6, [new(surface, new(1))])], new FftRandomState(1, 0));
            return Enumerable.Range(0, 81).Select(index => new TilePosition(index % 9, index / 9, 0))
                .Where(position => BattleSimulator.Apply(setup.InitialState, new MoveUnitCommand(unit, position),
                    BattleMovementPolicy.TerrainCostPrototype).Accepted).ToImmutableArray();
        }
        return new(original.BaseMove, package.Jobs[0].BaseMove, package.Jobs[0].BaseJump,
            Range(ContentPackage.Compose(Source), original.Id), Range(ContentPackage.Compose(Source, package), package.Jobs[0].Id));
    }

    private void RequireDraft()
    {
        if (draft is null) throw new InvalidOperationException("Create or reopen an authored copy first.");
    }
    private static JsonNode NumberOrText(string value) => int.TryParse(value.Trim(), NumberStyles.AllowLeadingSign,
        CultureInfo.InvariantCulture, out int number) ? JsonValue.Create(number)! : JsonValue.Create(value)!;
    private static string Text(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text)
        ? text : value?.ToJsonString() ?? "";
}

public sealed record EditorAbility(string Id, string JpCost);
public sealed record EditorMovementPreview(int SourceMove, int AuthoredMove, int AuthoredJump,
    ImmutableArray<TilePosition> SourceReachable, ImmutableArray<TilePosition> AuthoredReachable);
