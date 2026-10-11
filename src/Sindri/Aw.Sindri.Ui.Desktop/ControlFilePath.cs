namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// Where the control plane writes control.json on this machine (docs/csharp/sindri.md "Files"): AW_SINDRI_HOME, else
/// $AW_STATE_DIR/sindri-app, else ~/.agentic-workflow/sindri-app.
/// </summary>
internal static class ControlFilePath
{
    internal static string From(Func<string, string?> environment, string userHome)
    {
        var home = environment("AW_SINDRI_HOME") is { Length: > 0 } explicitHome
            ? explicitHome
            : Path.Combine(environment("AW_STATE_DIR") is { Length: > 0 } root ? root : Path.Combine(userHome, ".agentic-workflow"), "sindri-app");
        return Path.Combine(home, "control.json");
    }
}
