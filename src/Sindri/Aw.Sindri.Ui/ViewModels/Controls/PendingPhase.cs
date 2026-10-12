namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>The pending actions one phase ships (UI spec §4.8: Health lists the catalog's pending rows by phase).</summary>
/// <param name="Heading">"arrives in P3".</param>
/// <param name="Controls">Its pending controls.</param>
internal sealed record PendingPhase(string Heading, IReadOnlyList<ControlViewModel> Controls);
