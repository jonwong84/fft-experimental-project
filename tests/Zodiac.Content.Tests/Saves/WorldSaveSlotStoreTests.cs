using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Xunit;

namespace Zodiac.Content.Tests.Saves;

public sealed class WorldSaveSlotStoreTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"fft-world-store-"+Guid.NewGuid().ToString("N"));
    private WorldCheckpointService Session(WorldSavePublisher? publisher=null)
    {
        var p=SyntheticWorldCheckpoint.CreateProfile();
        var store=publisher is null?new WorldSaveSlotStore(p,root):new WorldSaveSlotStore(p,root,publisher);
        return new(store,SyntheticWorldCheckpoint.Start(p));
    }
    [Fact]
    public void Empty_catalog_has_ten_known_slots_and_does_not_create_user_directory()
    {
        var service=Session();Assert.Equal(10,service.Store.Catalog().Length);Assert.False(Directory.Exists(root));
        Assert.All(service.Store.Catalog(),s=>Assert.Equal(WorldSaveFileStatus.Empty,s.Primary.Status));
        Assert.Contains("FFTReimplementation",WorldSaveSlotStore.DefaultDirectory());
    }
    [Theory] [InlineData("../slot-01")] [InlineData("slot-01.json")] [InlineData("slot-00")] [InlineData("slot-11")]
    [InlineData("SLOT-01")] [InlineData("C:\\elsewhere")] [InlineData("")] [InlineData("slot-01/child")]
    public void Unsafe_or_unknown_slot_names_publish_nothing(string slot)
    {
        var s=Session();var state=s.Current;Assert.False(s.Save(slot,null).Succeeded);Assert.Same(state,s.Current);Assert.False(Directory.Exists(root));
        Assert.Throws<ArgumentException>(()=>s.Store.Inspect(slot));
    }
    [Fact]
    public void Confirmed_overwrite_retains_valid_backup_and_stale_save_load_or_cancel_preserve_live_state()
    {
        var s=Session();var a=s.Save("slot-01",null);Assert.True(a.Succeeded,a.Diagnostic);byte[] original=File.ReadAllBytes(Path.Combine(root,"slot-01.json"));
        Assert.True(s.Travel(SyntheticWorldCheckpoint.Hill).Accepted);var live=s.Current;
        var b=s.Save("slot-01",a.Revision);Assert.True(b.Succeeded,b.Diagnostic);Assert.NotEqual(a.Revision,b.Revision);
        Assert.Equal(original,File.ReadAllBytes(Path.Combine(root,"slot-01.bak")));
        var catalog=s.Store.Inspect("slot-01");Assert.Equal(93,catalog.Primary.Summary!.Funds);Assert.Equal(100,catalog.Backup.Summary!.Funds);
        Assert.False(s.Save("slot-01",a.Revision).Succeeded);Assert.False(s.Save("slot-01",null).Succeeded);
        Assert.False(s.Load("slot-01",a.Revision!,true).Succeeded);Assert.True(s.Load("slot-01",b.Revision!,false).Canceled);Assert.Same(live,s.Current);
        var fresh=Session();Assert.True(fresh.Load("slot-01",b.Revision!,true).Succeeded);Assert.Equal(93,fresh.Current!.Funds);
        Assert.Empty(Directory.GetFiles(root,"*.tmp"));
    }
    [Fact]
    public void Corrupt_primary_never_falls_back_and_explicit_recovery_preserves_damaged_bytes()
    {
        var s=Session();var a=s.Save("slot-01",null);s.Travel(SyntheticWorldCheckpoint.Hill);Assert.True(s.Save("slot-01",a.Revision).Succeeded);
        var live=s.Current;string path=Path.Combine(root,"slot-01.json");File.WriteAllText(path,"{}");byte[] damaged=File.ReadAllBytes(path);
        var info=s.Store.Inspect("slot-01");Assert.Equal(WorldSaveFileStatus.Invalid,info.Primary.Status);Assert.Null(info.Primary.Summary);
        Assert.False(s.Load("slot-01",info.Primary.Revision!,true).Succeeded);Assert.Same(live,s.Current);
        Assert.False(s.Save("slot-01",info.Primary.Revision).Succeeded);Assert.Equal(damaged,File.ReadAllBytes(path));
        var recovered=s.Load("slot-01",info.Backup.Revision!,true,backup:true);Assert.True(recovered.Succeeded,recovered.Diagnostic);
        Assert.True(recovered.RecoveredFromBackup);Assert.Equal(100,s.Current!.Funds);Assert.Equal(damaged,File.ReadAllBytes(path));
        Assert.True(s.Save("slot-02",null).Succeeded);
        File.WriteAllText(Path.Combine(root,"slot-01.bak"),"{}");var now=s.Current;info=s.Store.Inspect("slot-01");
        Assert.False(s.Load("slot-01",info.Backup.Revision!,true,backup:true).Succeeded);Assert.Same(now,s.Current);
    }
    [Theory] [InlineData("create")] [InlineData("flush")] [InlineData("promotion")] [InlineData("access")]
    public void Injected_publishing_failures_preserve_live_state_primary_backup_and_cleanup(string stage)
    {
        var good=Session();var a=good.Save("slot-01",null);good.Travel(SyntheticWorldCheckpoint.Hill);Assert.True(good.Save("slot-01",a.Revision).Succeeded);
        var before=File.ReadAllBytes(Path.Combine(root,"slot-01.json"));var backup=File.ReadAllBytes(Path.Combine(root,"slot-01.bak"));
        var broken=Session(new FailingPublisher(stage));var live=broken.Current;
        Assert.False(broken.Save("slot-01",good.Store.Inspect("slot-01").Primary.Revision).Succeeded);
        Assert.Same(live,broken.Current);Assert.Equal(before,File.ReadAllBytes(Path.Combine(root,"slot-01.json")));
        Assert.Equal(backup,File.ReadAllBytes(Path.Combine(root,"slot-01.bak")));Assert.Empty(Directory.GetFiles(root,"*.tmp"));
    }
    [Fact]
    public void Serialization_failure_and_busy_lock_preserve_files_and_session()
    {
        var s=Session();var first=s.Save("slot-01",null);var before=File.ReadAllBytes(Path.Combine(root,"slot-01.json"));var live=s.Current;
        var foreign=SyntheticWorldCheckpoint.CreateProfile();
        Assert.Throws<WorldSaveValidationException>(()=>s.Store.Save("slot-01",SyntheticWorldCheckpoint.Start(foreign),first.Revision));
        using(var locked=new FileStream(Path.Combine(root,"slot-01.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {Assert.False(s.Save("slot-01",first.Revision).Succeeded);Assert.False(s.Load("slot-01",first.Revision!,true).Succeeded);Assert.Equal(WorldSaveFileStatus.Unavailable,s.Store.Inspect("slot-01").Primary.Status);}
        Assert.Same(live,s.Current);Assert.Equal(before,File.ReadAllBytes(Path.Combine(root,"slot-01.json")));Assert.Empty(Directory.GetFiles(root,"*.tmp"));
    }
    [Fact]
    public async Task Two_cooperating_concurrent_writers_cannot_both_overwrite_the_same_revision()
    {
        var seed=Session();var first=seed.Save("slot-01",null);var a=Session();var b=Session();
        a.Travel(SyntheticWorldCheckpoint.Hill);b.Travel(SyntheticWorldCheckpoint.Hill);
        using var gate=new ManualResetEventSlim();
        var left=Task.Run(()=>{gate.Wait();return a.Save("slot-01",first.Revision);});
        var right=Task.Run(()=>{gate.Wait();return b.Save("slot-01",first.Revision);});gate.Set();
        var results=await Task.WhenAll(left,right).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(results,r=>r.Succeeded);Assert.Equal(93,seed.Store.Inspect("slot-01").Primary.Summary!.Funds);
    }
    [Fact]
    public void Windows_delete_sharing_failure_preserves_flushed_primary_and_backup()
    {
        if(!OperatingSystem.IsWindows())return;
        var s=Session();var first=s.Save("slot-01",null);byte[] before=File.ReadAllBytes(Path.Combine(root,"slot-01.json"));
        s.Travel(SyntheticWorldCheckpoint.Hill);
        using(var held=new FileStream(Path.Combine(root,"slot-01.json"),FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.False(s.Save("slot-01",first.Revision).Succeeded);
        Assert.Equal(before,File.ReadAllBytes(Path.Combine(root,"slot-01.json")));Assert.Empty(Directory.GetFiles(root,"*.tmp"));
    }
    [Fact]
    public void Legacy_slot_reports_migration_and_invalid_oversized_or_wrong_profile_slots_publish_no_summary()
    {
        Directory.CreateDirectory(root);File.Copy(Path.Combine(AppContext.BaseDirectory,"Fixtures","Saves","synthetic-world-idle-v1.json"),Path.Combine(root,"slot-01.json"));
        var s=Session();var info=s.Store.Inspect("slot-01");Assert.Equal(1,info.Primary.SourceSchemaVersion);
        Assert.True(s.Load("slot-01",info.Primary.Revision!,true).WasMigrated);
        File.WriteAllBytes(Path.Combine(root,"slot-02.json"),new byte[65537]);Assert.Null(s.Store.Inspect("slot-02").Primary.Summary);
        var profile=s.Store.Profile;var wrong=new WorldSaveSlotStore(new("sample:profile/wrong",1,1,profile.Definition),root);
        Assert.Equal(WorldSaveFileStatus.Invalid,wrong.Inspect("slot-01").Primary.Status);
    }
    private sealed class FailingPublisher(string stage) : WorldSavePublisher
    {
        internal override FileStream Create(string path)
        {if(stage=="access")throw new UnauthorizedAccessException("injected access denial");if(stage=="create")throw new IOException("injected create failure");return base.Create(path);}
        internal override void Flush(FileStream file)
        {if(stage=="flush")throw new IOException("injected flush failure");base.Flush(file);}
        internal override void Promote(string temporary,string destination,string backup,bool exists)
        {if(stage=="promotion")throw new IOException("injected promotion failure");base.Promote(temporary,destination,backup,exists);}
    }
    [Fact]
    public void File_at_storage_root_reports_unavailable_and_save_preserves_session_and_file()
    {
        File.WriteAllText(root,"reserved");var s=Session();var live=s.Current;
        Assert.Equal(WorldSaveFileStatus.Unavailable,s.Store.Inspect("slot-01").Primary.Status);
        Assert.False(s.Save("slot-01",null).Succeeded);Assert.Same(live,s.Current);Assert.Equal("reserved",File.ReadAllText(root));
    }
    [Fact]
    public void Malformed_revision_and_absent_live_session_publish_nothing()
    {
        var s=Session();var live=s.Current;
        Assert.False(s.Save("slot-01","not-a-revision").Succeeded);
        Assert.False(s.Load("slot-01","not-a-revision",true).Succeeded);Assert.Same(live,s.Current);
        var empty=new WorldCheckpointService(s.Store);Assert.False(empty.Save("slot-01",null).Succeeded);Assert.Null(empty.Current);
        Assert.False(Directory.Exists(root));
    }
    public void Dispose()
    {
        string path=Path.GetFullPath(root),temp=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))+Path.DirectorySeparatorChar;
        if(!path.StartsWith(temp,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)||!Path.GetFileName(path).StartsWith("fft-world-store-",StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected test directory.");
        if(Directory.Exists(path))Directory.Delete(path,true);
        else if(File.Exists(path))File.Delete(path);
    }
}
