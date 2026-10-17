using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>Drives the projector while a test waits on a command (a command waits for the views to reach the ledger head).</summary>
internal sealed class McpPump : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task _loop = Task.CompletedTask;

    public static McpPump Start(GoldenHost host)
    {
        var pump = new McpPump();
        pump._loop = Task.Run(async () =>
        {
            while (!pump._stop.IsCancellationRequested)
            {
                try
                {
                    await ViewScenarios.CatchUp(host, pump._stop.Token);
                    await Task.Delay(10, pump._stop.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, pump._stop.Token);
        return pump;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _loop;
        _stop.Dispose();
    }
}
