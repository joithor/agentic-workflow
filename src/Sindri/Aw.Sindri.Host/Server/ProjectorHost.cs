namespace Aw.Sindri.Host.Server;

/// <summary>
/// Runs the projector while 'sindri serve' runs: a pass, then a second's wait on the clock (P5's ledger signal replaces
/// the poll). A stalled projector's pass is a no-op until its 30 s retry (Projector.StallRetry).
/// </summary>
internal sealed partial class ProjectorHost(Projector projector, TimeProvider clock, ILogger<ProjectorHost> log) : BackgroundService
{
    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _ = await projector.CatchUp(stoppingToken).ConfigureAwait(false);
                await Task.Delay(_tick, clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a broken pass must not stop the read side; the next tick tries again
            {
                LogPassFailed(log, e.GetType().Name, e.Message);
                await Task.Delay(_tick, clock, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The projector's pass threw {Type}: {Detail}")]
    private static partial void LogPassFailed(ILogger logger, string type, string detail);
}
