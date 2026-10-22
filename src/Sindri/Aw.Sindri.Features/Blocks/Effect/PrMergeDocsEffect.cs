#pragma warning disable CS9113 // red stub: the real effect reads these
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.merge-docs' (PB spec §4.6): merges the stacked docs pull request of the source pull request after the source merged.</summary>
/// <param name="pulls">The forge.</param>
/// <param name="facts">Reads 'pr' back.</param>
/// <param name="store">Stores 'fact:docsMerge'.</param>
/// <param name="profile">The approved profile: the merge method.</param>
/// <param name="record">The merge record, where the docsMerge row goes.</param>
internal sealed class PrMergeDocsEffect(IPullRequests pulls, IFactContents facts, IFactStore store, IApprovedProfile profile, IMergeRecord record) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.MergeDocs;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
