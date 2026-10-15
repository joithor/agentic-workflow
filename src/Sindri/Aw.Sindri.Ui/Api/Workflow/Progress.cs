namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A done-of-total pair.</summary>
/// <param name="Done">Done, as served.</param>
/// <param name="Total">Total, as served.</param>
internal sealed record Progress(
    int Done,
    int Total);
