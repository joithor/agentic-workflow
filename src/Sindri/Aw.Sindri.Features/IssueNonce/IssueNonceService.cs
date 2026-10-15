using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.IssueNonce;

/// <summary>
/// 'IssueNonce' (engine spec rev 8 §5.2, §6.3, class issue): a fresh nonce for one command on an existing instance,
/// valid 10 minutes once a command claims it. The instance must exist (SND-WF-001). Until API P1's nonce table lands
/// the nonces live in <see cref="ICommandNonces"/>.
/// </summary>
/// <param name="directory">Resolves the address.</param>
/// <param name="runtime">Checks the instance exists.</param>
/// <param name="nonces">Mints the nonce.</param>
internal sealed class IssueNonceService(IInstanceDirectory directory, IInstanceRuntime runtime, ICommandNonces nonces)
    : IService<IssueNonceRequest, IssueNonceResponse>
{
    public async ValueTask<Result<IssueNonceResponse>> Handle(IssueNonceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var address = await directory.Resolve(request.Instance, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return address.Error;
        }

        var instance = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        return instance.IsError ? instance.Error : new IssueNonceResponse(address.Value, nonces.Issue());
    }
}
