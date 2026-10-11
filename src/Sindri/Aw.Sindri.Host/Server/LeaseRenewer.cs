using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Host.Server;

/// <summary>Renews the host lease every 20 s (§17.22 D2). A lost lease stops the process: it must not write under a stale epoch.</summary>
internal sealed partial class LeaseRenewer(ILease lease, HeldLease held, IShutdown shutdown, TimeProvider clock, ILogger<LeaseRenewer> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LeaseTiming.RenewEvery, clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (held.Current is not { } grant)
            {
                continue;
            }

            var renewed = await lease.Renew(grant, stoppingToken).ConfigureAwait(false);
            if (renewed.IsSuccess)
            {
                held.Hold(renewed.Value);
                continue;
            }

            LogLost(log, renewed.Error.Code, renewed.Error.Message);
            held.Clear();
            Environment.ExitCode = 1;
            shutdown.Request();
            return;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Code} {Detail}; stopping so nothing is written under a stale epoch")]
    private static partial void LogLost(ILogger logger, string code, string detail);
}
