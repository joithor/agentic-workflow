using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.check', on the block runtime: <c language="text">ScopingSteps.Check</c>'s code behind <see cref="IBlockStep"/>. Every run appends its round line to the
/// instance's rounds.jsonl in the run area, as the desk does (scoping spec §8.3, §9), and the next round's number, trigger and diff come from the lines already there.
/// A run area that cannot be written fails the Step: a round is never dropped.
/// </summary>
/// <param name="area">The instance's run area.</param>
internal sealed class ScopeCheckStep(IRunArea area) : ScopingCodeStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.check");

    /// <inheritdoc/>
    protected override ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken) =>
        Rounds(area, context, cancellationToken);

    /// <inheritdoc/>
    protected override ValueTask<Result<bool>> Keep(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken) =>
        WriteFiles(area, context, response, cancellationToken);
}
