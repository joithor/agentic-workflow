namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A drift block.</summary>
/// <param name="Block">Block, as served.</param>
/// <param name="NeedsReapproval">NeedsReapproval, as served.</param>
internal sealed record StatusDrift(
    string Block,
    bool NeedsReapproval);
