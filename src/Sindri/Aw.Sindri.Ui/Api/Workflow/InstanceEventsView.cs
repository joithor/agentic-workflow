namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>GET /sindri/workflow/instances/{id}/events: the instance's ledger events, oldest first.</summary>
/// <param name="Events">The events.</param>
/// <param name="Cursor">The last sequence read.</param>
internal sealed record InstanceEventsView(IReadOnlyList<InstanceEventRow> Events, long Cursor);
