using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Infrastructure.Store;
using Aw.CodeIndex.Testing;
using Aw.Kernel.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.CodeIndex.Tests.Store;

/// <summary>What only the SQLite store promises: the files on disk, the pointer, the sweep, and the schema (spec §4.1, §4.2).</summary>
[Lane(TestLane.Integration)]
public sealed partial class SqliteIndexStoreFilesTests : IDisposable
{
    private static readonly RepoName _repo = RepoName.From("fixture");
    private static readonly DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken _none = CancellationToken.None;

    private readonly string _root = Directory.CreateTempSubdirectory("aw-codeindex-files-").FullName;
    private readonly FakeTimeProvider _time = new(_now);

    private string Indexes => Path.Combine(_root, "code-index");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task A_second_build_of_the_repo_waits_until_the_first_is_disposed()
    {
        var store = new SqliteIndexStore(Indexes, _time, new FakeRepoIndexLock());
        var first = (await store.BeginBuild(_repo, incremental: true, _none)).Value;

        var refused = await store.BeginBuild(_repo, incremental: true, _none);
        await first.DisposeAsync();
        var later = await store.BeginBuild(_repo, incremental: true, _none);

        Assert.Equal("SND-CODEIDX-016", refused.Error.Code);
        Assert.True(later.IsSuccess);
        await later.Value.DisposeAsync();
    }

    [Fact]
    public async Task A_published_index_is_one_private_file_and_a_pointer_with_no_sidecars()
    {
        var store = Store();
        await Publish(store);

        var names = Directory.GetFiles(Indexes).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        var ulid = (await File.ReadAllTextAsync(Pointer, _none)).Trim();

        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{26}$", ulid);
        Assert.Equal([$"fixture.{ulid}.db", "fixture.current"], names);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(Indexes, $"fixture.{ulid}.db")));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Pointer));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Indexes));
        }
    }

    [Fact]
    public async Task A_published_file_has_the_schema_version_the_rules_name_and_leaves_wal_mode()
    {
        await Publish(Store());

        Assert.Equal(CodeIndexRules.SchemaVersion, Convert.ToInt32(await Scalar(LiveFile, "PRAGMA user_version"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("delete", await Scalar(LiveFile, "PRAGMA journal_mode"));
    }

    [Fact]
    public async Task The_schema_has_every_table_and_index_of_version_1()
    {
        await Publish(Store());

        var names = new List<string>();
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = LiveFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await using (connection)
        {
            await connection.OpenAsync(_none);
            var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type IN ('table', 'index') AND name NOT LIKE 'sqlite_%' ORDER BY name";
            var reader = await command.ExecuteReaderAsync(_none);
            while (await reader.ReadAsync(_none))
            {
                names.Add(reader.GetString(0));
            }
        }

        Assert.Equal(
            [
                "bands", "bands_key", "bodies", "call_sites", "call_sites_name", "deps", "edges", "edges_dst", "edges_name", "edges_src", "edges_uq",
                "embeddings", "files", "graph_edges", "graph_nodes", "layers", "meta", "symbols", "symbols_file", "symbols_name", "symbols_shape",
            ],
            names);
    }

    [Fact]
    public async Task A_stale_wal_beside_a_published_file_changes_nothing()
    {
        var store = Store();
        await Publish(store);
        await File.WriteAllBytesAsync(LiveFile + "-wal", [.. Enumerable.Repeat((byte)7, 4096)], _none);
        await File.WriteAllBytesAsync(LiveFile + "-shm", [.. Enumerable.Repeat((byte)7, 4096)], _none);

        Assert.NotNull((await store.ReadHeader(_repo, _none)).Value);
        var build = (await store.BeginBuild(_repo, incremental: true, _none)).Value;
        await using (build)
        {
            Assert.True(build.Incremental);
            _ = Assert.Single((await build.StoredFiles(_none)).Value);
        }
    }

    [Fact]
    public async Task A_build_in_progress_is_recorded_and_the_record_goes_with_the_build()
    {
        var store = Store();
        var build = (await store.BeginBuild(_repo, incremental: false, _none)).Value;
        var recorded = (await File.ReadAllTextAsync(Path.Combine(Indexes, "fixture.building"), _none)).Trim();
        Assert.True(File.Exists(Path.Combine(Indexes, $"fixture.{recorded}.db")));
        Assert.False(File.Exists(Pointer));

        await build.DisposeAsync();

        Assert.Empty(Directory.GetFiles(Indexes));
    }

    [Fact]
    public async Task Publishing_removes_the_building_record_and_keeps_the_old_file_for_readers()
    {
        var store = Store();
        await Publish(store);
        var first = LiveFile;

        await Publish(store);

        Assert.NotEqual(first, LiveFile, StringComparer.Ordinal);
        Assert.True(File.Exists(first));
        Assert.False(File.Exists(Path.Combine(Indexes, "fixture.building")));
    }

    [Fact]
    public async Task Reading_never_creates_a_file_or_a_directory()
    {
        var store = Store();

        Assert.Null((await store.ReadHeader(_repo, _none)).Value);
        Assert.True((await store.OpenRead(_repo, _none)).IsError);
        Assert.True((await store.Sweep(_repo, _none)).IsSuccess);

        Assert.False(Directory.Exists(Indexes));
    }

    [Fact]
    public async Task A_duplicate_ref_in_one_write_is_refused_and_writes_nothing()
    {
        var store = Store();
        var build = (await store.BeginBuild(_repo, incremental: false, _none)).Value;
        await using (build)
        {
            var twin = Symbol("src/A.cs", "A.Run");

            var refused = await build.WriteFiles([FileOf("src/A.cs", twin, twin)], _none);

            Assert.Equal("AW-DATA-004", refused.Error.Code);
            Assert.Empty((await build.StoredFiles(_none)).Value);
        }
    }

    private SqliteIndexStore Store() => new(Indexes, _time, new InProcessRepoIndexLock());

    private string Pointer => Path.Combine(Indexes, "fixture.current");

    private string LiveFile => Path.Combine(Indexes, $"fixture.{File.ReadAllText(Pointer).Trim()}.db");

    private static async Task Publish(SqliteIndexStore store)
    {
        var build = (await store.BeginBuild(_repo, incremental: false, _none)).Value;
        await using (build)
        {
            Assert.True((await build.WriteFiles([FileOf("src/A.cs", Symbol("src/A.cs", "A.Run"))], _none)).IsSuccess);
            Assert.True((await build.Publish(new IndexMeta(CommitSha.From(new string('a', 40)), "main", _now, CodeIndexRules.SchemaVersion, "parser-1", LockSha: null, new IndexCounts(0, 0, 0)), _none)).IsSuccess);
        }
    }

    private static async Task<object?> Scalar(string file, string sql)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await using (connection)
        {
            await connection.OpenAsync(_none);
            var command = connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync(_none);
        }
    }

    private static SymbolRecord Symbol(string file, string qualified)
        => new(SymbolRef.From($"fixture:{file}#{qualified}"), qualified[(qualified.LastIndexOf('.') + 1)..], qualified, SymbolKind.Method, Arity: 0, StartLine: 1, EndLine: 5, Exported: true, Utility: false, "void M()", "shape", TokenCount: 10, Complexity: 1, MinHash: default, [], Body: null, []);

    private static FileRecord FileOf(string path, params SymbolRecord[] symbols) => new(path, $"blob-{path}", 10, "csharp", Parsed: true, symbols);
}
