using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>A re-read of the frame while time passes (review run 1, V1): the engine's relative times and the children's ages move on every read, and a control the human is on must not be replaced because of them.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceFrameRereadTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    // "(1h 24m)" reads "(1h 25m)" a minute later: the last digit before each "m" counts up (wrapping at 9).
    private static string Minute(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i + 1 < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9' && chars[i + 1] == 'm')
            {
                chars[i] = (char)('0' + ((chars[i] - '0' + 1) % 10));
            }
        }

        return new string(chars);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<InstanceFrameViewModel> Opened(string scenario)
    {
        var golden = GoldenFiles.Node("workflow-instance", scenario);
        var frame = new InstanceFrameViewModel(_server, _clipboard, _clock);
        await frame.Open(_api, (string)golden["address"]!, (string)golden["id"]!, Token);
        return frame;
    }

    // The same instance a minute later: every relative time in the status text moves on, the timeout counts down, the children age.
    private async Task<InstanceDetail> Later(InstanceFrameViewModel frame, string scenario, Action<JsonNode> also)
    {
        var node = GoldenFiles.Node("workflow-instance", scenario);
        var status = node["current"]!["status"]!;
        status["text"] = Minute((string)status["text"]!);
        if (status["timeout"] is { } timeout)
        {
            timeout["inSeconds"] = (long)timeout["inSeconds"]! - 60;
        }

        also(node);
        _server.WorkflowAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, node.ToJsonString());
        await frame.Refresh(_api, Token);
        return FixtureInstances.Parse<InstanceDetail>(node);
    }

    private static void AgeEveryChild(JsonNode node)
    {
        switch (node)
        {
            case JsonObject o:
                if (o["ageSeconds"] is not null)
                {
                    o["ageSeconds"] = (long)o["ageSeconds"]! + 120;
                }

                foreach (var child in o.Select(p => p.Value).OfType<JsonNode>().ToList())
                {
                    AgeEveryChild(child);
                }

                break;
            case JsonArray a:
                foreach (var child in a.OfType<JsonNode>().ToList())
                {
                    AgeEveryChild(child);
                }

                break;
            default:
                break;
        }
    }

    [Fact]
    public async Task A_reread_with_a_new_status_time_keeps_the_open_cancel_confirm()
    {
        var frame = await Opened("incident-loop");
        var expanded = frame.Expanded!;
        expanded.AskCancel();
        Assert.True(expanded.IsConfirmingCancel);

        var later = await Later(frame, "incident-loop", _ => { });

        Assert.Same(expanded, frame.Expanded);
        Assert.True(frame.Expanded!.IsConfirmingCancel);
        Assert.Equal(Labels.TimeoutLine(later.Current.Status.Timeout!), frame.Expanded.TimeoutLine);
    }

    [Fact]
    public async Task A_reread_copies_the_status_text_it_now_shows()
    {
        var frame = await Opened("incident-loop");
        var expanded = frame.Expanded!;

        var later = await Later(frame, "incident-loop", _ => { });
        await expanded.CopyStatus(Token);

        Assert.Equal(later.Current.Status.Text, _clipboard.Text);
    }

    [Fact]
    public async Task A_reread_with_older_children_keeps_the_panel_and_its_rows()
    {
        var frame = await Opened("project-build");
        var panel = frame.Children!;
        var row = panel.Groups[0].AllRows.First();
        var before = row.Age;

        var later = await Later(frame, "project-build", AgeEveryChild);

        var served = FixtureInstances.ChildRows(later.Children[0]).First();
        Assert.Same(panel, frame.Children);
        Assert.Same(row, frame.Children!.Groups[0].AllRows.First());
        Assert.NotEqual(before, row.Age, StringComparer.Ordinal);
        Assert.Equal(Labels.RowAge(TimeSpan.FromSeconds(served.AgeSeconds)), row.Age);
        Assert.Contains(row.Age, row.Meta, StringComparison.Ordinal);
    }

    private static void ChildIs(JsonNode node, string state) =>
        node["current"]!["status"]!["child"] = new JsonObject { ["id"] = "WFI-child", ["address"] = "ITM-01/item/1/step.4", ["state"] = state, ["reason"] = null };

    [Fact]
    public async Task A_reread_where_only_the_childs_state_changes_updates_the_pointer()
    {
        var golden = GoldenFiles.Node("workflow-instance", "incident-loop");
        ChildIs(golden, "running");
        _server.WorkflowAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, golden.ToJsonString());
        var frame = new InstanceFrameViewModel(_server, _clipboard, _clock);
        await frame.Open(_api, (string)golden["address"]!, (string)golden["id"]!, Token);
        Assert.Contains("running", frame.Expanded!.Child!.Text, StringComparison.Ordinal);

        _ = await Later(frame, "incident-loop", node => ChildIs(node, "parked"));

        Assert.Contains("parked", frame.Expanded!.Child!.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("running", frame.Expanded.Child.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_catalog_that_arrives_after_the_frame_shows_gives_the_protected_child_actions_their_lock_button()
    {
        var catalog = new ViewModels.Controls.CatalogViewModel(_clipboard, _clock);
        var golden = GoldenFiles.Node("workflow-instance", "project-build");
        var frame = new InstanceFrameViewModel(_server, _clipboard, _clock, catalog: catalog);
        await frame.Open(_api, (string)golden["address"]!, (string)golden["id"]!, Token);
        Assert.DoesNotContain(frame.Children!.Groups[0].AllRows.SelectMany(r => r.Actions), a => a.Protected is not null);

        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        await frame.Refresh(_api, Token);

        Assert.Contains(frame.Children!.Groups[0].AllRows.SelectMany(r => r.Actions), a => a.Protected is not null);
    }
}
