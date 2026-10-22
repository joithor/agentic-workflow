using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Docs;

/// <summary>'docs check': the docs rules over a worktree, shared with 'step.docs.check'.</summary>
/// <param name="diff">Lists the paths the docs worktree changed since the scope's base.</param>
/// <param name="files">Reads the changed docs and the files their links name.</param>
internal sealed class DocsCheckService(IRunDiff diff, IBriefFiles files) : IService<DocsCheckRequest, DocsCheckResponse>
{
    public ValueTask<Result<DocsCheckResponse>> Handle(DocsCheckRequest request, CancellationToken cancellationToken)
    {
        _ = (diff, files);
        throw new NotSupportedException();
    }
}
