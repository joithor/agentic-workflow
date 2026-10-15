using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Features.WorktreeList;

/// <summary>Lists the team repo's worktrees with the sweep's verdict on each; removes nothing.</summary>
/// <param name="config">The team config.</param>
/// <param name="inspector">Judges the worktrees.</param>
internal sealed class ListWorktreesService(IMetaConfig config, WorktreeInspector inspector) : IService<ListWorktreesRequest, ListWorktreesResponse>
{
    public async ValueTask<Result<ListWorktreesResponse>> Handle(ListWorktreesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var reports = await inspector.Inspect(team.Value, WorktreeScope.All, request.Fetch, onlyPath: null, cancellationToken).ConfigureAwait(false);
        return reports.IsError ? reports.Error : new ListWorktreesResponse(reports.Value);
    }
}
