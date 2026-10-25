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

    [Theory(Timeout = 10_000), Trait("Lane", "unit")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_approve_starts_the_control_plane_unless_no_start(bool noStart)
    {
        var launcher = new FakeControlPlaneLauncher { OnLaunch = () => _probe.Answer = _found };
        var preview = Encoding.UTF8.GetString(AwJson.Serialize(new ApproveProfileResponse(SliceHarness.Profile().Hash, [], Approved: false, Sequence: null), SliceHarness.Json));
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: new CliStubApi(HttpStatusCode.OK, preview));
        var start = Start(launcher, client);
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

    private StartCommand Start(FakeControlPlaneLauncher launcher, ControlPlaneClient client) =>
        new(_probe, launcher, client, new Output(_out, _error, SliceHarness.Json), _clock);

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
