using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Scoping.Steps;

/// <summary>
/// A scoping Step the block runtime runs (scoping spec §8.2, §9): the same code the desk helper runs (<see cref="DeskBlocks"/>), moved behind
/// <see cref="IBlockStep"/>, so a scoping instance passes the deterministic Steps without a desk task. The context's facts become the code's
/// facts (JSON text by key, without the 'fact:' prefix); the code's outputs become typed facts again. A subclass names its Step; one that needs
/// more than the facts (gather's Step inputs) overrides <see cref="Extra"/>.
/// </summary>
/// <param name="step">The Step's descriptor (its id picks the code).</param>
internal abstract class ScopingCodeStep(BlockDescriptor step) : IBlockStep
{
    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(step.Id.Value);
    }

    /// <summary>The code's facts beyond the context's own: what the Step's inputs gave it. None by default.</summary>
    /// <param name="context">The Step's context.</param>
    /// <returns>JSON text by code fact key.</returns>
    protected virtual IReadOnlyDictionary<string, string> Extra(StepContext context) => new Dictionary<string, string>(StringComparer.Ordinal);
}
