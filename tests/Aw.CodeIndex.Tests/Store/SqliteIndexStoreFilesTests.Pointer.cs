using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Tests.Store;

public sealed partial class SqliteIndexStoreFilesTests
{
    [Theory]
    [InlineData("not a ulid")]
    [InlineData("01JC0000000000000000000000/../x")]
    [InlineData("")]
    public async Task A_pointer_that_is_not_a_ulid_is_damage(string content)
    {
        var store = Store();
        await Publish(store);
        await File.WriteAllTextAsync(Pointer, content, _none);

        Assert.Equal("SND-CODEIDX-010", (await store.ReadHeader(_repo, _none)).Error.Code);
        Assert.Equal("SND-CODEIDX-010", (await store.OpenRead(_repo, _none)).Error.Code);
    }

    [Fact]
    public async Task A_pointer_naming_a_missing_file_is_damage_and_a_build_starts_over()
    {
        var store = Store();
        await Publish(store);
        await File.WriteAllTextAsync(Pointer, OldUlid + "\n", _none);

        Assert.Equal("SND-CODEIDX-010", (await store.ReadHeader(_repo, _none)).Error.Code);
        var build = (await store.BeginBuild(_repo, incremental: true, _none)).Value;
        await using (build)
        {
            Assert.False(build.Incremental);
        }
    }

    [Fact]
    public async Task A_symbolic_link_for_the_pointer_or_the_index_is_damage()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var store = Store();
        await Publish(store);
        var live = LiveFile;
        var target = Path.Combine(_root, "elsewhere.db");
        File.Move(live, target);
        _ = File.CreateSymbolicLink(live, target);
        Assert.Equal("SND-CODEIDX-010", (await store.ReadHeader(_repo, _none)).Error.Code);

        File.Delete(live);
        File.Move(target, live);
        var pointerTarget = Path.Combine(_root, "elsewhere.current");
        File.Move(Pointer, pointerTarget);
        _ = File.CreateSymbolicLink(Pointer, pointerTarget);
        Assert.Equal("SND-CODEIDX-010", (await store.ReadHeader(_repo, _none)).Error.Code);
    }

    [Fact]
    public async Task Another_schema_version_is_damage_to_a_reader_and_a_fresh_start_to_a_build()
    {
        var store = Store();
        await Publish(store);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = LiveFile, Pooling = false }.ToString());
        await using (connection)
        {
            await connection.OpenAsync(_none);
            var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 2";
            _ = await command.ExecuteNonQueryAsync(_none);
        }

        Assert.Equal("SND-CODEIDX-010", (await store.ReadHeader(_repo, _none)).Error.Code);
        var build = (await store.BeginBuild(_repo, incremental: true, _none)).Value;
        await using (build)
        {
            Assert.False(build.Incremental);
            Assert.Null(build.Previous);
            Assert.Empty((await build.StoredFiles(_none)).Value);
        }
    }

    [Fact]
    public async Task A_failed_quick_check_sends_an_incremental_build_back_to_scratch()
    {
        var store = Store();
        await Publish(store);
        var bytes = await File.ReadAllBytesAsync(LiveFile, _none);
        // Page 1 (the schema) and page 2 (the meta table, created first) are intact, so the file opens and reads its header; every table after them is zeroed.
        Array.Clear(bytes, 8192, bytes.Length - 8192);
        await File.WriteAllBytesAsync(LiveFile, bytes, _none);

        var build = (await store.BeginBuild(_repo, incremental: true, _none)).Value;
        await using (build)
        {
            Assert.False(build.Incremental);
        }
    }
}
