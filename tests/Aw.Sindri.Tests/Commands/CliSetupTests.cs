using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri setup [--json]': a client of GET /sindri/setup that only reports, never starts the control plane.</summary>
public sealed class CliSetupTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly DateTimeOffset _at = new(2026, 10, 19, 9, 30, 0, TimeSpan.Zero);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Body(params SetupStep[] steps) => Encoding.UTF8.GetString(AwJson.Serialize(new GetSetupResponse(steps, _at), SliceHarness.Json));

    private SetupVerbs Verbs(FakeControlPlaneProbe probe, Api api) =>
        new(new ControlPlaneClient(probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Signal: null, Terminal: true, Prover: false), new InMemoryHumanTokenStore(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: api),
            new Output(_out, _error, SliceHarness.Json));

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_exits_1_without_calling_anything_when_serve_is_down()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(new FakeControlPlaneProbe(), api).Run([], Token);

        Assert.Equal((1, 0), (code, api.Calls));
        Assert.StartsWith("SND-API-001 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_prints_a_line_per_step_with_its_plain_title_state_and_detail_and_exits_1_while_one_is_open()
    {
        var api = new Api(HttpStatusCode.OK, Body(
            SetupSteps.Repo(new RepoFact(1)),
            SetupSteps.Profile(new ProfileFact(ProfilePresence.Unapproved)),
            SetupSteps.Recipes(new RecipesFact(26, 20)),
            SetupSteps.Map(new MapFact(MapPhase.Failed, FailureCode: "SND-CONTEXT-040"))));

        var code = await Verbs(_up, api).Run([], Token);

        Assert.Equal((1, "/sindri/setup"), (code, api.Path));
        Assert.Equal(
            "done      Add your repo: 1 repository added.\n"
            + "needs-you Approve your profile: Your profile is ready for you to read and approve.\n"
            + "needs-you Approve the workflows: 6 workflows waiting for approval. (20 of 26)\n"
            + "failed    Map your code: The last try at building the map didn't finish.\n"
            + "1 of 4 steps done.\n",
            _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_exits_0_when_every_step_is_done()
    {
        var api = new Api(HttpStatusCode.OK, Body(SetupSteps.Repo(new RepoFact(1)), SetupSteps.Profile(new ProfileFact(ProfilePresence.Approved))));

        var code = await Verbs(_up, api).Run([], Token);

        Assert.Equal((0, "2 of 2 steps done."), (code, _out.ToString().ReplaceLineEndings("\n").Split('\n')[^2]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_json_prints_the_routes_body_as_served()
    {
        var body = Body(SetupSteps.Repo(new RepoFact(0)));

        var code = await Verbs(_up, new Api(HttpStatusCode.OK, body)).Run(["--json"], Token);

        Assert.Equal((1, body), (code, _out.ToString().Trim()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Setup_refuses_an_argument_it_does_not_know_before_calling_anything()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(_up, api).Run(["--repair"], Token);

        Assert.Equal((2, 0), (code, api.Calls));
    }

    private sealed class Api(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Path = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
