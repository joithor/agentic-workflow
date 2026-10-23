namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A typical and a heavy figure; each null when there were not enough runs.</summary>
/// <param name="Median">The typical value.</param>
/// <param name="P90">The value nine in ten stay under.</param>
internal sealed record StatsSpreadView(long? Median, long? P90);
