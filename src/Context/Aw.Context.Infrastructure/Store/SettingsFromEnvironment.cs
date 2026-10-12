namespace Aw.Context.Infrastructure.Store;

/// <summary>
/// Reads this package's settings from the environment Composition passes in. The engine's state lives in Sindri's app
/// home (context spec §3): $AW_SINDRI_HOME/context, else $AW_STATE_DIR/sindri-app/context, else
/// ~/.agentic-workflow/sindri-app/context; never the TS sindri's $AW_STATE_DIR/sindri.
/// </summary>
internal static class SettingsFromEnvironment
{
    internal static ContextStoreSettings Read(Func<string, string?> environment)
    {
        var home = environment("AW_SINDRI_HOME") is { Length: > 0 } sindri
            ? sindri
            : Path.Combine(environment("AW_STATE_DIR") is { Length: > 0 } root
                ? root
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow"), "sindri-app");
        return new ContextStoreSettings(Path.GetFullPath(Path.Combine(home, "context")));
    }
}
