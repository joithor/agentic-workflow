namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>The app's source files in the checkout the tests run from (found by walking up to AgenticWorkflow.slnx).</summary>
internal static class UiSource
{
    /// <summary>src/Sindri/Aw.Sindri.Ui.</summary>
    internal static string Library => Path.Combine(Root, "src", "Sindri", "Aw.Sindri.Ui");

    /// <summary>The library's Theme folder.</summary>
    internal static string Theme => Path.Combine(Library, "Theme");

    /// <summary>Every .axaml file of the UI projects (the library and its heads), outside bin and obj.</summary>
    internal static IEnumerable<string> Axaml =>
        Directory.EnumerateDirectories(Path.Combine(Root, "src", "Sindri"), "Aw.Sindri.Ui*")
            .SelectMany(d => Directory.EnumerateFiles(d, "*.axaml", SearchOption.AllDirectories))
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => string.Equals(p, "bin", StringComparison.Ordinal) || string.Equals(p, "obj", StringComparison.Ordinal)));

    private static string Root
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
                {
                    return dir.FullName;
                }
            }

            throw new InvalidOperationException("the tests run outside the checkout: no AgenticWorkflow.slnx above " + AppContext.BaseDirectory);
        }
    }
}
