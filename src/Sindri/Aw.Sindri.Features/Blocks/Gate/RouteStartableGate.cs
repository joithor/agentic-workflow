using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.route.startable' (spec §12, engine P7 plan D7): a stub over the approved profile's 'workflow.route.startable'.
/// 'all' (the default, also when no profile is approved) passes; 'none' fails with the reason a human reads.
/// </summary>
/// <param name="profile">The approved profile.</param>
internal sealed class RouteStartableGate(IApprovedProfile profile) : IGate, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("gate.route.startable"), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.None, Parameters: [], Rung.Deterministic, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(profile);
        throw new NotSupportedException("RouteStartableGate.Evaluate is engine P7 unit U1");
    }
}
