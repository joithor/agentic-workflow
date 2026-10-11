namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// Where the control plane writes control.json on this machine (docs/csharp/sindri.md "Files"): AW_SINDRI_HOME, else
/// $AW_STATE_DIR/sindri-app, else ~/.agentic-workflow/sindri-app.
/// </summary>
internal static class ControlFilePath
{
    internal static string From(Func<string, string?> environment, string userHome) => throw new NotSupportedException($"red {environment("HOME")}{userHome}");
}
