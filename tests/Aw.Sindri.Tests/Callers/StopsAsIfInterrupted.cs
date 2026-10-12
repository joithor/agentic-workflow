using Microsoft.Extensions.Hosting;

namespace Aw.Sindri.Tests.Callers;

/// <summary>Stops the application once it has started, as the console lifetime does on Ctrl-C (SIGINT).</summary>
internal sealed class StopsAsIfInterrupted(IHostApplicationLifetime lifetime) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        lifetime.StopApplication();
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
