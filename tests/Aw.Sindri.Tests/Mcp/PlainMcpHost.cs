using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>One host with nothing in it, shared by the tests that only list tools or read an empty queue.</summary>
public sealed class PlainMcpHost : IAsyncLifetime
{
    private GoldenHost? _host;

    internal GoldenHost Host => _host ?? throw new InvalidOperationException("the host isn't started");

    public async ValueTask InitializeAsync() => _host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }
}
