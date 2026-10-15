using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Finds and connects to the dev server a CLI command talks to (engine P4a: no 'serve' route yet): the
/// '--temporal host:port' override when given, else the frontend control.json's 'temporal' field records. The connected
/// client is kept for the life of the process, one per target and namespace: a connection is a gRPC channel, and the
/// bridge asks for one every few seconds. A target that changes (serve restarted on another port) connects anew.
/// </summary>
/// <param name="endpoints">control.json.</param>
/// <param name="settings">The namespace.</param>
internal sealed class TemporalFrontend(IEndpointFile endpoints, TemporalSettings settings) : IDisposable
{
    private readonly SemaphoreSlim _connecting = new(1, 1);
    private volatile Cached? _cached;

    public void Dispose() => _connecting.Dispose();

    /// <summary>Connects to the frontend, on the settings' namespace.</summary>
    /// <param name="flag">The '--temporal host:port' override, or null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The client, or SND-API-001 when serve's server isn't recorded or doesn't answer.</returns>
    public async ValueTask<Result<ITemporalClient>> Connect(string? flag, CancellationToken cancellationToken)
    {
        var target = flag;
        if (target is null)
        {
            var read = await endpoints.Read(cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            if (read.Value?.Temporal is not { } temporal)
            {
                return SindriErrors.NotRunning.With(
                    "control.json records no Temporal dev server (fix: start sindri serve)");
            }

            target = $"127.0.0.1:{temporal.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        return await Connected(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The kept client for <paramref name="target"/> on the settings' namespace, connecting once when there is none.</summary>
    private async ValueTask<Result<ITemporalClient>> Connected(string target, CancellationToken cancellationToken)
    {
        var key = $"{settings.Namespace}@{target}";
        if (_cached is { } cached && string.Equals(cached.Key, key, StringComparison.Ordinal))
        {
            return Results.Success(cached.Client);
        }

        await _connecting.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is { } again && string.Equals(again.Key, key, StringComparison.Ordinal))
            {
                return Results.Success(again.Client);
            }

            var client = await TemporalClient.ConnectAsync(new TemporalClientConnectOptions(target) { Namespace = settings.Namespace }).ConfigureAwait(false);
            _cached = new Cached(key, client);
            return Results.Success<ITemporalClient>(client);
        }
        catch (Exception e) when (e is RpcException or InvalidOperationException) // boundary: an unreachable frontend is an expected failure
        {
            return SindriErrors.NotRunning.With($"Temporal at {target} doesn't answer: {e.Message} (fix: start sindri serve)");
        }
        finally
        {
            _ = _connecting.Release();
        }
    }

    private sealed record Cached(string Key, ITemporalClient Client);
}
