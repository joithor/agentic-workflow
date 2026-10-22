using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>'gate.pr.diff-scope' (PB spec §10.1): stub until its runtime lands.</summary>
/// <param name="paths">Lists the paths between two refs.</param>
/// <param name="content">Reads the lines the push added and the paths it deleted.</param>
internal sealed class PrDiffScopeGate(IChangedPaths paths, IDiffContent content) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = PrGates.DiffScope;

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
