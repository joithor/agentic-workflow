using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.merge' (PB spec §10.1, W23): merges with the forge's expected-head check.</summary>
/// <param name="pulls">The pull requests port.</param>
/// <param name="facts">Reads 'fact:pr' and 'fact:prState' back.</param>
/// <param name="profile">The approved profile: the merge method when the recipe names none.</param>
internal sealed class PrMergeEffect(IPullRequests pulls, IFactContents facts, IApprovedProfile profile) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Merge;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
