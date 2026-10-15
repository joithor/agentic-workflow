using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Theme;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The lane anchors (docs/csharp/sindri-ui.md "Lane anchors"): where each marker sits, so two lanes' merges touch different lines. The checks assert order, never adjacency to a neighbour, and each runs again over a sample of the insertion its marker prescribes.</summary>
[Lane(TestLane.Unit)]
public sealed class LaneAnchorTests
{
    private static string[] Lines(string path) => File.ReadAllLines(Path.Combine(UiSource.Root, path));

    private static int Find(string[] lines, string text) => Array.FindIndex(lines, l => l.Contains(text, StringComparison.Ordinal));

    private static string[] Insert(string[] lines, int at, params string[] add) => [.. lines[..at], .. add, .. lines[at..]];

    private const string ClientPath = "src/Sindri/Aw.Sindri.Ui/Api/SindriApiClient.cs";
    private const string SheetPath = "src/Sindri/Aw.Sindri.Ui/ViewModels/Shell/ShellViewModel.Sheet.cs";
    private const string WindowPath = "src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml";

    private static void AssertClient(string[] lines)
    {
        var u3 = Find(lines, "LANE ANCHOR U3'");
        var u8 = Find(lines, "LANE ANCHOR U8");
        var recipe = Find(lines, "Task<Result<RecipeView>> Recipe(");

        Assert.True(recipe >= 0 && u3 > recipe && u3 - recipe <= 4, "the U3' marker belongs right after Recipe(...), found at index " + u3.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(u3 + 1, u8);
    }

    [Fact]
    public void The_client_anchors_follow_the_public_reads_and_point_at_no_other_file()
    {
        var lines = Lines(ClientPath);

        AssertClient(lines);
        Assert.DoesNotContain(lines, l => l.Contains("Calls.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void The_client_anchors_survive_a_read_above_the_first_marker_and_a_send_below_the_second()
    {
        var lines = Lines(ClientPath);
        var u3 = Find(lines, "LANE ANCHOR U3'");
        var withRead = Insert(lines, u3, "    /// <summary>Reads X.</summary>", "    public Task<Result<RecipeView>> Nodes(Uri api, CancellationToken cancellationToken) => Get<RecipeView>(api, \"x\", cancellationToken);", string.Empty);
        var u8 = Find(withRead, "LANE ANCHOR U8");
        var both = Insert(withRead, u8 + 1, "    /// <summary>Sends Y.</summary>", "    public Task<Result<RecipeView>> Send(Uri api, CancellationToken cancellationToken) => Get<RecipeView>(api, \"y\", cancellationToken);");

        AssertClient(both);
    }

    private static void AssertSwitch(string[] lines)
    {
        var property = Find(lines, "IsSheetPlaceholderShown =>");
        var u3 = Find(lines, "LANE ANCHOR U3'");
        var u8 = Find(lines, "LANE ANCHOR U8");
        var fallback = Find(lines, "_ => true");

        Assert.True(property >= 0 && u3 > property && u8 == u3 + 1 && fallback == u8 + 1, "the markers are adjacent inside the switch, above its '_ => true' arm");
    }

    [Fact]
    public void Both_lane_markers_sit_inside_the_placeholder_switch_just_before_its_default_arm()
        => AssertSwitch(File.ReadAllLines(Path.Combine(UiSource.Root, SheetPath)));

    [Fact]
    public void The_switch_anchors_survive_an_arm_above_the_first_marker_and_one_below_the_second()
    {
        var lines = File.ReadAllLines(Path.Combine(UiSource.Root, SheetPath));
        var withAbove = Insert(lines, Find(lines, "LANE ANCHOR U3'"), "        DeepLinkKind.NeedsYou or DeepLinkKind.Decision => false,");
        var both = Insert(withAbove, Find(withAbove, "LANE ANCHOR U8") + 1, "        DeepLinkKind.Workflow => false,");

        AssertSwitch(both);
    }

    private static void AssertWindow(string[] xaml)
    {
        var sheet = Find(xaml, "x:Name=\"Sheet\"");
        var content = Find(xaml, "x:Name=\"SheetContent\"");
        var u3 = Find(xaml, "LANE ANCHOR U3'");
        var u8 = Find(xaml, "LANE ANCHOR U8");
        var palette = Find(xaml, "x:Name=\"PaletteLayer\"");

        Assert.True(sheet >= 0 && content > sheet, "SheetContent is inside the Sheet layer");
        Assert.True(u3 > content && u8 == u3 + 1 && u8 < palette, "both markers follow SheetContent's start, not a root layer");
        var closes = xaml.Skip(u8 + 1).TakeWhile(l => !l.Contains("</Border>", StringComparison.Ordinal)).Any(l => l.Contains("</Panel>", StringComparison.Ordinal));
        Assert.True(closes, "SheetContent closes right after the markers, before the Sheet layer ends");
    }

    [Fact]
    public void The_window_has_one_sheet_layer_and_the_lane_anchors_sit_inside_its_content()
        => AssertWindow(File.ReadAllLines(Path.Combine(UiSource.Library, "Views", "Shell", "ShellWindow.axaml")));

    [Fact]
    public void The_window_anchors_survive_a_block_above_the_first_marker_and_one_below_the_second()
    {
        var xaml = File.ReadAllLines(Path.Combine(UiSource.Root, WindowPath));
        var withAbove = Insert(xaml, Find(xaml, "LANE ANCHOR U3'"), "<Border x:Name=\"A\">", "  <TextBlock Text=\"a\" />", "</Border>");
        var both = Insert(withAbove, Find(withAbove, "LANE ANCHOR U8") + 1, "<Border x:Name=\"B\">", "  <TextBlock Text=\"b\" />", "</Border>");

        AssertWindow(both);
    }

    [Fact]
    public void Every_marker_says_where_to_insert_and_not_between_the_markers()
    {
        var files = new[]
        {
            "src/Sindri/Aw.Sindri.Ui/Api/ISindriApi.cs", "src/Sindri/Aw.Sindri.Ui/Api/SindriApiClient.cs", "src/Sindri/Aw.Sindri.Ui/Api/SindriUiJsonContext.cs",
            "src/Sindri/Aw.Sindri.Ui/ViewModels/Shell/ShellViewModel.cs", "src/Sindri/Aw.Sindri.Ui/ViewModels/Shell/ShellViewModel.Sheet.cs", "src/Sindri/Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml",
            "tests/Aw.Sindri.Ui.Testing/FakeSindriApi.Workflow.cs", "tests/Aw.Sindri.Ui.Testing/FakeSindriHandler.cs", "tests/Aw.Sindri.Ui.Testing/SindriApiContract.cs", "tests/Aw.Sindri.Ui.Testing/SindriApiContract.Workflow.cs",
            "docs/csharp/sindri-ui.md",
        };
        var markers = files.SelectMany(f => File.ReadAllLines(Path.Combine(UiSource.Root, f)).Where(l => l.Contains("LANE ANCHOR U3':", StringComparison.Ordinal) || l.Contains("LANE ANCHOR U8:", StringComparison.Ordinal)).Select(l => (File: f, Line: l))).ToList();

        Assert.True(markers.Count >= 2 * files.Length);
        Assert.All(markers, m => Assert.True(
            m.Line.Contains("never between the markers", StringComparison.Ordinal) && (m.Line.Contains("immediately above", StringComparison.Ordinal) || m.Line.Contains("immediately below", StringComparison.Ordinal)),
            m.File + ": " + m.Line));
    }
}
