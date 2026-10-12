namespace Aw.Sindri.Ui.Api;

/// <summary>The status's health (UI spec E-14, computed by Sindri's HealthSummary): the pill renders it verbatim.</summary>
/// <param name="Word">'healthy', 'degraded', 'stalled' or 'rebuilding'.</param>
/// <param name="Problems">What is wrong, in Sindri's order.</param>
internal sealed record HealthView(string Word, IReadOnlyList<HealthProblemView> Problems);
