namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The router stub's cursor (engine P7 plan D8): the last ledger sequence it has handled, in 'workflow/router.cursor'.</summary>
/// <param name="home">Where the file is.</param>
internal sealed class RouterStubCursor(WorkflowsHome home) : SequenceCursor(home.RouterCursorFile, "the router stub's cursor");
