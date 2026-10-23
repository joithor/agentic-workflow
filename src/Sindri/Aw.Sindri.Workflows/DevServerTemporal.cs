using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The <see cref="ITemporalServer"/> adapter: the pinned CLI's dev server on this home's database (<see cref="TemporalDevServer"/>).</summary>
internal sealed class DevServerTemporal(TemporalSettings settings, TemporalDevServer server) : ITemporalServer
{
    private static readonly TimeSpan _inspectWait = TimeSpan.FromSeconds(3);

    public async ValueTask<Result<TemporalEndpoint>> Ensure(CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.Executable))
        {
            return SindriErrors.StartFailed.With($"the pinned temporal CLI {SindriTemporal.PinnedVersion} isn't installed at {settings.Executable}, "
                + "so serve can't start the Temporal dev server (fix: scripts/binaries-lock.sh fetch temporal)");
        }

        try
        {
            var running = await server.StartAsync(new TemporalServerStart(settings.Executable, settings.Home, settings.Namespace), cancellationToken).ConfigureAwait(false);
            return new TemporalEndpoint(running.ProcessId, running.Frontend.Port, running.UiPort);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.StartFailed.With($"the Temporal dev server didn't start: {e.Message} "
                + $"(fix: see {Path.Combine(settings.Home, "logs", SindriTemporal.LogFile)}, then run sindri doctor)");
        }
    }

    public async ValueTask<Result<Unit>> Stop(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var running in server.Find(settings.Home))
            {
                try
                {
                    await server.StopAsync(running, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // StopAsync killed it before it gave up; the others go too, then the cancellation propagates
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Unit.Value;
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.StartFailed.With($"the Temporal dev server couldn't be stopped: {e.Message} (fix: run sindri doctor)");
        }
    }

    public async ValueTask<TemporalFacts> Inspect(CancellationToken cancellationToken)
    {
        if (server.Find(settings.Home) is not [var running, ..])
        {
            return new TemporalFacts(Running: null, SindriTemporal.PinnedVersion, Version: null, Reachable: false, NamespacePresent: false, Problem: null);
        }

        var (answer, problem) = await DevServerProbe.Ask(running.Frontend, settings.Namespace, _inspectWait, cancellationToken).ConfigureAwait(false);
        return new TemporalFacts(new TemporalEndpoint(running.ProcessId, running.Frontend.Port, running.UiPort), SindriTemporal.PinnedVersion,
            server.Version(running.Executable), Reachable: answer != DevServerAnswer.Unreachable, NamespacePresent: answer == DevServerAnswer.Ready, problem);
    }
}
