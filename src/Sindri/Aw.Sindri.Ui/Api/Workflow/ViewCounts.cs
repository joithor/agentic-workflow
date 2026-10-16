namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The counts the frame's tabs show.</summary>
/// <param name="Timeline">Timeline, as served.</param>
/// <param name="Decisions">Decisions, as served.</param>
/// <param name="Requests">Requests, as served.</param>
/// <param name="Context">Context, as served.</param>
internal sealed record ViewCounts(
    int Timeline,
    int Decisions,
    int Requests,
    int? Context);
