namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The event bridge's cursor (engine spec rev 8 §5.4; engine P4 unit (d)): the last ledger sequence it has delivered, in 'workflow/events.cursor'.</summary>
/// <param name="home">Where the file is.</param>
internal sealed class EventBridgeCursor(WorkflowsHome home) : SequenceCursor(home.EventsCursorFile, "the event bridge's cursor");
