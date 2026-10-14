namespace Aw.Context.Infrastructure.Store;

/// <summary>Where the engine keeps its state (SettingsFromEnvironment).</summary>
/// <param name="Home">The context directory (0700): context.db and its -wal and -shm files (0600).</param>
internal sealed record ContextStoreSettings(string Home)
{
    /// <summary>The store.</summary>
    public string DatabasePath => Path.Combine(Home, "context.db");

    /// <summary>The only folder map exports are written to (S-3a): 0700, files 0600.</summary>
    public string ExportsDirectory => Path.Combine(Home, "exports");
}
