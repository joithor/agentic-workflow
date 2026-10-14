using Aw.Context.Infrastructure.Store;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Tests.Infrastructure;

public sealed class ContextDatabaseTests
{
    private static readonly string[] _suffixes = ["", "-wal", "-shm"];

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_store_migrates_with_fts5_and_its_files_are_owner_only()
    {
        using var home = new TempHome();

        Assert.Null((await home.Database.Readable(CancellationToken.None)).Value);   // nothing refreshed yet: no file is made
        var sessions = (await home.Database.Writable(CancellationToken.None)).Value;
        var session = (await sessions.BeginSession(CancellationToken.None)).Value;
        await using (session)
        {
            _ = await SqliteQueries.Execute(session, new SqlStatement("INSERT INTO doc (repo, path, blob, title, blocked) VALUES ('r', 'p', 'b', 't', 0)"), CancellationToken.None);
            _ = await SqliteQueries.Execute(session, new SqlStatement("INSERT INTO doc_chunk (repo, path, blob, line_start, line_end, text) VALUES ('r', 'p', 'b', 1, 2, 'split pane orientation')"), CancellationToken.None);
            _ = await session.Commit(CancellationToken.None);
        }

        home.Database.RestrictFiles();
        await using var connection = new SqliteConnection("Data Source=" + home.Settings.DatabasePath + ";Mode=ReadOnly");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var match = connection.CreateCommand();
        match.CommandText = "SELECT count(*) FROM doc_chunk_fts WHERE doc_chunk_fts MATCH '\"orientation\"'";
        Assert.Equal(1L, await match.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(home.Settings.Home));
        foreach (var file in _suffixes.Select(s => home.Settings.DatabasePath + s).Where(File.Exists))
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }

        Assert.Equal(ContextDatabase.Migrations.Count, (await new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(home.Settings.DatabasePath)), home.Clock)
            .Migrate(ContextDatabase.Migrations, CancellationToken.None)).Value.ToVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_migrations_are_named_for_their_stage() =>
        Assert.All(ContextDatabase.Migrations, m => Assert.Matches("^s[0-9][a-z0-9]{0,3}_", m.Name));
}
