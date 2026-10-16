namespace Aw.Context.Infrastructure.Store;

/// <summary>Where the engine keeps its state (SettingsFromEnvironment).</summary>
/// <param name="Home">The context directory (0700): context.db and its -wal and -shm files (0600).</param>
internal sealed record ContextStoreSettings(string Home)
{
    /// <summary>The store.</summary>
    public string DatabasePath => Path.Combine(Home, "context.db");

    /// <summary>The only folder map exports are written to (S-3a): 0700, files 0600.</summary>
    public string ExportsDirectory => Path.Combine(Home, "exports");

    /// <summary>The eval folder (spec §9.1): 0700.</summary>
    public string EvalDirectory => Path.Combine(Home, "eval");

    /// <summary>Where probe runs keep their reports (S-3b): 0700, files 0600.</summary>
    public string ProbesDirectory => Path.Combine(EvalDirectory, "probes");

    /// <summary>Where backtest reports are kept (S-7A): 0700, files 0600.</summary>
    public string BacktestDirectory => Path.Combine(EvalDirectory, "backtest");

    /// <summary>Where map-quality reports are kept (S-7A): 0700, files 0600.</summary>
    public string MapQualityDirectory => Path.Combine(EvalDirectory, "map");

    /// <summary>Where a backtest caches its as-of maps (S-7A): 0700, files 0600.</summary>
    public string MapCacheDirectory => Path.Combine(EvalDirectory, "cache", "maps");

    /// <summary>The probe file 'context eval probes' reads when none is named.</summary>
    public string DefaultProbeFile => Path.Combine(EvalDirectory, "probes.json");
}
