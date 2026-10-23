using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The adapter's own cases beyond the contract: the doctor's line on a real server, and a missing binary.</summary>
[Lane(TestLane.Integration)]
public sealed class DevServerTemporalTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 60_000)]
    public async Task Doctor_reports_a_running_server_ok_at_the_pin_with_the_namespace()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = new DevServerTemporal(new TemporalSettings(_home.Path, PinnedTemporal.Path()), new TemporalDevServer(ChildEnvironment.Start));
        var started = (await adapter.Ensure(ct)).Value;
        try
        {
            var check = DoctorRules.Temporal(await adapter.Inspect(ct));

            Assert.Equal(CheckState.Ok, check.State);
            Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"127.0.0.1:{started.Port} answers (pid {started.Pid}), version {PinnedTemporal.Version} as pinned, namespace sindri present"), check.Detail);
        }
        finally
        {
            _ = await adapter.Stop(ct);
        }

        var stopped = DoctorRules.Temporal(await adapter.Inspect(ct));
        Assert.Equal((CheckState.Fail, "sindri"), (stopped.State, stopped.Fix));
    }

    [Fact(Timeout = 60_000)]
    public async Task A_stop_cancelled_while_it_waits_for_a_hung_worker_still_leaves_no_dev_server()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = new DevServerTemporal(new TemporalSettings(_home.Path, PinnedTemporal.Path()), new TemporalDevServer(ChildEnvironment.Start));
        var started = (await adapter.Ensure(ct)).Value;
        var runtime = new TemporalRuntime(adapter, TimeProvider.System);
        runtime.Track(new TaskCompletionSource().Task);   // a worker that never stops
        using var gaveUp = new CancellationTokenSource(TimeSpan.FromSeconds(1));   // the CLI or a test teardown timing out

        try
        {
            _ = await runtime.Stop(gaveUp.Token);
        }
        catch (OperationCanceledException)
        {
            // the caller gave up; the server must be gone all the same
        }

        // why: the dev server runs detached (setsid), so one a cancelled stop leaves behind outlives serve and the test run
        Assert.True(DevServerProcesses.Gone(started.Pid), "the dev server is still running");
        Assert.Empty(new TemporalDevServer(ChildEnvironment.Start).Find(_home.Path));
    }

    [Fact(Timeout = 60_000)]
    public async Task Ensure_without_the_pinned_binary_is_SND_CLI_002_naming_the_fetch()
    {
        var missing = Path.Combine(_home.Path, "no-such", "temporal");
        var adapter = new DevServerTemporal(new TemporalSettings(_home.Path, missing), new TemporalDevServer(ChildEnvironment.Start));

        var refused = await adapter.Ensure(TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.StartFailed.Code, refused.Error.Code);
        Assert.EndsWith("(fix: scripts/binaries-lock.sh fetch temporal)", refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains(missing, refused.Error.Message, StringComparison.Ordinal);
    }
}
