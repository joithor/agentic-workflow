using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Finds and connects to the dev server a CLI command talks to (engine P4a: no 'serve' route yet): the
/// '--temporal host:port' override when given, else the frontend control.json's 'temporal' field records.
/// </summary>
/// <param name="endpoints">control.json.</param>
internal sealed class TemporalFrontend(IEndpointFile endpoints)
{
    /// <summary>Connects to the frontend, on the 'sindri' namespace.</summary>
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
                    "control.json records no Temporal dev server (fix: start sindri serve, or pass --temporal <host:port>)");
            }

            target = $"127.0.0.1:{temporal.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        try
        {
            return await TemporalClient.ConnectAsync(new TemporalClientConnectOptions(target) { Namespace = SindriTemporal.Namespace }).ConfigureAwait(false);
        }
        catch (Exception e) when (e is RpcException or InvalidOperationException) // boundary: an unreachable frontend is an expected failure
        {
            return SindriErrors.NotRunning.With($"Temporal at {target} doesn't answer: {e.Message} (fix: start sindri serve, or pass --temporal <host:port>)");
        }
    }
}
