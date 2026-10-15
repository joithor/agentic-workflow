namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The parent of a child instance.</summary>
/// <param name="Id">Id, as served.</param>
/// <param name="Address">Address, as served.</param>
/// <param name="Title">Title, as served.</param>
/// <param name="Task">Task, as served.</param>
/// <param name="Workstream">Workstream, as served.</param>
internal sealed record ParentRef(
    string Id,
    string Address,
    string Title,
    string? Task,
    string? Workstream);
