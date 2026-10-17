using System.Runtime.InteropServices;
using System.Text.Json;
using Aw.Kernel.Telemetry;
using Aw.Meta.Host;
using Aw.Meta.Host.Commands;
using Microsoft.Extensions.DependencyInjection;

var (arguments, environment, problem) = TeamOption.Apply(args, Environment.GetEnvironmentVariable);
if (problem.Length > 0)
{
    return CommandOutput.Usage(ConsoleIo.Standard(), problem, "[--team <name>] <command> [arguments]");
}

var services = Composition.Compose(new ServiceCollection(), environment, TimeProvider.System);
var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
await using (provider.ConfigureAwait(false))
{
    // SIGINT, SIGTERM and SIGHUP (a closed pane) cancel the running command instead of ending the process: a train then stops its
    // steps' process trees and writes its end line before aw-meta exits. A second signal is not swallowed: it ends the process.
    using var interrupt = new CancellationTokenSource();
    void OnSignal(PosixSignalContext context)
    {
        context.Cancel = !interrupt.IsCancellationRequested;
        interrupt.Cancel();
    }

    using var onInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, OnSignal);
    using var onTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnSignal);
    using var onHup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnSignal);
    _ = provider.GetRequiredService<TelemetryRuntime>();   // resolved so the container disposes it on exit, which flushes
    try
    {
        return await CommandTable.Run(arguments, provider, provider.GetRequiredService<JsonSerializerOptions>(), ConsoleIo.Standard() with { Env = environment }, interrupt.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (interrupt.IsCancellationRequested)
    {
        return 130;   // interrupted by SIGINT, SIGTERM or SIGHUP
    }
}
