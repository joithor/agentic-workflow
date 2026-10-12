namespace Aw.Context.Infrastructure;

/// <summary>Context's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Context keeps its state: $AW_SINDRI_HOME/context (spec §3).</param>
internal sealed record ContextSettings(string StateDirectory)
{
    /// <summary>The store, context.db.</summary>
    public string DatabasePath => Path.Combine(StateDirectory, "context.db");

    /// <summary>The only folder exports are written to.</summary>
    public string ExportsDirectory => Path.Combine(StateDirectory, "exports");
}
