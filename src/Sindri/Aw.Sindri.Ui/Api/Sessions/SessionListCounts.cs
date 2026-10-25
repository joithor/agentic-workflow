namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>The head counts, taken before any filter.</summary>
/// <param name="Live">Live, as served.</param>
/// <param name="Asking">Asking, as served.</param>
/// <param name="OutsideWorkflow">OutsideWorkflow, as served.</param>
internal sealed record SessionListCounts(int Live, int Asking, int OutsideWorkflow);
