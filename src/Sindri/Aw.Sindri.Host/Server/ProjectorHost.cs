namespace Aw.Sindri.Host.Server;

/// <summary>Runs the projector in the background while 'sindri serve' runs.</summary>
internal sealed class ProjectorHost : BackgroundService
{
    public ProjectorHost(Projector projector, TimeProvider clock)
    {
        _ = (projector, clock);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
