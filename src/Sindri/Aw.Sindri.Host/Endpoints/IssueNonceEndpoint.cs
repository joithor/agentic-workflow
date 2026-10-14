using Aw.Kernel.Services;
using Aw.Sindri.Features.IssueNonce;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/workflow/instances/{instanceId}/nonce (engine spec rev 8 §5.2, §6.3; issue): a nonce for the next command
/// on the instance. Any caller whose class may command an instance may ask (API spec §2.5), so a session can get one.
/// </summary>
internal static class IssueNonceEndpoint
{
    internal static RouteGroupBuilder MapIssueNonce(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/instances/{instanceId}/nonce", static async (string instanceId, IService<IssueNonceRequest, IssueNonceResponse> service, CancellationToken ct)
            => (await service.Handle(new IssueNonceRequest(InstanceRoute.Reference(instanceId)), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
