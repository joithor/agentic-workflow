using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>Which verbs start the control plane (API spec §7): never from an agent session; 'profile approve' from a human unless --no-start.</summary>
public sealed class CliNoStartTests : IDisposable
{
    private static readonly (ControlEndpoint, Ping) _found = (new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly FakeControlPlaneProbe _probe = new();
    private readonly SteppedClock _clock = new(new FakeTimeProvider(KnownInstants.Epoch));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static AgentSession InAgent => new(Signal: "CLAUDECODE is set", Terminal: true, Prover: false);

    private static AgentSession Human => new(Signal: null, Terminal: true, Prover: false);

    [Fact(Timeout = 10_000), Trait("Lane", "unit")]
    public async Task Bare_sindri_in_an_agent_session_prints_SND_API_001_and_launches_nothing()
    {
        var launcher = new FakeControlPlaneLauncher();
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: new CliStubApi(HttpStatusCode.OK, "{}"));

        var code = await Start(launcher, client, InAgent).Run(TestContext.Current.CancellationToken);

        Assert.Equal((1, 0), (code, launcher.Launches));
        Assert.Equal("SND-API-001 Sindri isn't running; ask the human to run 'sindri' (an agent session never starts it: CLAUDECODE is set)\n",
            _error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact(Timeout = 10_000), Trait("Lane", "unit")]
    public async Task An_agent_session_never_starts_it_for_a_command_either()
    {
        var launcher = new FakeControlPlaneLauncher();
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: new CliStubApi(HttpStatusCode.OK, "{}"));

        var refused = await Start(launcher, client, InAgent).EnsureRunning(launch: true, TestContext.Current.CancellationToken);

        Assert.Equal(("SND-API-001", 0), (refused?.Code, launcher.Launches));
        Assert.Contains("ask the human to run 'sindri'", refused?.Message, StringComparison.Ordinal);
    }

    [Theory(Timeout = 10_000), Trait("Lane", "unit")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_approve_starts_the_control_plane_unless_no_start(bool noStart)
    {
        var launcher = new FakeControlPlaneLauncher { OnLaunch = () => _probe.Answer = _found };
        var preview = Encoding.UTF8.GetString(AwJson.Serialize(new ApproveProfileResponse(SliceHarness.Profile().Hash, [], Approved: false, Sequence: null), SliceHarness.Json));
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: new CliStubApi(HttpStatusCode.OK, preview));
        var start = Start(launcher, client, Human);
        var verbs = new Verbs(client, _probe, new Output(_out, _error, SliceHarness.Json), new ServiceCollection().BuildServiceProvider(),
            ct => start.EnsureRunning(launch: !noStart, ct));

        var code = await Drive(verbs.Approve(hashText: null, TestContext.Current.CancellationToken));

        Assert.Equal((1, noStart ? 0 : 1), (code, launcher.Launches));
        if (noStart)
        {
            Assert.StartsWith("SND-API-001 ", _error.ToString(), StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("to approve: sindri profile approve", _out.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact(Timeout = 10_000), Trait("Lane", "unit")]
    public async Task A_session_without_a_terminal_doesnt_start_it_for_a_command()
    {
        var launcher = new FakeControlPlaneLauncher();
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: new CliStubApi(HttpStatusCode.OK, "{}"));

        var down = await Start(launcher, client, new AgentSession(Signal: null, Terminal: false, Prover: false)).EnsureRunning(launch: true, TestContext.Current.CancellationToken);

        Assert.Equal(("SND-API-001", 0), (down?.Code, launcher.Launches));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("CLAUDECODE is set", true)]
    [InlineData("parent process codex", true)]
    [InlineData(null, false)]
    public void Serve_is_refused_whenever_the_cli_saw_an_agent_signal(string? signal, bool refused)
    {
        // The prover marker acts earlier, in AgentSession.Detect (CliCallerTests.Prover_marker_skips_the_parent_check_but_not_the_variables).
        var refusal = StartCommand.ServeRefusal(new AgentSession(signal, Terminal: false, Prover: false));

        Assert.Equal(refused, refusal is not null);
        if (refusal is not null)
        {
            Assert.Equal("SND-API-001", refusal.Code);
            Assert.Contains($"an agent session never starts Sindri ({signal}); ask the human to run 'sindri'", refusal.Message, StringComparison.Ordinal);
        }
    }

    private StartCommand Start(FakeControlPlaneLauncher launcher, ControlPlaneClient client, AgentSession session) =>
        new(_probe, launcher, client, new Output(_out, _error, SliceHarness.Json), _clock, session);

    /// <summary>Steps the fake clock one poll at a time, each once the start wait armed its delay, until <paramref name="run"/> ends.</summary>
    private async Task<T> Drive<T>(Task<T> run)
    {
        while (true)
        {
            await _clock.Armed(() => run.IsCompleted);
            if (run.IsCompleted)
            {
                return await run;
            }

            await _clock.Step(StartCommand.Poll);
        }
    }
}
