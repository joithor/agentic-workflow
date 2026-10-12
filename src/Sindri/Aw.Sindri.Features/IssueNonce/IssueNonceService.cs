using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.IssueNonce;

/// <summary>'IssueNonce' (engine spec rev 8 §5.2, §6.3). Engine P4 unit (b): red stub.</summary>
internal sealed class IssueNonceService(IInstanceDirectory directory, IInstanceRuntime runtime, ICommandNonces nonces)
    : IService<IssueNonceRequest, IssueNonceResponse>
{
    public ValueTask<Result<IssueNonceResponse>> Handle(IssueNonceRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"engine P4 unit (b): IssueNonce ({directory}, {runtime}, {nonces})");
}
