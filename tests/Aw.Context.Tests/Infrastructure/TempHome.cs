
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Store;
using Aw.Redaction;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>A throwaway Sindri home with the real store, git and scrubber over it; Dispose deletes it.</summary>
internal sealed class TempHome : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-ctx-home-").FullName;

    public TempHome()
    {
        Settings = SettingsFromEnvironment.Read(name => name == "AW_SINDRI_HOME" ? _root : null);
        Database = new ContextDatabase(Settings, Clock);
        Index = new SqliteDocIndex(Database, Git, Scrubber, Clock);
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));

    public ContextStoreSettings Settings { get; }

    public ContextDatabase Database { get; }

    public GitCli Git { get; } = new();

    public RedactionScrubber Scrubber { get; } = new(Redactor.Default);

    public SqliteDocIndex Index { get; }

    public void Dispose()
    {
        Database.Dispose();
        Directory.Delete(_root, recursive: true);
    }
}
