using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Keeps the current map's view warm: shortly after serve starts and every 30 s after, one read as a person at the app (ContextWarmup). While
/// nothing was written it is a cache hit; after a write it builds the view the next search would otherwise wait for.
/// </summary>
internal sealed partial class ContextWarmHost(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ContextWarmHost> log) : BackgroundService
{
    private static readonly TimeSpan _first = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _every = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = _first;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(wait, clock, stoppingToken).ConfigureAwait(false);
                wait = _every;
                var scope = scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    _ = await scope.ServiceProvider.GetRequiredService<ContextWarmup>().Warm(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)   // boundary: a failed warm read must not stop the process; the next one tries again
            {
                LogFailed(log, e.GetType().Name, e.Message);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The context warm read threw {Type}: {Detail}; trying again")]
    private static partial void LogFailed(ILogger logger, string type, string detail);
}
