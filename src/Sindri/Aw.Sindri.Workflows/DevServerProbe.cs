using System.Net;
using System.Net.Sockets;
using Temporalio.Api.OperatorService.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The dev server's frontend over the Temporal SDK: is the 'sindri' namespace there, and the search attributes it needs.</summary>
internal static class DevServerProbe
{
    /// <summary>Asks the frontend for the 'sindri' namespace, within <paramref name="timeout"/>.</summary>
    /// <param name="frontend">The loopback frontend.</param>
    /// <param name="timeout">How long the probe may take.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>The answer, and the reason when it isn't ready.</returns>
    public static async Task<(DevServerAnswer Answer, string? Problem)> Ask(IPEndPoint frontend, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        try
        {
            using (var tcp = new TcpClient())
            {
                await tcp.ConnectAsync(frontend, bounded.Token).ConfigureAwait(false);   // refused fast, before the SDK's connect
            }

            // WaitAsync bounds the wait itself: the SDK's lazy connect ignores the RPC's cancellation, so against a server
            // that accepts TCP but never answers (SIGSTOPped, say) the call alone would hang.
            _ = await Connection(frontend).WorkflowService
                .DescribeNamespaceAsync(new DescribeNamespaceRequest { Namespace = SindriTemporal.Namespace }, Options(timeout, bounded.Token))
                .WaitAsync(bounded.Token).ConfigureAwait(false);
            return (DevServerAnswer.Ready, null);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)
        {
            return (DevServerAnswer.NoNamespace, e.Message);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            return (DevServerAnswer.Unreachable, e.Message);
        }
    }

    /// <summary>Registers the search attributes the namespace doesn't have yet (idempotent: an existing one is left alone).</summary>
    /// <param name="frontend">The loopback frontend.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when they are registered.</returns>
    public static async Task RegisterSearchAttributes(IPEndPoint frontend, CancellationToken cancellationToken)
    {
        var connection = Connection(frontend);
        var options = Options(TimeSpan.FromSeconds(10), cancellationToken);
        var listed = await connection.OperatorService
            .ListSearchAttributesAsync(new ListSearchAttributesRequest { Namespace = SindriTemporal.Namespace }, options)
            .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        var add = new AddSearchAttributesRequest { Namespace = SindriTemporal.Namespace };
        foreach (var (name, type) in SindriTemporal.SearchAttributes.Where(a => !listed.CustomAttributes.ContainsKey(a.Name)))
        {
            add.SearchAttributes.Add(name, type);
        }

        if (add.SearchAttributes.Count > 0)
        {
            _ = await connection.OperatorService.AddSearchAttributesAsync(add, options).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
    }

    private static TemporalConnection Connection(IPEndPoint frontend) =>
        TemporalConnection.CreateLazy(new TemporalConnectionOptions(frontend.ToString()));

    private static RpcOptions Options(TimeSpan timeout, CancellationToken cancellationToken) =>
        new() { Timeout = timeout, CancellationToken = cancellationToken, Retry = false };
}
