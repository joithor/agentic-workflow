using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.WorktreeList;

/// <summary>Lists the team repo's worktrees with the sweep's verdict on each; removes nothing.</summary>
internal sealed class ListWorktreesService : IService<ListWorktreesRequest, ListWorktreesResponse>
{
    public ValueTask<Result<ListWorktreesResponse>> Handle(ListWorktreesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
