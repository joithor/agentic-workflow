namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The paused line of a taken-over status.</summary>
/// <param name="TimeoutLeftSeconds">TimeoutLeftSeconds, as served.</param>
internal sealed record StatusPaused(
    long TimeoutLeftSeconds);
