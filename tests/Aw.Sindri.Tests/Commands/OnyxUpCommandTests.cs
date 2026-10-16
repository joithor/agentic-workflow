using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Onyx;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri onyx up' and 'onyx down' as OnyxVerbs runs them, over the fake rig: who may run them, what they print where, and the exit codes.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class OnyxUpCommandTests : IDisposable
{
    private const string HandOver = "Onyx is running. Account setup (sign-up and Sindri's key) comes next: `sindri onyx up` will continue it once that stage lands.";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly OnyxUpRig _rig = new();
    private readonly ServiceProvider _services;

    public OnyxUpCommandTests()
    {
        _rig.Progress.Forward = new ConsoleOnyxProgress(_error);
        _services = new ServiceCollection()
            .AddSingleton(_rig.Service())
            .AddSingleton<IService<OnyxDownRequest, OnyxDownResponse>>(
                new OnyxDownService(_rig.Workspace, new Aw.Sindri.Core.Onyx.OnyxFacts(_rig.Runtime, _rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), _rig.Runtime))
            .BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    private static AgentSession Human { get; } = new(Signal: null, Terminal: true, Prover: false);

    private OnyxVerbs Verbs(AgentSession session) => new(_services, new Output(_out, _error, _json), session);

    [Fact, Trait("Lane", "unit")]
    public async Task Up_as_a_session_is_refused_with_SND_AUTH_002_and_calls_nothing()
    {
        var code = await Verbs(new AgentSession("CLAUDECODE is set", Terminal: true, Prover: false)).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("CLAUDECODE is set", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("ask the human to run `sindri onyx up`", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_rig.Runtime.Calls);
        Assert.Equal(string.Empty, _out.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_without_a_terminal_is_refused()
    {
        var code = await Verbs(new AgentSession(Signal: null, Terminal: false, Prover: false)).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("no terminal", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_rig.Runtime.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_prints_url_version_and_the_P3_hand_over_line_on_stdout_and_progress_on_stderr()
    {
        var code = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        var lines = _out.ToString().TrimEnd('\n').Split('\n');
        Assert.Equal(
            [
                "url       http://127.0.0.1:3000",
                "version   v4.9.0",
                "ran       fetch, config, pull, start",
                "skipped   none",
                HandOver,
            ],
            lines,
            StringComparer.Ordinal);
        Assert.Contains("fetch: running\n", _error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("start: running", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("running\n", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_names_the_skipped_steps_on_a_rerun()
    {
        _ = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);
        _out.GetStringBuilder().Clear();

        _ = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Contains("ran       none", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("skipped   fetch, config, pull, start", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_json_prints_the_response_only()
    {
        var code = await Verbs(Human).Up(json: true, TestContext.Current.CancellationToken);

        var response = AwJson.Deserialize<OnyxUpResponse>(Encoding.UTF8.GetBytes(_out.ToString()), _json).Value;
        Assert.Equal(0, code);
        Assert.Equal("http://127.0.0.1:3000", response.Url);
        Assert.DoesNotContain("USER_AUTH", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_maps_SND_ONYX_003_to_exit_1_with_the_fix_text()
    {
        _rig.Probes.FreeBytes = 1L << 30;

        var code = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-ONYX-003", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Free space on that volume, then run this again", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _out.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_maps_a_busy_lock_to_exit_1()
    {
        using var held = _rig.Workspace.TryLock();

        var code = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-API-009", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_as_a_session_is_refused()
    {
        var code = await Verbs(new AgentSession("CLAUDECODE is set", Terminal: true, Prover: false)).Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_rig.Runtime.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_prints_stopped_and_exits_0()
    {
        _ = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);
        _out.GetStringBuilder().Clear();

        var code = await Verbs(Human).Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("Onyx stopped", _out.ToString(), StringComparison.Ordinal);
        Assert.All(_rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_on_a_box_never_set_up_says_so_and_exits_0()
    {
        var code = await Verbs(Human).Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("stopped", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("nothing was running", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_maps_a_remote_docker_to_exit_1()
    {
        _ = await Verbs(Human).Up(json: false, TestContext.Current.CancellationToken);
        _rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = "tcp://10.0.0.5:2375" };

        var code = await Verbs(Human).Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-ONYX-010", _error.ToString(), StringComparison.Ordinal);
    }
}
