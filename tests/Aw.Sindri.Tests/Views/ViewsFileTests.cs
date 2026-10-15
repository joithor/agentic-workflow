using Aw.Kernel.Data;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Views;

public sealed class ViewsFileTests : IDisposable
{
    private readonly TempHome _home = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Views_db_is_0600_and_reads_never_create_it()
    {
        using var views = new SqliteViews(Path.Combine(_home.Path, "home"));

        Assert.Null((await views.Reader.State(Token)).Value);
        Assert.Empty((await views.Reader.List<ServiceRow>(ViewNames.Services, Token)).Value);
        Assert.False(File.Exists(views.Path));
        _ = (await views.Store.Open(ViewSchema.Version, Token)).Value;

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(views.Path));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_views_db_the_projector_created_but_hasnt_migrated_yet_reads_as_empty()
    {
        using var views = new SqliteViews(Path.Combine(_home.Path, "home"));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(views.Path)!);
        await File.WriteAllBytesAsync(views.Path, [], Token);   // the window between the projector creating the file and its migration committing

        var state = await views.Reader.State(Token);
        var rows = await views.Reader.List<ServiceRow>(ViewNames.Services, Token);
        var row = await views.Reader.Get<ServiceRow>(ViewNames.Services, "any", Token);

        Assert.Null(state.Value);
        Assert.Empty(rows.Value);
        Assert.Null(row.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_views_migration_creates_the_state_and_one_table_per_view()
    {
        var path = Path.Combine(_home.Path, "steps.db");
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(path)), new FakeTimeProvider(), backupBeforeUpgrade: false);

        var migrated = await runner.Migrate(ViewsDatabase.Migrations, Token);

        Assert.True(migrated.IsSuccess, migrated.IsError ? migrated.Error.Message : null);
        Assert.Equal(["projector_state", "view_profile", "view_services"], Tables(path));
    }

    private static List<string> Tables(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT IN ('schema_version', 'sqlite_sequence') ORDER BY name";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
