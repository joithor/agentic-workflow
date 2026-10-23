#pragma warning disable CS9113 // red stub: the real effect reads these
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.restack' (PB spec §4.6): moves the pull request onto its new base.</summary>
internal sealed class PrRestackEffect(IPullRequests pulls, IRunDiff diff, IWorkspaceRebase rebase, RebasedRangeScan scan, IFactContents facts, IFactStore store, IMergeRecord record) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Restack;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
