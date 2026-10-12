using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

public sealed class GraphStoreFileTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task The_store_and_its_wal_and_shm_are_0600_in_a_0700_folder()
    {
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        using var database = new ContextDatabase(settings, new FakeTimeProvider());

        _ = await new SqliteGraphStore(database).SaveCurrent(SampleMap.Build(), SampleMap.At, TestContext.Current.CancellationToken);

        Assert.Equal(settings.DatabasePath, Path.Combine(_home.Root, "context", "context.db"));
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(settings.Home));
        foreach (var file in new[] { settings.DatabasePath, settings.DatabasePath + "-wal", settings.DatabasePath + "-shm" }.Where(File.Exists))
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_read_never_creates_the_store()
    {
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        using var database = new ContextDatabase(settings, new FakeTimeProvider());

        var current = await new SqliteGraphStore(database).Current(TestContext.Current.CancellationToken);

        Assert.Null(current.Value);
        Assert.False(File.Exists(settings.DatabasePath));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_graph_tables_are_migration_0002_after_the_repo_docs_one() =>
        Assert.Equal("1 s1a_repo_docs, 2 s3a_graph", string.Join(", ", ContextDatabase.Migrations.Select(m => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{m.Version} {m.Name}"))));

    [Fact, Trait("Lane", "integration")]
    public async Task The_fixture_map_round_trips_through_yaml_the_store_and_back()
    {
        var path = FixtureMap.WriteTo(_home.Root);
        var file = await new MapFileReader(FixtureMap.Json).Read(path, TestContext.Current.CancellationToken);
        var map = Maps.Checked(file.Value.Document);
        using var database = new ContextDatabase(SettingsFromEnvironment.Read(_home.Environment), new FakeTimeProvider());
        var store = new SqliteGraphStore(database);

        var saved = await store.SaveCurrent(map, SampleMap.At, TestContext.Current.CancellationToken);
        var read = await store.Read(saved.Value.Number, TestContext.Current.CancellationToken);

        Assert.Equal(Canonical.Of(map.Graph), Canonical.Of(read.Value));
    }
}
