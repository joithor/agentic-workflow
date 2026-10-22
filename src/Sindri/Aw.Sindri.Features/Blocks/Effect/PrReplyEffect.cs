using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.reply' (PB spec §5, §10.1): thread, reaction, fixed, fixedReplies, none and hold forms.</summary>
/// <param name="pulls">The pull requests port.</param>
/// <param name="facts">Reads 'fact:pr', 'fact:comment' and 'fact:reply' back.</param>
internal sealed class PrReplyEffect(IPullRequests pulls, IFactContents facts) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Reply;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
