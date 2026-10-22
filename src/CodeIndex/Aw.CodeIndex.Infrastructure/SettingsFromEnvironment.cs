namespace Aw.CodeIndex.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static CodeIndexStateSettings Read(Func<string, string?> environment)
    {
        var state = environment("AW_STATE_DIR") is { Length: > 0 } root
            ? root
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");

        // Spec §4.1: the index files live under Sindri's home, which defaults to the state directory's sindri-app.
        var home = environment("AW_SINDRI_HOME") is { Length: > 0 } sindri ? sindri : Path.Combine(state, "sindri-app");
        return new CodeIndexStateSettings(StateDirectory: Path.Combine(state, "codeindex"), IndexDirectory: Path.Combine(home, "code-index"));
    }
}
