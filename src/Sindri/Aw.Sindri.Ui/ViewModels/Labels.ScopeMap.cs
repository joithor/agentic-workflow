using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the scope-map fact viewer (screens §5.2, the demo slice of U13). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The toggle that shows the fact as its raw tree.</summary>
    internal const string ShowRaw = "Show raw";

    /// <summary>The toggle that shows the fact as the table again.</summary>
    internal const string ShowTable = "Show table";

    /// <summary>The section of workstreams.</summary>
    internal const string ScopeWorkstreams = "WORKSTREAMS";

    /// <summary>The section of flagged items.</summary>
    internal const string ScopeFlagged = "FLAGGED";

    /// <summary>The section of excluded items.</summary>
    internal const string ScopeExcluded = "EXCLUDED";

    /// <summary>The section of unresolved items.</summary>
    internal const string ScopeUnresolved = "UNRESOLVED";

    /// <summary>The title of a surface that has no title, name or id the viewer can read.</summary>
    internal const string ScopeUnnamed = "(unnamed surface)";

    /// <summary>The table's accessible name.</summary>
    /// <param name="label">The fact's label.</param>
    /// <returns>The words.</returns>
    internal static string ScopeTableName(string label) => $"{label} as a table";

    /// <summary>The counts line's surfaces.</summary>
    /// <param name="count">How many.</param>
    /// <returns>"4 surfaces".</returns>
    internal static string ScopeSurfaces(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "surface" : "surfaces")}");

    /// <summary>One graph status in the counts line.</summary>
    /// <param name="count">How many.</param>
    /// <param name="graph">The status word.</param>
    /// <returns>"2 mapped".</returns>
    internal static string ScopeGraphCount(int count, string graph) => string.Create(CultureInfo.InvariantCulture, $"{count} {graph}");

    /// <summary>A surface's graph chip: a glyph for the three known words, the raw word for any other, always with the word.</summary>
    /// <param name="graph">The status word.</param>
    /// <returns>"▣ mapped", "◇ new", "░ unmapped", or the word.</returns>
    internal static string ScopeGraphChip(string graph) => graph switch
    {
        "mapped" => "▣ mapped",
        "new" => "◇ new",
        "unmapped" => "░ unmapped",
        _ => graph,
    };

    /// <summary>A surface's file count.</summary>
    /// <param name="count">How many.</param>
    /// <returns>"3 files".</returns>
    internal static string ScopeFiles(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "file" : "files")}");

    /// <summary>A surface's citation count.</summary>
    /// <param name="count">How many.</param>
    /// <returns>"2 citations".</returns>
    internal static string ScopeCitations(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "citation" : "citations")}");

    /// <summary>The accessible name of one surface row.</summary>
    /// <param name="title">The surface.</param>
    /// <param name="chip">Its graph chip, or empty.</param>
    /// <param name="count">Its count, or empty.</param>
    /// <returns>The words.</returns>
    internal static string ScopeRowName(string title, string chip, string count) =>
        string.Join(", ", new[] { title, chip, count }.Where(s => s.Length > 0));

    /// <summary>What the polite line says once the table is there.</summary>
    /// <param name="label">The fact's label.</param>
    /// <param name="counts">The counts line.</param>
    /// <returns>The words.</returns>
    internal static string ScopeLoaded(string label, string counts) => $"{label} loaded: {counts}";
}
