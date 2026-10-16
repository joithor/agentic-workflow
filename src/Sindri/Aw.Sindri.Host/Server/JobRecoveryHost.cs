using Aw.Sindri.Features.Jobs;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Ends the jobs the previous process left running (API P6 unit F): one <see cref="JobRecovery"/> attempt each second until it succeeds (the lease is held
/// and the views have caught up with the head at start), then it is done for the life of the process. A job is never re-run.
/// </summary>
internal sealed partial class JobRecoveryHost(JobRecovery recovery, TimeProvider clock, ILogger<JobRecoveryHost> log) : BackgroundService
{
    private static readonly TimeSpan _retry = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ended = await recovery.Run(stoppingToken).ConfigureAwait(false);
                if (ended.IsSuccess)
                {
                    if (ended.Value > 0)
                    {
                        LogRecovered(log, ended.Value);
                    }

                    return;
                }

                await Task.Delay(_retry, clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a broken attempt must not stop the process; the next one tries again
            {
                LogFailed(log, e.GetType().Name, e.Message);
                await Task.Delay(_retry, clock, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SND-JOB-001 {Count} job(s) the previous process left running were ended as interrupted")]
    private static partial void LogRecovered(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job recovery threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
