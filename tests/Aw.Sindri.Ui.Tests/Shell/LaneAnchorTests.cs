using System.Globalization;
using System.Xml.Linq;
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

    private static void AssertClient(string[] lines)
    {
        var u3 = Find(lines, "LANE ANCHOR U3'");
        var u8 = Find(lines, "LANE ANCHOR U8");
        var recipe = Find(lines, "Task<Result<RecipeView>> Recipe(");
        var events = Find(lines, " Events(");

        Assert.True(recipe >= 0 && recipe < u3, "the U3' marker belongs after Recipe(...), found at index " + u3.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(u3 + 1, u8);
        Assert.True(u8 < events, "the markers sit before Events(...)");
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

    private static bool IsArmOrBlank(string line)
    {
        var text = line.Trim();
        return text.Length == 0 || (text.StartsWith("DeepLinkKind.", StringComparison.Ordinal) && text.EndsWith("=> false,", StringComparison.Ordinal));
    }

    private static void AssertSwitch(string[] lines)
    {
        var property = Find(lines, "IsSheetPlaceholderShown =>");
        var u3 = Find(lines, "LANE ANCHOR U3'");
        var u8 = Find(lines, "LANE ANCHOR U8");
        var fallback = Find(lines, "_ => true");

        Assert.True(property >= 0 && property < u3 && u8 == u3 + 1 && u8 < fallback, "the markers are adjacent inside the switch, above its '_ => true' arm");
        Assert.All(lines[(u8 + 1)..fallback], l => Assert.True(IsArmOrBlank(l), "between u8 and the fallback only `… => false,` arms: " + l));
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

    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Named(XDocument doc, string name) => doc.Descendants().Single(e => string.Equals((string?)e.Attribute(_x + "Name"), name, StringComparison.Ordinal));

    private static XComment Marker(XDocument doc, string text) => doc.DescendantNodes().OfType<XComment>().Single(c => c.Value.Contains(text, StringComparison.Ordinal));

    private static void AssertWindow(XDocument doc)
    {
        var sheet = Named(doc, "Sheet");
        var content = Named(doc, "SheetContent");
        var u3 = Marker(doc, "LANE ANCHOR U3'");
        var u8 = Marker(doc, "LANE ANCHOR U8");

        Assert.Contains(content, sheet.Descendants());
        Assert.Same(content, u3.Parent);
        Assert.Same(content, u8.Parent);
        Assert.Same(u8, u3.NextNode);
    }

    private static XDocument Window() => XDocument.Load(Path.Combine(UiSource.Library, "Views", "Shell", "ShellWindow.axaml"));

    [Fact]
    public void The_window_has_one_sheet_layer_and_the_lane_anchors_sit_inside_its_content() => AssertWindow(Window());

    [Fact]
    public void The_window_anchors_survive_a_block_above_the_first_marker_and_one_below_the_second()
    {
        var doc = Window();
        Marker(doc, "LANE ANCHOR U3'").AddBeforeSelf(new XElement("Border", new XElement("TextBlock", new XAttribute("Text", "a"))));
        Marker(doc, "LANE ANCHOR U8").AddAfterSelf(new XElement("Border", new XElement("TextBlock", new XAttribute("Text", "b"))));

        AssertWindow(doc);
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
