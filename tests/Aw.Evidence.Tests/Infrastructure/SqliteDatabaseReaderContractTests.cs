using System.Security.Cryptography;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Infrastructure.Databases;
using Aw.Evidence.Testing;
using Aw.Kernel.Data;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The SQLite reader, called directly (no pre-run check in front of it), against real files in a temp directory.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteDatabaseReaderContractTests : ReadOnlyDatabaseContract, IDisposable
{
    private readonly TempDirectory _root = new();

    internal static async Task MakeNotesAsync(string path)
    {
        var opened = await new SqliteConnectionFactory(new SqliteOptions(path)).Open(TestContext.Current.CancellationToken);
        await using var connection = opened.Value;
        var command = connection.CreateCommand();
        await using var scope = command.ConfigureAwait(false);
        command.CommandText = "CREATE TABLE notes(id INTEGER PRIMARY KEY, text TEXT); INSERT INTO notes(text) VALUES ('hello'), ('world'), ('again')";
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    protected override async Task<DatabaseUnderTest> ArrangeAsync()
    {
        var source = _root.Combine("repo/data/notes.db");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        await MakeNotesAsync(source);
        var work = _root.Combine("run/db");
        return new DatabaseUnderTest(new SqliteDatabaseReader(), new SqliteTarget(source, work), () => Task.FromResult(Snapshot(_root.Combine("repo"), _root.Combine("run"))));
    }

    protected override IReadOnlyList<(string Name, string Sql)> Bypasses() =>
    [
        ("a writing CTE", "WITH x AS (SELECT 'w' AS t) INSERT INTO notes(text) SELECT t FROM x"),
        ("a PRAGMA assignment", "PRAGMA user_version = 9"),
        ("PRAGMA called as a function", "PRAGMA user_version(9)"),
        ("ATTACH of a new file", $"ATTACH '{_root.Combine("run/new.db")}' AS n"),
        ("VACUUM INTO a new file", $"VACUUM INTO '{_root.Combine("run/vacuumed.db")}'"),
        ("a delete", "DELETE FROM notes"),
        ("DDL", "CREATE TABLE t(a)"),
    ];

    [Fact]
    public async Task The_source_file_is_never_changed_and_the_copy_lives_in_the_work_directory()
    {
        var db = await ArrangeAsync();
        var source = ((SqliteTarget)db.Target).SourcePath;
        var before = Sha(source);

        await using (var session = (await db.Reader.OpenAsync(db.Target, TestContext.Current.CancellationToken)).Value)
        {
            _ = await session.QueryAsync(ReadAll, 10, TestContext.Current.CancellationToken);
            _ = await session.QueryAsync("DELETE FROM notes", 10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(before, Sha(source));
        Assert.True(File.Exists(_root.Combine("run/db/evidence-copy.sqlite")));
    }

    [Fact]
    public async Task A_seed_that_attaches_the_source_or_vacuums_into_a_file_outside_the_run_is_refused_and_touches_nothing()
    {
        var db = await ArrangeAsync();
        var source = ((SqliteTarget)db.Target).SourcePath;
        var outside = _root.Combine("repo/seed-vacuum.db");
        var seed = $"ATTACH '{source}' AS src;\nINSERT INTO src.notes(text) VALUES ('x');\nDETACH src;\nVACUUM INTO '{outside}';\n";
        var before = Sha(source);

        var opened = await new SqliteDatabaseReader().OpenAsync(((SqliteTarget)db.Target) with { SeedSql = seed }, TestContext.Current.CancellationToken);

        Assert.True(opened.IsError);
        Assert.Equal(Core.EvidenceErrors.DatabaseRefused.Code, opened.Error.Code);
        Assert.Equal(before, Sha(source));
        Assert.False(File.Exists(outside));
    }

    [Theory]
    [InlineData("ATTACH ':memory:' AS m")]
    [InlineData("DETACH m")]
    [InlineData("VACUUM")]
    [InlineData("SELECT load_extension('x')")]
    public async Task A_seed_statement_that_reaches_another_file_is_refused(string seed)
    {
        var db = await ArrangeAsync();

        var opened = await new SqliteDatabaseReader().OpenAsync(((SqliteTarget)db.Target) with { SeedSql = seed }, TestContext.Current.CancellationToken);

        Assert.True(opened.IsError);
        Assert.Equal(Core.EvidenceErrors.DatabaseRefused.Code, opened.Error.Code);
    }

    [Fact]
    public async Task A_missing_source_is_unavailable_and_creates_no_file_there()
    {
        var missing = _root.Combine("repo/none.db");
        _ = Directory.CreateDirectory(_root.Combine("repo"));

        var opened = await new SqliteDatabaseReader().OpenAsync(new SqliteTarget(missing, _root.Combine("run/db")), TestContext.Current.CancellationToken);

        Assert.Equal(Core.EvidenceErrors.DatabaseUnavailable.Code, opened.Error.Code);
        Assert.False(File.Exists(missing));
    }

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    // The main files and the names of everything else beside them; SQLite's -wal and -shm files come and go with a connection.
    private static string Snapshot(params string[] directories) => string.Join(
        '\n',
        directories.Where(Directory.Exists).SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
            .Where(static f => !f.EndsWith("-wal", StringComparison.Ordinal) && !f.EndsWith("-shm", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).Select(static f => f + " " + Sha(f)));

    public void Dispose() => _root.Dispose();
}
