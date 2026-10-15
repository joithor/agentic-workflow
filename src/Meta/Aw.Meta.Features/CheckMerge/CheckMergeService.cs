using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Features.CheckMerge;

/// <summary>
/// Checks the merge preconditions for a pull request: the latest gate is green (or every red step is a classified flake),
/// the head and main are where the gate saw them, the merge order holds, and the pull request is open and not a draft.
/// </summary>
/// <param name="readiness">Gathers the facts and runs the merge gate.</param>
internal sealed class CheckMergeService(MergeReadiness readiness) : IService<CheckMergeRequest, CheckMergeResponse>
{
    public async ValueTask<Result<CheckMergeResponse>> Handle(CheckMergeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var assessed = await readiness.Assess(request.Pr, cancellationToken).ConfigureAwait(false);
        return assessed.IsError ? assessed.Error : new CheckMergeResponse(request.Pr, assessed.Value.Ready, assessed.Value.Checks);
    }
}
