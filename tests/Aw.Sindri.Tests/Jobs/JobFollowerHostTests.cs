using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The job follower against a real host on loopback (API P6 unit H, left from unit F): the stream ends when the process stops.</summary>
[Lane(TestLane.Integration)]
public sealed class JobFollowerHostTests
{
    [Fact(Timeout = 60_000)]
    public async Task A_follower_sees_the_stream_end_and_exits_1()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await JobHost.Start(ct);
        try
        {
            var (job, sequence) = await JobHost.StartEcho(host, lines: 1_000, delayMs: 20);
            var started = new JobStartedResponse(new Core.Commands.CommandAccepted(CommandId.From(Guid.CreateVersion7()), sequence), job,
                new JobLinks($"/sindri/jobs/{job.Value}", $"/sindri/jobs/{job.Value}/cancel", "/sindri/events?after=" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) + "&types=job.*"));
            var probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri(host.App.Urls.First()), 42, 3), new Ping(42, 3));
            var tokens = new HostToken(host.Token);
            using var client = new ControlPlaneClient(probe, SliceHarness.Json,
                CallerHeaders.For(new AgentSession(Signal: null, Terminal: true, Prover: false), tokens, _ => ValueTask.FromResult((RepoName?)null)));
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var follow = Task.Run(() => new JobFollower(client, new Output(stdout, stderr, SliceHarness.Json), TimeProvider.System).Follow(started, json: false, default, default), ct);

            while (!stdout.ToString().Contains("echo", StringComparison.Ordinal))
            {
                await Task.Delay(20, ct);   // the first line arrived: the follower is on the stream
            }

            await JobHost.ReleaseLease(host, ct);
            await host.DisposeAsync();

            Assert.Equal(1, await follow.WaitAsync(TimeSpan.FromSeconds(30), ct));
            Assert.Contains("not running", stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private sealed class HostToken(HumanToken token) : IHumanTokenStore
    {
        public ValueTask<Result<HumanToken>> Mint(CancellationToken cancellationToken) => ValueTask.FromResult<Result<HumanToken>>(token);

        public ValueTask<Result<HumanToken?>> Read(CancellationToken cancellationToken) => ValueTask.FromResult<Result<HumanToken?>>(token);

        public ValueTask<Result<Unit>> Remove(HumanToken mine, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
