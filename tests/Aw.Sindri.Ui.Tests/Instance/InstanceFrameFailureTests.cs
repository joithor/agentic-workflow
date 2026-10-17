using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>A first read that fails (review run 1, V6): whatever the kind of failure, the frame leaves "Loading" for a problem state that has Back, never a skeleton that never ends.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceFrameFailureTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (string Address, string Id) Project => (FixtureInstances.Detail("project-build").Address, FixtureInstances.Detail("project-build").Id);

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "SND-AUTH-001")]
    [InlineData(HttpStatusCode.Conflict, "SND-API-010")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "SND-API-503")]
    [InlineData(HttpStatusCode.GatewayTimeout, "SND-API-504")]
    public async Task A_failed_first_read_shows_the_served_code_and_detail_with_Back(HttpStatusCode status, string code)
    {
        _server.WorkflowAnswer = RouteAnswer.Problem(status, code, "the served detail");
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);

        await frame.Open(_api, Project.Address, Project.Id, Token);

        Assert.Equal((true, false), (frame.IsProblem, frame.IsLoading));
        Assert.Contains(code, frame.ProblemText, StringComparison.Ordinal);
        Assert.Contains("the served detail", frame.ProblemText, StringComparison.Ordinal);
        Assert.Equal(FrameFocus.Back, frame.InitialFocus);
    }

    [Fact]
    public async Task A_failed_address_lookup_shows_a_problem_too()
    {
        _server.WorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Forbidden, "SND-AUTH-001", "the served detail");
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);

        await frame.Open(_api, Project.Address, id: null, Token);

        Assert.Equal((true, false), (frame.IsProblem, frame.IsLoading));
        Assert.Contains("SND-AUTH-001", frame.ProblemText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_later_read_leaves_the_problem_when_the_instance_is_served()
    {
        _server.WorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-API-010", "views are rebuilding");
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);
        await frame.Open(_api, Project.Address, Project.Id, Token);
        _server.WorkflowAnswer = null;

        await frame.Refresh(_api, Token);

        Assert.Equal((FramePhase.Shown, false), (frame.Phase, frame.IsProblem));
    }

    [Fact]
    public async Task A_link_that_matches_several_instances_opens_none_of_them()
    {
        var body = GoldenFiles.Node("workflow-instances", "open");
        var rows = body["instances"]!.AsArray();
        var twin = rows[0]!.DeepClone();
        twin["address"] = "twin/" + (string)rows[0]!["address"]!;
        twin["id"] = "WFI-twin";
        rows.Add(twin);
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);

        await frame.Open(_api, (string)rows[0]!["itemId"]!, id: null, Token);   // an item id: both rows match, neither is exact

        Assert.Equal(FramePhase.NotFound, frame.Phase);
    }

    [Fact]
    public async Task A_stacked_open_that_cannot_be_read_focuses_Back()
    {
        _server.WorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-API-404", "no such instance");
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), _clock);
        frame.Intend(Project.Address, FrameIntent.OpenInitial);

        await frame.Open(_api, Project.Address, Project.Id, Token);

        Assert.Equal(FrameFocus.Back, frame.WantedFocus?.Target);
    }
}
