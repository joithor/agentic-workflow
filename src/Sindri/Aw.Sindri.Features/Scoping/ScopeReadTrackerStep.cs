using Aw.Kernel;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.scope.read-tracker', on the block runtime: the tracker project or issue through the tracker port.</summary>
/// <param name="tracker">The tracker port.</param>
/// <param name="shape">The engine's text shape.</param>
/// <param name="scrubber">Takes secrets out of tracker text.</param>
/// <param name="clock">When the record was read.</param>
internal sealed class ScopeReadTrackerStep(ITrackerSource tracker, IScopingTextShape shape, ITextScrubber scrubber, TimeProvider clock) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.read-tracker");

    /// <inheritdoc/>
    protected override ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        _ = (tracker, shape, scrubber, clock);
        throw new NotSupportedException();
    }
}
