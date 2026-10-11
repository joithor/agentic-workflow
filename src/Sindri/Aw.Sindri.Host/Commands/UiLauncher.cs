namespace Aw.Sindri.Host.Commands;

/// <summary>Bare 'sindri' opens the Sindri app (S1b, docs/csharp/sindri-ui.md).</summary>
internal static class UiLauncher
{
    internal const string Variable = "AW_SINDRI_UI";

    internal static string Locate(Func<string, string?> environment, string baseDirectory) => throw new NotSupportedException($"red {environment(Variable)}{baseDirectory}");

    internal static string Open(string path) => throw new NotSupportedException($"red {path}");
}
