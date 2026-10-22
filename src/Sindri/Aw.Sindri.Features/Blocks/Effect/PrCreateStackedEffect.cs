#pragma warning disable CS9113 // red stub: the real effect reads these
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>'effect.pr.create-stacked' (PB spec §7.2): opens the stacked docs pull request on the source pull request's head.</summary>
/// <param name="pulls">The forge.</param>
/// <param name="repos">Which repository the docs workspace belongs to.</param>
/// <param name="diff">Reads the docs workspace's head.</param>
/// <param name="contents">Reads the facts it takes.</param>
/// <param name="store">Stores 'fact:docsPr'.</param>
internal sealed class PrCreateStackedEffect(IPullRequests pulls, IForgeRepos repos, IRunDiff diff, IFactContents contents, IFactStore store) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.CreateStacked;

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
