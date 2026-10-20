using Aw.Kernel;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.scope.read-change', on the block runtime: the change's diff, commits and PR body through the change port.</summary>
/// <param name="changes">The change port.</param>
/// <param name="checkouts">Where the repo's checkout is.</param>
/// <param name="shape">The engine's text shape.</param>
/// <param name="scrubber">Takes secrets out of the change's text.</param>
/// <param name="clock">When the change was read.</param>
internal sealed class ScopeReadChangeStep(IChangeSource changes, IRepoCheckouts checkouts, IScopingTextShape shape, ITextScrubber scrubber, TimeProvider clock) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.read-change");

    /// <inheritdoc/>
    protected override ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        _ = (changes, checkouts, shape, scrubber, clock);
        throw new NotSupportedException();
    }
}
