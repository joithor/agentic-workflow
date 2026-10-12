namespace Aw.Context.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    /// <summary>
    /// The state directory is Sindri's app home's 'context' folder (spec §3): $AW_SINDRI_HOME/context, else
    /// $AW_STATE_DIR/sindri-app/context, else ~/.agentic-workflow/sindri-app/context. Never the TS $AW_STATE_DIR/sindri/.
    /// </summary>
    internal static ContextSettings Read(Func<string, string?> environment)
    {
        if (environment("AW_SINDRI_HOME") is { Length: > 0 } home)
        {
            return new(Path.Combine(home, "context"));
        }

        var state = environment("AW_STATE_DIR") is { Length: > 0 } root
            ? root
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        return new(Path.Combine(state, "sindri-app", "context"));
    }
}
