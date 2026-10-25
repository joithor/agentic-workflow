using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Store;

public sealed partial class SqliteIndexStoreFilesTests
{
    private const string OldUlid = "01JC0000000000000000000000";
    private const string OtherUlid = "01JC0000000000000000000001";

    [Fact]
    public async Task Sweep_deletes_old_leftovers_with_their_sidecars_and_nothing_else()
    {
        var store = Store();
        await Publish(store);
        var leftover = Leave($"fixture.{OldUlid}.db", old: true);
        var leftoverWal = Leave($"fixture.{OldUlid}.db-wal", old: true);
        var leftoverShm = Leave($"fixture.{OldUlid}.db-shm", old: true);
        var young = Leave($"fixture.{OtherUlid}.db", old: false);
        var notAnIndex = Leave("fixture.notes.db", old: true);
        var otherRepo = Leave($"fixture.extra.{OldUlid}.db", old: true);
        var oldLive = LiveFile;
        File.SetLastWriteTimeUtc(oldLive, _now.UtcDateTime.AddHours(-3));

        Assert.True((await store.Sweep(_repo, _none)).IsSuccess);

        Assert.False(File.Exists(leftover) || File.Exists(leftoverWal) || File.Exists(leftoverShm));
        Assert.True(File.Exists(young), "a file younger than 10 minutes may still be open in a reader");
        Assert.True(File.Exists(notAnIndex), "the pattern is exact: a name that is not a ULID is not an index file");
        Assert.True(File.Exists(otherRepo), "another repo's files, even one whose name starts with this repo's, are not touched");
        Assert.True(File.Exists(oldLive), "the file the pointer names is never swept");
    }

    [Fact]
    public async Task Sweep_keeps_the_running_builds_file_however_old_and_removes_superseded_ones()
    {
        var store = Store();
        await Publish(store);
        var superseded = LiveFile;
        await Publish(store);
        File.SetLastWriteTimeUtc(superseded, _now.UtcDateTime.AddHours(-1));
        var running = Leave($"fixture.{OldUlid}.db", old: true);
        await File.WriteAllTextAsync(Path.Combine(Indexes, "fixture.building"), OldUlid + "\n", _none);

        Assert.True((await store.Sweep(_repo, _none)).IsSuccess);

        Assert.False(File.Exists(superseded));
        Assert.True(File.Exists(running));
    }

    [Fact]
    public async Task A_repo_name_with_a_dot_sweeps_only_its_own_files()
    {
        var store = Store();
        var dotted = RepoName.From("a.b");
        var build = (await store.BeginBuild(dotted, incremental: false, _none)).Value;
        await build.DisposeAsync();
        var short_ = Leave($"a.{OldUlid}.db", old: true);
        var own = Leave($"a.b.{OldUlid}.db", old: true);

        Assert.True((await store.Sweep(dotted, _none)).IsSuccess);

        Assert.False(File.Exists(own));
        Assert.True(File.Exists(short_), "repo 'a' is another repo");
    }

    private string Leave(string name, bool old)
    {
        _ = Directory.CreateDirectory(Indexes);
        var path = Path.Combine(Indexes, name);
        File.WriteAllText(path, "leftover");
        File.SetLastWriteTimeUtc(path, old ? _now.UtcDateTime.AddHours(-1) : _now.UtcDateTime.AddMinutes(-5));
        return path;
    }
}
