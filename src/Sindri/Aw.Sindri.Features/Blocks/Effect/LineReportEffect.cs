using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.line.report' (line spec BD7, LN-WF1, Q13): the phase tells its line that it has 'shipped' or 'merged'.
/// </summary>
/// <param name="area">The instance's run area, where the report is recorded.</param>
/// <param name="contents">Reads the line context fact.</param>
/// <param name="clock">When a report is written.</param>
internal sealed class LineReportEffect(IRunArea area, IFactContents contents, TimeProvider clock) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => LineEffects.Report;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
