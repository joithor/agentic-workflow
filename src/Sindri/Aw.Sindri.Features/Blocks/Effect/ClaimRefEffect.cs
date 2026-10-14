using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.claim.ref' (engine spec §5.7, §12; engine P6 unit (d)): claims the item in 'workflow_claims' (item, owner
/// instance, attempt) and creates the marker ref 'sindri/{user}/{item}' at the default branch's head of the repo
/// 'fact:item' names. It replies 'claim.created' with 'created', or 'claimExists' when another instance holds the item.
/// </summary>
/// <param name="claims">The 'workflow_claims' table.</param>
/// <param name="liveness">Whether a holder is still live.</param>
/// <param name="refs">Creates the marker ref.</param>
/// <param name="facts">Reads 'fact:item' back.</param>
/// <param name="profile">The approved profile: the user and the repo's path.</param>
internal sealed class ClaimRefEffect(IWorkflowClaims claims, IInstanceLiveness liveness, IClaimRefs refs, IFactContents facts, IApprovedProfile profile)
    : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("effect.claim.ref"), BlockKind.Effect, Version: 1, Inputs: [FactKey.From("fact:item")], Outputs: [], Outcomes: [],
        Capability.Effects | Capability.WriteScope, Parameters: [], Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = (claims, liveness, refs, facts, profile);
        throw new NotSupportedException("engine P6 unit (d): effect.claim.ref (workflow_claims and the marker ref)");
    }
}
