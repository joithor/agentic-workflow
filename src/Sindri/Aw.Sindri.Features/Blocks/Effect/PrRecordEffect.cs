using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.record' (PB spec §4.7): not built yet.</summary>
/// <param name="area">The instance's run area, where the merge record lives.</param>
/// <param name="contents">Reads the facts it takes.</param>
/// <param name="clock">When a row is written.</param>
internal sealed class PrRecordEffect(IRunArea area, IFactContents contents, TimeProvider clock) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Record;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        _ = (area, contents, clock);   // why: the stub reads its ports until the runtime does
        throw new NotSupportedException();
    }
}
