using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedAssetManifestTests
{
    [Fact]
    public void Save_and_load_preserves_sorted_manifest_entries()
    {
        using var directory = new TemporaryDirectory();
        var manifest = new PreparedAssetManifest(
        [
            CreateEntry("base:z/last", "Z/LAST.BIN", [3]),
            CreateEntry("base:a/first", "A/FIRST.BIN", [1, 2]),
        ]);
        string path = Path.Combine(directory.Path, PreparedAssetManifest.FileName);

        manifest.SaveToFile(path);
        PreparedAssetManifest loaded = PreparedAssetManifest.LoadFromFile(path);

        Assert.Equal(PreparedAssetManifest.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(new[] { "base:a/first", "base:z/last" }, loaded.Assets.Select(entry => entry.Id.Value));
    }

    [Fact]
    public void Load_rejects_unsupported_schema_version()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, PreparedAssetManifest.FileName);
        File.WriteAllText(path, "{\"schemaVersion\":2,\"assets\":[]}");

        Assert.Contains("Unsupported", Assert.Throws<InvalidDataException>(
            () => PreparedAssetManifest.LoadFromFile(path)).Message);
    }

    [Fact]
    public void Constructor_rejects_duplicate_ids_and_unsafe_paths()
    {
        PreparedAssetEntry first = CreateEntry("base:one", "one.bin", [1]);
        PreparedAssetEntry duplicate = CreateEntry("base:one", "two.bin", [2]);
        Assert.Throws<ArgumentException>(() => new PreparedAssetManifest([first, duplicate]));

        Assert.Throws<ArgumentException>(() => new PreparedAssetManifest(
            [CreateEntry("base:traversal", "../outside.bin", [1])]));
    }

    [Fact]
    public void LooseAssetStore_loads_by_id_and_verifies_size_and_hash()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "BATTLE"));
        byte[] contents = [1, 2, 3, 4];
        File.WriteAllBytes(Path.Combine(directory.Path, "BATTLE", "CYOKO.SPR"), contents);
        var entry = CreateEntry("base:unit/chocobo/sprite", "BATTLE/CYOKO.SPR", contents);
        var manifest = new PreparedAssetManifest([entry]);
        manifest.SaveToFile(Path.Combine(directory.Path, PreparedAssetManifest.FileName));

        byte[] loaded = LooseAssetStore.Load(directory.Path).ReadAllBytes(entry.Id);

        Assert.Equal(contents, loaded);
    }

    [Fact]
    public void LooseAssetStore_rejects_files_that_differ_from_manifest_hash()
    {
        using var directory = new TemporaryDirectory();
        byte[] expected = [1, 2, 3];
        File.WriteAllBytes(Path.Combine(directory.Path, "asset.bin"), [1, 2, 4]);
        var entry = CreateEntry("base:test/asset", "asset.bin", expected);
        var store = new LooseAssetStore(directory.Path, new PreparedAssetManifest([entry]));

        Assert.Contains("SHA-256", Assert.Throws<InvalidDataException>(
            () => store.ReadAllBytes(entry.Id)).Message);
    }

    [Fact]
    public void LooseAssetStore_rejects_unknown_ids()
    {
        var store = new LooseAssetStore("assets", new PreparedAssetManifest([]));

        Assert.Throws<KeyNotFoundException>(() => store.ReadAllBytes(new AssetId("base:missing")));
    }

    [Fact]
    public void Bounded_load_rejects_manifest_budget_before_file_access_and_accepts_exact_limit()
    {
        using var directory = new TemporaryDirectory();
        byte[] contents = [1, 2, 3, 4];
        var entry = CreateEntry("sample:bounded", "asset.bin", contents);
        var store = new LooseAssetStore(directory.Path, new PreparedAssetManifest([entry]));
        // Deliberately missing: budget rejection must happen before file access/allocation.
        Assert.Contains("load budget", Assert.Throws<InvalidDataException>(() => store.ReadAllBytes(entry.Id, 3)).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.ReadAllBytes(entry.Id, -1));
        File.WriteAllBytes(Path.Combine(directory.Path, entry.RelativePath), contents);
        Assert.Equal(contents, store.ReadAllBytes(entry.Id, 4));
    }

    private static PreparedAssetEntry CreateEntry(string id, string path, byte[] contents) =>
        new(new AssetId(id), path, contents.Length, Convert.ToHexStringLower(SHA256.HashData(contents)));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
