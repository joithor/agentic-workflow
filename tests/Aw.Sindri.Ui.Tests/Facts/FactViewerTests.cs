using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Facts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>The viewers (screens §5.2, U8 T5): text, file, item and link show the row; payload reads the fact's body. Every figure is a golden's.</summary>
[Lane(TestLane.Unit)]
public sealed class FactViewerTests
{
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly List<string?> _opened = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private FactViewerContext Context(string scenario) => new(_server, _clipboard, _clock, () => FactFixtures.Api, FixtureInstances.Detail(scenario).Id, id => _opened.Add(id));

    private static string Body(string scenario) => (string)System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("workflow-instance-fact", scenario))!["body"]!;

    [Fact]
    public async Task Text_shows_one_line_and_Copy()
    {
        var viewer = new TextFactViewer(FactFixtures.Row("incident-loop", "fact:severity"), Context("incident-loop"));
        Assert.Equal(FactFixtures.Row("incident-loop", "fact:severity").Summary, viewer.Line);

        await viewer.Load(Token);
        await viewer.Copy(Token);

        Assert.Equal((Body("text"), Body("text"), Labels.Copied), (viewer.Line, _clipboard.Text, viewer.Copying.Label));
        Assert.Contains(viewer.Line, viewer.CopyName, StringComparison.Ordinal);
        Assert.True(viewer.Copying.IsAnnouncing);
    }

    [Fact]
    public void File_shows_name_size_hash_and_Open()
    {
        var row = FactFixtures.Row("project-build", "fact:prd");

        var viewer = new FileFactViewer(row, new FakeLauncher());

        Assert.Equal((row.Ref!.Name, row.Ref.Path, true), (viewer.Name, viewer.Path, viewer.CanOpen));
        Assert.Contains((row.Ref.Size!.Value / 1024).ToString(System.Globalization.CultureInfo.InvariantCulture), viewer.SizeText, StringComparison.Ordinal);
        var hash = row.Ref.Hash!;
        Assert.Contains(hash[..hash.IndexOf(':', StringComparison.Ordinal)], viewer.HashText, StringComparison.Ordinal);
        Assert.StartsWith(viewer.HashText[..8], hash, StringComparison.Ordinal);
        Assert.Contains(row.Ref.Path!, viewer.OpenName, StringComparison.Ordinal);
        viewer.ReportOpened(opened: false);
        Assert.Contains(row.Ref.Path!, viewer.OpenNote, StringComparison.Ordinal);
    }

    [Fact]
    public void Item_shows_id_title_state_and_Open_opens_the_item_sheet()
    {
        var row = FactFixtures.Row("done", "fact:item");
        var viewer = new ItemFactViewer(row, Context("done"));

        viewer.Open();

        Assert.Equal(row.Ref!.ItemId, viewer.ItemId);
        Assert.Equal([row.Ref.ItemId], _opened);
        Assert.Equal(row.Summary, $"{viewer.ItemId} {viewer.Detail}");
        Assert.Contains(viewer.ItemId, viewer.OpenName, StringComparison.Ordinal);
    }

    [Fact]
    public void Link_shows_host_and_title_and_Open_external()
    {
        var fixture = FactFixtures.Fixture()["body"]!;
        var viewer = new LinkFactViewer(FactFixtures.Link(), new FakeLauncher());

        Assert.Equal(((string?)fixture["host"], (string?)fixture["title"], (string?)fixture["url"], true), (viewer.Host, viewer.Title, viewer.Url, viewer.CanOpen));
        Assert.Contains(viewer.Host, viewer.OpenText, StringComparison.Ordinal);
        Assert.Contains(viewer.Host, viewer.OpenName, StringComparison.Ordinal);

        var row = FactFixtures.Link();
        var unsafeLink = new LinkFactViewer(row with { Ref = row.Ref! with { Url = "file:///etc/hosts" } }, new FakeLauncher());
        Assert.False(unsafeLink.CanOpen);
    }

    [Fact]
    public async Task Link_Open_hands_an_http_or_https_link_to_the_launcher_and_nothing_else()
    {
        var launcher = new FakeLauncher();
        var link = FactFixtures.Link();
        var good = new LinkFactViewer(link, launcher);
        var unsafeLink = new LinkFactViewer(link with { Ref = link.Ref! with { Url = "file:///etc/hosts" } }, launcher);

        await good.Open(TestContext.Current.CancellationToken);
        await unsafeLink.Open(TestContext.Current.CancellationToken);

        Expect.Sequence([new Uri(good.Url).AbsoluteUri], launcher.Opened);
    }

    [Fact]
    public async Task File_Open_reports_what_the_launcher_answered_and_never_opens_a_relative_path()
    {
        var launcher = new FakeLauncher();
        launcher.AddFile("/work/docs/prd.md");
        var row = FactFixtures.Row("project-build", "fact:prd");
        var rooted = row with { Ref = row.Ref! with { Path = "/work/docs/prd.md" } };
        var viewer = new FileFactViewer(rooted, launcher);

        await viewer.Open(TestContext.Current.CancellationToken);
        Expect.Sequence(["/work/docs/prd.md"], launcher.Opened);
        Assert.Equal(string.Empty, viewer.OpenNote);

        launcher.Refuse = true;
        await viewer.Open(TestContext.Current.CancellationToken);
        Assert.Equal(Labels.FileNotOpened(viewer.Path), viewer.OpenNote);

        launcher.Refuse = false;
        var relative = new FileFactViewer(rooted with { Ref = rooted.Ref! with { Path = "docs/prd.md" } }, launcher);
        await relative.Open(TestContext.Current.CancellationToken);
        Expect.Sequence(["/work/docs/prd.md"], launcher.Opened);
        Assert.Equal(Labels.FileNotOpened(relative.Path), relative.OpenNote);
    }
}
