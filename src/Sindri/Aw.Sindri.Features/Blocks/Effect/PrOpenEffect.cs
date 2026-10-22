using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.open' (line spec §7.5, PB spec BD1): opens the phase's pull request from the frozen hand-off, or updates the one already open for its head branch.
/// </summary>
/// <param name="pulls">The forge.</param>
/// <param name="repos">Which repository the workspace belongs to.</param>
/// <param name="files">Reads pr.json and body.md of the hand-off.</param>
/// <param name="contents">Reads the stored facts.</param>
/// <param name="store">Stores 'fact:pr'.</param>
/// <param name="profile">The approved profile: 'workflow.ship.draft'.</param>
internal sealed class PrOpenEffect(IPullRequests pulls, IForgeRepos repos, IPacketFiles files, IFactContents contents, IFactStore store, IApprovedProfile profile)
    : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("effect.pr.open"), BlockKind.Effect, Version: 1, Inputs: [FactKey.From("fact:handoff"), FactKey.From("fact:workspace")],
        Outputs: [new OutputSpec(FactKey.From("fact:pr"), [OutcomeName.From("opened"), OutcomeName.From("updated")])], Outcomes: [],
        Capability.Effects, Parameters: [], Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
