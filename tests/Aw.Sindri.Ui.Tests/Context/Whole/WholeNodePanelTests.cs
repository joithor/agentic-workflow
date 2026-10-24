using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>Clicking a node on the whole map keeps the pin and opens a side panel (owner answer q101): kind and title, the source and description when served, and the links grouped by band; a link jumps to its node.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeNodePanelTests
{
    private const string Node = "feature:tabs.tab-manager";
    private const string Surface = "surface:command/switch-tab";
    private const string Record = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000201";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Served(string extra = "") => $$"""
        {
          "mapVersion": "v1",
          "node": { "key": "{{Node}}", "kind": "feature", "label": "Tab manager", "provenance": "human", "confirmed": true, "trust": "trusted", "confidence": 1{{extra}} },
          "aliases": [],
          "out": [ { "from": "{{Node}}", "type": "touches", "to": "{{Surface}}", "provenance": "human", "confirmed": true } ],
          "in": [
            { "from": "code:src/tabs/TabManager.ts", "type": "implements", "to": "{{Node}}", "provenance": "deterministic", "confirmed": false },
            { "from": "{{Record}}", "type": "mentions", "to": "{{Node}}", "provenance": "human", "confirmed": true },
            { "from": "mystery:x", "type": "relates", "to": "{{Node}}", "provenance": "human", "confirmed": true },
            { "from": "unknown:y", "type": "cites", "to": "{{Node}}", "provenance": "human", "confirmed": true, "otherKind": "decision", "otherLabel": "Use tabs" }
          ]
        }
        """;

    private static async Task<(ContextPageViewModel Page, FakeLauncher Launcher, FakeSindriApi Api)> Open(string body)
    {
        var api = new FakeSindriApi { NodeAnswer = new RouteAnswer(HttpStatusCode.OK, body) };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var launcher = new FakeLauncher();
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }, launcher));
        await page.Opened(Token);
        return (page, launcher, api);
    }

    private static async Task Click(ContextPageViewModel page, string key)
    {
        var rect = page.Whole.Geometry!.Nodes.First(n => n.Key == key).Rect;
        page.Whole.PointerClicked(rect.CenterX, rect.CenterY);
        await page.Whole.Panel.Reading;
    }

    [Fact]
    public async Task A_click_pins_the_node_and_opens_the_panel_with_its_kind_title_and_links_by_band()
    {
        var (page, _, api) = await Open(Served());

        await Click(page, Node);

        var panel = page.Whole.Panel;
        Assert.Equal(Node, page.Whole.Pinned);
        Assert.Equal((true, false, false), (panel.IsOpen, panel.IsLoading, panel.IsFailed));
        Assert.Equal(("feature", "Tab manager"), (panel.Kind, panel.Title));
        Assert.Contains("node:" + Node, api.ContextLog);
        Assert.Equal(
            [Labels.ContextWholeBandWhy, Labels.ContextMapAreaProduct, Labels.ContextMapAreaCode, Labels.ContextWholeBandOther],
            panel.Groups.Select(g => g.Title));
        Assert.Equal(["← " + KnownLabel(page, Record) + " · mentions", "← Use tabs · cites"], panel.Groups[0].Rows.Select(r => r.Text));
        Assert.Contains(panel.Groups[1].Rows, r => r.Key == Surface && r.Text.EndsWith(" · touches", StringComparison.Ordinal) && r.Text.StartsWith("→ ", StringComparison.Ordinal));
        Assert.Equal(["← code:src/tabs/TabManager.ts · implements"], panel.Groups[2].Rows.Select(r => r.Text));
        Assert.Equal(["← mystery:x · relates"], panel.Groups[3].Rows.Select(r => r.Text));
    }

    [Fact]
    public async Task A_source_with_a_link_shows_and_opens_and_a_description_shows()
    {
        var (page, launcher, _) = await Open(Served(""", "source": { "kind": "ticket", "ref": "LIN-12", "title": "Restore layout", "url": "https://linear.app/x/issue/LIN-12" }, "description": "Keeps the tabs." """));

        await Click(page, Node);
        await page.Whole.Panel.OpenSource(Token);

        var panel = page.Whole.Panel;
        Assert.Equal(("ticket: Restore layout", true, true), (panel.SourceText, panel.HasSource, panel.HasLink));
        Assert.Equal(("Keeps the tabs.", true), (panel.Description, panel.HasDescription));
        Assert.Equal(["https://linear.app/x/issue/LIN-12"], launcher.Opened);
    }

    [Fact]
    public async Task A_source_without_a_url_is_plain_text_and_opens_nothing()
    {
        var (page, launcher, _) = await Open(Served(""", "source": { "kind": "commit", "ref": "abc1234" }"""));

        await Click(page, Node);
        await page.Whole.Panel.OpenSource(Token);

        Assert.Equal(("commit: abc1234", true, false), (page.Whole.Panel.SourceText, page.Whole.Panel.HasSource, page.Whole.Panel.HasLink));
        Assert.Empty(launcher.Opened);
    }

    [Fact]
    public async Task A_missing_source_and_description_show_nothing()
    {
        var (page, _, _) = await Open(Served());

        await Click(page, Node);

        var panel = page.Whole.Panel;
        Assert.Equal((false, false, false), (panel.HasSource, panel.HasLink, panel.HasDescription));
        Assert.Equal((string.Empty, string.Empty), (panel.SourceText, panel.Description));
    }

    [Fact]
    public async Task A_link_that_is_not_a_web_address_is_not_opened()
    {
        var (page, launcher, _) = await Open(Served(""", "source": { "kind": "doc", "ref": "README", "url": "file:///etc/passwd" }"""));

        await Click(page, Node);
        await page.Whole.Panel.OpenSource(Token);

        Assert.False(page.Whole.Panel.HasLink);
        Assert.Empty(launcher.Opened);
    }

    [Fact]
    public async Task A_link_in_the_panel_jumps_to_that_node_pins_it_and_loads_its_panel()
    {
        var (page, _, api) = await Open(Served());
        await Click(page, Node);
        var centred = new List<WholeRect>();
        page.Whole.CentreRequested += (_, e) => centred.Add(e.Value);

        page.Whole.Panel.Groups[1].Rows.First(r => r.Key == Surface).Jump();
        await page.Whole.Panel.Reading;

        Assert.Equal(Surface, page.Whole.Pinned);
        Assert.Equal(Surface, page.Whole.Panel.Key);
        Assert.Contains("node:" + Surface, api.ContextLog);
        Assert.Equal([page.Whole.Geometry!.Nodes.First(n => n.Key == Surface).Rect], centred);
    }

    [Fact]
    public async Task Esc_closes_the_panel_first_and_the_pin_stays_until_the_next_Esc()
    {
        var (page, _, _) = await Open(Served());
        await Click(page, Node);

        Assert.True(page.Back());
        Assert.Equal((false, Node), (page.Whole.Panel.IsOpen, page.Whole.Pinned));
        Assert.True(page.Back());
        Assert.Null(page.Whole.Pinned);
    }

    [Fact]
    public async Task The_close_button_closes_the_panel_and_the_pin_stays()
    {
        var (page, _, _) = await Open(Served());
        await Click(page, Node);

        Assert.True(page.Whole.Panel.Close());

        Assert.Equal((false, Node), (page.Whole.Panel.IsOpen, page.Whole.Pinned));
    }

    [Fact]
    public async Task A_failed_read_shows_the_failure_line_and_keeps_the_title()
    {
        var (page, _, _) = await Open(Served());
        var api = (FakeSindriApi)page.Ports.Api;
        api.NodeAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-040", "no such node");

        await Click(page, Node);

        Assert.Equal((true, false, true), (page.Whole.Panel.IsOpen, page.Whole.Panel.IsLoading, page.Whole.Panel.IsFailed));
        Assert.Equal("Tab manager", page.Whole.Panel.Title);
    }

    [Fact]
    public async Task While_the_node_is_read_the_panel_is_open_and_loading()
    {
        var (page, _, api) = await Open(Served());
        api.NodeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var rect = page.Whole.Geometry!.Nodes.First(n => n.Key == Node).Rect;
        page.Whole.PointerClicked(rect.CenterX, rect.CenterY);

        Assert.Equal((true, true, false), (page.Whole.Panel.IsOpen, page.Whole.Panel.IsLoading, page.Whole.Panel.IsFailed));
        api.NodeGate.SetResult();
        await page.Whole.Panel.Reading;
        Assert.False(page.Whole.Panel.IsLoading);
    }

    // The label the panel shows for a node that is on the map (a record's label is not its key).
    private static string KnownLabel(ContextPageViewModel page, string key) => page.Whole.Geometry!.Nodes.First(n => n.Key == key).Label;
}
