using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The detail sheet keeps the kind of link that opened it (screens spec §3), and the window has one sheet layer that chooses its content by that kind.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSheetKindTests
{
    private static async Task<ShellViewModel> Shell()
    {
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(TestContext.Current.CancellationToken);
        return shell;
    }

    private static DeepLink Link(string text) => DeepLink.TryParse(text, out var link) ? link : throw new InvalidOperationException(text);

    [Fact]
    public async Task The_same_subject_under_two_kinds_gives_two_sheet_states()
    {
        var shell = await Shell();

        shell.Navigate(Link("item/X"));
        var item = shell.SheetLink;
        shell.Navigate(Link("workflow/X"));
        var workflow = shell.SheetLink;

        Assert.Equal(Link("item/X"), item);
        Assert.Equal(Link("workflow/X"), workflow);
        Assert.Equal("X", shell.SheetSubject);
    }

    [Fact]
    public async Task The_edge_tab_reopens_the_kind_that_was_open()
    {
        var shell = await Shell();
        shell.Navigate(Link("item/A"));
        shell.Navigate(Link("recipe/workflow.project-build"));
        shell.CloseSheet();

        shell.ReopenSheet();

        Assert.True(shell.IsSheetOpen);
        Assert.Equal(Link("recipe/workflow.project-build"), shell.SheetLink);
        Assert.Equal("workflow.project-build", shell.SheetSubject);
    }

    [Fact]
    public async Task There_is_no_sheet_link_before_a_subject_is_opened()
    {
        var shell = await Shell();

        Assert.Null(shell.SheetLink);
        Assert.Equal(string.Empty, shell.SheetSubject);
        Assert.False(shell.HasEdgeTab);
    }

    [Fact]
    public void The_window_has_one_sheet_layer_and_the_lane_anchors_sit_inside_its_content()
    {
        var xaml = File.ReadAllLines(Path.Combine(UiSource.Library, "Views", "Shell", "ShellWindow.axaml"));
        var sheet = Array.FindIndex(xaml, l => l.Contains("x:Name=\"Sheet\"", StringComparison.Ordinal));
        var content = Array.FindIndex(xaml, l => l.Contains("x:Name=\"SheetContent\"", StringComparison.Ordinal));
        var u3 = Array.FindIndex(xaml, l => l.Contains("LANE ANCHOR U3'", StringComparison.Ordinal));
        var u8 = Array.FindIndex(xaml, l => l.Contains("LANE ANCHOR U8", StringComparison.Ordinal));
        var palette = Array.FindIndex(xaml, l => l.Contains("x:Name=\"PaletteLayer\"", StringComparison.Ordinal));

        Assert.True(sheet >= 0 && content > sheet, "SheetContent is inside the Sheet layer");
        Assert.True(u3 > content && u8 == u3 + 1 && u8 < palette, "both markers follow SheetContent's start, not a root layer");
        var closes = xaml.Skip(u8 + 1).TakeWhile(l => !l.Contains("</Border>", StringComparison.Ordinal)).Any(l => l.Contains("</Panel>", StringComparison.Ordinal));
        Assert.True(closes, "SheetContent closes right after the markers, before the Sheet layer ends");
    }

    [Fact]
    public async Task Before_any_sheet_is_opened_the_placeholder_shows()
    {
        var shell = await Shell();

        Assert.True(shell.IsSheetPlaceholderShown);
    }

    [Fact]
    public async Task The_placeholder_shows_for_every_kind_until_a_lane_claims_it()
    {
        var shell = await Shell();

        foreach (var kind in Enum.GetValues<DeepLinkKind>())
        {
            shell.OpenSheet(new DeepLink(kind, kind == DeepLinkKind.HomeNeedsYou ? null : "X"));

            Assert.True(shell.IsSheetPlaceholderShown, kind.ToString());
        }
    }

    [Fact]
    public async Task Opening_the_sheet_raises_the_placeholder_flag()
    {
        var shell = await Shell();
        var seen = new List<string>();
        shell.PropertyChanged += (_, e) => seen.Add(e.PropertyName ?? string.Empty);

        shell.OpenSheet(Link("item/X"));

        Assert.Contains(seen, name => string.Equals(name, nameof(ShellViewModel.IsSheetPlaceholderShown), StringComparison.Ordinal));
    }

    [Fact]
    public void The_sheet_body_is_the_placeholder_and_shows_by_the_flag()
    {
        var xaml = File.ReadAllText(Path.Combine(UiSource.Library, "Views", "Shell", "ShellWindow.axaml"));
        var body = xaml[xaml.IndexOf("x:Name=\"SheetBody\"", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("/>", StringComparison.Ordinal)];

        Assert.Contains("IsVisible=\"{Binding IsSheetPlaceholderShown}\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_lane_markers_sit_inside_the_placeholder_switch_just_before_its_default_arm()
    {
        var lines = File.ReadAllLines(Path.Combine(UiSource.Library, "ViewModels", "Shell", "ShellViewModel.Sheet.cs"));
        var property = Array.FindIndex(lines, l => l.Contains("IsSheetPlaceholderShown =>", StringComparison.Ordinal));
        var u3 = Array.FindIndex(lines, l => l.Contains("LANE ANCHOR U3'", StringComparison.Ordinal));
        var u8 = Array.FindIndex(lines, l => l.Contains("LANE ANCHOR U8", StringComparison.Ordinal));
        var fallback = Array.FindIndex(lines, l => l.Contains("_ => true", StringComparison.Ordinal));

        Assert.True(property >= 0 && u3 > property && u8 == u3 + 1 && fallback == u8 + 1, "the markers are adjacent inside the switch, above its '_ => true' arm");
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
