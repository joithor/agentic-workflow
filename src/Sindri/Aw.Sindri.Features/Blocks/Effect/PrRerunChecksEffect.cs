using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.rerun-checks' (PB spec §4.6): not built yet.</summary>
/// <param name="pulls">The forge port.</param>
/// <param name="contents">Reads the facts it takes.</param>
internal sealed class PrRerunChecksEffect(IPullRequests pulls, IFactContents contents) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.RerunChecks;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        _ = (pulls, contents);   // why: the stub reads its ports until the runtime does
        throw new NotSupportedException();
    }
}
