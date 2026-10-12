namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// Where the control plane writes control.json on this machine (docs/csharp/sindri.md "Files"): AW_SINDRI_HOME, else
/// $AW_STATE_DIR/sindri-app, else ~/.agentic-workflow/sindri-app.
/// </summary>
internal static class ControlFilePath
{
    /// <summary>The control plane's per-start human token beside control.json: '&lt;home&gt;/auth/human.token' (API spec §6, docs/csharp/sindri.md "Files").</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="userHome">The user's home directory.</param>
    /// <returns>The token file's path.</returns>
    internal static string Token(Func<string, string?> environment, string userHome) =>
        Path.Combine(Path.GetDirectoryName(From(environment, userHome)) ?? userHome, "auth", "human.token");

    internal static string From(Func<string, string?> environment, string userHome)
    {
        var home = environment("AW_SINDRI_HOME") is { Length: > 0 } explicitHome
            ? explicitHome
            : Path.Combine(environment("AW_STATE_DIR") is { Length: > 0 } root ? root : Path.Combine(userHome, ".agentic-workflow"), "sindri-app");
        return Path.Combine(home, "control.json");
    }
}
