namespace Aw.Sindri.Tests.Mcp;

/// <summary>One world for the tests that only file requests or are refused; each asserts on its own ids, repo or error, never on a count.</summary>
public sealed class SharedMcpWorld : IAsyncLifetime
{
    private McpWorld? _world;

    internal McpWorld World => _world ?? throw new InvalidOperationException("the world isn't started");

    public async ValueTask InitializeAsync() => _world = await McpWorld.Start(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_world is not null)
        {
            await _world.DisposeAsync();
        }
    }
}
