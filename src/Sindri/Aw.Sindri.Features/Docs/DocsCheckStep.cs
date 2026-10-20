using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Docs;

/// <summary>'step.docs.check' on the block runtime (docs-sync spec §7.2, API17): the docs commit holds doc paths only, its links resolve and its markdown parses.</summary>
/// <param name="diff">Lists the paths the docs worktree changed.</param>
/// <param name="files">Reads the changed docs and the files their links name.</param>
internal sealed class DocsCheckStep(IRunDiff diff, IBriefFiles files) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.docs.check");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        _ = diff;
        _ = files;
        throw new NotSupportedException();
    }
}
