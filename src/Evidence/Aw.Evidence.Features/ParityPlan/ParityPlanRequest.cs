namespace Aw.Evidence.Features.ParityPlan;

/// <summary>Plans the Linear attachments of a parity run; every image must be reviewed before the plan unblocks.</summary>
/// <param name="RunDirectory">The run directory holding parity.json (absolute).</param>
/// <param name="Reviewed">The image files already opened and reviewed (absolute, as the plan lists them).</param>
public sealed record ParityPlanRequest(string RunDirectory, IReadOnlyList<string> Reviewed);
