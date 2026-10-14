using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.notify.desk' (engine spec §6.3, §12; README X5, X50; engine P6 unit (c)): a notice on the human's desk, one
/// line of 'notifications.jsonl' per effect identity and a macOS notification when one is available. A recipe's
/// replies 'notify.delivered' with 'delivered'; an engine notice replies nothing.
/// </summary>
internal sealed class NotifyDeskEffect : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("effect.notify.desk"), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.Effects, Parameters: [], Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        throw new NotSupportedException("engine P6 unit (c): effect.notify.desk (notifications.jsonl through IDeskNotices)");
    }
}
