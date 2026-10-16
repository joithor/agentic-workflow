using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Features.MergeRun;

/// <summary>
/// Runs after a successful merge-run: sweeps the worktrees of the merged pull request (its head branch and its gate trees). The merge is
/// already done and recorded, so a sweep that can't run never fails it; the response just carries no worktree line.
/// </summary>
/// <param name="merge">The merge-run service.</param>
/// <param name="config">The team config.</param>
/// <param name="sweeper">Removes the worktrees.</param>
internal sealed class SweepAfterMergeService(IService<MergeRunRequest, MergeRunResponse> merge, IMetaConfig config, WorktreeSweeper sweeper) : IService<MergeRunRequest, MergeRunResponse>
{
    public async ValueTask<Result<MergeRunResponse>> Handle(MergeRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var merged = await merge.Handle(request, cancellationToken).ConfigureAwait(false);
        if (merged.IsError || !merged.Value.Merged)
        {
            return merged;
        }

        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var line = team.IsError ? Results.Failure<string>(team.Error) : await sweeper.Triggered(team.Value, [request.Pr], "merge-run", cancellationToken).ConfigureAwait(false);
        return line.IsSuccess && line.Value.Length > 0 ? merged.Value with { Worktrees = line.Value } : merged;
    }
}
