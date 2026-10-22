using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.push' (PB spec §10.1): not built yet.</summary>
/// <param name="pulls">The forge port.</param>
/// <param name="diff">Reads the workspace.</param>
/// <param name="contents">Reads the facts it takes.</param>
/// <param name="facts">Stores the fact it replies with.</param>
internal sealed class PrPushEffect(IPullRequests pulls, IRunDiff diff, IFactContents contents, IFactStore facts) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Push;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        _ = (pulls, diff, contents, facts);   // why: the stub reads its ports until the runtime does
        throw new NotSupportedException();
    }
}
