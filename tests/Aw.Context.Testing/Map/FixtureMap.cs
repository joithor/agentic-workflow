using System.Text.Json;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Json;

namespace Aw.Context.Testing.Map;

/// <summary>The split-panes product map ('Fixtures/split-panes/map/product-map.yaml'), hand-authored and synthetic.</summary>
public static class FixtureMap
{
    /// <summary>Its resource path in the fixture.</summary>
    public const string Path = "map/product-map.yaml";

    /// <summary>The repo it maps.</summary>
    public const string Repo = "aw-split-panes";

    /// <summary>AwJson options over the map types (in production the Sindri host's JSON context holds them).</summary>
    public static JsonSerializerOptions Json { get; } = AwJson.CreateOptions(MapJsonContext.Default);

    /// <summary>The same options, indented: golden files read better.</summary>
    public static JsonSerializerOptions IndentedJson { get; } = AwJson.CreateOptions(MapJsonContext.Default, indented: true);

    /// <summary>The YAML text.</summary>
    public static string Yaml => SplitPanesFixture.ReadText(Path);

    /// <summary>The nodes the 'implements' edges with aw-split-panes evidence point at, and the code files they start from ("that repo's files and code", spec §3): a session's scope.</summary>
    public static IReadOnlyList<string> ScopedNodes { get; } =
    [
        "action:tab-manager.restore-layout", "code:src/layout/PaneResize.ts", "code:src/layout/SplitPane.ts", "code:src/tabs/TabManager.ts",
        "feature:layout.pane-resize", "feature:layout.split-pane", "feature:layout.split-pane.orientation", "feature:tabs.tab-manager",
        "surface:command/split-vertical", "surface:command/switch-tab", "surface:command/toggle-orientation", "surface:keybinding/ctrl+shift+d", "surface:setting/paneResizeStep",
    ];

    /// <summary>The 'implements' edges among a session's scope: file to feature.</summary>
    public const int ScopedEdges = 14;

    /// <summary>Writes the YAML to <paramref name="directory"/>/product-map.yaml.</summary>
    /// <param name="directory">An existing directory outside every repo.</param>
    /// <returns>The file's path.</returns>
    public static string WriteTo(string directory)
    {
        var path = System.IO.Path.Combine(directory, "product-map.yaml");
        File.WriteAllText(path, Yaml);
        return path;
    }
}
