using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The frame's agent Step links back to its session (L2): "Session ▸" shows only when a session row with the run's launch id is known, and opens that session's sheet.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentRunSessionLinkTests
{
    private const string SessionLaunch = "LCH-00000000000000000000000000000001";   // the launched row of sessions/fixture-three-providers.json
    private const string SessionRef = "cursor/pid-333-100";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<InstanceFrameViewModel> Opened(string launch)
    {
        var detail = FixtureInstances.Detail("incident-loop");
        var body = GoldenFiles.Node("workflow-instance", "incident-loop");
        body["current"]!["status"]!["run"] = new JsonObject { ["runtime"] = "agent", ["launch"] = launch };
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        _server.Launches[launch] = RouteAnswer.Api("launches-id", "started-engine");
        _server.Items[detail.ItemId!] = new RouteAnswer(HttpStatusCode.OK, AgentRunFixtures.LiveSessionBody());
        var frame = new InstanceFrameViewModel(_server, _clipboard, _clock);
        await frame.Open(_api, detail.Address, detail.Id, Token);
        return frame;
    }

    [Fact]
    public async Task A_known_session_for_the_launch_shows_the_link_and_opens_its_sheet()
    {
        var frame = await Opened(SessionLaunch);
        var asked = new List<string>();
        frame.SessionRequested += (_, e) => asked.Add(e.Ref);
        var expanded = frame.Expanded!;

        Assert.Equal((true, Labels.SessionLink), (expanded.HasSession, expanded.SessionLinkText));
        Assert.Contains(SessionRef, expanded.SessionLinkName, StringComparison.Ordinal);
        expanded.OpenSession();

        Assert.Equal([SessionRef], asked);
    }

    [Fact]
    public async Task No_session_row_with_that_launch_shows_no_link()
    {
        var frame = await Opened(AgentRunFixtures.Launch);
        var asked = false;
        frame.SessionRequested += (_, _) => asked = true;

        frame.Expanded!.OpenSession();

        Assert.False(frame.Expanded.HasSession);
        Assert.False(asked);
    }

    [Fact]
    public async Task Sessions_that_cannot_be_read_leave_the_run_as_it_was()
    {
        _server.SessionAnswer = RouteAnswer.Problem("sessions", "unsupported-platform");

        var frame = await Opened(SessionLaunch);

        Assert.Equal((false, true), (frame.Expanded!.HasSession, frame.Expanded.HasRun));
    }
}
