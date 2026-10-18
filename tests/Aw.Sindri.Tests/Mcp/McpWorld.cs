using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>A fresh two-repo host with an item in each, whose views a background pump keeps caught up (the host runs no projector of its own).</summary>
internal sealed class McpWorld : IAsyncDisposable
{
    private McpPump? _pump;

    public required GoldenHost Host { get; init; }

    public required string AlphaItem { get; init; }

    public required string BetaItem { get; init; }

    public static async Task<McpWorld> Start(CancellationToken ct, bool pump = true, Action<IServiceCollection>? extra = null)
    {
        var host = await GoldenHost.Start(temporary: true, ct, SeededMcp.Profile, extra: extra);
        await ViewScenarios.Approve(host, ct);
        var world = new McpWorld
        {
            Host = host,
            AlphaItem = await ItemScenarios.Create(host, "Alpha one", "alpha"),
            BetaItem = await ItemScenarios.Create(host, "Beta one", "beta"),
        };
        await ViewScenarios.CatchUp(host, ct);
        world._pump = pump ? McpPump.Start(host) : null;
        return world;
    }

    public async Task<string> Human(string route, CancellationToken ct)
    {
        await Settle(ct);
        return (await Host.Http.Ask(Get(route, CallerRequests.Human(Host.Token.Value)))).Body;
    }

    public Task Settle(CancellationToken ct) => ViewScenarios.CatchUp(Host, ct);

    public async ValueTask DisposeAsync()
    {
        if (_pump is not null)
        {
            await _pump.DisposeAsync();
        }

        await Host.DisposeAsync();
    }
}
