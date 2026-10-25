using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Runs a gate by its id, in a scope of its own (engine P7 plan D7); registered by AddSindriWorkflows over the AddGate registrations.</summary>
/// <param name="gate">The gate's id.</param>
/// <param name="context">What it evaluates.</param>
/// <param name="cancellationToken">Cancelled when the activity is.</param>
/// <returns>The gate's result; null when this build has no runtime for the gate.</returns>
internal delegate ValueTask<GateResult?> GateDispatch(BlockId gate, GateContext context, CancellationToken cancellationToken);
