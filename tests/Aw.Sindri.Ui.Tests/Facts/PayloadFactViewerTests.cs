using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Facts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>The payload viewer (screens §5.2, U8 T5): View reads the fact's route, shows the first three keys, then a tree; a truncated body says so and a missing fact shows the served detail.</summary>
[Lane(TestLane.Unit)]
public sealed class PayloadFactViewerTests
{
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonObject Golden(string scenario) => (JsonObject)JsonNode.Parse(GoldenFiles.Api("workflow-instance-fact", scenario))!; // why: the golden is an object

    private PayloadFactViewer Viewer(string instance, string key) =>
        new(FactFixtures.Row(instance, key), new FactViewerContext(_server, new FakeClipboard(), _clock, () => FactFixtures.Api, FixtureInstances.Detail(instance).Id, _ => { }));

    [Fact]
    public async Task View_reads_the_fact_route_and_shows_the_body_once_as_a_tree()
    {
        var body = (JsonObject)Golden("payload")["body"]!; // why: the golden's body is an object
        var viewer = Viewer("incident-loop", "fact:alert");
        Assert.True(viewer.IsLoading);

        await viewer.Load(Token);

        var keys = body.Select(p => p.Key).ToList();
        Assert.Equal((false, string.Empty, string.Empty), (viewer.IsLoading, viewer.ProblemText, viewer.TruncatedNote));
        Assert.Equal((string.Empty, false), (viewer.ScalarLine, viewer.HasScalarLine));   // the tree draws the body: no summary above it
        Assert.Equal(keys, viewer.Tree.Select(n => n.Key), StringComparer.Ordinal);
        var incident = viewer.Tree.Single(n => n.Key == "incident");
        Assert.Equal(((JsonObject)body["incident"]!).Select(p => p.Key), incident.Children.Select(c => c.Key), StringComparer.Ordinal);
        Assert.Contains((string)body["incident"]!["id"]!, incident.Children.Single(c => c.Key == "id").Text, StringComparison.Ordinal);
        Assert.Equal(body["tags"]!.AsArray().Count, viewer.Tree.Single(n => n.Key == "tags").Children.Count);
    }

    [Fact]
    public async Task A_lone_value_body_shows_as_one_line()
    {
        _server.WorkflowAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, """{"key":"fact:alert","type":"payload","body":"plain words","truncated":false}""");
        var viewer = Viewer("incident-loop", "fact:alert");

        await viewer.Load(Token);

        Assert.Equal(("plain words", true, 0), (viewer.ScalarLine, viewer.HasScalarLine, viewer.Tree.Count));
    }

    [Fact]
    public async Task Payload_truncated_says_so()
    {
        var viewer = Viewer("incident-loop", "fact:history");

        await viewer.Load(Token);

        Assert.True((bool)Golden("truncated")["truncated"]!);
        Assert.Equal(Labels.PayloadTruncated, viewer.TruncatedNote);
        Assert.Equal(((JsonObject)Golden("truncated")["body"]!).Select(p => p.Key), viewer.Tree.Select(n => n.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Missing_fact_shows_the_served_detail()
    {
        var viewer = new PayloadFactViewer(
            FactFixtures.Row("project-build", "fact:project") with { Key = "fact:unknown" },
            new FactViewerContext(_server, new FakeClipboard(), _clock, () => FactFixtures.Api, FixtureInstances.Detail("project-build").Id, _ => { }));

        await viewer.Load(Token);

        var detail = (string)JsonNode.Parse(GoldenFiles.Api("workflow-instance-fact", "not-found"))!["detail"]!;
        Assert.Equal((false, detail), (viewer.IsLoading, viewer.ProblemText));
        Assert.Empty(viewer.Tree);
    }

    [Fact]
    public async Task A_body_with_control_characters_is_shown_scrubbed()
    {
        _server.WorkflowAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, """{"key":"fact:alert","type":"payload","body":{"note":"a‮b\u0007c"},"truncated":false}""");
        var viewer = Viewer("incident-loop", "fact:alert");

        await viewer.Load(Token);

        Assert.DoesNotContain(viewer.Tree[0].Text, c => c is '‮' or '\u0007');
        Assert.StartsWith("note", viewer.Tree[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_viewer_with_no_api_to_ask_says_so_instead_of_showing_an_empty_body()
    {
        var context = new FactViewerContext(_server, new FakeClipboard(), _clock, () => null, FixtureInstances.Detail("incident-loop").Id, _ => { });
        var viewer = new PayloadFactViewer(FactFixtures.Row("incident-loop", "fact:alert"), context);

        await viewer.Load(Token);

        Assert.Equal((Labels.PayloadNotRunning, false), (viewer.ProblemText, viewer.IsShown));
        Assert.Equal((true, Labels.PayloadNotRunning), (viewer.Voice.IsAnnouncing, viewer.Voice.Announcement));
    }

    [Fact]
    public async Task The_not_running_message_is_said_a_turn_after_the_viewer_is_shown()
    {
        var context = new FactViewerContext(_server, new FakeClipboard(), _clock, () => null, FixtureInstances.Detail("incident-loop").Id, _ => { });
        var viewer = new PayloadFactViewer(FactFixtures.Row("incident-loop", "fact:alert"), context);

        // what the live region held at the instant the problem became visible: a change observed, not a turn raced
        var announcingWhenShown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(PayloadFactViewer.ProblemText), StringComparison.Ordinal))
            {
                _ = announcingWhenShown.TrySetResult(viewer.Voice.IsAnnouncing);
            }
        };
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bound.CancelAfter(TimeSpan.FromSeconds(30));

        var loading = viewer.Load(Token);

        // the live region is born with the viewer: its text must change after that to be spoken, so the first Say waits a turn
        Assert.False(await announcingWhenShown.Task.WaitAsync(bound.Token));
        await loading.WaitAsync(bound.Token);
        Assert.Equal((Labels.PayloadNotRunning, true, Labels.PayloadNotRunning), (viewer.ProblemText, viewer.Voice.IsAnnouncing, viewer.Voice.Announcement));
    }

    [Fact]
    public async Task The_outcome_of_the_read_is_said_once_by_a_polite_line()
    {
        var viewer = Viewer("incident-loop", "fact:alert");

        await viewer.Load(Token);

        Assert.Equal((true, Labels.PayloadLoaded(viewer.Row.Label)), (viewer.Voice.IsAnnouncing, viewer.Voice.Announcement));
    }
}
