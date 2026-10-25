using Aw.Kernel;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the setup writes as the goldens serve them (api #528's AddRepo; the Onyx and MCP ones are spec-shaped).</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task AddRepo_answers_the_started_job_and_the_server_saw_the_path()
    {
        var server = new FakeSindriApi();

        var started = (await Reach(server).AddRepo(_api, new AddRepoRequest("/work/demo"), Token)).Value;

        Expect.Equal(("JOB-00000000000000000000000000000006", 2L), (started.JobId, started.Accepted.Sequence));
        Expect.Sequence([new AddRepoRequest("/work/demo")], server.AddedRepos);
    }

    [Theory]
    [InlineData("not-a-checkout", "SND-SETUP-001", ErrorKind.Invalid)]
    [InlineData("not-absolute", "SND-SETUP-001", ErrorKind.Invalid)]
    [InlineData("already-added", "SND-SETUP-002", ErrorKind.Conflict)]
    [InlineData("name-taken", "SND-SETUP-002", ErrorKind.Conflict)]
    [InlineData("bad-name", "SND-SETUP-002", ErrorKind.Conflict)]
    [InlineData("profile-changed", "SND-PROFILE-002", ErrorKind.Conflict)]
    public async Task AddRepo_refusals_carry_the_apis_code_and_kind(string scenario, string code, ErrorKind kind)
    {
        var server = new FakeSindriApi { AddRepoAnswer = RouteAnswer.Problem("setup-repos", scenario) };

        var result = await Reach(server).AddRepo(_api, new AddRepoRequest("/work/demo"), Token);

        Expect.Equal((code, kind), (result.Error.Code, result.Error.Kind));
    }

    [Fact]
    public async Task RegisterMcp_answers_the_started_job_and_a_repo_the_profile_does_not_name_is_a_conflict()
    {
        var server = new FakeSindriApi();
        var api = Reach(server);

        var started = (await api.RegisterMcp(_api, new RegisterMcpRequest(), Token)).Value;
        server.RegisterMcpAnswer = RouteAnswer.Problem("setup-mcp", "not-approved");
        var refused = await api.RegisterMcp(_api, new RegisterMcpRequest("other"), Token);

        Expect.Equal("JOB-000000000000000000000000000000b1", started.JobId);
        Expect.Equal(("SND-SETUP-004", ErrorKind.Conflict), (refused.Error.Code, refused.Error.Kind));
        Expect.Equal(2, server.RegisteredMcp.Count);
    }

    [Fact]
    public async Task SetupOnyx_answers_the_started_job_and_a_box_without_docker_says_so()
    {
        var server = new FakeSindriApi();
        var api = Reach(server);

        var started = (await api.SetupOnyx(_api, Token)).Value;
        server.SetupOnyxAnswer = RouteAnswer.Problem("onyx-setup", "no-docker");
        var refused = await api.SetupOnyx(_api, Token);

        Expect.Equal("JOB-000000000000000000000000000000c1", started.JobId);
        Expect.Equal("SND-ONYX-001", refused.Error.Code);
        Expect.Equal(2, server.SetupOnyxCalls);
    }

    [Theory]
    [InlineData("wrong-password", "SND-ONYX-007", ErrorKind.Invalid)]
    [InlineData("not-waiting", "SND-SETUP-006", ErrorKind.Conflict)]
    [InlineData("missing-field", "SND-SETUP-005", ErrorKind.Invalid)]
    public async Task ConnectOnyx_refusals_carry_the_apis_code_and_kind(string scenario, string code, ErrorKind kind)
    {
        var server = new FakeSindriApi { ConnectOnyxAnswer = RouteAnswer.Problem("onyx-account", scenario) };

        var result = await Reach(server).ConnectOnyx(_api, new OnyxLogin("a@b.c", "pw"), Token);

        Expect.Equal((code, kind), (result.Error.Code, result.Error.Kind));
    }

    [Fact]
    public async Task ConnectOnyx_answers_the_job_that_was_waiting_and_the_server_got_both_fields()
    {
        var server = new FakeSindriApi();

        var started = (await Reach(server).ConnectOnyx(_api, new OnyxLogin("me@example.com", "s3cret"), Token)).Value;

        Expect.Equal("JOB-000000000000000000000000000000c1", started.JobId);
        Expect.Sequence([new OnyxLogin("me@example.com", "s3cret")], server.OnyxLogins);
    }

    [Fact]
    public async Task DeferOnyx_answers_where_onyx_stands_now()
    {
        var server = new FakeSindriApi();
        var api = Reach(server);

        var later = (await api.DeferOnyx(_api, new OnyxDeferRequest(Deferred: true), Token)).Value;
        server.DeferOnyxAnswer = RouteAnswer.SpecShaped("onyx-defer", "undone");
        var back = (await api.DeferOnyx(_api, new OnyxDeferRequest(Deferred: false), Token)).Value;

        Expect.Equal((true, false), (later.Deferred, back.Deferred));
        Expect.Sequence([new OnyxDeferRequest(Deferred: true), new OnyxDeferRequest(Deferred: false)], server.OnyxDeferrals);
    }

    [Fact]
    public async Task A_write_to_a_sindri_that_is_down_is_unreachable_and_nothing_is_kept()
    {
        var server = new FakeSindriApi();
        server.GoDown();

        var result = await Reach(server).AddRepo(_api, new AddRepoRequest("/work/demo"), Token);

        Expect.Equal("SND-UI-002", result.Error.Code);
        Expect.Equal(0, server.AddedRepos.Count);
    }
}
