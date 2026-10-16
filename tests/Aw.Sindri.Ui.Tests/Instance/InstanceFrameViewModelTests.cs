using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The instance frame's header and reads (screens §4.2, U8 T3): an address resolved through the list or an id read straight, every header word a served field, and the two failure states.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceFrameViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Golden(string scenario) => GoldenFiles.Node("workflow-instance", scenario);

    private static string Text(JsonNode? node) => (string)node!;

    private InstanceFrameViewModel Frame() => new(_server, _clipboard, _clock);

    private async Task<InstanceFrameViewModel> Opened(string scenario)
    {
        var frame = Frame();
        await frame.Open(_api, Text(Golden(scenario)["address"]), Text(Golden(scenario)["id"]), Token);
        return frame;
    }

    // "since 09:14 (3h 12m)": the engine writes it on the status text's first line.
    private static string SinceOf(string scenario)
    {
        var line = Text(Golden(scenario)["current"]!["status"]!["text"]).Split('\n')[0];
        return line[line.IndexOf("since ", StringComparison.Ordinal)..];
    }

    [Theory]
    [InlineData("project-build")]
    [InlineData("item-produce")]
    public async Task Resolves_a_deep_link_address_by_the_list_then_reads_by_id(string scenario)
    {
        var frame = Frame();

        await frame.Open(_api, Text(Golden(scenario)["address"]), id: null, Token);

        Assert.Equal(new InstanceQuery(Address: Text(Golden(scenario)["address"])), _server.LastInstancesQuery);
        Assert.Equal(Text(Golden(scenario)["id"]), _server.LastInstanceId);
        Assert.Equal((FramePhase.Shown, Text(Golden(scenario)["address"])), (frame.Phase, frame.Address));
    }

    [Fact]
    public async Task Opened_from_a_row_reads_by_id_with_no_lookup()
    {
        var frame = await Opened("project-build");

        Assert.Equal((0, Text(Golden("project-build")["id"]), FramePhase.Shown), (_server.InstancesCalls, _server.LastInstanceId, frame.Phase));
    }

    [Theory]
    [InlineData("project-build")]
    [InlineData("escalated-gate")]
    [InlineData("incident-loop")]
    [InlineData("dev-desk-round-trip")]
    public async Task Header_reads_address_title_recipe_hash12_version_state_since(string scenario)
    {
        var frame = await Opened(scenario);

        var golden = Golden(scenario);
        var header = golden["current"]!["status"]!["header"]!;
        Assert.Equal(
            (Text(golden["address"]), Text(golden["title"]), Text(golden["recipe"]!["id"]), Text(header["hash12"]), $"v{golden["recipe"]!["version"]}", Text(header["state"]), SinceOf(scenario)),
            (frame.Address, frame.Title, frame.Recipe, frame.Hash12, frame.Version, frame.StateText, frame.SinceText));
        Assert.Contains(frame.Recipe + "@" + frame.Hash12, frame.Identity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Header_adds_detail_only_when_served()
    {
        var escalated = await Opened("escalated-gate");
        var plain = await Opened("incident-loop");

        var detail = Text(Golden("escalated-gate")["current"]!["status"]!["header"]!["detail"]);
        Assert.Equal((true, detail), (escalated.HasDetail, escalated.Detail));
        Assert.Contains($"{escalated.StateText}: {detail}", escalated.Identity, StringComparison.Ordinal);
        Assert.Equal((false, string.Empty), (plain.HasDetail, plain.Detail));
        Assert.DoesNotContain(plain.StateText + ":", plain.Identity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Subject_line_is_the_served_label_with_Open_in_from_href()
    {
        var linked = await Opened("project-build");
        var plain = await Opened("incident-loop");

        var subject = Golden("project-build")["subject"]!;
        var host = new Uri(Text(subject["href"])).Host;
        Assert.Equal((Text(subject["label"]), true, Text(subject["href"])), (linked.Subject!.Label, linked.Subject.HasHref, linked.Subject.Href));
        Assert.Contains(host, linked.Subject.OpenInText, StringComparison.Ordinal);
        Assert.Contains(host, linked.Subject.OpenInName, StringComparison.Ordinal);
        Assert.Contains(linked.Address, linked.Subject.OpenInName, StringComparison.Ordinal);
        Assert.Equal((Text(Golden("incident-loop")["subject"]!["label"]), false), (plain.Subject!.Label, plain.Subject.HasHref));
    }

    [Fact]
    public async Task Initial_focus_is_a_read_action_never_Take_over_or_Cancel()
    {
        var linked = await Opened("project-build");
        var plain = await Opened("incident-loop");
        _server.WorkflowAnswer = RouteAnswer.Problem("workflow-instance", "not-found");
        var missing = await Opened("project-build");

        Assert.Equal((FrameFocus.OpenIn, FrameFocus.CopyStatus, FrameFocus.Back), (linked.InitialFocus, plain.InitialFocus, missing.InitialFocus));
        string[] commands = ["TakeOver", "Cancel", "Takeover"];
        InstanceFrameViewModel[] frames = [linked, plain, missing];
        Assert.All(frames, f => Assert.DoesNotContain(f.InitialFocus.ToString(), commands, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Not_found_shows_the_served_detail_with_Back()
    {
        _server.WorkflowAnswer = RouteAnswer.Problem("workflow-instance", "not-found");

        var frame = await Opened("project-build");

        Assert.Equal((FramePhase.NotFound, Text(GoldenFiles.Node("workflow-instance", "not-found")["detail"]), Labels.Back), (frame.Phase, frame.ProblemText, frame.BackText));
        Assert.Null(frame.Band);
    }

    [Fact]
    public async Task An_address_no_row_answers_to_is_not_found()
    {
        var frame = Frame();

        var absent = Text(Golden("project-build")["address"]) + "/absent";   // an address the list does not serve

        await frame.Open(_api, absent, id: null, Token);

        Assert.Equal((FramePhase.NotFound, Labels.InstanceNotFound(absent)), (frame.Phase, frame.ProblemText));
    }

    [Fact]
    public async Task Unreadable_shows_the_shared_state_with_Back()
    {
        var body = (JsonObject)GoldenFiles.Node("workflow-instance", "project-build");
        _ = body.Remove("states");
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());

        var frame = await Opened("project-build");

        Assert.Equal((FramePhase.Unreadable, Labels.StripUnreadable, Labels.Back, FrameFocus.Back), (frame.Phase, frame.ProblemText, frame.BackText, frame.InitialFocus));
    }

    [Fact]
    public async Task A_re_read_that_finds_the_instance_done_keeps_the_content_and_says_done()
    {
        var frame = await Opened("project-build");
        var band = frame.Band;
        _server.WorkflowAnswer = RouteAnswer.Api("workflow-instance", "done");

        await frame.Refresh(_api, Token);

        Assert.Equal((FramePhase.Shown, Labels.InstanceDone, Text(Golden("done")["current"]!["status"]!["header"]!["state"])), (frame.Phase, frame.Note, frame.StateText));
        Assert.NotNull(frame.Band);
        Assert.NotSame(band, frame.Band);
    }

    [Fact]
    public async Task A_re_read_that_no_longer_finds_the_instance_keeps_what_was_shown_and_says_so()
    {
        var frame = await Opened("project-build");
        _server.WorkflowAnswer = RouteAnswer.Problem("workflow-instance", "not-found");

        await frame.Refresh(_api, Token);

        Assert.Equal((FramePhase.Shown, Labels.InstanceGone, Text(Golden("project-build")["title"])), (frame.Phase, frame.Note, frame.Title));
    }

    [Fact]
    public async Task Open_in_hands_the_subjects_link_to_the_launcher()
    {
        var launcher = new FakeLauncher();
        var scenario = "project-build";
        var detail = FixtureInstances.Detail(scenario);
        var line = new SubjectLineViewModel(detail.Subject, detail.Address, launcher);

        await line.OpenIn(Token);

        Assert.True(line.HasHref);
        Expect.Sequence([new Uri(line.Href).AbsoluteUri], launcher.Opened);
        var plain = new SubjectLineViewModel(detail.Subject with { Href = "ftp://example.test/x" }, detail.Address, launcher);
        await plain.OpenIn(Token);
        _ = Assert.Single(launcher.Opened);
    }
}
