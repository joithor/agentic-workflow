namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The totals beside the needs-you rows in Home's summary line.</summary>
/// <param name="Running">Items running.</param>
/// <param name="WithYou">Items taken over by the human.</param>
/// <param name="Queued">Items queued.</param>
internal sealed record HomeCounts(int Running, int WithYou, int Queued);
