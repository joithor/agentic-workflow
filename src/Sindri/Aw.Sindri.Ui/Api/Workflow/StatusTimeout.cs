namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A state timeout.</summary>
/// <param name="Kind">Kind, as served.</param>
/// <param name="InSeconds">InSeconds, as served.</param>
/// <param name="On">On, as served.</param>
internal sealed record StatusTimeout(
    TimeoutKind Kind,
    long InSeconds,
    string On);
