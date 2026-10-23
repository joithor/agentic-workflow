using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The engine's goldens for what a desk step asks the person to read, the session behind a step and the reason code (engine q65, #833): the buttons, the session link and the words.</summary>
[Lane(TestLane.Unit)]
public sealed class DeskReviewGoldenTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private const string ScopePath = "/home/dev/.agentic-workflow/packets/acme/web/scope-7/frozen";
    private const string PrUrl = "https://github.com/acme/web/pull/412";
    private readonly FakeSindriApi _server = new();
    private readonly FakeLauncher _launcher = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_desk_reviews_golden_draws_a_button_for_the_scope_and_the_PR_and_none_for_the_questions()
    {
        var detail = FixtureInstances.Detail("desk-reviews");
        var expanded = new ExpandedStateViewModel(detail.Current, detail.Address, new FakeClipboard(), _clock) { Launcher = _launcher };
        _launcher.AddFile(ScopePath);

        Assert.Equal(["Open the scope", "Open the PR"], expanded.Reviews.Select(b => b.Label), StringComparer.Ordinal);   // the questions (no path) stay in the desk form
        await expanded.Reviews[0].Open();
        await expanded.Reviews[1].Open();

        Assert.Equal([ScopePath, PrUrl], _launcher.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public void The_agent_run_golden_names_the_session_the_run_links_to()
    {
        var detail = FixtureInstances.Detail("agent-run-session");

        var session = Assert.IsType<SessionHandle>(detail.Current.Status.Run!.Session);

        Assert.Equal("claude-code/0b1c2d3e-0000-4000-8000-000000000001", session.Ref());
    }

    [Fact]
    public async Task The_frame_links_to_the_served_session_without_looking_it_up()
    {
        var detail = FixtureInstances.Detail("agent-run-session");
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "agent-run-session"));
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);
        await frame.Open(_api, detail.Address, detail.Id, Token);
        var asked = new List<string>();
        frame.SessionRequested += (_, e) => asked.Add(e.Ref);

        frame.Expanded!.OpenSession();

        Assert.Equal((true, Labels.SessionLink), (frame.Expanded.HasSession, frame.Expanded.SessionLinkText));
        Assert.Equal(["claude-code/0b1c2d3e-0000-4000-8000-000000000001"], asked, StringComparer.Ordinal);
    }

    [Fact]
    public void The_status_golden_names_the_session_too()
    {
        var status = FixtureInstances.Status("agent-run-session");

        Assert.Equal("claude-code/0b1c2d3e-0000-4000-8000-000000000001", status.Run!.Session!.Ref());
    }

    [Fact]
    public void A_dispatch_refused_status_reads_as_words_in_the_frame()
    {
        var status = FixtureInstances.Status("parked-dispatch-refused");

        var expanded = new ExpandedStateViewModel(new CurrentState(status.Header.State, Step: null, Gates: [], Desk: null, status), status.Header.Address, new FakeClipboard(), _clock);

        Assert.Equal(("SND-WF-010", "The step's dispatch was refused"), (status.ReasonCode, expanded.ReasonLine));
    }
}
