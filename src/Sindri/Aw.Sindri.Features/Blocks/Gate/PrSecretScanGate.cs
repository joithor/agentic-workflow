using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>'gate.pr.secret-scan' (PB spec §10.1): stub until its runtime lands.</summary>
/// <param name="content">Reads the lines the push added.</param>
/// <param name="scrubber">The shared secret patterns (Aw.Redaction).</param>
internal sealed class PrSecretScanGate(IDiffContent content, ITextScrubber scrubber) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = PrGates.SecretScan;

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
