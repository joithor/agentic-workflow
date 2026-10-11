using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The typed client of Sindri's loopback HTTP API.</summary>
/// <param name="handler">The HTTP handler (a socket handler in the app, a canned one in tests).</param>
internal sealed class SindriApiClient(HttpMessageHandler handler) : ISindriApi
{
    public Task<Result<PingView>> Ping(Uri api, CancellationToken cancellationToken) => throw new NotSupportedException($"red {handler} {SindriUiErrors.NotRunning.Code} {SindriUiJsonContext.Default.Options.PropertyNamingPolicy}");

    public Task<Result<StatusView>> Status(Uri api, CancellationToken cancellationToken) => throw new NotSupportedException($"red {handler} {SindriUiErrors.NotRunning.Code} {SindriUiJsonContext.Default.Options.PropertyNamingPolicy}");

    public IAsyncEnumerable<EventView> Events(Uri api, long after, CancellationToken cancellationToken) => throw new NotSupportedException($"red {handler} {SindriUiErrors.NotRunning.Code} {SindriUiJsonContext.Default.Options.PropertyNamingPolicy}");
}
